/**
 * **One question, answered end to end, on a loop.**
 *
 * The hero used to be an empty query box, which assumes a reader already knows
 * what to type — and the whole difficulty with a new database is that nobody
 * does. So it shows the thing being used: a cursor arrives on a name, the
 * editor opens a card and goes looking, the query behind that card slides in,
 * the engine walks the index, the card fills in, and the cursor leaves. Then it
 * does it again.
 *
 * **Everything in it comes from the engine.** The file pane is built from the
 * demo database's own `code.Decl` rows — their names, their lines and the
 * signature each one carries on its value side — so the code on screen is the
 * code the index is about, painted by the same Rust rules the code browser
 * falls back to. The scan is the real trace, folded a frame at a time. The
 * answer is what the query returns. The only invented thing is the pointer.
 */
import { useEffect, useMemo, useRef, useState } from 'react'
import { Spinner } from '@astryxdesign/core/Spinner'
import { Text } from '@astryxdesign/core/Text'
import { useEngine } from './engine'
import { Code } from './book/Code'
import { tokenize } from './book/highlight'
import { fold, type Moment } from './run'
import type { Database, RowBytes } from './wasm'

/** The name the story is about, and the one the cursor comes to rest on. */
const SUBJECT = 'Config'

/** Find-all-references, which is the question a code index exists to answer. */
const QUERY = `{file = P, name = N, line = L} where
  code.Ref {to = code.Decl {name = "${SUBJECT}"},
            from = code.Decl {file = F, name = N, line = L}};
  F = code.File P`

/**
 * **The file the story opens on.**
 *
 * The declarations are the index's own: `null` is a line the database has a
 * `code.Decl` for, and its name, its line number and its signature are spliced
 * in from there. The lines between them are invented, and are here so the pane
 * reads as a file rather than as three facts in a list — a hero claiming to be
 * a code index should look like one. Nothing in the story is read off them.
 */
const LIB_RS: (string | null)[] = [
  'use std::{fs, path::PathBuf};',
  '',
  null, //                                   3 — struct Config
  '    root: PathBuf,',
  '    strict: bool,',
  '}',
  '',
  'impl Default for Config {',
  '    fn default() -> Self {',
  '        Self { root: PathBuf::from("."), strict: false }',
  '    }',
  '}',
  '',
  '/// Read a configuration file from disk.',
  '///',
  '/// Errors if the file is missing, or if the',
  '/// text it holds is not valid configuration.',
  '#[must_use]',
  '#[allow(clippy::needless_pass_by_value)]',
  null, //                                  20 — fn load(path: &str) -> Config
  '    let text = fs::read_to_string(path).unwrap_or_default();',
  '    parse(&text)',
]

/** The predicates this query reads, in the order a scan meets them. */
const TOUCHED = ['code.Decl', 'code.File', 'code.Ref']

/**
 * The beats, in order, and how long each holds. `scan` sets its own pace from
 * the trace. They loop, so the cursor has to leave again — an animation that
 * ends with the pointer parked on the word cannot start over without a cut.
 */
const BEATS = ['approach', 'hover', 'query', 'scan', 'answer', 'leave', 'rest'] as const
type Beat = (typeof BEATS)[number]
const HOLD: Record<Beat, number> = {
  approach: 820,
  hover: 1150,
  query: 2700,
  scan: 600,
  answer: 2700,
  leave: 850,
  rest: 550,
}
const FRAME = 190

/** The four a reader can jump to; the rest are the movement between them. */
const DOTS: { at: number; label: string }[] = [
  { at: 1, label: 'The question' },
  { at: 2, label: 'The query' },
  { at: 3, label: 'The scan' },
  { at: 4, label: 'The answer' },
]

const CAPTION: Record<Beat, string> = {
  approach: 'You hover a name. Your editor opens a card and goes looking.',
  hover: 'You hover a name. Your editor opens a card and goes looking.',
  query: 'Behind it, one query. There is no API to learn — this is the whole of it.',
  scan: 'Fjord walks the index, one stored row at a time.',
  answer: 'Two references, with the file and the line that holds each one.',
  leave: 'Two references, with the file and the line that holds each one.',
  rest: 'Two references, with the file and the line that holds each one.',
}

/** Whether this reader has asked for no movement. */
const still = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false

type Decl = { id: string; file: string; name: string; line: number; signature: string }

