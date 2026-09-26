import { useEffect } from 'react'
import { Heading } from '@astryxdesign/core/Heading'
import { Text } from '@astryxdesign/core/Text'
import { Link } from '@astryxdesign/core/Link'
import { Code as InlineCode } from '@astryxdesign/core/Code'
import { Divider } from '@astryxdesign/core/Divider'
import { Card } from '@astryxdesign/core/Card'
import { Grid } from '@astryxdesign/core/Grid'
import { Section } from '@astryxdesign/core/Section'
import { VStack } from '@astryxdesign/core/Stack'
import { components } from './mdx'
import { navTitle, neighbours, page as findPage } from './content'
import { route } from './links'
import { scrollTo } from './router'
import { useScrollRoot } from './scrollRoot'

const SITE = 'Fjord DB'

/**
 * How wide a page is allowed to get.
 *
 * A fixed measure is right for prose and wrong for a page, because most of a
 * page here is not prose: a demo, a plan table or a fence has more to show at
 * 1440 than at 880, and on a wide window the difference was empty gutter. The
 * column grows with the window; the *reading* measure is kept by `book.css`,
 * which caps the paragraphs rather than the page.
 */
const PAGE_WIDTH = 'min(100%, 1440px)'

/* The page sits in a centring flex row, where a bare `max-width` is only a
   ceiling: the item is still sized by its contents, so the column ended up as
   wide as its widest paragraph rather than as wide as it was allowed. It has to
   ask for the width and be capped, not just be capped. */
const PAGE_SIZE = { width: '100%', maxWidth: PAGE_WIDTH } as const

/**
 * One page of the book: the prose as it was written, with the demos running.
 *
 * The page is a component, compiled from MDX, and `components` is the whole of
 * how it is styled — a heading is a `Heading`, a table is a `Table`, a fence is
 * a `Code` the engine paints. A page that needs more than prose names the
 * component it needs and MDX resolves it here, so a diagram is a diagram rather
 * than a picture of one drawn in dashes.
 */
export function PageView({ slug, hash }: { slug: string; hash: string }) {
  const page = findPage(slug)

  useEffect(() => {
    document.title = page ? (page.slug === 'index' ? SITE : `${page.title} · ${SITE}`) : SITE
  }, [page])

  // A fragment names a heading that only exists once this page has rendered; a
  // page without one starts at its beginning, which is what every page opened
  // from the reading order or the pager is.
  //
  // The region and not `document.querySelector('.astryx-layout-content')`, which
  // is what this was: three elements carry that class and the first of them is
  // the shell's, which never scrolls — so a reader who left one page at the
  // bottom arrived at the next one at the bottom.
  const root = useScrollRoot()
  useEffect(() => {
    if (hash) scrollTo(hash)
    else root?.current?.scrollTo({ top: 0 })
  }, [slug, hash, root])

  if (!page) {
    return (
      <Section padding={6} paddingBlock={8} {...PAGE_SIZE}>
        <VStack gap={3}>
          <Heading level={1}>Not a page</Heading>
          <Text type="large" color="secondary">
            There is no <InlineCode>{slug}</InlineCode> in the book.{' '}
            <Link href={route('index')}>Start at the beginning</Link>.
          </Text>
        </VStack>
      </Section>
    )
  }

  const { previous, next } = neighbours(slug)
  const { Body } = page

  return (
    <Section padding={6} paddingBlock={8} {...PAGE_SIZE} data-testid="prose">
      <VStack gap={4} align="stretch" className="book-column">
        {page.group && (
          <Text type="label" color="accent" weight="bold">
            {page.group.toUpperCase()}
          </Text>
        )}
        <Heading level={1} type="display-2">
          {page.title}
        </Heading>
        {page.description && (
          <Text as="p" size="lg" color="secondary">
            {page.description}
          </Text>
        )}

        {/* Keyed by the page: a demo holds its query in state, so two pages whose
            demos land in the same position would have React reuse the instance
            and keep the *previous* page's query — a wrong demo that renders
            perfectly and says nothing about it. */}
        <Body key={slug} components={components} />

        <Divider />

        <Grid columns={2} gap={3}>
          {previous ? (
            <Card padding={3}>
              <Link href={route(previous)} data-testid="pager-prev">
                <VStack gap={0.5}>
                  <Text type="supporting">Previous</Text>
                  <Text weight="semibold">{navTitle(previous)}</Text>
                </VStack>
              </Link>
            </Card>
          ) : (
            <span />
          )}
          {next && (
            <Card padding={3}>
              <Link href={route(next)} data-testid="pager-next">
                <VStack gap={0.5} align="end">
                  <Text type="supporting">Next</Text>
                  <Text weight="semibold">{navTitle(next)}</Text>
                </VStack>
              </Link>
            </Card>
          )}
        </Grid>
      </VStack>
    </Section>
  )
}
