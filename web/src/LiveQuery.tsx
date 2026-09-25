/**
 * **The engine, answering a question, on the landing page.**
 *
 * The book's `Demo` shows the pipeline — tokens, a parse tree, a plan, the
 * machine stepping. That is the right thing in a page whose paragraph above it
 * is about one of those stages, and the wrong thing as a first impression: a
 * reader who has not yet been told what a predicate is does not want a register
 * file. So this is the same engine with everything between taken out. A query
 * goes in, rows come out, and the only other thing it can say is that the query
 * did not compile.
 *
 * It is still the real compiler and the real executor, over the real demo
 * database. Nothing here is a recording.
 */
import { useMemo, useState } from 'react'
import { Spinner } from '@astryxdesign/core/Spinner'
import { Text } from '@astryxdesign/core/Text'
import { useEngine } from './engine'
import { Editor } from './Editor'
import type { DiagnosticView, RowView } from './wasm'

/** A row, written the way the language writes it back. */
function show(value: unknown): string {
  return typeof value === 'string' ? `"${value}"` : (JSON.stringify(value) ?? '—')
}

export function LiveQuery({ initial }: { initial: string }) {
  // The landing page is the one page that *should* fetch the engine: the demo
  // is the argument, not an illustration beside it.
  const { engine, failure } = useEngine(true)
  const [query, setQuery] = useState(initial)

  const blank = query.trim() === ''

  const result = useMemo(() => {
    if (!engine || blank) return null
    try {
      const schema = engine.sampleSchema
      const tokens = engine.lex(query)
      // Compile first: a query that does not compile has no rows to show, and
      // its diagnostic is the more useful answer.
      const compiled = engine.compile(schema, query)
      const faults: DiagnosticView[] = [...tokens.diagnostics, ...compiled.diagnostics]
      return {
        tokens: tokens.tokens,
        faults,
        rows: faults.length === 0 ? engine.run(schema, query).rows : ([] as RowView[]),
        broke: null as string | null,
      }
    } catch (error: unknown) {
      return { tokens: [], faults: [], rows: [] as RowView[], broke: String(error) }
    }
  }, [engine, blank, query])

  const faults = result?.faults ?? []
  const flaws = faults.flatMap((fault) => fault.labels.map((label) => label.span))

  return (
    <div className="live">
      <div className="live-head">
        <Text size="sm" weight="semibold">
          Ask the database something
        </Text>
        <Text size="sm" color="secondary">
          four files, seven declarations, and the references between them
        </Text>
      </div>

      <Editor
        source={query}
        tokens={result?.tokens ?? []}
        highlight={null}
        onChange={setQuery}
        rows="query"
        flaws={flaws}
      />

      <div className="live-rows">
        {failure ? (
          <p className="live-note">The engine did not load: {failure}</p>
        ) : !engine ? (
          <p className="live-note">
            <Spinner size="sm" /> loading the engine…
          </p>
        ) : blank ? (
          <p className="live-note">Type a query.</p>
        ) : result?.broke ? (
          <p className="live-note">{result.broke}</p>
        ) : faults.length ? (
          <ul className="live-faults">
            {faults.map((fault, i) => (
              <li key={i}>
                {fault.code ? <code>{fault.code}</code> : null} {fault.message}
              </li>
            ))}
          </ul>
        ) : result && result.rows.length === 0 ? (
          <p className="live-note">No rows.</p>
        ) : (
          <ol className="live-answer">
            {result?.rows.map((row, i) => (
              <li key={i}>
                <code>{show(row.value)}</code>
              </li>
            ))}
          </ol>
        )}
      </div>
    </div>
  )
}