/** Everything the panels need, read out of the engine once. */
type Line = { n: number; text: string; subject: boolean; hit: boolean }

type Story = {
  file: string
  lines: Line[]
  decls: Map<string, Decl>
  index: { predicate: string; row: RowBytes }[]
  moments: Moment[]
  current: (string | null)[]
  examined: number[]
  answer: Record<string, unknown>[]
}

export function HeroStory() {
  const { engine, failure } = useEngine(true)
  // Read at first render rather than in an effect: starting the story and then
  // cancelling it is a frame of movement for exactly the reader who asked for
  // none. They get the answer, and the dots to walk the rest themselves.
  // Starts on the last beat, not the first: the cursor's travel is a CSS
  // transition, and a transition needs something to change. Beginning already
  // on `approach` would find the pointer at its destination with nothing to
  // animate, so the first thing a reader saw would be the one frame the whole
  // panel is about.
  const [beat, setBeat] = useState(() => (still() ? 4 : BEATS.length - 1))
  const [frame, setFrame] = useState(0)
  const [playing, setPlaying] = useState(() => !still())

  const story = useMemo((): Story | null => {
    if (!engine) return null
    try {
      const schema = engine.sampleSchema
      const db: Database = engine.database(schema)
      const rowsOf = (name: string) => db.predicates.find((p) => p.name === name)?.rows ?? []

      const paths = new Map(rowsOf('code.File').map((r) => [r.fact, r.decoded as string]))
      const decls = new Map<string, Decl>(
        rowsOf('code.Decl').map((r) => {
          const key = r.decoded as { file: string; name: string; line: number }
          return [
            r.fact,
            {
              id: r.fact,
              file: paths.get(key.file) ?? key.file,
              name: key.name,
              line: key.line,
              signature: String(r.value_decoded ?? ''),
            },
          ]
        }),
      )

      const subject = [...decls.values()].find((d) => d.name === SUBJECT)
      const answer = engine.run(schema, QUERY).rows.map((r) => r.value as Record<string, unknown>)
      const here = new Map(
        [...decls.values()].filter((d) => d.file === subject?.file).map((d) => [d.line, d]),
      )
      // A declaration's own line carries its own signature, with the brace the
      // index has no reason to store.
      const lines: Line[] = LIB_RS.map((filler, i) => {
        const n = i + 1
        const decl = here.get(n)
        return {
          n,
          text: filler ?? `${decl?.signature ?? ''} {`,
          subject: decl?.name === SUBJECT,
          hit: answer.some((row) => row.file === subject?.file && row.line === n),
        }
      })

      const index = TOUCHED.flatMap((name) =>
        rowsOf(name).map((row: RowBytes) => ({ predicate: name, row })),
      )

      const trace = engine.trace(schema, QUERY)
      // One frame per *moment*, not per transition: a reader watching rows
      // light up does not want the four steps it takes to leave one of them.
      const frames: number[] = []
      let last = ''
      for (let at = 0; at < trace.steps.length; at++) {
        const moment = fold(trace, at)
        const mark = [...moment.held].sort().join(',') + '|' + moment.rows.length
        if (mark !== last) {
          frames.push(at)
          last = mark
        }
      }
      const moments = frames.map((at) => fold(trace, at))

      return {
        file: subject?.file ?? '',
        lines,
        decls,
        index,
        moments,
        // **One row is current, the rest are merely held.** A four-level plan
        // stands on four rows at once, and lighting all of them says the
        // machine is in four places.
        current: moments.map((m) => m.registers.find((r) => r.written)?.key ?? null),
        examined: frames.map((at) =>
          trace.steps[at].examined.reduce((total, rows) => total + rows, 0),
        ),
        answer,
      }
    } catch {
      return null
    }
  }, [engine])

  const count = story?.moments.length ?? 0

  useEffect(() => {
    if (!playing || !story) return
    const here = BEATS[beat]
    if (here === 'scan' && frame < count - 1) {
      const timer = setTimeout(() => setFrame((f) => f + 1), FRAME)
      return () => clearTimeout(timer)
    }
    const timer = setTimeout(() => {
      setBeat((b) => (b + 1) % BEATS.length)
      setFrame(0)
    }, HOLD[here])
    return () => clearTimeout(timer)
  }, [playing, story, beat, frame, count])

  const here = BEATS[beat]
  // **The cursor sets out on `approach` and the card opens on `hover`.** Tied
  // to the same beat they happened at once, and the card was open before the
  // pointer had reached the word it was about.
  const onName = beat <= 4
  const cardOpen = beat >= 1 && beat <= 5
  const answered = beat === 4 || beat === 5
  const panelOpen = here === 'query' || here === 'scan'

  const at = Math.min(frame, Math.max(count - 1, 0))
  const moment = here === 'scan' ? (story?.moments[at] ?? null) : null
  const current = here === 'scan' ? (story?.current[at] ?? null) : null
  const examined = story?.examined[at] ?? 0

  return (
    <div className="story" data-testid="hero-story">
      <div className="story-body">
        <Pane story={story} onName={onName} cardOpen={cardOpen} answered={answered} />

        {/* Slides in over the file, and back out when it has had its say. */}
        <aside className={`story-panel${panelOpen ? ' is-open' : ''}`} aria-hidden={!panelOpen}>
          {!story ? null : here === 'scan' ? (
            <Scanning story={story} moment={moment} current={current} examined={examined} />
          ) : (
            <div className="story-query">
              <div className="story-scan-head">
                <span>the query</span>
              </div>
              <Code lang="sigla" source={QUERY} />
            </div>
          )}
        </aside>

        {failure ? <p className="story-note">The engine did not load: {failure}</p> : null}
        {!story && !failure ? (
          <p className="story-note">
            <Spinner size="sm" /> loading the engine…
          </p>
        ) : null}
      </div>

      <div className="story-foot">
        <Text as="p" size="sm" color="secondary" className="story-caption">
          {CAPTION[here]}
        </Text>
        <div className="story-dots">
          {DOTS.map((dot) => (
            <button
              key={dot.at}
              type="button"
              className={beat === dot.at ? 'is-at' : undefined}
              aria-label={dot.label}
              aria-current={beat === dot.at}
              onClick={() => {
                setPlaying(false)
                setBeat(dot.at)
                setFrame(dot.at === 3 ? Math.max(count - 1, 0) : 0)
              }}
            />
          ))}
          <button
            type="button"
            className="story-play"
            aria-label={playing ? 'Pause the demonstration' : 'Play the demonstration'}
            onClick={() => setPlaying((p) => !p)}
          >
            {playing ? 'Pause' : 'Play'}
          </button>
        </div>
      </div>
    </div>
  )
}

