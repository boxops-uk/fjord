/**
 * **The book's diagrams, as elements rather than as dashes.**
 *
 * A byte layout, a protocol exchange, a directory tree and a pipeline were all
 * drawn here in box-drawing characters inside a fenced block. That rendering
 * costs three things this one gets back: a field whose *width* is its size, a
 * direction shown by *position* rather than by a glyph, and text that reflows
 * on a phone instead of scrolling a picture sideways.
 *
 * The design system has no component for any of this — `astryx search
 * "diagram"` returns nothing — so these are the book's own, in the sense
 * `app.css`'s panels are the workbench's. Every value they use is a token, so
 * both themes follow the page without a second palette.
 *
 * **The words stay in the DOM.** Nothing here is an image or a canvas: a
 * reader can select a field name, the command palette can find it, and a
 * screen reader reads the same order the prose does. The only SVG is the
 * arrowheads, which carry no information a caption does not.
 *
 * **Three of them take their content as text**, because a tree, an exchange
 * and a ladder are lists and a page is easier to read and to diff when they
 * are written as lists. The grammars are one line each and are documented at
 * the component that parses them.
 */
import type { CSSProperties, ReactNode } from 'react'
import './diagram.css'

/* ------------------------------------------------------------------ frame -- */

/**
 * The wrapper every diagram shares: a `figure`, an optional caption, and the
 * marker the smoke check counts.
 *
 * `data-diagram` is that marker. A page's blocks are counted from its source
 * and from the rendered document and the two must agree, so a diagram whose
 * mapping went missing has to be visible to the count — the failure a book of
 * components has is silent, not loud.
 */
function Figure({
  kind,
  caption,
  scroll = false,
  children,
}: {
  kind: string
  caption?: ReactNode
  scroll?: boolean
  children: ReactNode
}) {
  return (
    <figure className="diagram" data-diagram={kind}>
      {scroll ? <div className="diagram-scroll">{children}</div> : children}
      {caption ? <figcaption>{caption}</figcaption> : null}
    </figure>
  )
}

/** An arrowhead, sized from the text around it and coloured by it. */
function Arrow({ dir = 'right' }: { dir?: 'right' | 'left' | 'down' }) {
  const turn = { right: 0, left: 180, down: 90 }[dir]
  return (
    <svg
      className="arrow"
      width="1em"
      height="1em"
      viewBox="0 0 16 16"
      aria-hidden="true"
      focusable="false"
      style={{ transform: `rotate(${turn}deg)` }}
    >
      <path d="M1 8h11M8.5 4l4 4-4 4" fill="none" stroke="currentColor" strokeWidth="1.5" />
    </svg>
  )
}

/** The head of a sequence message. Its shaft is the rule beside it. */
function Head({ dir }: { dir: 'left' | 'right' }) {
  return (
    <svg
      className="head"
      width="9"
      height="9"
      viewBox="0 0 9 9"
      aria-hidden="true"
      focusable="false"
      style={dir === 'left' ? { transform: 'rotate(180deg)' } : undefined}
    >
      <path d="M0 0.5 L9 4.5 L0 8.5 Z" fill="currentColor" />
    </svg>
  )
}

/** The connector between two stacked nodes: a line, then a head. */
function Link() {
  return (
    <svg
      className="flow-link"
      width="16"
      height="28"
      viewBox="0 0 16 28"
      aria-hidden="true"
      focusable="false"
    >
      <path d="M8 0v22M4.5 18l3.5 4 3.5-4" fill="none" stroke="currentColor" strokeWidth="1.5" />
    </svg>
  )
}

/* ------------------------------------------------------------ byte layout -- */

export type ByteField = {
  /** What the field is called, in the words the protocol uses. */
  name: string
  /** Its size, in whatever unit the diagram counts. Sets the field's width. */
  size?: number
  /** How that size is written — `1`, `24 bits`, `length bytes`. */
  span?: string
  /** An example of what it holds, for a diagram showing one real message. */
  value?: string
  /** What the field's size buys — the capacity a bit width comes to. */
  foot?: string
  /** A field whose size is not fixed: hatched, because it is not to scale. */
  variable?: boolean
  /** The numbers at this field's two edges, for a bit layout. */
  from?: string
  to?: string
  /** A run this field belongs to, braced underneath with that label. */
  group?: string
}

