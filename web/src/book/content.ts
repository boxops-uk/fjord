/**
 * **The book, as this site reads it.**
 *
 * A page is an MDX module in `src/content/`: prose, compiled to components, with
 * its own title and its own list of headings. There is no second copy and no
 * second renderer — the arrangement this replaced had the pages parsed twice, by
 * a Python generator and a TypeScript port of it, and a page that renders
 * differently depending on which parser found it is a page in two states.
 *
 * The reading order is `content/nav.json`, which the sidebar and the route list
 * both read, so neither can disagree about what the book contains.
 */
import type { ComponentType } from 'react'
import navigation from '../content/nav.json'

/** One heading, with the anchor it declares. Computed by `mdx/headings.mjs`. */
export type Heading = { level: number; id: string; text: string; prose: string }

/** One search entry per heading: the heading, and the prose under it. */
export type Entry = { title: string; page: string; slug: string; anchor: string; text: string }

/** What an MDX page exports: itself, what it is called, and what it declares. */
type Module = {
  default: ComponentType<{ components?: Record<string, unknown> }>
  meta?: { title?: string; description?: string }
  headings?: Heading[]
}

const SOURCES = import.meta.glob('../content/*.mdx', { eager: true }) as Record<string, Module>

export type Page = {
  slug: string
  title: string
  description: string
  group: string
  Body: Module['default']
  headings: Heading[]
}

/** The playground is this site's own page: interactive, so the book has none. */
export const PLAYGROUND = { slug: 'playground', title: 'Playground' }

/**
 * So is the code browser. It sits beside the playground because the two are the
 * same claim asked twice: the workbench is the engine answering for a query, and
 * this is the engine answering for a repository — every question on it a seek
 * against an index in the tab. A reading order that mentioned only the first
 * would leave the second reachable by knowing the URL.
 */
export const BROWSE = { slug: 'browse', title: 'Code browser' }

export const GROUPS: { label: string; pages: { slug: string; title: string }[] }[] = [
  { label: 'Try it', pages: [PLAYGROUND, BROWSE] },
  ...navigation.groups,
]

export const ORDER: string[] = GROUPS.flatMap((group) => group.pages.map((page) => page.slug))

const GROUP_OF = new Map(
  GROUPS.flatMap((group) => group.pages.map((page) => [page.slug, group.label] as const)),
)

const TITLE_OF = new Map(
  GROUPS.flatMap((group) => group.pages.map((page) => [page.slug, page.title] as const)),
)

const PAGES = new Map<string, Page>()
for (const [path, module] of Object.entries(SOURCES)) {
  const slug = path.slice(path.lastIndexOf('/') + 1, -'.mdx'.length)
  PAGES.set(slug, {
    slug,
    title: module.meta?.title ?? slug,
    description: module.meta?.description ?? '',
    group: GROUP_OF.get(slug) ?? '',
    Body: module.default,
    headings: module.headings ?? [],
  })
}

export function page(slug: string): Page | null {
  return PAGES.get(slug) ?? null
}

export function navTitle(slug: string): string {
  return TITLE_OF.get(slug) ?? PAGES.get(slug)?.title ?? slug
}

/** The table of contents: the sections of a page, not every heading in it. */
export function toc(slug: string): Heading[] {
  return (PAGES.get(slug)?.headings ?? []).filter(
    (heading) => heading.id && (heading.level === 2 || heading.level === 3),
  )
}

let index: Entry[] | null = null

/**
 * Every heading in the book, with the prose under it.
 *
 * Built once, on the first search rather than on the way to the first paragraph
 * anyone reads — but it is now a walk over lists the compiler already produced,
 * not a second parse of twenty-two pages.
 */
export function searchIndex(): Entry[] {
  index ??= ORDER.flatMap((slug) => {
    const found = PAGES.get(slug)
    if (!found) return []
    return found.headings.map((heading) => ({
      title: heading.text || found.title,
      page: found.title,
      slug,
      anchor: heading.id,
      text: heading.prose,
    }))
  })
  return index
}

export function neighbours(slug: string): { previous: string | null; next: string | null } {
  const at = ORDER.indexOf(slug)
  if (at === -1) return { previous: null, next: null }
  return {
    previous: at > 0 ? ORDER[at - 1] : null,
    next: at + 1 < ORDER.length ? ORDER[at + 1] : null,
  }
}
