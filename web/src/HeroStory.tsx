/**
 * **One question, answered end to end, on a loop — over the real index.**
 *
 * The hero used to be an empty query box, which assumes a reader already knows
 * what to type, and the whole difficulty with a new database is that nobody
 * does. So it shows the thing being used: a cursor arrives on a name, the
 * editor opens a card and goes looking, the query behind that card slides in,
 * the engine seeks into the index, the card fills in, and the cursor leaves.
 * Then it does it again.
 *
 * **It ran on a toy, and now it does not.** The first version traced over the
 * demo database, which is seventeen rows — so the playback walked almost all of
 * them and reported examining sixteen, which is a scan, which is the one thing
 * this engine is built not to do. A reader took the honest lesson from it. This
 * runs over the corpus the code browser reads, and the numbers it reports are
 * the ones that make the argument: seventeen rows read out of twenty-four
 * thousand, because the key leads with the field the query constrains.
 *
 * **Everything in it comes from the engine.** The file pane is the real file,
 * painted with the semantic runs the index itself carries. The band is the real
 * trace: the seek ranges are the bytes the executor opened, and the second one
 * has the first one's row spliced into it, which is what the engine does
 * instead of having a join. The answer is what the query returned. The only
 * invented thing is the pointer.
 */
import { useEffect, useMemo, useRef, useState } from 'react'
import { Spinner } from '@astryxdesign/core/Spinner'
import { Text } from '@astryxdesign/core/Text'
import { Code } from './book/Code'
import { loadCorpus, type Blob, type Corpus } from './corpus'
import { paint } from './highlight'
import { fold, type Moment } from './run'

/** The name the story is about, and the one the cursor comes to rest on. */
const SUBJECT = 'ByteBuffer'

/**
 * **Find-all-references, written so that both levels seek.**
 *
 * `nameLowercase` leads `SearchEntry`'s key and `target` leads `SymbolXRef`'s,
 * so each level is a range the executor opens rather than a filter it applies
 * to everything. Writing it against `name` instead would compile and answer the
 * same rows, and would scan the whole predicate to do it — which is the
 * difference the band underneath is there to show.
 */
const QUERY = `{file = P, at = S} where
  codemarkup.SearchEntry {nameLowercase = "bytebuffer", name = N, kind = _,
                          symbol = T, file = _, line = _};
  codemarkup.SymbolXRef {target = T, file = F, span = S};
  F = src.File P`

/** How much of the file the pane shows, and how much of it sits above the name. */
const WINDOW = 22
const LEAD = 7

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
  answer: 2900,
  leave: 850,
  rest: 550,
}
const FRAME = 230

/** The four a reader can jump to; the rest are the movement between them. */
const DOTS: { at: number; label: string }[] = [
  { at: 1, label: 'The question' },
  { at: 2, label: 'The query' },
  { at: 3, label: 'The seek' },
  { at: 4, label: 'The answer' },
]

/** Whether this reader has asked for no movement. */
const still = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false

/** A line of the file, with the colour runs the index carries for it. */
type Line = {
  n: number
  text: string
  tokens: { type: string; from: number; to: number }[]
  subject: boolean
}

/** One row the run actually opened, in the order it met them. */
type Row = { fact: string; predicate: string; label: string }

/** Where the answers are, as an editor would group them. */
type Where = { file: string; lines: number[] }

type Story = {
  path: string
  name: string
  lines: Line[]
  /** Every fact in the index, which is the number the band is measured against. */
  facts: number
  rows: Row[]
  moments: Moment[]
  /** Which opened row the machine stands on, per moment. */
  at: (number | null)[]
  /** The range the current level opened, per moment. */
  seeking: (string | null)[]
  examined: number[]
  answers: number
  where: Where[]
}

