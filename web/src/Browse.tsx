import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
  type SVGProps,
} from 'react'
import { Banner } from '@astryxdesign/core/Banner'
import { BottomSheet } from '@astryxdesign/core/BottomSheet'
import { Button } from '@astryxdesign/core/Button'
import { Card } from '@astryxdesign/core/Card'
import { Code, CodeBlock } from '@astryxdesign/core/CodeBlock'
import { EmptyState } from '@astryxdesign/core/EmptyState'
import { Heading } from '@astryxdesign/core/Heading'
import { HoverCard } from '@astryxdesign/core/HoverCard'
import { Icon } from '@astryxdesign/core/Icon'
import { useMediaQuery } from '@astryxdesign/core/hooks'
import { Layout as Panes, LayoutContent, LayoutHeader, LayoutPanel } from '@astryxdesign/core/Layout'
import { List, ListItem } from '@astryxdesign/core/List'
import { ResizeHandle, useResizable } from '@astryxdesign/core/Resizable'
import { Spinner } from '@astryxdesign/core/Spinner'
import { HStack, VStack } from '@astryxdesign/core/Stack'
import { Text } from '@astryxdesign/core/Text'
import { TextInput } from '@astryxdesign/core/TextInput'
import { Token } from '@astryxdesign/core/Token'
import { Toolbar } from '@astryxdesign/core/Toolbar'
import { TreeList } from '@astryxdesign/core/TreeList'
import type { TreeListItemData } from '@astryxdesign/core/TreeList'
import {
  type Blob,
  type Corpus,
  type Definition,
  type Entry,
  type Info,
  type PackageRef,
  type Project,
  loadCorpus,
} from './corpus'
import { paint } from './highlight'
import { markupSyntax } from './theme'
import { byteOffsetOf, links, positionAt, rectOf, spanAt } from './xref'

/**
 * A count, above the list it counts and on that list's own left edge.
 *
 * `List` gives its `header` slot a bottom margin and nothing else, so a header
 * put there starts at the panel's edge while every row under it starts 8px in.
 * The eye reads that misalignment before it reads either number. Step 2 is not
 * a guess: it is where `ListItem` puts its own content, so the count and the
 * names below it share one edge by construction rather than by coincidence.
 */
function SectionLabel({ children }: { children: ReactNode }) {
  return (
    <VStack paddingInline={2}>
      <Text size="sm" color="secondary">
        {children}
      </Text>
    </VStack>
  )
}

/**
 * **The centre pane's header, whatever the pane is showing** — the name, the path
 * under it, and the one control this place has.
 *
 * One component rather than one per view: a file, a directory and a project are three
 * answers to *where am I*, and a header stated three times drifts. Where that shows is
 * the `Raw`/`Project` toggle — a control a reader clicks twice in a row. Give each
 * reading its own header and the button moves out from under the pointer the moment
 * the two disagree about a padding or a type size.
 *
 * The name breaks anywhere, so a long one wraps rather than shrinking the action out
 * of the row it shares.
 */
function PaneHeader({
  name,
  path,
  action,
}: {
  name: string
  path: string
  action?: ReactNode
}) {
  return (
    <HStack justify="between" align="center" gap={4} paddingInline={4} paddingBlock={3}>
      <VStack gap={1}>
        <Heading level={3} wordBreak="break-all">
          {name}
        </Heading>
        <Text size="sm" color="secondary" wordBreak="break-all">
          {path}
        </Text>
      </VStack>
      {action}
    </HStack>
  )
}

/**
 * **A code browser over a real index**, with nothing behind it but a static file.
 *
 * The corpus is `Boxops.Fjord.Client` walked by Roslyn, exported as a store image
 * and fetched like any other asset. Every question on this page — the tree, the
 * outline, the references, the search — is a sigla seek against that image, run by
 * the same executor the server runs, in the tab.
 *
 * **The colours are the compiler's, not a guess.** `src.FileLineStyles` holds
 * Roslyn's own classifier output as LSP semantic tokens; the module decodes it and
 * `paint` lifts it onto offsets, so `CodeBlock`'s `tokenizer` is handed real
 * semantic highlighting rather than a regular expression's idea of C#.
 */
