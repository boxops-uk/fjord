#!/usr/bin/env python3
"""Build `index.scip`, the fixture SCIP index, from an independent encoder.

**Why a script and not a captured artifact.** There is no SCIP indexer in this
repository and there is not going to be one — that is R9's whole point — so the fixture
has to be written. Writing it *here*, in another language, from the field numbers in the
specification, is what keeps the test from being the C# reader agreeing with the C#
writer: two implementations of the same wire format have to meet in the middle.

**The field numbers are the specification's**, and they are the one thing a reviewer
should check. They are frozen by protobuf's compatibility rules; the names beside them are
the names `scip.proto` gives.

Run it from anywhere:  python3 make-index.py
"""

import struct
import sys
from pathlib import Path

# ---- the wire format ---------------------------------------------------------------


def varint(value: int) -> bytes:
    out = bytearray()
    while True:
        byte = value & 0x7F
        value >>= 7
        out.append(byte | (0x80 if value else 0))
        if not value:
            return bytes(out)


def tag(field: int, wire: int) -> bytes:
    return varint((field << 3) | wire)


def delimited(field: int, payload: bytes) -> bytes:
    return tag(field, 2) + varint(len(payload)) + payload


def text(field: int, value: str) -> bytes:
    return delimited(field, value.encode("utf-8"))


def number(field: int, value: int) -> bytes:
    return tag(field, 0) + varint(value)


def packed_int32s(field: int, values: list[int]) -> bytes:
    return delimited(field, b"".join(varint(v) for v in values))


# ---- SCIP's messages, by the field numbers in scip.proto ---------------------------

# SymbolRole
DEFINITION = 0x1
READ_ACCESS = 0x8

# SymbolInformation.Kind
KIND_FUNCTION = 17
KIND_PARAMETER = 37
KIND_CLASS = 7
KIND_METHOD = 26

# SyntaxKind
SYNTAX_KEYWORD = 4
SYNTAX_IDENTIFIER = 6
SYNTAX_IDENTIFIER_FUNCTION = 15
SYNTAX_STRING_LITERAL = 27

# PositionEncoding
UTF16 = 2


def occurrence(rng: list[int], symbol: str, roles: int = 0, syntax: int = 0) -> bytes:
    out = packed_int32s(1, rng) + text(2, symbol)          # range, symbol
    if roles:
        out += number(3, roles)                            # symbol_roles
    if syntax:
        out += number(5, syntax)                           # syntax_kind
    return out


def symbol_information(symbol: str, kind: int, display: str) -> bytes:
    return text(1, symbol) + number(5, kind) + text(6, display)  # symbol, kind, display_name


def document(path: str, language: str, body: str, occurrences: list[bytes],
             symbols: list[bytes]) -> bytes:
    out = text(1, path)                                    # relative_path
    for one in occurrences:
        out += delimited(2, one)                           # occurrences
    for one in symbols:
        out += delimited(3, one)                           # symbols
    out += text(4, language)                               # language
    out += text(5, body)                                   # text
    out += number(6, UTF16)                                # position_encoding
    return out


def metadata(project_root: str) -> bytes:
    tool = text(1, "scip-typescript-fixture") + text(2, "0.1.0")
    return number(1, 1) + delimited(2, tool) + text(3, project_root)  # version, tool_info, root


# ---- the fixture itself -------------------------------------------------------------

PACKAGE = "scip-typescript npm fixture 1.0.0"
GREET = f"{PACKAGE} src/`greet.ts`/greet()."
WHO = f"{PACKAGE} src/`greet.ts`/greet().(who)"
MAIN = f"{PACKAGE} src/`main.ts`/main()."

# **A non-ASCII character on purpose.** `héllo` is six UTF-16 code units and seven UTF-8
# bytes, so every occurrence after it on that line has a character offset that is not a
# byte offset — which is the conversion the whole converter turns on.
GREET_TS = 'export function greet(who: string): string {\n  return `héllo ${who}`;\n}\n'
MAIN_TS = 'import { greet } from "./greet";\n\nexport function main(): string {\n  return greet("world");\n}\n'


def greet_document() -> bytes:
    # Ranges are [startLine, startCharacter, endLine, endCharacter], zero-based, and
    # single-line ranges may drop the second line.
    occurrences = [
        occurrence([0, 0, 6], "", syntax=SYNTAX_KEYWORD),               # `export`
        occurrence([0, 7, 15], "", syntax=SYNTAX_KEYWORD),              # `function`
        occurrence([0, 16, 21], GREET, DEFINITION, SYNTAX_IDENTIFIER_FUNCTION),
        occurrence([0, 22, 25], WHO, DEFINITION, SYNTAX_IDENTIFIER),
        # `who` inside the template literal, on the line with the non-ASCII text.
        occurrence([1, 18, 21], WHO, READ_ACCESS, SYNTAX_IDENTIFIER),
    ]
    symbols = [
        symbol_information(GREET, KIND_FUNCTION, "greet"),
        symbol_information(WHO, KIND_PARAMETER, "who"),
    ]
    return document("src/greet.ts", "TypeScript", GREET_TS, occurrences, symbols)


def main_document() -> bytes:
    occurrences = [
        occurrence([0, 9, 14], GREET, READ_ACCESS, SYNTAX_IDENTIFIER),   # the import
        occurrence([2, 16, 20], MAIN, DEFINITION, SYNTAX_IDENTIFIER_FUNCTION),
        occurrence([3, 9, 14], GREET, READ_ACCESS, SYNTAX_IDENTIFIER),   # the call
        occurrence([3, 15, 22], "", syntax=SYNTAX_STRING_LITERAL),       # `"world"`
    ]
    symbols = [symbol_information(MAIN, KIND_FUNCTION, "main")]
    return document("src/main.ts", "TypeScript", MAIN_TS, occurrences, symbols)


def index() -> bytes:
    out = delimited(1, metadata("file:///fixture"))          # metadata
    out += delimited(2, greet_document())                    # documents
    out += delimited(2, main_document())
    # An external symbol: a reference into a package this index does not contain.
    out += delimited(3, symbol_information(
        "scip-typescript npm typescript 5.0.0 lib/`lib.es5.d.ts`/String#",
        KIND_CLASS, "String"))
    return out


if __name__ == "__main__":
    path = Path(__file__).with_name("index.scip")
    payload = index()
    path.write_bytes(payload)
    print(f"wrote {path} ({len(payload)} bytes)", file=sys.stderr)
