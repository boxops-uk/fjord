/**
 * **A few lines of a file, coloured by the index that holds it.**
 *
 * The search results and the walkthrough's file pane want the same thing: a
 * window of source with the walker's own semantic runs on it, rather than a
 * regular expression's guess about what a language looks like. So both render
 * through here and differ only in how many lines they ask for and whether they
 * mark a word in them.
 *
 * The window itself is `highlight.snippet`, because it is a painting concern and
 * belongs beside the painter that answers a line at a time — which is what keeps
 * a result list from painting nine hundred lines of a file to show five.
 */
import type { Line } from './highlight'

/**
 * One line, with an optional word boxed.
 *
 * `mark` is a position in the line's text, which is how a caller says *this
 * occurrence* rather than *this word* — a line naming something twice should not
 * box both when the index has a span for one.
 */
export function Painted({ line, mark }: { line: Line; mark?: number }) {
  const out: React.ReactNode[] = []
  let cut = 0

  line.runs.forEach((run, at) => {
    if (run.from > cut) out.push(line.text.slice(cut, run.from))
    out.push(
      <span
        key={at}
        className={`code-tok code-tok-${run.type}${run.from === mark ? ' is-subject' : ''}`}
      >
        {line.text.slice(run.from, run.to)}
      </span>,
    )
    cut = run.to
  })

  if (cut < line.text.length) out.push(line.text.slice(cut))
  return <>{out}</>
}

/** A window of a file, as a numbered listing. */
export function Listing({
  lines,
  at,
  mark,
  className,
}: {
  lines: Line[]
  /** The line the snippet is about, marked as the one being pointed at. */
  at?: number
  /** Where in that line the word is. */
  mark?: number
  className?: string
}) {
  return (
    <ol className={['listing', className].filter(Boolean).join(' ')}>
      {lines.map((line) => (
        <li key={line.n} className={line.n === at ? 'is-at' : undefined}>
          <span className="n">{line.n}</span>
          <span className="src">
            <Painted line={line} mark={line.n === at ? mark : undefined} />
          </span>
        </li>
      ))}
    </ol>
  )
}