/** The file, built out of what the index knows about it. */
function Pane({
  story,
  onName,
  cardOpen,
  answered,
}: {
  story: Story | null
  onName: boolean
  cardOpen: boolean
  answered: boolean
}) {
  if (!story) return <div className="story-file" />
  return (
    <div className="story-file">
      <div className="story-file-name">{story.file}</div>
      <ol className="story-code">
        {story.lines.map((line) => (
          <li key={line.n} className={answered && line.hit ? 'is-hit' : undefined}>
            <span className="n">{line.n}</span>
            <span className="src">
              <Rust text={line.text} subject={line.subject} />
            </span>
            {line.subject ? (
              <>
                {cardOpen ? <Popover story={story} answered={answered} /> : null}
                <Pointer on={onName} />
              </>
            ) : null}
          </li>
        ))}
      </ol>
    </div>
  )
}

/**
 * A line of Rust, painted by the rules the code browser falls back to — so the
 * code in the hero looks like the code in the browser, rather than like plain
 * text with one word picked out of it.
 *
 * Only the declaration's own line boxes the subject. Marking every occurrence
 * is what an editor does, but the lines around it are invented and the index
 * has no reference on them — so a reader counting the boxes would get a
 * different answer from the one the card gives.
 */
function Rust({ text, subject }: { text: string; subject?: boolean }) {
  const tokens = useMemo(() => tokenize(text, 'rust'), [text])
  const out: React.ReactNode[] = []
  let cut = 0
  tokens.forEach((token, i) => {
    if (token.start > cut) out.push(text.slice(cut, token.start))
    const word = text.slice(token.start, token.end)
    out.push(
      <span
        key={i}
        className={`story-tok story-tok-${token.type}${subject && word === SUBJECT ? ' subject' : ''}`}
      >
        {word}
      </span>,
    )
    cut = token.end
  })
  if (cut < text.length) out.push(text.slice(cut))
  return <>{out}</>
}