export function Browse() {
  const [corpus, setCorpus] = useState<Corpus | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  /**
   * **What the middle pane is showing** — a file's code, or a directory's contents.
   *
   * A browser with no file open used to pick one, which meant every visit began in
   * `BlockTarget.cs` for no reason a reader could name. A repository opens on its
   * root, and a directory opens on its listing; the code is what a *file* is for.
   */
  const [place, setPlace] = useState<{ kind: 'file' | 'dir'; path: string }>({
    kind: 'dir',
    path: '',
  })

  /** The open file, or `null` where a directory is showing. */
  const chosen = place.kind === 'file' ? place.path : null

  const setPath = useCallback((path: string) => setPlace({ kind: 'file', path }), [])
  const [symbol, setSymbol] = useState<string | null>(null)
  const [term, setTerm] = useState('')
  /**
   * Where the last jump landed. A reference is a *span*, not a file, so
   * navigating to one and highlighting the file's declaration instead would put
   * the reader in the right file at the wrong line — which is worse than not
   * moving, because it looks like it worked.
   */
  const [at, setAt] = useState<{ path: string; start: number } | null>(null)
  /** Which panel is open as a sheet, on a screen too narrow to give it a column. */
  const [sheet, setSheet] = useState<'files' | 'outline' | null>(null)

  /**
   * Responsive contract, at the frame root — and the shell's own nav is 260 of
   * the width above 768px, which is what these numbers are budgeted against:
   *   > 1200px  files 260 | code | outline 380
   *   <= 1200px the outline leaves the frame; a button in the toolbar raises it
   *             as a sheet
   *   <= 900px  the file tree goes the same way, and the code has the width
   *
   * The code is the thing this page is for, so it is the region that never
   * yields: two panels either side of it left it 124px wide on a phone, which
   * is a code browser with no code in it.
   */
  const roomForTheTree = useMediaQuery('(min-width: 900px)')
  const roomForTheOutline = useMediaQuery('(min-width: 1200px)')

  useEffect(() => {
    loadCorpus().then(setCorpus, (error: unknown) => setFailure(String(error)))
  }, [])

  const tree = useResizable({
    // Wider than it was, because the tree has depth now: paths are relative to the
    // repository rather than to the one project, so every file sits four levels in
    // and the indent is width the name used to have.
    defaultSize: 300,
    minSizePx: 200,
    maxSizePx: 480,
    autoSaveId: 'fjord-browse-tree',
  })
  const panel = useResizable({
    defaultSize: 380,
    minSizePx: 300,
    maxSizePx: 520,
    autoSaveId: 'fjord-browse-panel',
  })

  /**
   * **Which directories a reader has opened.** It only ever grows: a collapse is
   * `TreeList`'s own business and its override wins over the seed below, so closing
   * something stays closed without this having to forget it.
   */
  const [pulled, setPulled] = useState<Set<string>>(() => new Set())

  /**
   * **The tree, one level at a time.** `''` is the root and a directory key carries
   * its trailing `/`; `codeview::children` is a prefix seek over `src.File`, so each
   * of these reads the band of keys under one directory rather than the whole index.
   *
   * Derived rather than stored, which is what keeps a level's arrival from being a
   * state update inside an effect: opening a directory changes `pulled`, and the
   * listings for what is open are a function of that.
   */
  const listing = useMemo(() => {
    const held = new Map<string, Entry[]>()
    if (!corpus) return held

    const want = (prefix: string) => {
      if (!held.has(prefix)) held.set(prefix, corpus.children(prefix))
      return held.get(prefix) ?? []
    }

    want('')

    for (const opened of pulled) want(opened)
    // The way down to whatever is showing, and — where that is a directory — the
    // directory itself, which the middle pane lists.
    for (const ancestor of ancestorsOf(place.path)) want(ancestor)
    if (place.kind === 'dir') want(place.path === '' ? '' : `${place.path}/`)

    return held
  }, [corpus, pulled, place])

  /** Record a directory as opened. An event, not an effect: a reader did this. */
  const open = useCallback((prefix: string) => {
    setPulled((held) => (held.has(prefix) ? held : new Set(held).add(prefix)))
  }, [])

  const path = chosen

  /**
   * Which directories to draw open: what a reader opened, and the way down to the
   * file showing. A file reached by following a reference can be anywhere, including
   * inside directories nobody has touched, so the tree opens the way to it rather
   * than leaving the selection somewhere it cannot be seen.
   */
  const expanded = useMemo(() => {
    const open = new Set([...pulled, ...ancestorsOf(place.path)])
    // The directory being listed is open in the tree too — a reader standing in it
    // and seeing it shut in the sidebar is being told two things at once.
    if (place.kind === 'dir' && place.path !== '') open.add(`${place.path}/`)
    return open
  }, [pulled, place])

  /**
   * This file's references, flat — fetched once per file, hit-tested per move.
   *
   * **Each getter is read exactly once here**, because a `wasm_bindgen` getter
   * returning a `Vec` copies it out of linear memory on every access. Holding the
   * `FileRefs` handle and reading `.spans` inside the mouse handler would copy two
   * thousand integers per mouse move, which is the cost this flat shape exists to
   * avoid.
   */
  const fileRefs = useMemo(() => {
    if (!corpus || !path) return null
    const held = corpus.refs(path)
    return { spans: held.spans, symbols: held.symbols, locals: held.locals }
  }, [corpus, path])

  // Memoised together, and the tokenizer with them: `CodeBlock` holds it in a
  // dependency array, so a fresh closure per render would re-tokenise the file on
  // every keystroke somewhere else on the page. The references are woven in here
  // rather than drawn over the top: a reference is underlined by the same span
  // that colours it, which is the only place the block will take it.
  /** What the directory view lists — sorted the way the tree sorts. */
  const entries = useMemo(() => {
    if (place.kind !== 'dir') return []
    const held = listing.get(place.path === '' ? '' : `${place.path}/`) ?? []
    return [...held].sort((left, right) => {
      if (left.is_dir !== right.is_dir) return left.is_dir ? -1 : 1
      return left.name.localeCompare(right.name)
    })
  }, [listing, place])

  /**
   * **A project file has two readings**, and this is which one is showing.
   *
   * The text says what somebody wrote; the build layer says what MSBuild made of it —
   * the framework a `<TargetFramework>` resolved to, the version a floating reference
   * landed on, the edges a path string only implies. Neither is the other's summary, so
   * this is a toggle rather than a fold.
   */
  const [reading, setReading] = useState<'raw' | 'project'>('project')

  /** What the build layer holds about this file, where it is a project at all. */
  const built = useMemo(
    () => (corpus && path ? corpus.project(path) : undefined),
    [corpus, path],
  )
  const asks = useMemo(
    () => (corpus && built && path ? corpus.packages(path) : []),
    [corpus, built, path],
  )

  const blob = useMemo(
    () => (corpus && path ? corpus.open(path) : null),
    [corpus, path],
  )

  const painted = useMemo(
    () => (blob ? paint(blob, fileRefs ? links(fileRefs) : []) : null),
    [blob, fileRefs],
  )
  const tokenizer = useMemo(() => () => painted?.spans ?? [], [painted])

  const outline = useMemo(
    () => (corpus && path ? corpus.outline(path) : []),
    [corpus, path],
  )
  const uses = useMemo(
    () => (corpus && symbol ? corpus.references(symbol) : []),
    [corpus, symbol],
  )
  /**
   * What the chosen symbol *is* — the name and kind the references pane titles
   * itself with. Asked of the whole index rather than of this file's outline: a
   * symbol can be reached by clicking a use of something declared elsewhere, and
   * that is exactly the case where a reader most needs to be told what they have.
   */
  /** The same, for the symbol the references pane is titled with. */
  /**
   * What the chosen symbol says about itself — now answerable for one this index only
   * *names*, because the indexer writes `codemarkup.SymbolInfo` for a reference target
   * with no source location and that predicate is keyed `{symbol}` alone.
   */
  const stated = useMemo(
    () => (corpus && symbol ? corpus.info(symbol) : undefined),
    [corpus, symbol],
  )

  const selected = useMemo(
    () => (corpus && symbol ? (corpus.definitions(symbol)[0] ?? null) : null),
    [corpus, symbol],
  )

  const hits = useMemo(
    () => (corpus && term.trim() ? corpus.search(term.trim()) : []),
    [corpus, term],
  )

  /**
   * This file's declarations as a flat `[start, length, row]` triple per entry,
   * ordered — the same shape `spanAt` binary-searches for a reference, because a
   * declaration is hit-tested on the same mouse move and by the same code.
   */
  const declarations = useMemo(() => {
    const flat = new Int32Array(outline.length * 3)
    outline.forEach((found, row) => {
      flat[row * 3] = found.start
      flat[row * 3 + 1] = found.length
      flat[row * 3 + 2] = row
    })
    return flat
  }, [outline])

  const highlighted = useMemo(() => {
    if (!blob) return []
    // A jump wins over a declaration: it is the more specific thing the reader
    // just asked for, and it is the only one that can name a line no outline row
    // does.
    if (at && at.path === path) return [lineAt(blob, at.start)]
    if (!symbol) return []
    return outline
      .filter((found) => found.symbol === symbol)
      .map((found) => lineAt(blob, found.start))
  }, [at, blob, outline, path, symbol])

  /**
   * What a point in the code names: a symbol, a declaration in this same file, or
   * nothing. **Global references are asked first**, because a local one covering
   * the same text would be the narrower, less useful answer — a parameter's own
   * declaration site is also a use of its type.
   */
  const resolve = (x: number, y: number): Jump | null => {
    if (!view.current || !blob || !fileRefs) return null

    const at = positionAt(view.current, x, y)
    if (!at) return null

    const offset = byteOffsetOf(blob, at.line, at.column)
    if (offset === null) return null

    const global = spanAt(fileRefs.spans, 2, offset)
    if (global !== -1)
      return {
        kind: 'symbol',
        symbol: fileRefs.symbols[global],
        // `spanAt` answers in *entries*, not in numbers: `symbols` is one per
        // entry and so is indexed directly, while `spans` is two numbers per
        // entry and has to be multiplied through. Reading it as a flat index
        // returns the span of some earlier reference, which still resolves and
        // still draws — just around the wrong name.
        at: { start: fileRefs.spans[global * 2], length: fileRefs.spans[global * 2 + 1] },
      }

    // **A declaration is followable too.** `FileXRef` holds the *uses* of a symbol,
    // so the name in `public void Dispose()` is in none of them — and a reader who
    // clicks the name of the thing they are looking at is asking the same question
    // as one who clicks a use of it. The spans are the outline's, already loaded.
    const declared = spanAt(declarations, 3, offset)
    if (declared !== -1)
      return {
        kind: 'symbol',
        symbol: outline[declarations[declared * 3 + 2]].symbol,
        at: { start: declarations[declared * 3], length: declarations[declared * 3 + 1] },
      }

    const local = spanAt(fileRefs.locals, 4, offset)
    if (local !== -1)
      return {
        kind: 'local',
        start: fileRefs.locals[local * 4 + 2],
        at: { start: fileRefs.locals[local * 4], length: fileRefs.locals[local * 4 + 1] },
      }

    return null
  }

  /**
   * **Settle on what the pointer is over**, and after a pause put a card on it.
   *
   * The pause is the whole design: a reader crossing a line passes over several
   * names to reach the one they want, and a card that appears under each of them
   * flickers. Landing on the same span again is not a new rest — the identity is
   * the span's start, so re-entering a name the card is already on leaves it alone.
   */
  const rest = (jump: Jump | null) => {
    setOverLink(jump !== null)

    if (!jump) {
      window.clearTimeout(waiting.current)
      resting.current = -1
      setHover(null)
      return
    }

    if (jump.at.start === resting.current) return
    resting.current = jump.at.start
    window.clearTimeout(waiting.current)
    setHover(null)

    waiting.current = window.setTimeout(() => {
      if (!view.current || !blob) return
      const rect = rectOf(
        view.current,
        blob,
        lineAt(blob, jump.at.start),
        jump.at.start,
        jump.at.length,
      )
      if (!rect) return

      // **A local answers too, with the little it has.** It carries no symbol on
      // purpose — SCIP would name it by an occurrence ordinal that moves when the file
      // is edited — so there is nothing to look up and nothing to describe. What there
      // is, is where it was declared, which is the question a reader hovering one has.
      setHover(
        jump.kind === 'symbol'
          ? { kind: 'symbol', symbol: jump.symbol, rect }
          : { kind: 'local', line: lineAt(blob, jump.start), rect },
      )
    }, 350)
  }

  const follow = (jump: Jump) => {
    if (jump.kind === 'local') {
      // Its declaration is a span in this same file, which is the whole reason
      // these carry no symbol.
      if (path) setAt({ path, start: jump.start })
      return
    }

    setSymbol(jump.symbol)

    // A symbol with no definition here is ordinary — `System.String` is named by
    // this index and declared outside it — so the uses panel still answers for it
    // and the view simply does not move.
    const [declared] = corpus?.definitions(jump.symbol) ?? []
    if (declared) {
      setPath(declared.path)
      setAt({ path: declared.path, start: declared.start })
    }
  }

  /**
   * **Bring the highlighted line into view.**
   *
   * Highlighting a line a thousand lines down and leaving the reader at the top
   * is not a jump — it is the same page with a mark they cannot see. `CodeBlock`
   * stamps `data-line` on each line, which is a data attribute and so part of
   * what it offers rather than something prised out of its internals.
   */
  const view = useRef<HTMLDivElement>(null)
  const target = highlighted[0]
  /** Whether something under the cursor can be followed — the only affordance a
   *  hit-tested link has, since nothing wraps it in an element to style. */
  const [overLink, setOverLink] = useState(false)
  /**
   * **What the pointer is resting on, and where that is on screen.**
   *
   * Held rather than derived, because the card is not the hover: a reader crossing
   * a line of code passes over four names on the way to the one they meant, and a
   * card per name is a strobe. The span's own start is the identity — the same name
   * twice on a line is two spans, and the card should move between them.
   */
  const [hover, setHover] = useState<
    | { kind: 'symbol'; symbol: string; rect: DOMRect }
    | { kind: 'local'; line: number; rect: DOMRect }
    | null
  >(null)
  const resting = useRef(-1)
  const waiting = useRef(0)

  /** What the hovered symbol says about itself. */
  const card = useMemo(
    () => (corpus && hover?.kind === 'symbol' ? corpus.info(hover.symbol) : undefined),
    [corpus, hover],
  )
  /**
   * And where it lives. `SymbolInfo` carries a signature, which qualifies a member by
   * its type (`IBlockTarget.Write`) but says nothing about the namespace above it —
   * `qualified` is on the definition, so the card asks for both.
   */
  /** What the id alone says, for a symbol this index names but does not declare. */
  const cardAt = useMemo(
    () =>
      corpus && hover?.kind === 'symbol'
        ? (corpus.definitions(hover.symbol)[0] ?? null)
        : null,
    [corpus, hover],
  )
  const pending = useRef(0)

  // A card is pinned to a rectangle, and a scroll moves the rectangle out from
  // under it. Capture, because the box that scrolls is the code pane rather than
  // the window, and a scroll event does not bubble.
  useEffect(() => {
    const drop = () => {
      window.clearTimeout(waiting.current)
      resting.current = -1
      setHover(null)
    }
    window.addEventListener('scroll', drop, true)
    return () => window.removeEventListener('scroll', drop, true)
  }, [])

  useEffect(() => {
    if (!target || !view.current) return

    const line = view.current.querySelector(`[data-line="${target}"]`)
    if (!line) return

    // **The code pane, not the document.** `scrollIntoView` scrolls every
    // scrollable ancestor, which here includes the page — so jumping to a line
    // also scrolls the site header away, and the reader loses the frame around
    // what they just asked for.
    const scroller = scrollableAround(line)
    if (!scroller) return

    const box = line.getBoundingClientRect()
    const frame = scroller.getBoundingClientRect()
    scroller.scrollTop += box.top - frame.top - (frame.height - box.height) / 2
  }, [target, path])

  if (failure) {
    return (
      <VStack padding={4} gap={3}>
        <Banner status="error" title="The index did not load">
          {failure}
        </Banner>
        <Text color="secondary">
          The corpus is built rather than checked in — run{' '}
          <Code>./scripts/build-corpus.sh</Code> and reload.
        </Text>
      </VStack>
    )
  }

  if (!corpus) {
    return (
      <VStack align="center" justify="center" padding={6} gap={3}>
        <Spinner label="Loading the index" />
        <Text color="secondary">Fetching the index and putting it back into a store…</Text>
      </VStack>
    )
  }

  /**
   * **The two panels, written once and placed twice.** Each is a column where
   * there is width for one and a sheet where there is not, and the same JSX
   * either way — a second copy is a second place for a row to forget to close
   * the sheet it was chosen in.
   */
  const files = (
    <>
      {/* The field's own edge, rather than a wrapper's: `padding={3}` put it 12
          in while the names below it start at 8, so the list looked indented
          from a box it is meant to hang under. Only the inline axis moves —
          the space above and below the field is not what was wrong. */}
      <VStack gap={2} paddingBlock={3} paddingInline={2}>
        {/* The label is hidden rather than dropped: `TextInput` requires one, and
            a placeholder is not a substitute — it names the field only until
            someone types in it, and a screen reader that meets the box after
            that finds an input with no name at all. */}
        <TextInput
          label="Search"
          isLabelHidden
          placeholder="Search"
          startIcon={<Icon icon="search" color="inherit" />}
          value={term}
          onChange={setTerm}
        />
      </VStack>

      {term.trim() ? (
        hits.length === 0 ? (
          <EmptyState title="No name starts with that" />
        ) : (
          <List density="compact" header={<SectionLabel>{hits.length} matching</SectionLabel>}>
            {hits.map((hit) => (
              <ListItem
                key={`${hit.symbol}:${hit.path}:${hit.line}`}
                label={hit.name}
                description={`${hit.path}:${hit.line}`}
                endContent={<Token size="sm" label={readable(hit.kind)} />}
                onClick={() => {
                  setPath(hit.path)
                  setSymbol(hit.symbol)
                  setAt(null)
                  setSheet(null)
                }}
                isSelected={hit.symbol === symbol}
              />
            ))}
          </List>
        )
      ) : (
        <TreeList
          density="compact"
          variant="noGuides"
          items={asTree(
            listing,
            expanded,
            place.path,
            open,
            (file) => {
              setPath(file)
              setSymbol(null)
              setAt(null)
              setSheet(null)
            },
            (dir) => {
              setPlace({ kind: 'dir', path: dir })
              setSymbol(null)
              setAt(null)
              setSheet(null)
            },
          )}
        />
      )}
    </>
  )

  const declared = (
    <>
      {/* **One pane, two questions**, rather than two lists sharing a column.
          Choosing a symbol is asking "where is this used", and the outline it
          was chosen from is no longer the answer to anything on screen — so the
          uses take the pane and the outline steps aside, the way GitHub's does.
          The `All symbols` link is the whole way back, and it is at the top
          because that is where a reader looks for it. */}
      {symbol ? (
        <VStack gap={4}>
          {/* The title's block padding, so choosing a symbol does not lift the panel's
              contents by the header's own inset. The inline step stays the uses list's:
              this block's first row is a control, and its label is already inset by the
              button's own padding. */}
          <VStack gap={2} paddingInline={2} paddingBlock={3}>
            {/* Back before title, as a bar of its own: it is a control, and a
                reader scanning for the way out should meet it before the name
                of the thing they are in. */}
            <HStack>
              <Button
                variant="ghost"
                size="sm"
                label="All symbols"
                icon={<Icon icon="chevronLeft" color="inherit" />}
                onClick={() => setSymbol(null)}
                data-testid="all-symbols"
              />
            </HStack>
            {/* A symbol with no definition here is ordinary — `System.String` is
                named by this index and declared outside it — so the pane titles
                itself with what it has and says which case this is, rather than
                falling back to the outline and looking like the click missed. */}
            <VStack gap={0}>
              <Heading level={3} wordBreak="break-all">
                {selected?.name ?? stated?.signature ?? symbol}
              </Heading>
              {selected || stated ? (
                <Text size="sm" color="secondary">
                  {[
                    (selected?.kind ?? stated?.kind) === 'other'
                      ? ''
                      : readable(selected?.kind ?? stated?.kind ?? ''),
                    selected ? '' : stated?.modifiers,
                  ]
                    .filter(Boolean)
                    .join(' · ')}
                </Text>
              ) : null}
              {selected?.qualified || stated?.qualified ? (
                <Text size="sm" color="secondary" wordBreak="break-all">
                  {selected?.qualified || stated?.qualified}
                </Text>
              ) : null}
              {!selected && stated?.package ? (
                <Text size="sm" color="secondary">
                  from {stated.package}
                </Text>
              ) : null}
              {!selected && stated?.doc ? <Text size="sm">{stated.doc}</Text> : null}
            </VStack>
          </VStack>

          <List
            density="compact"
            header={
              <SectionLabel>
                {uses.length} {uses.length === 1 ? 'use' : 'uses'} across the index
              </SectionLabel>
            }
          >
            {uses.map((use) => (
              <ListItem
                key={`${use.path}:${use.start}`}
                label={basename(use.path)}
                description={[folder(use.path), `byte ${use.start}`].filter(Boolean).join(' · ')}
                onClick={() => {
                  setPath(use.path)
                  setAt({ path: use.path, start: use.start })
                  setSheet(null)
                }}
                isSelected={at?.path === use.path && at.start === use.start}
              />
            ))}
          </List>
        </VStack>
      ) : (
        <VStack gap={4}>
          {/* **The centre pane's header padding**, so the panel titles itself on the
              same line the file names itself on. Step 4 rather than the 2 the tree
              below it uses: the count under this carries `SectionLabel`'s own step on
              top of its block's, so 4 is where the title and the count share an edge —
              and nothing in the tree moves for it. */}
          <VStack paddingInline={4} paddingBlock={3}>
            <Heading level={3}>Symbols</Heading>
          </VStack>

          {outline.length === 0 ? (
            // Two different emptinesses, and saying the wrong one is a small lie: a
            // directory declares nothing because it is not a file, which is not the
            // same as a file that happens to declare nothing.
            path ? (
              <EmptyState title="Nothing declared here" />
            ) : (
              <EmptyState
                title="No file open"
                description="Choose one to see what it declares."
              />
            )
          ) : (
            /* **Under what declares them, not in a heap.** A file's symbols arrive in
               position order, which already puts a member after its type — but a flat
               list says nothing about which type, and two methods called `Write` in one
               file are then two identical rows. Containment is a `Relation` the index
               holds, so the nesting is the producer's answer rather than this page
               splitting qualified names to guess at one. */
            <VStack gap={0} paddingInline={2}>
              <SectionLabel>{outline.length} declared</SectionLabel>
              <TreeList
                density="compact"
                variant="noGuides"
                items={asOutline(outline, blob, symbol, (found) => {
                  setSymbol(found.symbol)
                  setAt({ path: found.path, start: found.start })
                  setSheet(null)
                })}
              />
            </VStack>
          )}
        </VStack>
      )}
    </>
  )

  /**
   * **The header the centre pane always has.** The root is the one place without
   * one: it has no name to draw, and a heading over a repository's own files is a
   * sentence a reader has to get past to reach the list they came for.
   */
  const header =
    place.kind === 'file' ? (
      <PaneHeader
        name={basename(place.path)}
        path={place.path}
        action={
          // **The way back to what the build made of it.** Only over a project:
          // every other file has one reading, and a control offering a second would
          // be a question with one answer. It is one button holding both labels
          // rather than one per reading, so switching cannot move it.
          built ? (
            <Button
              variant="secondary"
              size="sm"
              label={reading === 'project' ? 'Raw' : 'Project'}
              onClick={() => setReading(reading === 'project' ? 'raw' : 'project')}
            />
          ) : undefined
        }
      />
    ) : place.path === '' ? null : (
      <PaneHeader name={basename(place.path)} path={place.path} />
    )

  return (
    <>
      <Panes
        /* In the frame rather than over the code, and only for a panel that has
           lost its column: the code region scrolls, so a bar inside it is a way
           back to the files that scrolls away the moment a reader reads. */
        header={
          roomForTheTree && roomForTheOutline ? undefined : (
            <LayoutHeader hasDivider>
              <Toolbar
                label="Panels"
                size="sm"
                startContent={
                  <>
                    {!roomForTheTree && (
                      <Button variant="secondary" label="Files" onClick={() => setSheet('files')} />
                    )}
                    {!roomForTheOutline && place.kind === 'file' && (
                      <Button
                        variant="secondary"
                        label="Outline"
                        onClick={() => setSheet('outline')}
                      />
                    )}
                  </>
                }
              />
            </LayoutHeader>
          )
        }
        start={
          roomForTheTree ? (
            <>
              <LayoutPanel label="Files" width={tree.size} isScrollable padding={0}>
                {files}
              </LayoutPanel>
              <ResizeHandle
                direction="horizontal"
                hasDivider
                resizable={tree.props}
                label="Resize the file tree"
              />
            </>
          ) : undefined
        }
        content={
          <LayoutContent isScrollable padding={0}>
            <VStack
              ref={view}
              gap={0}
              style={{
                cursor: overLink ? 'pointer' : undefined,
                // **A gutter four digits wide, however short the file.** `CodeBlock`
                // sizes it to the widest number it will draw, so every file gets a
                // different left margin and the code slides sideways as a reader moves
                // between them. Four is where a real file stops being unusual; past
                // 9,999 lines it grows, because clipping the number would be worse than
                // the shift. `app.css` is what applies it — the component writes the
                // property inline, and only `!important` outranks that.
                ['--fj-gutter' as string]: `${Math.max(4, String(blob?.lines ?? 0).length)}ch`,
              }}
              onClick={(event) => {
                const jump = resolve(event.clientX, event.clientY)
                if (jump) follow(jump)
              }}
              onMouseMove={(event) => {
                // Coalesced to a frame: a caret lookup per pixel is work nobody
                // sees, and the answer cannot change faster than a repaint.
                const { clientX, clientY } = event
                if (pending.current) return
                pending.current = requestAnimationFrame(() => {
                  pending.current = 0
                  rest(resolve(clientX, clientY))
                })
              }}
              onMouseLeave={() => {
                setOverLink(false)
                rest(null)
              }}
            >
            {header}
            {built && path && reading === 'project' ? (
              <ProjectView
                built={built}
                asks={asks}
                onOpen={(next) => {
                  setPlace({ kind: 'file', path: next })
                  setSymbol(null)
                  setAt(null)
                }}
              />
            ) : painted && path ? (
              /* `width="100%"` so a short file still fills the pane rather than
                 shrinking to its longest line. It costs nothing: the block's own body
                 is the horizontal scroller either way, so a line wider than the pane
                 scrolls rather than being clipped.

                 Untitled, because the header above it is already the file's name and
                 its path: a block titled with the path as well says it twice, at two
                 sizes, and the second one moves as the reading changes. */
              <CodeBlock
                code={painted.code}
                language={built ? 'xml' : 'csharp'}
                // A project file is tokenized by the design system rather than by the
                // index, and its markup rules put every element name in the slot this
                // theme paints a refused byte with — so it takes the palette that says
                // an element name is the language, not an error.
                syntaxTheme={built ? markupSyntax : undefined}
                tokenizer={built ? undefined : tokenizer}
                hasLineNumbers
                highlightLines={highlighted}
                container="section"
                width="100%"
                size="sm"
              />
            ) : (
              /* **A directory is a page, not an empty state.** What a repository shows
                 when no file is open is the folder you are standing in — the same
                 listing the tree draws, at the size a reader can read. */
              <VStack gap={0} width="100%" padding={4}>
                {/* Bordered, the way every repository draws a directory: the listing is
                    an object on the page rather than the page itself. */}
                <Card padding={0}>
                <List density="compact" hasDividers>
                  {place.path !== '' ? (
                    <ListItem
                      key=".."
                      label=".."
                      startContent={<FolderIcon width={16} height={16} />}
                      onClick={() => setPlace({ kind: 'dir', path: folder(place.path) })}
                    />
                  ) : null}
                  {entries.map((entry) => (
                    <ListItem
                      key={entry.path}
                      label={entry.name}
                      data-testid="browse-entry"
                      data-dir={String(entry.is_dir)}
                      startContent={
                        entry.is_dir ? (
                          <FolderIcon width={16} height={16} />
                        ) : (
                          <FileIcon width={16} height={16} />
                        )
                      }
                      onClick={() => {
                        setSymbol(null)
                        setAt(null)
                        setSheet(null)
                        setPlace({ kind: entry.is_dir ? 'dir' : 'file', path: entry.path })
                      }}
                    />
                  ))}
                </List>
                </Card>
              </VStack>
            )}
            </VStack>
          </LayoutContent>
        }
        end={
          // **No symbols beside a directory.** The pane answers "what does this file
          // declare", and a directory declares nothing — an empty column saying so is
          // width taken from the listing for the sake of a sentence.
          roomForTheOutline && place.kind === 'file' ? (
            <>
              <ResizeHandle
                direction="horizontal"
                isReversed
                hasDivider
                resizable={panel.props}
                label="Resize the panel"
              />
              <LayoutPanel label="Outline" width={panel.size} isScrollable padding={0}>
                {declared}
              </LayoutPanel>
            </>
          ) : undefined
        }
      />

      {/* **Raised over the code rather than squeezed beside it**, which is what
          the frame does with a panel it has no column for. `isOpen` is derived
          and not stored: a sheet is only ever open where its column is not, so
          a window that grows past the breakpoint puts the panel back in the
          frame and takes the sheet with it. */}
      <BottomSheet
        isOpen={sheet === 'files' && !roomForTheTree}
        onOpenChange={(open) => setSheet(open ? 'files' : null)}
        label="Files"
        // Tall because it carries a text input: it is the one height that moves
        // for the mobile keyboard rather than sitting under it.
        height="tall"
      >
        {files}
      </BottomSheet>

      <BottomSheet
        isOpen={sheet === 'outline' && !roomForTheOutline}
        onOpenChange={(open) => setSheet(open ? 'outline' : null)}
        label="Outline"
        height="capped"
      >
        {declared}
      </BottomSheet>

      {/* **The card, hung on the name rather than on the pointer.**
          `CodeBlock` paints with the CSS Custom Highlight API, so there is no
          element under the name to wrap — the trigger is a box laid over the
          rectangle the name occupies, and `HoverCard` positions against that.
          `pointer-events: none` so the box cannot take the click that follows
          the name, or the hover it is a consequence of. */}
      {hover && (
        <HoverCard
          isOpen
          placement="above"
          label={title(hover, card)}
          content={
            <VStack gap={2} maxWidth={460}>
              <Text size="sm" weight="bold" wordBreak="break-word">
                {title(hover, card)}
              </Text>
              {card?.qualified || cardAt?.qualified ? (
                <Text size="sm" color="secondary" wordBreak="break-all">
                  {card?.qualified || cardAt?.qualified}
                </Text>
              ) : null}
              {/* **What kind of thing it is, and what is in front of it.** The kind is
                  the first thing a reader wants and the last thing a signature says: an
                  argument reads `string socketPath` whether it is a parameter, a field
                  or a local, and only this line tells them apart. It comes from the
                  declaration, so a symbol declared elsewhere has none — the modifiers
                  are then what there is. */}
              {aboutIt(hover, cardAt, card) ? (
                <Text size="sm" color="secondary">
                  {aboutIt(hover, cardAt, card)}
                </Text>
              ) : null}
              {card?.doc ? <Text size="sm">{card.doc}</Text> : null}
              {/* **Where it ships from**, said by the index rather than read out of an
                  identifier. Shown only where nothing here declares it: a local symbol's
                  package is this one, and saying so on every card would be noise. */}
              {!cardAt && card?.package ? (
                <Text size="sm" color="secondary">
                  from {card.package}
                </Text>
              ) : null}
              {hover.kind === 'symbol' && !cardAt && !card ? (
                <Text size="sm" color="secondary">
                  declared outside this index
                </Text>
              ) : null}
            </VStack>
          }
        >
          <div
            aria-hidden
            data-testid="hover-anchor"
            style={{
              position: 'fixed',
              left: hover.rect.x,
              top: hover.rect.y,
              width: hover.rect.width,
              height: hover.rect.height,
              pointerEvents: 'none',
            }}
          />
        </HoverCard>
      )}
    </>
  )
}

