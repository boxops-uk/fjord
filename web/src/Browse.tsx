import { useEffect, useMemo, useRef, useState } from 'react'
import { Banner } from '@astryxdesign/core/Banner'
import { Code, CodeBlock } from '@astryxdesign/core/CodeBlock'
import { EmptyState } from '@astryxdesign/core/EmptyState'
import { Layout as Panes, LayoutContent, LayoutPanel } from '@astryxdesign/core/Layout'
import { List, ListItem } from '@astryxdesign/core/List'
import { ResizeHandle, useResizable } from '@astryxdesign/core/Resizable'
import { Spinner } from '@astryxdesign/core/Spinner'
import { HStack, VStack } from '@astryxdesign/core/Stack'
import { Text } from '@astryxdesign/core/Text'
import { TextInput } from '@astryxdesign/core/TextInput'
import { Token } from '@astryxdesign/core/Token'
import { TreeList } from '@astryxdesign/core/TreeList'
import type { TreeListItemData } from '@astryxdesign/core/TreeList'
import { type Blob, type Corpus, loadCorpus } from './corpus'
import { paint } from './highlight'
import { byteOffsetOf, positionAt, spanAt } from './xref'

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
  const [chosen, setPath] = useState<string | null>(null)
  const [symbol, setSymbol] = useState<string | null>(null)
  const [term, setTerm] = useState('')
  /**
   * Where the last jump landed. A reference is a *span*, not a file, so
   * navigating to one and highlighting the file's declaration instead would put
   * the reader in the right file at the wrong line — which is worse than not
   * moving, because it looks like it worked.
   */
  const [at, setAt] = useState<{ path: string; start: number } | null>(null)

  useEffect(() => {
    loadCorpus().then(setCorpus, (error: unknown) => setFailure(String(error)))
  }, [])

  const tree = useResizable({
    defaultSize: 260,
    minSizePx: 200,
    maxSizePx: 420,
    autoSaveId: 'fjord-browse-tree',
  })
  const panel = useResizable({
    defaultSize: 380,
    minSizePx: 300,
    maxSizePx: 520,
    autoSaveId: 'fjord-browse-panel',
  })

  const paths = useMemo(() => corpus?.files() ?? [], [corpus])

  /**
   * Which file is open — the one chosen, or the first there is.
   *
   * Derived rather than seeded by an effect: "nothing chosen yet" and "the first
   * file" are the same state, and writing one into the other on mount is a second
   * render that says nothing new.
   */
  const path = chosen ?? paths[0] ?? null

  const blob = useMemo(
    () => (corpus && path ? corpus.open(path) : null),
    [corpus, path],
  )

  // Memoised together, and the tokenizer with them: `CodeBlock` holds it in a
  // dependency array, so a fresh closure per render would re-tokenise the file on
  // every keystroke somewhere else on the page.
  const painted = useMemo(() => (blob ? paint(blob) : null), [blob])
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
  const hits = useMemo(
    () => (corpus && term.trim() ? corpus.search(term.trim()) : []),
    [corpus, term],
  )

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
    if (global !== -1) return { kind: 'symbol', symbol: fileRefs.symbols[global] }

    const local = spanAt(fileRefs.locals, 4, offset)
    if (local !== -1) return { kind: 'local', start: fileRefs.locals[local * 4 + 2] }

    return null
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
  const pending = useRef(0)

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

  return (
    <Panes
      start={
        <>
          <LayoutPanel label="Files" width={tree.size} isScrollable padding={0}>
            <VStack gap={2} padding={3}>
              <TextInput
                label="Search"
                placeholder="A name, or the start of one"
                value={term}
                onChange={setTerm}
              />
            </VStack>

            {term.trim() ? (
              hits.length === 0 ? (
                <EmptyState title="No name starts with that" />
              ) : (
                <List density="compact" header={<Text size="sm" color="secondary">{hits.length} matching</Text>}>
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
                      }}
                      isSelected={hit.symbol === symbol}
                    />
                  ))}
                </List>
              )
            ) : (
              <TreeList
                density="compact"
                items={asTree(paths, path, (chosen) => {
                  setPath(chosen)
                  setSymbol(null)
                  setAt(null)
                })}
              />
            )}
          </LayoutPanel>
          <ResizeHandle
            direction="horizontal"
            hasDivider
            resizable={tree.props}
            label="Resize the file tree"
          />
        </>
      }
      content={
        <LayoutContent isScrollable padding={0}>
          <VStack
            ref={view}
            gap={0}
            style={{ cursor: overLink ? 'pointer' : undefined }}
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
                setOverLink(resolve(clientX, clientY) !== null)
              })
            }}
            onMouseLeave={() => setOverLink(false)}
          >
          {painted && path ? (
            // `width="100%"` so a short file still fills the pane rather than
            // shrinking to its longest line. It costs nothing: the block's own
            // body is the horizontal scroller either way, so a line wider than
            // the pane scrolls rather than being clipped.
            <CodeBlock
              code={painted.code}
              language="csharp"
              title={path}
              tokenizer={tokenizer}
              hasLineNumbers
              highlightLines={highlighted}
              container="section"
              width="100%"
              size="sm"
            />
          ) : (
            <EmptyState title="Choose a file" description="The index holds every file on the left." />
          )}
          </VStack>
        </LayoutContent>
      }
      end={
        <>
          <ResizeHandle
            direction="horizontal"
            isReversed
            hasDivider
            resizable={panel.props}
            label="Resize the panel"
          />
          <LayoutPanel label="Outline" width={panel.size} isScrollable padding={0}>
            <VStack gap={3} padding={3}>
              <HStack justify="between" align="center">
                <Text size="sm" color="secondary">
                  {corpus.rows.toLocaleString()} facts · schema {corpus.fingerprint.slice(0, 10)}
                </Text>
              </HStack>
            </VStack>

            {symbol && (
              <List
                density="compact"
                hasDividers
                header={
                  <Text size="sm" color="secondary">
                    {uses.length} {uses.length === 1 ? 'use' : 'uses'} across the index
                  </Text>
                }
              >
                {uses.map((use) => (
                  <ListItem
                    key={`${use.path}:${use.start}`}
                    label={use.path}
                    description={`byte ${use.start}`}
                    onClick={() => {
                      setPath(use.path)
                      setAt({ path: use.path, start: use.start })
                    }}
                    isSelected={at?.path === use.path && at.start === use.start}
                  />
                ))}
              </List>
            )}

            {outline.length === 0 ? (
              <EmptyState title="Nothing declared here" />
            ) : (
              <List
                density="compact"
                header={<Text size="sm" color="secondary">{outline.length} declared</Text>}
              >
                {outline.map((found) => (
                  <ListItem
                    key={`${found.symbol}:${found.start}`}
                    label={found.name}
                    description={found.kind === 'other' ? undefined : readable(found.kind)}
                    endContent={<Token size="sm" label={String(blob ? lineAt(blob, found.start) : 0)} />}
                    onClick={() => {
                      setSymbol(found.symbol)
                      setAt({ path: found.path, start: found.start })
                    }}
                    isSelected={found.symbol === symbol}
                  />
                ))}
              </List>
            )}

          </LayoutPanel>
        </>
      }
    />
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
type Jump = { kind: 'symbol'; symbol: string } | { kind: 'local'; start: number }

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
  paths: string[],
  selected: string | null,
  onChoose: (path: string) => void,
): TreeListItemData[] {
  type Node = { children: Map<string, Node>; path?: string }
  const root: Node = { children: new Map() }

  for (const path of paths) {
    let at = root
    const segments = path.split('/')
    segments.forEach((segment, index) => {
      let next = at.children.get(segment)
      if (!next) {
        next = { children: new Map() }
        at.children.set(segment, next)
      }
      if (index === segments.length - 1) next.path = path
      at = next
    })
  }

  const build = (node: Node, name: string, id: string): TreeListItemData => {
    const children = [...node.children.entries()]
      .sort(([left, leftNode], [right, rightNode]) => {
        // Directories first, then by name — the order every file tree uses.
        const leftIsDir = leftNode.children.size > 0
        const rightIsDir = rightNode.children.size > 0
        if (leftIsDir !== rightIsDir) return leftIsDir ? -1 : 1
        return left.localeCompare(right)
      })
      .map(([childName, child]) => build(child, childName, `${id}/${childName}`))

    return {
      id,
      label: name,
      ...(children.length > 0 ? { children, isExpanded: true } : {}),
      ...(node.path
        ? { onClick: () => onChoose(node.path!), isSelected: node.path === selected }
        : {}),
    }
  }

  return [...root.children.entries()]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([name, node]) => build(node, name, name))
}

/** A union alternative's name as a reader would say it — `class_` is a class. */
function readable(kind: string): string {
  return kind.replace(/_$/, '')
}