/** The one invented thing on this panel. */
function Pointer({ on }: { on: boolean }) {
  const paint = {
    fill: 'var(--color-background-card)',
    stroke: 'var(--color-text-primary)',
    strokeWidth: 1.3,
    strokeLinejoin: 'round' as const,
  }
  return (
    <span className={`story-cursor${on ? ' is-on' : ''}`} aria-hidden="true">
      <span className="story-cursor-y">
        <svg className="is-arrow" width="18" height="18" viewBox="0 0 18 18">
          <path d="M2 1.5 L2 13.5 L5.4 10.4 L7.6 15.4 L10 14.3 L7.8 9.5 L12.3 9.3 Z" {...paint} />
        </svg>
        <svg className="is-hand" width="20" height="22" viewBox="0 0 20 22">
          <path
            d="M6.4 12V3.9a1.7 1.7 0 0 1 3.4 0v4.5a1.5 1.5 0 0 1 3 0v1a1.5 1.5 0 0 1 3 0v1.1
               a1.45 1.45 0 0 1 2.5 1v3.2c0 3.3-2.4 6.1-6 6.1h-1.4c-1.9 0-3.2-.8-4.3-2.3L3.1 14
               a1.6 1.6 0 0 1 2.4-2.1z"
            {...paint}
          />
        </svg>
      </span>
    </span>
  )
}

/** What the editor draws: open and looking, then filled in. */
function Popover({ story, answered }: { story: Story; answered: boolean }) {
  return (
    <div className="story-popover" role="note" aria-live="polite">
      {answered ? (
        <>
          <b>
            {story.answer.length} references to <code>{SUBJECT}</code>
          </b>
          <ul>
            {story.answer.map((row, i) => (
              <li key={i}>
                <code>
                  {String(row.file)}:{String(row.line)}
                </code>
                <span>in {String(row.name)}</span>
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p className="story-looking">
          <Spinner size="sm" /> finding references…
        </p>
      )}
    </div>
  )
}

/** The index, with the row the machine is standing on. */
function Scanning({
  story,
  moment,
  current,
  examined,
}: {
  story: Story
  moment: Moment | null
  current: string | null
  examined: number
}) {
  const list = useRef<HTMLOListElement>(null)
  const here = useRef<HTMLLIElement>(null)

  /**
   * **Keep the walked row in view, and touch nothing else.**
   *
   * `scrollIntoView` walks up every scrollable ancestor, so a playback running
   * in a hero that is half off the screen drags the whole page back to it — and
   * this one loops, so it does that every few seconds while somebody is trying
   * to read further down. Setting `scrollTop` on the list moves the list.
   */
  useEffect(() => {
    const box = list.current
    const row = here.current
    if (!box || !row) return
    const top = row.offsetTop
    const bottom = top + row.offsetHeight
    if (top < box.scrollTop) box.scrollTop = top
    else if (bottom > box.scrollTop + box.clientHeight) box.scrollTop = bottom - box.clientHeight
  }, [current])

  return (
    <div className="story-scan">
      <div className="story-scan-head">
        <span>the index</span>
        <span className="examined">{examined} rows examined</span>
      </div>
      <ol className="story-rows" ref={list}>
        {story.index.map(({ predicate, row }) => {
          const isHere = row.key === current
          const held = moment?.held.has(row.key) ?? false
          const dropped = moment?.droppedSoFar.has(row.key) ?? false
          return (
            <li
              key={row.key}
              ref={isHere ? here : undefined}
              className={isHere ? 'is-here' : held ? 'is-held' : dropped ? 'is-dropped' : undefined}
            >
              <span className="p">{predicate.replace('code.', '')}</span>
              <span className="k">{label(predicate, row, story.decls)}</span>
            </li>
          )
        })}
      </ol>
    </div>
  )
}

/** A stored row, in the shortest form that still says which row it is. */
function label(predicate: string, row: RowBytes, decls: Map<string, Decl>): string {
  const named = (id: unknown) => decls.get(String(id))?.name ?? String(id)
  if (predicate === 'code.File') return `"${String(row.decoded)}"`
  if (predicate === 'code.Decl') {
    const key = row.decoded as { name: string; line: number }
    return `${key.name} · line ${key.line}`
  }
  const key = row.decoded as { from: string; to: string }
  return `${named(key.from)} → ${named(key.to)}`
}