/** The nearest ancestor that actually scrolls vertically, or `null`. */
function scrollableAround(node: Element): Element | null {
  for (let at = node.parentElement; at; at = at.parentElement) {
    const overflow = getComputedStyle(at).overflowY
    if ((overflow === 'auto' || overflow === 'scroll') && at.scrollHeight > at.clientHeight) {
      return at
    }
  }
  return null
}

/** What following a point in the code does. */
/** Where a followable thing sits in the file, in bytes. */
type At = { start: number; length: number }

type Jump =
  | { kind: 'symbol'; symbol: string; at: At }
  | { kind: 'local'; start: number; at: At }

/**
 * Which line a byte offset falls on, one-based.
 *
 * A linear walk rather than a binary search: this runs once per outline row on a
 * file already in memory, and the largest file in the corpus is eight hundred lines.
 */
function lineAt(blob: Blob, offset: number): number {
  for (let line = blob.lines; line >= 1; line--) {
    const start = blob.start(line)
    if (start !== undefined && start <= offset) return line
  }
  return 1
}

/**
 * Paths to a tree, split on `/`.
 *
 * The index stores a path relative to its root, so a directory is a prefix rather
 * than a fact — there is nothing to query for here, only a string to split.
 */
function asTree(
  listing: Map<string, Entry[]>,
  opened: Set<string>,
  selected: string,
  list: (prefix: string) => void,
  onChoose: (path: string) => void,
  onEnter: (path: string) => void,
): TreeListItemData[] {
  const rows = (prefix: string): TreeListItemData[] => {
    const entries = listing.get(prefix) ?? []

    return [...entries]
      .sort((left, right) => {
        // Directories first, then by name — the order every file tree uses.
        if (left.is_dir !== right.is_dir) return left.is_dir ? -1 : 1
        return left.name.localeCompare(right.name)
      })
      .map((entry) => {
        if (!entry.is_dir) {
          return {
            id: entry.path,
            label: entry.name,
            onClick: () => onChoose(entry.path),
            isSelected: entry.path === selected,
          }
        }

        const under = `${entry.path}/`
        const known = listing.has(under)

        return {
          id: entry.path,
          label: entry.name,
          isSelected: entry.path === selected,
          // The row goes to the listing, the chevron opens the level. `TreeListItem`
          // gives a row with an `onClick` no toggle of its own, which is what a
          // repository browser does anyway: a directory is a page.
          onClick: () => onEnter(entry.path),
          isExpanded: opened.has(under),
          // **A directory nobody has opened still has to look openable**, or there is
          // no control to open it with. One placeholder row is what puts the toggle
          // there, and rendering that row is what says the toggle was used: `TreeList`
          // draws children only while a parent is expanded, so the placeholder mounts
          // at exactly the moment the answer is wanted — which is the callback this
          // component does not otherwise offer.
          children: known ? rows(under) : [{ id: `${under}\u2026`, label: <Opening onShown={() => list(under)} /> }],
        }
      })
  }

  return rows('')
}