/**
 * A byte or bit layout: the fields of a header, each drawn at its own size.
 *
 * Widths are `flex-grow`, so `stream:4` really is four times `kind:1` and a
 * reader can count the header off the picture. A variable-length field cannot
 * be to scale and says so by hatching rather than by being drawn at a width
 * that would be a lie.
 */
export function Bytes({
  fields,
  caption,
}: {
  fields: ByteField[]
  caption?: ReactNode
}) {
  const scaled = fields.some((f) => f.from || f.to)
  const grouped = fields.some((f) => f.group)

  // A run of neighbouring fields sharing a `group` is braced as one.
  const runs: { label?: string; grow: number }[] = []
  for (const field of fields) {
    const last = runs[runs.length - 1]
    const grow = field.size ?? 1
    if (last && last.label === field.group) last.grow += grow
    else runs.push({ label: field.group, grow })
  }

  return (
    <Figure kind="bytes" caption={caption} scroll>
      {scaled ? (
        <div className="bytes-scale" aria-hidden="true">
          {fields.map((field, i) => (
            <span key={i} style={{ flex: `${field.size ?? 1} 1 0` }}>
              <span>{field.from ?? ''}</span>
              <span>{field.to ?? ''}</span>
            </span>
          ))}
        </div>
      ) : null}

      <div className="bytes-row">
        {fields.map((field, i) => (
          <div
            key={i}
            className={`bytes-field${field.variable ? ' is-variable' : ''}`}
            style={{ flex: `${field.size ?? 1} 1 0` }}
          >
            <span className="name">{field.name}</span>
            {field.span ? <span className="size">{field.span}</span> : null}
            {field.value ? <span className="value">{field.value}</span> : null}
            {field.foot ? <span className="size">{field.foot}</span> : null}
          </div>
        ))}
      </div>

      {grouped ? (
        <div className="bytes-groups">
          {runs.map((run, i) => (
            <div
              key={i}
              className={`bytes-group${run.label ? ' is-braced' : ''}`}
              style={{ flex: `${run.grow} 1 0` }}
            >
              {run.label ?? ''}
            </div>
          ))}
        </div>
      ) : null}
    </Figure>
  )
}

/* ---------------------------------------------------------------- mapping -- */

/**
 * A named map from one shape to another — the two column families, as two
 * rows that stay aligned because they are a grid rather than space padding.
 *
 * Written one row per line, as `name | key → value`.
 */
export function Mapping({ children, caption }: { children: string; caption?: ReactNode }) {
  const rows = lines(children).map((line) => {
    const [name, rest = ''] = split(line, '|')
    const [from, to] = split(rest, '→')
    return { name, from, to }
  })

  return (
    <Figure kind="mapping" caption={caption}>
      <div className="mapping">
        {rows.map((row, i) => (
          <div className="mapping-row" key={i}>
            <span className="diagram-chip">{row.name}</span>
            <span className="from">{row.from}</span>
            <span className="arrow" aria-hidden="true">
              <Arrow />
            </span>
            <span className="to">{row.to}</span>
          </div>
        ))}
      </div>
    </Figure>
  )
}

/* --------------------------------------------------------------- sequence -- */

/**
 * A frame exchange, as a sequence diagram: two lifelines, and time going down.
 *
 * The book's protocol pages are all one shape — a client says something, a
 * server answers — and that is what a sequence diagram is for. Each party gets
 * a lifeline, each message an arrow between them, and the order on the page is
 * the order on the wire. Direction is the arrowhead and the side it lands on,
 * so nothing depends on a reader noticing which way a glyph points.
 *
 * **It stays a sequence diagram on a phone.** The lifelines are set in from
 * each edge by half an actor's width, so the message area is whatever is left
 * and the labels wrap inside it rather than the whole picture scrolling
 * sideways. Below 30rem the actors narrow and the diagram keeps its shape.
 *
 * Written one message per line: `→ S  what it carries`, with `…` on a line of
 * its own for a run that repeats.
 */
