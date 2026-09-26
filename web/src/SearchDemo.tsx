/**
 * **A search box over a real index**, on the landing page.
 *
 * The band under the hero used to be three paragraphs about predicates and keys,
 * which is the book's job and not a reason to keep reading. This is the same
 * claim made by letting somebody use it: type a name, get the classes and methods
 * that match with what the index knows about each, click one and land in the code
 * browser with it selected.
 *
 * **It opens empty.** A box with a word already in it and six rows under it is a
 * screenshot, and a reader scrolls past a screenshot. An empty box with a cursor
 * in it is an invitation, and the words offered under it are there so nobody has
 * to guess what this index contains.
 *
 * **Nothing here is a search index.** The box runs one sigla query per keystroke
 * against `codemarkup.SearchEntry`, whose key leads with the lowercased name for
 * exactly this — so a fuzzy pattern sits on the leading field and the executor
 * turns live automaton states into ranges and seeks past the dead bands. Each
 * card then asks two more: what the symbol says about itself, and how many places
 * refer to it. The query is printed under the results because that is the whole
 * pitch: there is no search API, there is a predicate with the right key.
 *
 * **The field is not the design system's.** `TextInput` tops out at a 36px box,
 * which is right for a form and half the size this wants to be — the bar is the
 * one thing in the band a reader has to notice. So the input is a plain one in
 * design tokens, with the label kept and hidden.
 *
 * **The corpus loads when the section comes near.** It is a few megabytes of a
 * real C# checkout walked by Roslyn, which is not something to spend on a reader
 * who bounces at the hero — and `loadCorpus` caches, so the code browser this
 * links into is already loaded by the time anybody clicks through.
 */
import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { Icon } from '@astryxdesign/core/Icon'
import { Spinner } from '@astryxdesign/core/Spinner'
import { Text } from '@astryxdesign/core/Text'
import { Code } from './book/Code'
import { route } from './book/links'
import { navigate } from './book/router'
import { loadCorpus, type Blob, type Corpus } from './corpus'
import { snippet, type Line } from './highlight'
import { Listing } from './Snippet'

/** Names that are actually in this index, so every word offered is a hit and not
 *  a guess — and between them they turn up a class, a struct, an interface, a
 *  method, a property, a field and a constant. */
const TRY = ['block', 'query', 'fact', 'frame', 'crc32']

/** Enough results to show that the kinds differ, few enough to read at a glance.
 *  Each one now carries five lines of source, so fewer of them go further. */
const SHOWN = 5

/** Lines of the file above and below the one a hit is declared on. */
const ABOVE = 2
const BELOW = 2

/** A hit and what the index knows about it, read out of WebAssembly once. */
type Card = {
  name: string
  symbol: string
  kind: string
  path: string
  line: number
  signature: string
  qualified: string
  doc: string
  uses: number
  /** The declaration and the lines either side of it, as the index paints them. */
  lines: Line[]
}