/**
 * A row that asks for its own contents the first time it is drawn.
 *
 * Once, and never again on a re-render: the fetch replaces this row with the entries
 * it found, so a second call could only be a loop through the state it just set.
 */
function Opening({ onShown }: { onShown: () => void }) {
  const asked = useRef(false)

  useEffect(() => {
    if (asked.current) return
    asked.current = true
    onShown()
  }, [onShown])

  return (
    <Text size="sm" color="secondary">
      …
    </Text>
  )
}

/**
 * A file's symbols as a tree, nested by what contains them.
 *
 * The edges come from `codemarkup.Relation {kind = contains}` — the schema is explicit
 * that neither `Definition` nor `FileDefinition` carries a container field, because
 * containment is a relation and joinable both ways. A row whose container is not itself
 * declared in this file sits at the top: a nested class in another file's partial half
 * is contained by something, just not by anything there is a row for here.
 *
 * Position order is kept throughout, so the tree reads down the file.
 */
function asOutline(
  found: Definition[],
  blob: Blob | null,
  selected: string | null,
  onChoose: (found: Definition) => void,
): TreeListItemData[] {
  const here = new Set(found.map((row) => row.symbol))
  const under = new Map<string, Definition[]>()
  const roots: Definition[] = []

  for (const row of found) {
    if (row.container && here.has(row.container) && row.container !== row.symbol) {
      const kin = under.get(row.container) ?? []
      kin.push(row)
      under.set(row.container, kin)
    } else {
      roots.push(row)
    }
  }

  const build = (row: Definition): TreeListItemData => {
    const kin = under.get(row.symbol) ?? []

    return {
      id: `${row.symbol}:${row.start}`,
      label: row.name,
      description: row.kind === 'other' ? undefined : readable(row.kind),
      endContent: <Token size="sm" label={String(blob ? lineAt(blob, row.start) : 0)} />,
      onClick: () => onChoose(row),
      isSelected: row.symbol === selected,
      // Open, because an outline that hides what it is an outline of is a list of
      // type names — and a reader opened this pane to see inside them.
      ...(kin.length > 0 ? { children: kin.map(build), isExpanded: true } : {}),
    }
  }

  return roots.map(build)
}