export function Sequence({
  children,
  from = 'client',
  to = 'server',
  caption,
}: {
  children: string
  from?: string
  to?: string
  caption?: ReactNode
}) {
  const rows = lines(children).map((line) => {
    if (/^[….]+$/.test(line.trim())) return { more: true, out: false, code: '', text: '' }
    const out = line.trimStart().startsWith('→')
    const rest = line.replace(/^\s*[→←]\s*/, '')
    const [code, text] = split(rest, /\s{2,}/)
    return { more: false, out, code, text }
  })

  return (
    <Figure kind="sequence" caption={caption}>
      <div className="sequence">
        <div className="sequence-actors">
          <span className="actor">{from}</span>
          <span className="actor">{to}</span>
        </div>
        <ol className="sequence-body">
          {rows.map((row, i) =>
            row.more ? (
              <li className="message is-more" key={i}>
                <span className="pause" aria-hidden="true">
                  ⋮
                </span>
              </li>
            ) : (
              <li className={`message ${row.out ? 'is-out' : 'is-in'}`} key={i}>
                <span className="label">
                  <span className="diagram-chip">{row.code}</span>
                  <span className="what">{row.text}</span>
                  {/* The arrow is a picture, so the direction is said once in
                      words for a reader who is not looking at it. */}
                  <span className="diagram-sr"> — to the {row.out ? to : from}</span>
                </span>
                <span className="wire" aria-hidden="true">
                  {row.out ? null : <Head dir="left" />}
                  <span className="rule" />
                  {row.out ? <Head dir="right" /> : null}
                </span>
              </li>
            ),
          )}
        </ol>
      </div>
    </Figure>
  )
}

/* ------------------------------------------------------------------- tree -- */

/**
 * A directory or command tree: an indent guide drawn as a border, and the
 * note about each entry in a column of its own rather than space-padded into
 * alignment behind it.
 *
 * Written one entry per line as `name    what it holds`, with a leading run of
 * `-` for the depth and a trailing `/` marking a directory.
 *
 * **Depth is a marker rather than indentation**, and that is not a taste: MDX
 * strips the leading whitespace of every line inside a JSX expression, so a
 * tree written as an indented block arrives here flat. A character it cannot
 * take away is the only depth that survives the compile.
 */
export function Tree({ children, caption }: { children: string; caption?: ReactNode }) {
  const raw = lines(children).map((line) => {
    const depth = /^-*/.exec(line)?.[0].length ?? 0
    const [label, note] = split(line.replace(/^-*\s*/, ''), /\s{2,}/)
    return { depth, label, note }
  })

  // Whether an entry is the last at its own depth before the level closes:
  // that is what decides if its rail stops at the elbow or runs past it, and
  // which of its ancestors still need a rail drawn beside this row.
  const rows = raw.map((row, i) => {
    let last = true
    for (let j = i + 1; j < raw.length; j++) {
      if (raw[j].depth < row.depth) break
      if (raw[j].depth === row.depth) {
        last = false
        break
      }
    }
    // One rail per *ancestor* level that still has entries to come, which is
    // depth - 1 of them: the elbow below accounts for the row's own level, and
    // the outermost level has nothing to its left.
    const open: boolean[] = []
    for (let d = 1; d < row.depth; d++) {
      let more = false
      for (let j = i + 1; j < raw.length; j++) {
        if (raw[j].depth < d) break
        if (raw[j].depth === d) {
          more = true
          break
        }
      }
      open.push(more)
    }
    return { ...row, last, open }
  })

  return (
    <Figure kind="tree" caption={caption}>
      <div className="tree">
        {rows.map((row, i) => (
          <div
            className="tree-row"
            key={i}
            // The row is `display: contents`, so this reaches the cells it
            // holds: on a phone the note wraps under the name and has to line
            // up with it, which only the row knows how deep it is.
            style={{ '--depth': row.depth } as CSSProperties}
          >
            <span className="name">
              {row.open.map((drawn, d) => (
                <span key={d} className={`rail${drawn ? '' : ' is-blank'}`} aria-hidden="true" />
              ))}
              {row.depth > 0 ? (
                <span
                  className={`rail is-elbow${row.last ? ' is-last' : ''}`}
                  aria-hidden="true"
                />
              ) : null}
              <span className="label">{row.label}</span>
            </span>
            {row.note ? <span className="note">{row.note}</span> : <span />}
          </div>
        ))}
      </div>
    </Figure>
  )
}

