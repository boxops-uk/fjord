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

/** A stretch of one line, in that line's string indices. */
type Stretch = { from: number; to: number }

/** A colour run, once it is a position in the line's text rather than a payload. */
type Run = Stretch & { type: SyntaxType }

/**
 * Join a blob's lines into the string `CodeBlock` renders, and lift every run onto
 * an offset into that string.
 *
 * The offsets are JavaScript string indices — UTF-16 code units — which is what a
 * `roslyn-lsp-1` run already counts in, so that path is an addition and nothing more.
 * A byte-counting encoding is converted per line, because a byte offset into UTF-8 is
 * not a position in a JavaScript string and using it as one silently mis-colours
 * every line after the first character outside ASCII.
 *
 * `links` is every followable byte range in the file, ordered — `xref.links`. They
 * arrive here rather than as a second pass because the conversion is the same one:
 * a reference counts bytes, as a `scip-syntax-1` run does, and this is the loop that
 * already holds the line and its byte→unit map.
 */
export function paint(blob: Blob, links: readonly number[] = []): Painted {
  const legend = blob.encoding ? LEGENDS[blob.encoding] : undefined
  const bytes = blob.encoding ? COUNTS_BYTES[blob.encoding] === true : false

  const texts: string[] = []
  const spans: Span[] = []
  let offset = 0
  let link = 0

  for (let line = 1; line <= blob.lines; line++) {
    const text = blob.text(line) ?? ''
    texts.push(text)

    // One map, two jobs: a byte-counting encoding's runs and every reference are
    // numbers in the same unit, and building it twice would walk the line twice.
    const start = blob.start(line) ?? 0
    const nextStart = blob.start(line + 1)
    const units =
      bytes || (link < links.length && (nextStart === undefined || links[link] < nextStart))
        ? unitsByByte(text)
        : null
    const runs: Run[] = []
    if (legend) {
      const painted = blob.runs(line)

      for (let at = 0; at < painted.length; at += 4) {
        const type = legend[painted[at + 2]]
        if (!type) continue

        const from = bytes && units ? (units[painted[at]] ?? text.length) : painted[at]
        const to =
          bytes && units
            ? (units[painted[at] + painted[at + 1]] ?? text.length)
            : painted[at] + painted[at + 1]

        // A run that does not lie inside its line is dropped rather than clamped:
        // it means the payload and the text disagree, and a clamped span paints
        // the wrong characters while looking like it worked.
        if (from >= to || to > text.length) continue

        runs.push({ type, from, to })
      }
    }

    const marks: Stretch[] = []
    if (units) {
      // `units.length - 1` is the line's length in bytes: the map's last entry is
      // the position one past its last character.
      const ends = start + units.length - 1

      while (link < links.length && links[link] < ends) {
        const from = units[links[link] - start]
        const to = units[links[link + 1] - start]
        link += 2

        // Dropped rather than clamped, for the reason a run is: a reference that
        // does not lie inside the line it starts on is a disagreement.
        if (from === undefined || to === undefined || from >= to) continue

        marks.push({ from, to })
      }
    }

    for (const span of weave(runs, marks, offset)) spans.push(span)

    // `+ 1` for the newline this line is joined with. The last one has none, which
    // costs an offset past the end that nothing reads.
    offset += text.length + 1
  }

  return { code: texts.join('\n'), spans }
}

/**
 * One line's two vocabularies as one list of spans — the colour a run carries,
 * and whether a reference covers it.
 *
 * **A span can only say one thing**, because the block paints one class (or one
 * highlight) per span and overlapping spans are not a shape it has: in its span
 * mode a second span over the same text repeats that text on the page. So the
 * two are cut against each other and a stretch that is both is its own type —
 * `type-xref` — which `app.css` gives the colour it merged with and the
 * underline that says it is followable.
 *
 * Runs arrive ordered and non-overlapping (the blob says so of its payload) and
 * so do marks (`xref.links` merges the two kinds), which is what lets one pass
 * over the cut points answer both.
 */
function weave(runs: readonly Run[], marks: readonly Stretch[], offset: number): Span[] {
  if (marks.length === 0) {
    return runs.map((run) => ({ type: run.type, start: offset + run.from, end: offset + run.to }))
  }

  const cuts = [
    ...new Set([...runs, ...marks].flatMap(({ from, to }) => [from, to])),
  ].sort((left, right) => left - right)

  const spans: Span[] = []
  let run = 0
  let mark = 0

  for (let at = 0; at + 1 < cuts.length; at++) {
    const from = cuts[at]
    const to = cuts[at + 1]

    while (run < runs.length && runs[run].to <= from) run++
    while (mark < marks.length && marks[mark].to <= from) mark++

    const colour = run < runs.length && runs[run].from <= from ? runs[run].type : null
    const linked = mark < marks.length && marks[mark].from <= from
    if (!colour && !linked) continue

    const type = colour ? (linked ? `${colour}-xref` : colour) : 'xref'

    // Adjacent pieces of one type are one span again: the cut points are every
    // boundary either vocabulary has, so a run a mark ends inside is cut twice,
    // and two spans meeting mid-word draw two underlines with a seam.
    const last = spans[spans.length - 1]
    if (last && last.type === type && last.end === offset + from) last.end = offset + to
    else spans.push({ type, start: offset + from, end: offset + to })
  }

  return spans
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