/**
 * **What the build made of a project file**, beside the file itself.
 *
 * Everything here is a fact the index already held and nothing showed: a `.csproj` was a
 * path in the tree with no text and no reading. The text answers "what was written"; this
 * answers "what was resolved", and the two differ exactly where it matters — a floating
 * `<PackageReference>` writes a range and resolves to a version, and only one of those is
 * what you are actually building against.
 *
 * The reverse edge is the one worth having. "What does this build against" is in the file;
 * "what breaks if I change it" is not, and `ProjectReferencedBy` is stored precisely so
 * that question is a seek rather than a scan of every other project.
 */
function ProjectView({
  built,
  asks,
  onOpen,
}: {
  built: Project
  asks: PackageRef[]
  onOpen: (path: string) => void
}) {
  const facts: [string, string][] = [
    ['Target framework', built.framework],
    ['SDK', built.sdk],
    ['Output', built.output],
    ['Assembly', built.assembly],
    ['Root namespace', built.namespace],
    ['Platform', built.platform],
  ]

  const linked = (title: string, paths: string[]) =>
    paths.length === 0 ? null : (
      <VStack gap={2}>
        <Heading level={4}>
          {title} ({paths.length})
        </Heading>
        <Card padding={0}>
          <List density="compact" hasDividers>
            {paths.map((each) => (
              <ListItem
                key={each}
                label={basename(each)}
                description={folder(each)}
                startContent={<FileIcon width={16} height={16} />}
                onClick={() => onOpen(each)}
              />
            ))}
          </List>
        </Card>
      </VStack>
    )

  return (
    <VStack gap={5} width="100%" padding={4}>
      {/* **What MSBuild resolved**, which is not what the file says: a project with no
          `<TargetFramework>` of its own still resolves one from what it imports. A field
          the build answered nothing for is left out rather than shown empty — the schema
          keeps "nothing" and "the empty string" apart, and a blank row loses that. */}
      <Card padding={3}>
        <VStack gap={2}>
          {facts
            .filter(([, value]) => value)
            .map(([label, value]) => (
              <HStack key={label} justify="between" align="center" gap={4}>
                <Text size="sm" color="secondary">
                  {label}
                </Text>
                <Text size="sm">{value}</Text>
              </HStack>
            ))}
        </VStack>
      </Card>

      {asks.length === 0 ? null : (
        <VStack gap={2}>
          <Heading level={4}>Packages ({asks.length})</Heading>
          <Card padding={0}>
            <List density="compact" hasDividers>
              {asks.map((each) => (
                <ListItem
                  key={`${each.name}@${each.version}`}
                  label={each.name}
                  // The resolved version is the identity; the range is what the file
                  // asked for, and saying so is the whole reason both are stored.
                  description={
                    each.range && each.range !== each.version
                      ? `${each.version} · asked for ${each.range}`
                      : each.version
                  }
                />
              ))}
            </List>
          </Card>
        </VStack>
      )}

      {linked('Builds against', built.references)}
      {linked('Used by', built.dependents)}
      {linked('Compiles', built.sources)}
    </VStack>
  )
}