/* ------------------------------------------------------------------- flow -- */

export type Stage = {
  /** The stage's name. */
  label?: string
  /** A sequence *inside* one stage, as chips: the compiler's phases. */
  phases?: string[]
  /** What the stage is, in a sentence. */
  note?: string
  /** `io` for what enters and leaves, `seam` for the contract in the middle. */
  tone?: 'io' | 'seam'
  /** The band this stage sits in. Consecutive stages naming one are drawn in it. */
  band?: string
}

/**
 * A pipeline, top to bottom, banded where the halves of a system meet.
 *
 * The seam is the one node in the accent, because what the picture is *for* is
 * that the two halves meet at exactly one thing.
 */
export function Flow({ stages, caption }: { stages: Stage[]; caption?: ReactNode }) {
  // A run of neighbouring stages naming one band is drawn inside it. A stage
  // naming none stands on its own, which is what puts the seam between the
  // two halves rather than inside either.
  const groups: { band?: string; stages: Stage[] }[] = []
  for (const stage of stages) {
    const last = groups[groups.length - 1]
    if (last && last.band === stage.band) last.stages.push(stage)
    else groups.push({ band: stage.band, stages: [stage] })
  }

  return (
    <Figure kind="flow" caption={caption}>
      <div className="flow">
        {groups.map((group, g) => (
          <Node key={g} first={g === 0}>
            {group.band ? (
              <div className="flow-band">
                <span className="caption">{group.band}</span>
                {group.stages.map((stage, i) => (
                  <Node key={i} first={i === 0}>
                    <StageBox stage={stage} />
                  </Node>
                ))}
              </div>
            ) : (
              group.stages.map((stage, i) => (
                <Node key={i} first={i === 0}>
                  <StageBox stage={stage} />
                </Node>
              ))
            )}
          </Node>
        ))}
      </div>
    </Figure>
  )
}

/** A node, with the connector that leads into it unless it is the first. */
function Node({ first, children }: { first: boolean; children: ReactNode }) {
  return (
    <>
      {first ? null : <Link />}
      {children}
    </>
  )
}

function StageBox({ stage }: { stage: Stage }) {
  const tone = stage.tone ? ` is-${stage.tone}` : ''
  return (
    <div className={`flow-node${tone}`}>
      {stage.label ? <span className="title">{stage.label}</span> : null}
      {stage.phases ? (
        <span className="flow-phases">
          {stage.phases.map((phase, i) => (
            <span key={i}>
              {i ? (
                <span className="sep" aria-hidden="true">
                  {' → '}
                </span>
              ) : null}
              {phase}
            </span>
          ))}
        </span>
      ) : null}
      {stage.note ? <span className="note">{stage.note}</span> : null}
    </div>
  )
}

/* ---------------------------------------------------------------- journey -- */

/**
 * One query's whole journey, as lanes of numbered steps with the hop between
 * them named.
 *
 * Written as lines: `# who` opens a lane, `| what the hop costs` is the note
 * between two lanes, and anything else is a step.
 */
