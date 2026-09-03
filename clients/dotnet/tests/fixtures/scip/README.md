# `scip` — a SCIP index, written here because there is no indexer to produce one

`index.scip` is a real SCIP index in the real wire format. It is not captured from a tool:
there is no SCIP indexer in this repository and there is not going to be one — that is
R9's whole point — so the fixture is written by [`make-index.py`](make-index.py) and the
binary is checked in beside it.

**Written in another language on purpose.** A fixture produced by the same code that reads
it tests nothing but self-consistency. The generator encodes protobuf from the field
numbers in the specification, independently of `Protobuf.cs`, so the two have to meet in
the middle of the wire format rather than agreeing about a shared idea of it.

**What a reviewer should check is the field numbers.** They are the one thing in either
implementation that cannot be derived — everything else is arithmetic — and both sides
write them beside the name `scip.proto` gives. They are frozen by protobuf's own
compatibility rules, which is what makes transcribing them safe rather than a copy that
will drift.

## What is in it

Two TypeScript documents, in a language nothing in this repository can compile:

- `src/greet.ts` declares `greet` and its parameter `who`, and uses `who` again inside a
  template literal. **That line contains `héllo`**, which is six UTF-16 code units and
  seven UTF-8 bytes — so every position after it is one the converter must convert rather
  than copy. An index that was right about ASCII and wrong about everything else passes
  every other assertion here and fails that one.
- `src/main.ts` imports `greet` and calls it, which is the cross-file reference
  go-to-definition and find-references are about.

Both documents carry their own text, so the converter needs no checkout on disk; both
declare `UTF16CodeUnitOffsetFromLineStart`, which is what a TypeScript indexer counts in.
There is one external symbol — a reference to `String` from a package the index does not
contain — because a real index always has some.

To rebuild it: `python3 make-index.py`.
