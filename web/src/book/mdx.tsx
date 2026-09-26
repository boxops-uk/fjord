/**
 * **The book's tags, as the design system's components.**
 *
 * A page is MDX, so it is written as prose and compiled to elements — `##` is an
 * `h2`, a pipe table is a `table`. None of those are what the site renders: a
 * heading is a `Heading`, a table is a `Table`, a fence is a `Code` that the
 * engine paints. This is the whole of that mapping, and it is the reason a page
 * can be written as prose and still inherit the type scale, the spacing and both
 * themes rather than a stylesheet's opinion of them.
 *
 * Anything the dialect cannot spell — a live demo, a callout, a byte layout —
 * is a component the page names directly. That is the point of the format: a
 * page that needs a diagram imports one instead of drawing it in dashes, and
 * `Diagram.tsx` is the set it imports from.
 */
import {
  Children,
  createContext,
  isValidElement,
  use,
  type ComponentProps,
  type ReactNode,
} from 'react'
import { Heading } from '@astryxdesign/core/Heading'
import { Text } from '@astryxdesign/core/Text'
import { Link } from '@astryxdesign/core/Link'
import { Code as InlineCode } from '@astryxdesign/core/Code'
import { List, ListItem } from '@astryxdesign/core/List'
import { Table, TableCell, TableHeaderCell, TableRow } from '@astryxdesign/core/Table'
import { Banner } from '@astryxdesign/core/Banner'
import { Blockquote } from '@astryxdesign/core/Blockquote'
import { Divider } from '@astryxdesign/core/Divider'
import { VStack } from '@astryxdesign/core/Stack'
import { Code } from './Code'
import { Demo } from '../demo/Demo'
import { Bytes, Flow, Journey, Ladder, Lifecycle, Mapping, Sequence, Tree } from './Diagram'
import { href } from './links'

/**
 * Whether the row being rendered is in the head.
 *
 * `TableRow` takes `isHeaderRow` as a prop and the design system reads no
 * context for it, but MDX emits the `thead` and the `tr` as separate elements —
 * so the section tells the row what it is on the way past.
 */
const InHead = createContext(false)

/**
 * The label a body cell carries for itself.
 *
 * A table is columns, and a column's meaning lives in a header cell that is
 * nowhere near it. That holds while the two are on one line; below the width
 * where three columns fit, the book stacks each row into a card and the header
 * row goes away — so every cell has to say what it is. The row hands each of
 * its cells the heading above it, and `book.css` decides which of the two is
 * showing.
 */
const CellLabel = createContext('')

/** The plain text of a node, which is what a heading is worth as a label. */
function plain(node: ReactNode): string {
  if (node === null || node === undefined || typeof node === 'boolean') return ''
  if (typeof node === 'string' || typeof node === 'number') return String(node)
  if (Array.isArray(node)) return node.map(plain).join('')
  if (isValidElement(node)) return plain((node.props as { children?: ReactNode }).children)
  return ''
}

/**
 * The headings of the first row that has any, read out of the table's own
 * children before it renders.
 *
 * Walking the element tree rather than the DOM is what keeps this declarative:
 * the labels are known at render, so a cell is never a cell that briefly had no
 * label. GFM gives every table a header row, so the first row holding `th`
 * cells is it.
 */
function headings(node: ReactNode): string[] {
  let found: string[] | null = null
  const walk = (current: ReactNode): void => {
    if (found || !current) return
    if (Array.isArray(current)) {
      current.forEach(walk)
      return
    }
    if (!isValidElement(current)) return
    const kids = (current.props as { children?: ReactNode }).children
    if (current.type === Tr) {
      const cells = Children.toArray(kids).filter(
        (cell) => isValidElement(cell) && cell.type === Th,
      )
      if (cells.length) {
        found = cells.map((cell) =>
          plain((cell as { props?: { children?: ReactNode } }).props?.children),
        )
        return
      }
    }
    walk(kids)
  }
  walk(node)
  return found ?? []
}

function TableBlock({ children }: ComponentProps<'table'>) {
  return (
    <Headings value={headings(children)}>
      <Table density="compact" verticalAlign="top">
        {children}
      </Table>
    </Headings>
  )
}

/** The headings of the table being rendered, for its body cells to read. */
const Headings = createContext<string[]>([])

function Tr({ children }: ComponentProps<'tr'>) {
  const head = use(InHead)
  const labels = use(Headings)
  return (
    <TableRow isHeaderRow={head}>
      {head
        ? children
        : // A context provider renders no element of its own, so the row's
          // children are still exactly its cells.
          Children.map(children, (cell, column) => (
            <CellLabel value={labels[column] ?? ''}>{cell}</CellLabel>
          ))}
    </TableRow>
  )
}

function Th({ children }: ComponentProps<'th'>) {
  return <TableHeaderCell>{children}</TableHeaderCell>
}

