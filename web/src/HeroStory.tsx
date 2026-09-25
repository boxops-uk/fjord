/**
 * **One question, answered end to end, on the landing page.**
 *
 * The hero used to be an empty query box, which assumes a reader already knows
 * what to type — and the whole difficulty with a new database is that nobody
 * does. So this shows the thing being used instead: you hover a name in an
 * editor, that becomes one query, the engine walks the index, and the answer
 * comes back as the two places the name is used.
 *
 * **Everything in it comes from the engine.** The file pane is built from the
 * demo database's own `code.Decl` rows — their names, their line numbers and
 * the signature each one carries on its value side — so the code on screen is
 * the code the index is about. The scan is the real trace, folded a frame at a
 * time. The answer is what the query returns. The only invented thing is the
 * mouse pointer.
 */
import { useEffect, useMemo, useRef, useState } from 'react'
import { Spinner } from '@astryxdesign/core/Spinner'
import { Text } from '@astryxdesign/core/Text'
import { useEngine } from './engine'
import { Code } from './book/Code'
import { fold, type Moment } from './run'
import type { Database, RowBytes } from './wasm'

/** The name the story is about, and the one the pane's cursor sits on. */
const SUBJECT = 'Config'

/** Find-all-references, which is the question a code index exists to answer. */
const QUERY = `{file = P, name = N, line = L} where
  code.Ref {to = code.Decl {name = "${SUBJECT}"},
            from = code.Decl {file = F, name = N, line = L}};
  F = code.File P`

/** The predicates this query reads, in the order a scan meets them. */
const TOUCHED = ['code.Decl', 'code.File', 'code.Ref']

const STAGES = [
  'You hover a name. Your editor opens a card and goes looking.',
  'Behind it, one query. There is no API to learn — this is the whole of it.',
  'Fjord walks the index, one stored row at a time.',
  'Two references, with the file and the line that holds each one.',
]

/** How long each stage holds, in milliseconds. The scan sets its own pace. */
const HOLD = [2900, 3000, 900, 0]
const FRAME = 190

/** Whether this reader has asked for no movement. */
const still = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false

type Decl = { id: string; file: string; name: string; line: number; signature: string }

/** Everything the panels need, read out of the engine once. */
type Story = {
  file: string
  pane: Decl[]
  decls: Map<string, Decl>
  index: { predicate: string; row: RowBytes }[]
  frames: number[]
  moments: Moment[]
  /** The row the machine moved to at each frame, and what it had read by then. */
  current: (string | null)[]
  examined: number[]
  answer: Record<string, unknown>[]
}

export function HeroStory() {
  const { engine, failure } = useEngine(true)
  // Somebody who has asked not to be moved gets the answer rather than the
  // journey, and the dots to walk it themselves. Read at first render rather
  // than in an effect: starting the story and then cancelling it is a frame of
  // movement for exactly the reader who asked for none.
  const [stage, setStage] = useState(() => (still() ? STAGES.length - 1 : 0))
  const [frame, setFrame] = useState(0)
  const [playing, setPlaying] = useState(() => !still())
  // Bumped on replay, and used as the pane's key: the cursor's travel and the
  // card's arrival are CSS animations, and an animation only plays on mount.
  const [run, setRun] = useState(0)

  const story = useMemo((): Story | null => {
    if (!engine) return null
    try {
      const schema = engine.sampleSchema
      const db: Database = engine.database(schema)
      const rowsOf = (name: string) => db.predicates.find((p) => p.name === name)?.rows ?? []

      // Every declaration, resolved through the file it names.
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

      // The file the pane shows is the one that declares the subject.
      const subject = [...decls.values()].find((d) => d.name === SUBJECT)
      const pane = [...decls.values()]
        .filter((d) => d.file === subject?.file)
        .sort((a, b) => a.line - b.line)

      // What the scan walks: the touched predicates, in scan order.
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
        pane,
        decls,
        index,
        frames,
        moments,
        // **One row is current, the rest are merely held.** A four-level plan
        // stands on four rows at once, and lighting all of them says the
        // machine is in four places. The one that changed at this step is where
        // it actually moved to.
        current: moments.map(
          (moment) => moment.registers.find((register) => register.written)?.key ?? null,
        ),
        // What the run had read by this frame, which is the engine's own count
        // rather than anything this page works out.
        examined: frames.map((at) =>
          trace.steps[at].examined.reduce((total, rows) => total + rows, 0),
        ),
        answer: engine.run(schema, QUERY).rows.map((r) => r.value as Record<string, unknown>),
      }
    } catch {
      return null
    }
  }, [engine])

  const count = story?.frames.length ?? 0
  useEffect(() => {
    if (!playing || !story) return
    if (stage === 2 && frame < count - 1) {
      const timer = setTimeout(() => setFrame((f) => f + 1), FRAME)
      return () => clearTimeout(timer)
    }
    if (stage < STAGES.length - 1) {
      const timer = setTimeout(() => setStage((s) => s + 1), HOLD[stage])
      return () => clearTimeout(timer)
    }
    return
  }, [playing, story, stage, frame, count])

  const goto = (next: number) => {
    setPlaying(false)
    setStage(next)
    setFrame(next === 2 ? Math.max(count - 1, 0) : 0)
  }

  const at = Math.min(frame, Math.max(count - 1, 0))
  const moment = (stage === 2 ? story?.moments[at] : null) ?? null
  const examined = stage === 2 ? (story?.examined[at] ?? 0) : 0
  const current = stage === 2 ? (story?.current[at] ?? null) : null

  return (
    <div className="story" data-testid="hero-story">
      <div className="story-panes">
        <Pane key={run} story={story} stage={stage} />
        <div className="story-stage">
          {failure ? (
            <p className="story-note">The engine did not load: {failure}</p>
          ) : !story ? (
            <p className="story-note">
              <Spinner size="sm" /> loading the engine…
            </p>
          ) : stage === 0 ? (
            <Asking />
          ) : stage === 1 ? (
            <Code lang="sigla" source={QUERY} />
          ) : stage === 2 ? (
            <Scanning story={story} moment={moment} current={current} examined={examined} />
          ) : (
            <Answer story={story} />
          )}
        </div>
      </div>

      <div className="story-foot">
        <Text as="p" size="sm" color="secondary" className="story-caption">
          {STAGES[stage]}
        </Text>
        <div className="story-dots">
          {STAGES.map((label, i) => (
            <button
              key={i}
              type="button"
              className={i === stage ? 'is-at' : undefined}
              aria-label={label}
              aria-current={i === stage}
              onClick={() => goto(i)}
            />
          ))}
          <button
            type="button"
            className="story-replay"
            onClick={() => {
              setStage(0)
              setFrame(0)
              setPlaying(true)
              setRun((n) => n + 1)
            }}
          >
            Replay
          </button>
        </div>
      </div>
    </div>
  )
}