export function Journey({ children, caption }: { children: string; caption?: ReactNode }) {
  const blocks: { who?: string; hop?: string; steps: string[] }[] = []
  for (const line of lines(children)) {
    if (line.startsWith('#')) blocks.push({ who: line.slice(1).trim(), steps: [] })
    else if (line.startsWith('|')) blocks.push({ hop: line.slice(1).trim(), steps: [] })
    else blocks[blocks.length - 1]?.steps.push(line.trim())
  }

  // A line opening with an ellipsis is a note on the steps above it rather
  // than a step of its own, so it takes no number.
  const isNote = (step: string) => step.startsWith('…')

  {
  }

  let n = 0
  return (
    <Figure kind="journey" caption={caption}>
      <div className="journey">
        {blocks.map((block, i) =>
          block.hop !== undefined ? (
            <div className="journey-hop" key={i}>
              <Link />
              <span className="via">{block.hop}</span>
            </div>
          ) : (
            <div className="journey-lane" key={i}>
              <span className="who">{block.who}</span>
              {block.steps.map((step) => {
                const note = isNote(step)
                if (!note) n += 1
                return (
                  <div className={`journey-step${note ? ' is-note' : ''}`} key={step}>
                    <span className="n">{note ? '' : n}</span>
                    <span className="what">{note ? step.replace(/^…\s*/, '') : step}</span>
                  </div>
                )
              })}
            </div>
          ),
        )}
      </div>
    </Figure>
  )
}

/* ----------------------------------------------------------------- ladder -- */

/**
 * The measurement ladder: rungs, grouped by what can be compared with what.
 *
 * Written one rung per line as `S1  executor  what it isolates`, with a line
 * of `--` between groups.
 */
export function Ladder({ children, caption }: { children: string; caption?: ReactNode }) {
  const groups: { id: string; what: string; note: string }[][] = [[]]
  for (const line of lines(children)) {
    if (/^-+$/.test(line.trim())) {
      groups.push([])
      continue
    }
    const [id, rest = ''] = split(line.trim(), /\s{2,}/)
    const [what, note] = split(rest, /\s{2,}/)
    groups[groups.length - 1].push({ id, what, note })
  }

  return (
    <Figure kind="ladder" caption={caption}>
      <div className="ladder">
        {groups
          .filter((group) => group.length)
          .map((group, g) => (
            <div className="ladder-group" key={g}>
              {group.map((rung) => (
                <div className="ladder-rung" key={rung.id}>
                  <span className="diagram-chip">{rung.id}</span>
                  <span className="what">{rung.what}</span>
                  <span className="note">{rung.note}</span>
                </div>
              ))}
            </div>
          ))}
      </div>
    </Figure>
  )
}

/* -------------------------------------------------------------- lifecycle -- */

/**
 * A database's three states and the two verbs that move between them.
 *
 * It takes no content because there is only one of these and it appears on two
 * pages: a component with no props cannot drift between them.
 */
export function Lifecycle({ caption }: { caption?: ReactNode }) {
  return (
    <Figure kind="lifecycle" caption={caption}>
      <div className="lifecycle">
        <div className="lifecycle-track">
          <div className="lifecycle-edge">
            <Arrow />
            <span className="verb">create</span>
          </div>
          <div className="lifecycle-state">
            <span className="name">Writable</span>
            <span className="can">ingest, derive</span>
          </div>
          <div className="lifecycle-edge">
            <Arrow />
            <span className="verb">finish</span>
          </div>
          <div className="lifecycle-state is-final">
            <span className="name">Complete</span>
            <span className="can">read only, forever</span>
          </div>
        </div>
        <p className="lifecycle-aside">
          A build that fails leaves the database <strong>Broken</strong>, named so it is not
          mistaken for either of the other two.
        </p>
      </div>
    </Figure>
  )
}

/* ------------------------------------------------------------------ parse -- */

/** The non-empty lines of a template literal, with its common indent removed. */
function lines(source: string): string[] {
  const kept = source.split('\n').filter((line) => line.trim())
  const indent = Math.min(...kept.map((line) => line.length - line.trimStart().length))
  return kept.map((line) => line.slice(indent).trimEnd())
}

/** A line cut in two at the first separator, both halves trimmed. */
function split(line: string, at: string | RegExp): [string, string] {
  const found = typeof at === 'string' ? line.indexOf(at) : line.search(at)
  if (found < 0) return [line.trim(), '']
  const width = typeof at === 'string' ? at.length : (line.slice(found).match(at)?.[0].length ?? 1)
  return [line.slice(0, found).trim(), line.slice(found + width).trim()]
}
