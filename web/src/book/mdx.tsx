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
 * Anything the dialect cannot spell — a live demo, a callout — is a component
 * the page names directly. That is the point of the format: a page that needs a
 * diagram imports one instead of drawing it in dashes.
 */
import { createContext, use, type ComponentProps, type ReactNode } from 'react'
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
import { href } from './links'

/**
 * Whether the row being rendered is in the head.
 *
 * `TableRow` takes `isHeaderRow` as a prop and the design system reads no
 * context for it, but MDX emits the `thead` and the `tr` as separate elements —
 * so the section tells the row what it is on the way past.
 */
const InHead = createContext(false)

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

  table: ({ children }: ComponentProps<'table'>) => (
    <Table density="compact" verticalAlign="top">
      {children}
    </Table>
  ),
  thead: ({ children }: ComponentProps<'thead'>) => <InHead value={true}>{children}</InHead>,
  tbody: ({ children }: ComponentProps<'tbody'>) => <>{children}</>,
  tr: ({ children }: ComponentProps<'tr'>) => {
    const head = use(InHead)
    return <TableRow isHeaderRow={head}>{children}</TableRow>
  },
  th: ({ children }: ComponentProps<'th'>) => <TableHeaderCell>{children}</TableHeaderCell>,
  td: ({ children }: ComponentProps<'td'>) => <TableCell>{children}</TableCell>,

  blockquote: ({ children }: ComponentProps<'blockquote'>) => (
    <Blockquote>
      <VStack gap={2}>{children}</VStack>
    </Blockquote>
  ),
  hr: () => <Divider />,

  // The components a page names for itself.
  Callout,
  Demo,
}