/**
 * A folder and a page, drawn here rather than imported.
 *
 * The design system's icon registry is a fixed set of semantic names — `menu`,
 * `chevronLeft`, `search` — and has no folder in it, because a folder is this page's
 * idea rather than a component's. Two paths each, `currentColor` so they take the
 * row's ink.
 */
function FolderIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true" {...props}>
      <path
        d="M3.5 6.5A1.5 1.5 0 0 1 5 5h3.8a1.5 1.5 0 0 1 1.2.6l1.1 1.4H19a1.5 1.5 0 0 1 1.5 1.5v9.5A1.5 1.5 0 0 1 19 19.5H5A1.5 1.5 0 0 1 3.5 18Z"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinejoin="round"
      />
    </svg>
  )
}

function FileIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true" {...props}>
      <path
        d="M6.5 3.5h7L19 9v11a1 1 0 0 1-1 1H6.5a1 1 0 0 1-1-1V4.5a1 1 0 0 1 1-1Z"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinejoin="round"
      />
      <path d="M13.5 3.5V9H19" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" />
    </svg>
  )
}

/** What a card is titled with: the declaration as written, or the name itself. */
function title(
  hover: { kind: 'symbol'; symbol: string } | { kind: 'local' },
  card: Info | undefined,
): string {
  if (hover.kind === 'local') return 'a local'
  return card?.signature ?? hover.symbol
}