function Td({ children }: ComponentProps<'td'>) {
  const label = use(CellLabel)
  // **A cell the row does not fill gets no label**, so that the card can drop
  // it whole rather than show a heading standing over nothing. Asked in CSS
  // this is `:only-child`, which counts elements and not the text beside them —
  // so every cell whose content was plain prose looked empty and vanished.
  const filled = plain(children).trim() !== ''
  return (
    <TableCell>
      {label && filled ? <span className="cell-label">{label}</span> : null}
      {children}
    </TableCell>
  )
}

/** The tone a callout carries, as the design system's four statuses. */
const STATUS = {
  note: 'info',
  warn: 'warning',
  invariant: 'success',
  gap: 'warning',
} as const

/**
 * An aside with a tone: a note, a warning, an invariant being stated.
 *
 * The book had these as `:::note`, a fenced dialect the two renderers had to
 * agree about. Now it is a component, which means a page that wants one that
 * holds a table or a demo just writes one — the fence could only hold what its
 * parser had been taught.
 */
export function Callout({
  tone = 'note',
  label,
  children,
}: {
  tone?: keyof typeof STATUS
  label?: ReactNode
  children: ReactNode
}) {
  return (
    <Banner status={STATUS[tone] ?? 'info'} title={label} defaultIsExpanded>
      <VStack gap={2}>{children}</VStack>
    </Banner>
  )
}

/** A fenced block, with the language the fence named. */
function Pre({ children }: { children?: ReactNode }) {
  // MDX gives a fence as `<pre><code class="language-rust">…</code></pre>`. The
  // fence is the thing being rendered, so the `code` inside it is read for its
  // language and its text and then dropped — it is markup, not content.
  const code = (children ?? null) as {
    props?: { className?: string; children?: ReactNode }
  } | null
  const className = code?.props?.className ?? ''
  const lang = /language-([\w-]+)/.exec(className)?.[1] ?? ''
  const source = String(code?.props?.children ?? '').replace(/\n$/, '')
  return <Code lang={lang} source={source} />
}

/**
 * A heading, at the level the prose asked for.
 *
 * The intrinsic props are read rather than spread: an `h2`'s DOM props carry a
 * `color` of type `string`, and the design system's is a token name — spreading
 * one into the other widens it back to `string` and the component stops being
 * typed. Only `id` matters here, and it is the anchor the page declares.
 */
function heading(level: 1 | 2 | 3 | 4) {
  return function Mapped({ id, children }: ComponentProps<'h2'>) {
    return (
      <Heading level={level} id={id} {...(level === 1 ? { type: 'display-2' as const } : {})}>
        {children}
      </Heading>
    )
  }
}

export const components = {
  h1: heading(1),
  h2: heading(2),
  h3: heading(3),
  h4: heading(4),
  h5: heading(4),
  h6: heading(4),

  p: ({ children }: ComponentProps<'p'>) => <Text as="p">{children}</Text>,

  a: ({ href: target = '', className, children }: ComponentProps<'a'>) => (
    <Link href={href(target)} isExternalLink={/^https?:/.test(target)} className={className}>
      {children}
    </Link>
  ),

  strong: ({ children }: ComponentProps<'strong'>) => <Text weight="semibold">{children}</Text>,
  del: ({ children }: ComponentProps<'del'>) => <Text hasStrikethrough>{children}</Text>,

  code: ({ children }: ComponentProps<'code'>) => <InlineCode>{children}</InlineCode>,
  pre: Pre,

  ul: ({ children }: ComponentProps<'ul'>) => (
    <List listStyle="disc" density="compact">
      {children}
    </List>
  ),
  ol: ({ children }: ComponentProps<'ol'>) => (
    <List listStyle="decimal" density="compact">
      {children}
    </List>
  ),
  li: ({ children }: ComponentProps<'li'>) => <ListItem label={children} />,

  table: TableBlock,
  thead: ({ children }: ComponentProps<'thead'>) => <InHead value={true}>{children}</InHead>,
  tbody: ({ children }: ComponentProps<'tbody'>) => <>{children}</>,
  tr: Tr,
  th: Th,
  td: Td,

  blockquote: ({ children }: ComponentProps<'blockquote'>) => (
    <Blockquote>
      <VStack gap={2}>{children}</VStack>
    </Blockquote>
  ),
  hr: () => <Divider />,

  // The components a page names for itself.
  Callout,
  Demo,

  // **The diagrams.** A page that needs one imports it rather than drawing it
  // in dashes, which is what the note at the top of this file promised and
  // what these finally make true. Keep this list in step with the one
  // `smoke.mjs` counts a page's diagrams by.
  Bytes,
  Flow,
  Journey,
  Ladder,
  Lifecycle,
  Mapping,
  Sequence,
  Tree,
}