/** The question, before anything has answered it. */
function Asking() {
  return (
    <div className="story-ask">
      <Text size="lg" weight="semibold">
        Where is <code>{SUBJECT}</code> used?
      </Text>
      <Text size="sm" color="secondary">
        The one question every code index exists to answer, and the one a grep cannot.
      </Text>
    </div>
  )
}

/** The file, built out of what the index knows about it. */
function Pane({ story, stage }: { story: Story | null; stage: number }) {
  const answered = stage === 3
  if (!story) return <div className="story-file" />
  return (
    <div className="story-file">
      <div className="story-file-name">{story.file}</div>
      <ol className="story-code">
        {story.pane.map((decl, i) => (
          <li
            key={decl.id}
            className={
              answered &&
              story.answer.some((row) => row.file === decl.file && row.line === decl.line)
                ? 'is-hit'
                : undefined
            }
          >
            {i > 0 ? <span className="gap" aria-hidden="true" /> : null}
            <span className="n">{decl.line}</span>
            <span className="src">
              <Signature text={decl.signature} />
            </span>
            {decl.name === SUBJECT ? (
              <>
                <Pointer />
                <Popover story={story} answered={answered} entering={stage === 0} />
              </>
            ) : null}
          </li>
        ))}
      </ol>
    </div>
  )
}

/** A declaration's own signature, with the subject picked out of it. */
function Signature({ text }: { text: string }) {
  const at = text.indexOf(SUBJECT)
  if (at < 0) return <>{text}</>
  return (
    <>
      {text.slice(0, at)}
      <span className="subject">{SUBJECT}</span>
      {text.slice(at + SUBJECT.length)}
    </>
  )
}

/**
 * The one invented thing on this panel: a cursor, travelling in and then
 * turning into the pointing hand a name you can act on always turns it into.
 *
 * Both shapes are drawn and cross-faded rather than swapped, so the arrival
 * reads as one gesture. The hand is nudged so its fingertip lands where the
 * arrow's tip was — a cursor that jumps at the moment of arrival undoes the
 * thing the travel was for.
 */
function Pointer() {
  const paint = {
    fill: 'var(--color-background-card)',
    stroke: 'var(--color-text-primary)',
    strokeWidth: 1.3,
    strokeLinejoin: 'round' as const,
  }
  return (
    <span className="story-pointer" aria-hidden="true">
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
  )
}

/**
 * What the editor draws: the card opens the moment you hover, with nothing in
 * it yet, and fills in when the answer arrives. Everything the rest of this
 * panel shows is what happened in between.
 */
function Popover({
  story,
  answered,
  entering,
}: {
  story: Story
  answered: boolean
  /** Only the opening stage plays the card's arrival. A reader who jumps
      straight to the answer should find the card already open, not wait out an
      animation timed from when the page loaded. */
  entering: boolean
}) {
  return (
    <div
      className={`story-popover${entering ? ' is-entering' : ''}`}
      role="note"
      aria-live="polite"
    >
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
  const here = useRef<HTMLLIElement>(null)
  useEffect(() => {
    here.current?.scrollIntoView({ block: 'nearest' })
  }, [current])

  return (
    <div className="story-scan">
      <div className="story-scan-head">
        <span>the index</span>
        <span className="examined">{examined} rows examined</span>
      </div>
      <ol className="story-rows">
        {story.index.map(({ predicate, row }) => {
          const here_ = row.key === current
          const held = moment?.held.has(row.key) ?? false
          const dropped = moment?.droppedSoFar.has(row.key) ?? false
          return (
            <li
              key={row.key}
              ref={here_ ? here : undefined}
              className={
                here_ ? 'is-here' : held ? 'is-held' : dropped ? 'is-dropped' : undefined
              }
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

/** What the answer is, in the language it was asked in. */
function Answer({ story }: { story: Story }) {
  return (
    <div className="story-answer">
      <div className="story-scan-head">
        <span>the answer</span>
        <span className="examined">{story.answer.length} rows</span>
      </div>
      <ol className="story-rows">
        {story.answer.map((row, i) => (
          <li key={i} className="is-answer">
            <span className="k">
              {'{'}file = &quot;{String(row.file)}&quot;, name = &quot;{String(row.name)}&quot;, line ={' '}
              {String(row.line)}
              {'}'}
            </span>
          </li>
        ))}
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