/**
 * The line that says what a thing *is* — its kind, then the words in front of it.
 *
 * A local carries where it was declared instead: it has no symbol on purpose, so there
 * is no kind recorded for it anywhere, and the line it was written on is both the only
 * thing known and the thing being asked.
 */
function aboutIt(
  hover: { kind: 'symbol' } | { kind: 'local'; line: number },
  declared: Definition | null,
  card: Info | undefined,
): string {
  if (hover.kind === 'local') return `local · declared on line ${hover.line}`
  // The declaration first, then what the symbol says about itself: both carry a kind,
  // and only the second exists for something this index does not declare.
  const named = declared?.kind ?? card?.kind ?? ''
  const kind = named && named !== 'other' ? readable(named) : ''
  return [kind, card?.modifiers].filter(Boolean).join(' · ')
}

/** Every directory prefix on the way to a path, each with its trailing `/`. */
function ancestorsOf(path: string | null): string[] {
  if (!path) return []
  const out: string[] = []
  let prefix = ''
  for (const segment of path.split('/').slice(0, -1)) {
    prefix = `${prefix}${segment}/`
    out.push(prefix)
  }
  return out
}

/** A union alternative's name as a reader would say it — `class_` is a class. */
function readable(kind: string): string {
  return kind.replace(/_$/, '')
}

/**
 * A path split the way every row here splits a name: the thing, then where it lives.
 *
 * A column three hundred pixels wide truncates from the right, so a full path in a
 * row's label hides the filename and keeps `clients/dotnet/Boxops.Fjord.Cl…` — the
 * half a reader already knows. The whole path is still on the page; it is the code
 * pane's title, where there is room for it.
 */
function basename(path: string): string {
  return path.slice(path.lastIndexOf('/') + 1)
}

function folder(path: string): string {
  const at = path.lastIndexOf('/')
  return at === -1 ? '' : path.slice(0, at)
}