export function SearchDemo() {
  const [term, setTerm] = useState('')
  const [corpus, setCorpus] = useState<Corpus | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  // A browser with no observer has no way to say "nearly", so it loads on mount.
  // Initialised rather than set from the effect: the effect's job is to watch,
  // and a `setState` in its body is a second render before the first has painted.
  const [near, setNear] = useState(() => !('IntersectionObserver' in window))
  const [active, setActive] = useState(0)
  const section = useRef<HTMLElement>(null)
  const field = useRef<HTMLInputElement>(null)
  const labelled = useId()

  // Near, not visible: a reader scrolling at speed should find it loaded, and the
  // margin is about one screen of runway for the fetch and the parse.
  useEffect(() => {
    const element = section.current
    if (!element || near) return
    const watcher = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          setNear(true)
          watcher.disconnect()
        }
      },
      { rootMargin: '600px' },
    )
    watcher.observe(element)
    return () => watcher.disconnect()
  }, [near])

  useEffect(() => {
    if (!near) return
    let live = true
    loadCorpus().then(
      (loaded) => live && setCorpus(loaded),
      (error: unknown) => live && setFailure(error instanceof Error ? error.message : String(error)),
    )
    return () => {
      live = false
    }
  }, [near])

  const asked = term.trim()

  /**
   * **Every hit, as plain objects, ranked, with its detail.**
   *
   * Read out at once because a `Hit` is a handle with a getter per field, and a
   * list that re-renders on an arrow key would cross into WebAssembly five times
   * a row for values that cannot have changed. The rank is presentation and
   * nothing else: the engine answers in key order, which is alphabetical, and a
   * reader typing `block` expects `Block` above `BlockWritten`.
   *
   * The signature and the use count are two more seeks per card, which is why
   * they are asked for the six that are shown rather than for everything matched.
   */
  const cards = useMemo<Card[]>(() => {
    if (!corpus || !asked) return []
    const lowered = asked.toLowerCase()
    const starts = (name: string) => (name.toLowerCase().startsWith(lowered) ? 0 : 1)

    // One handle per file, not one per result: two hits in the same file are the
    // common case, and opening it twice is reading it twice.
    const held = new Map<string, Blob>()
    const open = (path: string) => {
      let blob = held.get(path)
      if (!blob) {
        blob = corpus.open(path)
        held.set(path, blob)
      }
      return blob
    }

    return corpus
      .search(asked)
      .map((hit) => ({
        name: hit.name,
        symbol: hit.symbol,
        kind: hit.kind,
        path: hit.path,
        line: hit.line,
      }))
      .sort(
        (a, b) =>
          starts(a.name) - starts(b.name) ||
          a.name.length - b.name.length ||
          a.name.localeCompare(b.name),
      )
      .slice(0, SHOWN)
      .map((hit) => {
        const about = corpus.info(hit.symbol)
        const signature = about?.signature ?? ''
        return {
          ...hit,
          lines: snippet(open(hit.path), hit.line, ABOVE, BELOW),
          // For a type the index's signature is the bare name, which the card
          // has already said in larger letters. Only keep it where it carries
          // the return type and the declaring type, as it does for a member.
          signature: signature === hit.name ? '' : signature,
          qualified: about?.qualified ?? '',
          doc: about?.doc ?? '',
          uses: corpus.references(hit.symbol).length,
        }
      })
  }, [corpus, asked])

  const chosen = Math.min(active, Math.max(cards.length - 1, 0))

  /** Open a hit where the reader can read it: the file, with the symbol selected. */
  const open = (hit: Card) => {
    const params = new URLSearchParams()
    params.set('file', hit.path)
    params.set('symbol', hit.symbol)
    navigate(`${route('browse')}?${params}`)
  }

  const onKey = (event: React.KeyboardEvent) => {
    if (event.key === 'Escape') {
      setTerm('')
      return
    }
    if (cards.length === 0) return
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setActive((at) => (at + 1) % cards.length)
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActive((at) => (at - 1 + cards.length) % cards.length)
    } else if (event.key === 'Enter') {
      event.preventDefault()
      open(cards[chosen])
    }
  }

  const ask = (word: string) => {
    setTerm(word)
    setActive(0)
    field.current?.focus()
  }

  return (
    <div className="search-demo" ref={section} data-testid="search-demo">
      <div className="finder">
        <label className="finder-bar" htmlFor={labelled}>
          <Icon icon="search" color="inherit" />
          <input
            id={labelled}
            ref={field}
            type="text"
            autoComplete="off"
            spellCheck={false}
            aria-label="Search the index"
            placeholder="Search the index…"
            value={term}
            onChange={(event) => {
              setTerm(event.target.value)
              setActive(0)
            }}
            onKeyDown={onKey}
          />
          {term ? (
            <button
              type="button"
              className="finder-clear"
              aria-label="Clear the search"
              onClick={() => ask('')}
            >
              <Icon icon="close" size="sm" color="inherit" />
            </button>
          ) : null}
        </label>

        <div className="finder-try">
          <span>Try</span>
          {TRY.map((word) => (
            <button
              key={word}
              type="button"
              className={word === asked.toLowerCase() ? 'is-on' : undefined}
              onClick={() => ask(word)}
            >
              {word}
            </button>
          ))}
        </div>

        {failure ? (
          <p className="finder-note">The index did not load: {failure}</p>
        ) : asked === '' ? null : !corpus ? (
          <p className="finder-note">
            <Spinner size="sm" /> loading the index…
          </p>
        ) : cards.length === 0 ? (
          <p className="finder-note">Nothing in this index is named anything like that.</p>
        ) : (
          <ul className="finder-cards">
            {cards.map((hit, at) => (
              <li key={`${hit.symbol}:${hit.path}:${hit.line}`}>
                <button
                  type="button"
                  className={at === chosen ? 'is-active' : undefined}
                  onClick={() => open(hit)}
                  onMouseEnter={() => setActive(at)}
                >
                  <span className="card-top">
                    <span className="hit-kind" data-family={familyOf(hit.kind)}>
                      {labelOf(hit.kind)}
                    </span>
                    {hit.uses > 0 ? (
                      <span className="hit-uses">
                        {hit.uses} {hit.uses === 1 ? 'use' : 'uses'}
                      </span>
                    ) : null}
                  </span>

                  <span className="hit-name">{marked(hit.name, asked)}</span>

                  {/* The summary the source wrote, which the walker put in the
                      index — so a result says what the thing is for and not only
                      what it is called. */}
                  {hit.doc ? <span className="hit-doc">{hit.doc}</span> : null}

                  {/* **The lines around it, as the index paints them.** A name
                      and a path is a row in a table; the declaration itself is
                      what a reader was looking for, and the walker already holds
                      the colour runs for it. */}
                  <Listing
                    className="hit-lines"
                    lines={hit.lines}
                    at={hit.line}
                    mark={hit.lines.find((line) => line.n === hit.line)?.text.indexOf(hit.name)}
                  />

                  <span className="card-foot">
                    <span className="hit-where">
                      <b>{hit.qualified || hit.name}</b>
                      <em>
                        {hit.path.split('/').pop()} · line {hit.line}
                      </em>
                    </span>
                    <Icon icon="chevronRight" size="sm" color="inherit" />
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* **Folded away until a reader wants it.** The box has to work before it
          has to be explained, and the explanation is a query in a language
          nobody has read yet — which is the thing the page has to earn, not the
          thing it opens with. */}
      <details className="finder-said">
        <summary>
          {corpus ? `${corpus.rows.toLocaleString()} facts. ` : ''}
          There is no search API behind that box — it is one query. Show me.
        </summary>
        <Text as="p" size="sm" color="secondary">
          Re-run as you type, and the fuzz widens as the word gets longer. The pattern sits on{' '}
          <code>nameLowercase</code>, which leads the key, so this is a seek and not a scan over
          every name. Each result then asks two more: what the symbol says about itself, and every
          place that refers to it.
        </Text>
        <Code lang="sigla" source={queryFor(asked || TRY[0])} />
      </details>
    </div>
  )
}

/**
 * The query the box runs, with this term's pattern in it.
 *
 * **A restatement, and deliberately a close one.** The search itself is
 * `fjord_inspect::codeview::search`, compiled into the module — this prints what
 * that function builds so a reader can see it, which means the fuzz rule lives in
 * two places. It is three lines of Rust that have not changed since they were
 * written, and the failure is cosmetic: a printed pattern that does not match the
 * hits underneath it.
 */
function queryFor(term: string): string {
  const lowered = term.toLowerCase()
  const literal = JSON.stringify(lowered)
  const size = [...lowered].length
  const pattern = size <= 2 ? `${literal}..` : size <= 5 ? `${literal}~<1` : `${literal}~<2`
  return `{name = N, symbol = Sym, kind = K, path = P, line = L} where
  codemarkup.SearchEntry {nameLowercase = ${pattern}, name = N, kind = K,
                          symbol = S, file = F, line = L};
  S = src.Symbol Sym; F = src.File P`
}

/** A union alternative as a reader would say it. `class_` is a class, because the
 *  schema had to dodge a keyword and a reader should never see that it did. */
function labelOf(kind: string): string {
  if (kind === 'enumMember') return 'enum member'
  if (kind === 'typeParameter') return 'type parameter'
  return kind.replace(/_$/, '')
}

/** What sort of thing it is, for the one bit of colour the chips carry. A kind is
 *  the engine's own answer, so tinting by it says nothing the index did not. */
function familyOf(kind: string): string {
  if (['class_', 'struct_', 'interface_', 'enum_'].includes(kind)) return 'type'
  if (['constant', 'enumMember', 'variable'].includes(kind)) return 'value'
  if (['method_', 'constructor_', 'property_', 'field', 'event', 'operator'].includes(kind))
    return 'member'
  return 'other'
}

/**
 * The name with the part that was typed picked out.
 *
 * The match is fuzzy, so the typed word is not always a prefix of the name it
 * found — and underlining a stretch that is not there would be a lie about why
 * the card is in the list. Only a real occurrence is marked; anything else is
 * left plain and earns its place by being in the results at all.
 */
function marked(name: string, term: string) {
  const at = name.toLowerCase().indexOf(term.toLowerCase())
  if (at < 0 || term === '') return name
  return (
    <>
      {name.slice(0, at)}
      <mark>{name.slice(at, at + term.length)}</mark>
      {name.slice(at + term.length)}
    </>
  )
}