export function HeroStory() {
  const [corpus, setCorpus] = useState<Corpus | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
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

  // The hero is the page's first claim, so the index it claims to be reading is
  // fetched at once rather than when something scrolls into view. It is a third
  // of a megabyte compressed, and `loadCorpus` caches — so the search band and
  // the code browser both find it already here.
  useEffect(() => {
    let live = true
    loadCorpus().then(
      (loaded) => live && setCorpus(loaded),
      (error: unknown) => live && setFailure(error instanceof Error ? error.message : String(error)),
    )
    return () => {
      live = false
    }
  }, [])

  const story = useMemo((): Story | null => {
    if (!corpus) return null
    try {
      return build(corpus)
    } catch {
      return null
    }
  }, [corpus])

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

  const step = Math.min(frame, Math.max(count - 1, 0))

  return (
    <div className="story" data-testid="hero-story">
      <div className="story-body">
        <Pane story={story} onName={onName} cardOpen={cardOpen} answered={answered} />

        {/* Slides in over the file, and back out when it has had its say. */}
        <aside className={`story-panel${panelOpen ? ' is-open' : ''}`} aria-hidden={!panelOpen}>
          {!story ? null : here === 'scan' ? (
            <Seeking story={story} step={step} />
          ) : (
            <div className="story-query">
              <div className="story-scan-head">
                <span>the query</span>
              </div>
              <Code lang="sigla" source={QUERY} />
            </div>
          )}
        </aside>

        {failure ? <p className="story-note">The index did not load: {failure}</p> : null}
        {!story && !failure ? (
          <p className="story-note">
            <Spinner size="sm" /> loading the index…
          </p>
        ) : null}
      </div>

      <div className="story-foot">
        <Text as="p" size="sm" color="secondary" className="story-caption">
          {caption(here, story)}
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

/** What the foot says, which is the claim the panel above it is making. */
function caption(here: Beat, story: Story | null): string {
  if (here === 'approach' || here === 'hover')
    return 'You hover a name. Your editor opens a card and goes looking.'
  if (here === 'query')
    return 'Behind it, one query. There is no API to learn — this is the whole of it.'
  if (here === 'scan') {
    if (!story) return 'The key leads with the name, so the query seeks rather than scans.'
    return `The key leads with the name, so it seeks straight to the rows that match. ${
      story.examined.at(-1) ?? 0
    } read, out of ${story.facts.toLocaleString()}.`
  }
  if (!story) return 'Every reference, with the file and the line that holds it.'
  return `${story.answers} references, across ${story.where.length} files you never had open.`
}

// ======================================================================================
// Reading the story out of the index
// ======================================================================================

function build(corpus: Corpus): Story {
  const hit = corpus.search(SUBJECT).find((found) => found.name === SUBJECT)
  if (!hit) throw new Error(`the index has no ${SUBJECT}`)

  const declared = corpus.definitions(hit.symbol)[0]
  const blob = corpus.open(declared.path)
  const declaredAt = lineAt(blob, declared.start)

  // A window around the declaration rather than the whole file: the pane is the
  // reader's editor, and an editor is open somewhere rather than everywhere.
  const lines = source(blob, Math.max(1, declaredAt - LEAD), WINDOW, declaredAt)

  const trace = corpus.trace(QUERY)
  if (trace.diagnostics.length > 0) throw new Error(trace.diagnostics[0].message)

  const yields = trace.steps
    .filter((step) => step.event === 'yield')
    .map((step) => step.row as { file: string; at: { start: number; length: number } })

  const opened = new Map<string, Blob>()
  const readerOf = (path: string) => {
    let held = opened.get(path)
    if (!held) {
      held = corpus.open(path)
      opened.set(path, held)
    }
    return held
  }

  // Where each answer is, in the order the scan met them, grouped the way an
  // editor's references pane groups them. Two references on one line are one
  // entry: a reader counting them against the card would otherwise find the
  // same number written twice.
  const where: Where[] = []
  for (const row of yields) {
    const line = lineAt(readerOf(row.file), row.at.start)
    const seen = where.find((group) => group.file === row.file)
    if (!seen) where.push({ file: row.file, lines: [line] })
    else if (!seen.lines.includes(line)) seen.lines.push(line)
  }

  /**
   * **The rows the run actually opened**, in the order it bound them.
   *
   * A fetched row is left out: `src.File` is read through a reference the
   * previous level already held, so it is a point lookup and not a stretch of
   * the key space anybody seeks into. The band is about what a scan opened.
   */
  const rows: Row[] = []
  const seen = new Set<string>()
  let xref = 0
  for (const step of trace.steps) {
    for (const register of step.registers) {
      const fact = register.fact
      if (register.kind !== 'fact' || !fact || seen.has(fact)) continue
      const predicate = fact.slice(0, fact.indexOf('#'))
      if (predicate === 'src.File') continue
      seen.add(fact)
      rows.push({
        fact,
        predicate: predicate.replace('codemarkup.', ''),
        label:
          predicate === 'codemarkup.SymbolXRef'
            ? reference(yields[xref++], readerOf)
            : `${hit.name} · ${short(declared.path)}:${declaredAt}`,
      })
    }
  }

  // One frame per row the machine moves onto, not per transition: a reader
  // watching a band light up does not want the four steps it takes to leave a
  // row it has already read.
  const place = new Map(rows.map((row, at) => [row.fact, at]))
  const frames: number[] = []
  let last = -2
  for (let at = 0; at < trace.steps.length; at++) {
    const bound = trace.steps[at].registers.find(
      (register) => register.kind === 'fact' && register.fact && place.has(register.fact),
    )
    const standing = bound?.fact ? (place.get(bound.fact) ?? -1) : -1
    if (standing >= 0 && standing !== last) {
      frames.push(at)
      last = standing
    }
  }
  // A run that bound nothing recognisable still has to play something.
  if (frames.length === 0) frames.push(Math.max(trace.steps.length - 1, 0))
  // And one frame at the end: after the last row is bound the executor still
  // reads its way off the end of the range to find out it is done, and that
  // read is in the count. Stopping a frame early leaves the counter one short
  // of the number the profile reports.
  const finished = trace.steps.length - 1
  if (finished > (frames.at(-1) ?? -1)) frames.push(finished)

  const moments = frames.map((at) => fold(trace, at))

  const standing = moments.map((moment) => {
    const written = moment.registers.find((register) => register.written && register.fact)
    return written?.fact ? (place.get(written.fact) ?? null) : null
  })
  const ranges = moments.map((moment) => moment.scanning?.lo || null)

  /**
   * **A frame standing on no row holds the one before it.**
   *
   * The run's last act is to read its way off the end of the range, which binds
   * nothing and leaves the machine back at the outer level — so the closing
   * frame would drop the highlight and print the *first* level's key, which
   * reads as the band undoing itself at the exact moment it has finished making
   * its point. It rests on where it got to instead.
   */
  for (let at = 1; at < standing.length; at++) {
    if (standing[at] === null) {
      standing[at] = standing[at - 1]
      ranges[at] = ranges[at - 1]
    }
  }

  return {
    path: declared.path,
    name: hit.name,
    lines,
    facts: corpus.rows,
    rows,
    moments,
    at: standing,
    seeking: carry(ranges),
    examined: frames.map((at) => trace.steps[at].examined.reduce((total, rows) => total + rows, 0)),
    answers: yields.length,
    where,
  }
}

/**
 * A reference, as the row that names it.
 *
 * The column is there because two references share a line more often than not —
 * `ByteBuffer buffer = new ByteBuffer()` is two — and two rows reading
 * identically is a band that looks like it is repeating itself rather than one
 * showing eight distinct facts.
 */
function reference(
  row: { file: string; at: { start: number; length: number } } | undefined,
  readerOf: (path: string) => Blob,
): string {
  if (!row) return '…'
  const blob = readerOf(row.file)
  const line = lineAt(blob, row.at.start)
  return `${short(row.file)} · ${line}:${row.at.start - (blob.start(line) ?? 0) + 1}`
}

/** Each gap filled with the last thing that was there. */
function carry(values: (string | null)[]): (string | null)[] {
  let held: string | null = null
  return values.map((value) => {
    if (value) held = value
    return held
  })
}

/** The last segment of a path, which is what an editor's tab says. */
function short(path: string): string {
  return path.split('/').pop() ?? path
}

/**
 * Which line a byte offset falls on.
 *
 * A walk rather than a binary search: it runs a handful of times per build, over
 * files of tens to hundreds of lines, and the offsets it compares are the ones
 * the blob answers with.
 */
function lineAt(blob: Blob, byte: number): number {
  let line = 1
  for (let n = 1; n <= blob.lines; n++) {
    const start = blob.start(n)
    if (start === undefined || start > byte) break
    line = n
  }
  return line
}

/**
 * A window of the file, with the index's own colour runs lifted onto each line.
 *
 * `paint` joins the whole file and hands back offsets into that string, so this
 * splits it back apart rather than asking the blob line by line — the byte to
 * unit conversion a run needs is the fiddly part, and doing it twice is how two
 * copies of it come to disagree.
 *
 * **The subject is found on its declaration's line by name.** The index has a
 * span for it, counted in bytes, and converting one word's offset would mean a
 * second UTF-8 map for a line this already holds the text of. A declaration
 * names itself once.
 */
function source(blob: Blob, from: number, count: number, declared: number): Line[] {
  const { code, spans } = paint(blob)
  const texts = code.split('\n')

  const starts: number[] = []
  let offset = 0
  for (const text of texts) {
    starts.push(offset)
    offset += text.length + 1
  }

  const lines: Line[] = []
  for (let n = from; n < from + count && n <= texts.length; n++) {
    const start = starts[n - 1]
    const text = texts[n - 1] ?? ''
    lines.push({
      n,
      text,
      tokens: spans
        .filter((span) => span.start >= start && span.end <= start + text.length)
        .map((span) => ({ type: span.type, from: span.start - start, to: span.end - start })),
      subject: n === declared,
    })
  }
  return lines
}

// ======================================================================================
// The panels
// ======================================================================================

/** The file, as the index holds it. */
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
      <div className="story-file-name">{short(story.path)}</div>
      <ol className="story-code">
        {story.lines.map((line) => (
          <li key={line.n}>
            <span className="n">{line.n}</span>
            <span className="src">
              <Source line={line} />
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
 * One line, painted by the index rather than by a regular expression.
 *
 * These are `roslyn-lsp-1` semantic runs — the walker's own answer about what
 * each stretch of the line is — so the hero's code is coloured by the same facts
 * the rest of the query is reading. On the declaration's line the subject is
 * boxed, once.
 */
function Source({ line }: { line: Line }) {
  const out: React.ReactNode[] = []
  let cut = 0
  const boxed = line.subject ? line.text.indexOf(SUBJECT) : -1

  line.tokens.forEach((token, i) => {
    if (token.from > cut) out.push(line.text.slice(cut, token.from))
    const word = line.text.slice(token.from, token.to)
    out.push(
      <span
        key={i}
        className={`story-tok story-tok-${token.type}${token.from === boxed ? ' subject' : ''}`}
      >
        {word}
      </span>,
    )
    cut = token.to
  })
  if (cut < line.text.length) out.push(line.text.slice(cut))
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
            {story.answers} references to <code>{story.name}</code>
          </b>
          <ul>
            {story.where.map((group) => (
              <li key={group.file}>
                <code>{short(group.file)}</code>
                <span>
                  {group.lines.length === 1
                    ? `line ${group.lines[0]}`
                    : `lines ${group.lines.join(', ')}`}
                </span>
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

/**
 * **The seek, not the walk.**
 *
 * The first version of this listed the whole index and ran a highlight down it,
 * which is what a linear scan looks like and is the opposite of what happens.
 * What happens is that the executor opens one stretch of a sorted keyspace and
 * never reads the rest, so the bar is that proportion drawn honestly, a sliver
 * against the whole index, and the rows beneath it are the ones inside the
 * stretch. The key printed between them is the range's own first byte string,
 * and on the second level it begins with the row the first level bound, which
 * is what this engine does instead of having a join.
 */
function Seeking({ story, step }: { story: Story; step: number }) {
  const list = useRef<HTMLOListElement>(null)
  const here = useRef<HTMLLIElement>(null)

  const standing = story.at[step] ?? null
  const examined = story.examined[step] ?? 0
  const seeking = story.seeking[step]

  /**
   * **Keep the row in view, and touch nothing else.**
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
  }, [standing])

  // Floored so that a sliver stays a sliver a reader can see rather than a
  // rounding error they cannot. The number beside it is the real one.
  const share = Math.max((examined / Math.max(story.facts, 1)) * 100, 0.5)

  return (
    <div className="story-scan">
      <div className="story-scan-head">
        <span>the index</span>
        <span className="examined">
          {examined} of {story.facts.toLocaleString()} rows read
        </span>
      </div>

      <div className="story-extent" aria-hidden="true">
        <span className="story-slice" style={{ inlineSize: `${share}%` }} />
      </div>

      <p className="story-seek">
        {seeking ? (
          <>
            <span className="k">seek</span>
            <code>0x{seeking}</code>
          </>
        ) : (
          <span className="k">opening the range…</span>
        )}
      </p>

      <ol className="story-rows" ref={list}>
        {story.rows.map((row, at) => (
          <li
            key={row.fact}
            ref={at === standing ? here : undefined}
            className={at === standing ? 'is-here' : at < (standing ?? -1) ? 'is-held' : undefined}
          >
            <span className="p">{row.predicate}</span>
            <span className="k">{row.label}</span>
          </li>
        ))}
      </ol>
    </div>
  )
}
