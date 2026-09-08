/**
 * **From the index's colours to the design system's.**
 *
 * `src.FileLineStyles` is an opaque payload and the schema defines nothing about
 * what is in it — a highlighter's vocabulary is a presentation concern with its own
 * lifecycle, so pinning one in the schema would make every new token kind a breaking
 * edit to a predicate every published index carries. What a database holds is named
 * by `config.Setting {dimension = "style-encoding"}`, and **this file is the only
 * place that knows what those names mean**.
 *
 * The decoding is not here: the module does that, and hands back flat runs of
 * `[start, length, kind, modifiers]`. What is left is a table lookup — legend index
 * to one of Astryx's fourteen syntax token keys — which is the part that is genuinely
 * the page's, because it is a mapping onto *this* design system's palette.
 *
 * An encoding this table has no row for renders plain, which is what the schema asks
 * for and is a great deal better than colouring one producer's tokens with another
 * producer's legend.
 */
import type { Blob } from './corpus'

/** The token types an Astryx syntax theme colours. */
type SyntaxType =
  | 'keyword'
  | 'string'
  | 'comment'
  | 'number'
  | 'function'
  | 'type'
  | 'variable'
  | 'operator'
  | 'constant'
  | 'tag'
  | 'attribute'
  | 'property'
  | 'punctuation'

/**
 * `roslyn-lsp-1`'s legend, mapped onto those types **by index**.
 *
 * The order is `SemanticTokens.Legend` in the .NET indexer, which is fixed and
 * published rather than discovered — appending is safe there and renumbering is not,
 * exactly as for a schema union, which is what makes indexing this array sound.
 *
 * Index 0 is Roslyn's `Text`, its own fallback, and is deliberately `null`: emitting
 * a span for it would colour ordinary text as something, and the absence of a span
 * is how a run of plain text is spelled.
 */
const ROSLYN_LSP_1: (SyntaxType | null)[] = [
  null, // 0  Text — the fallback, and no span at all
  'keyword', // 1  Keyword
  'keyword', // 2  ControlKeyword
  'variable', // 3  Identifier
  'type', // 4  ClassName
  'type', // 5  RecordClassName
  'type', // 6  StructName
  'type', // 7  RecordStructName
  'type', // 8  InterfaceName
  'type', // 9  EnumName
  'type', // 10 DelegateName
  'type', // 11 TypeParameterName
  'type', // 12 ModuleName
  'type', // 13 NamespaceName
  'function', // 14 MethodName
  'function', // 15 ExtensionMethodName
  'property', // 16 PropertyName
  'property', // 17 FieldName
  'property', // 18 EventName
  'constant', // 19 ConstantName
  'variable', // 20 LocalName
  'variable', // 21 ParameterName
  'constant', // 22 EnumMemberName
  'variable', // 23 LabelName
  'comment', // 24 Comment
  'string', // 25 StringLiteral
  'string', // 26 VerbatimStringLiteral
  'constant', // 27 StringEscapeCharacter
  'number', // 28 NumericLiteral
  'operator', // 29 Operator
  'operator', // 30 OperatorOverloaded
  'punctuation', // 31 Punctuation
  'keyword', // 32 PreprocessorKeyword
  'comment', // 33 PreprocessorText
  'comment', // 34 ExcludedCode
  'comment', // 35 XmlDocCommentText
  'tag', // 36 XmlDocCommentName
  'punctuation', // 37 XmlDocCommentDelimiter
  'attribute', // 38 XmlDocCommentAttributeName
  'string', // 39 XmlDocCommentAttributeValue
  'punctuation', // 40 XmlDocCommentAttributeQuotes
  'comment', // 41 XmlDocCommentComment
  'string', // 42 XmlDocCommentCDataSection
  'constant', // 43 XmlDocCommentEntityReference
  'punctuation', // 44 XmlDocCommentProcessingInstruction
  'comment', // 45 RegexComment
  'constant', // 46 RegexCharacterClass
  'operator', // 47 RegexAnchor
  'operator', // 48 RegexQuantifier
  'punctuation', // 49 RegexGrouping
  'operator', // 50 RegexAlternation
  'string', // 51 RegexText
  'constant', // 52 RegexSelfEscapedCharacter
  'constant', // 53 RegexOtherEscape
]

const LEGENDS: Record<string, (SyntaxType | null)[]> = {
  'roslyn-lsp-1': ROSLYN_LSP_1,
}

/**
 * Whether a run's columns count in UTF-8 bytes rather than UTF-16 code units.
 *
 * `roslyn-lsp-1` is LSP's own unit, which is also a JavaScript string index, so its
 * numbers are usable as they stand. `scip-syntax-1` counts bytes, which they are not.
 */
const COUNTS_BYTES: Record<string, boolean> = {
  'roslyn-lsp-1': false,
  'scip-syntax-1': true,
}

/** What `CodeBlock`'s `tokenizer` prop returns. */
export type Span = { type: string; start: number; end: number }

/** One file's text, and the spans that colour it. */
export type Painted = { code: string; spans: Span[] }

/**
 * Join a blob's lines into the string `CodeBlock` renders, and lift every run onto
 * an offset into that string.
 *
 * The offsets are JavaScript string indices — UTF-16 code units — which is what a
 * `roslyn-lsp-1` run already counts in, so that path is an addition and nothing more.
 * A byte-counting encoding is converted per line, because a byte offset into UTF-8 is
 * not a position in a JavaScript string and using it as one silently mis-colours
 * every line after the first character outside ASCII.
 */
export function paint(blob: Blob): Painted {
  const legend = blob.encoding ? LEGENDS[blob.encoding] : undefined
  const bytes = blob.encoding ? COUNTS_BYTES[blob.encoding] === true : false

  const texts: string[] = []
  const spans: Span[] = []
  let offset = 0

  for (let line = 1; line <= blob.lines; line++) {
    const text = blob.text(line) ?? ''
    texts.push(text)

    if (legend) {
      const runs = blob.runs(line)
      const units = bytes ? unitsByByte(text) : null

      for (let at = 0; at < runs.length; at += 4) {
        const type = legend[runs[at + 2]]
        if (!type) continue

        const from = units ? (units[runs[at]] ?? text.length) : runs[at]
        const to = units
          ? (units[runs[at] + runs[at + 1]] ?? text.length)
          : runs[at] + runs[at + 1]

        // A run that does not lie inside its line is dropped rather than clamped:
        // it means the payload and the text disagree, and a clamped span paints
        // the wrong characters while looking like it worked.
        if (from >= to || to > text.length) continue

        spans.push({ type, start: offset + from, end: offset + to })
      }
    }

    // `+ 1` for the newline this line is joined with. The last one has none, which
    // costs an offset past the end that nothing reads.
    offset += text.length + 1
  }

  return { code: texts.join('\n'), spans }
}

/**
 * A byte offset into one line's UTF-8, to the JavaScript string index at the same
 * place. Sparse: only positions that begin a character are filled, which is every
 * position a well-formed span can name.
 */
function unitsByByte(text: string): number[] {
  const units: number[] = []
  let byte = 0

  for (let at = 0; at < text.length; ) {
    const point = text.codePointAt(at)!
    units[byte] = at

    byte +=
      point < 0x80 ? 1 : point < 0x800 ? 2 : point < 0x10000 ? 3 : 4
    at += point < 0x10000 ? 1 : 2
  }

  units[byte] = text.length
  return units
}
