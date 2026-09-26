/**
 * **The book, as facts** — the pages, the anchors they declare, the links they name.
 *
 * One JSON object per line on stdout, ready for `fjord write` against
 * `scripts/docs.sigla`. `scripts/check-links.py` runs this and asks the database
 * whether every link resolves.
 *
 * It runs **the site's own remark plugins**, which is the reason this is a Node
 * script and not twenty lines of Python: an anchor is whatever `mdx/headings.mjs`
 * gives a heading, so an anchor in the database is an anchor a reader can land on.
 * The gate this replaced computed anchors with a regular expression of its own and
 * got eight of them wrong — it matched `# ` inside code fences — which is what a
 * second parser of the same pages is always free to do.
 */
import { compile } from '@mdx-js/mdx'
import remarkGfm from 'remark-gfm'
import remarkHeadings from './mdx/headings.mjs'
import { readdirSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const CONTENT = resolve(import.meta.dirname, 'src/content')

/** The links a page names, and the anchors it writes by hand. */
function remarkGraph(out) {
  return (tree) => {
    const visit = (node) => {
      if (node.type === 'link') out.links.push(node.url)

      // `<a id="i9"></a>` and `<a href="storage.html">` — the book's authored HTML,
      // which MDX parses as JSX rather than leaving as a string.
      if (node.type === 'mdxJsxFlowElement' || node.type === 'mdxJsxTextElement') {
        if (node.name === 'a') {
          for (const attribute of node.attributes ?? []) {
            if (attribute.type !== 'mdxJsxAttribute') continue
            if (typeof attribute.value !== 'string') continue
            if (attribute.name === 'href') out.links.push(attribute.value)
            if (attribute.name === 'id') out.anchors.push(attribute.value)
          }
        }
      }

      for (const child of node.children ?? []) visit(child)
    }
    visit(tree)
  }
}

const line = (predicate, fact) => console.log(JSON.stringify({ predicate, fact }))

for (const file of readdirSync(CONTENT).filter((f) => f.endsWith('.mdx')).sort()) {
  const slug = file.slice(0, -'.mdx'.length)
  const found = { links: [], anchors: [] }
  const compiled = await compile(readFileSync(resolve(CONTENT, file)), {
    remarkPlugins: [remarkGfm, remarkHeadings, () => remarkGraph(found)],
  })

  line('doc.Page', slug)
  for (const heading of compiled.data.headings ?? []) {
    if (heading.id) line('doc.Anchor', { page: slug, id: heading.id })
  }
  for (const id of found.anchors) line('doc.Anchor', { page: slug, id })

  for (const target of found.links) {
    if (/^(https?:|mailto:)/.test(target)) continue
    const [page, anchor = ''] = target.split('#')
    // The book links between pages as `storage.html#keys`, which is the form every
    // link outside this repository was written against. Anything else — a relative
    // path to a file in the tree, say — is not a link between pages.
    if (page && !page.endsWith('.html')) continue
    const to = page ? page.slice(0, -'.html'.length) : slug
    line('doc.Link', { from: `web/src/content/${file}`, toPage: to, toAnchor: anchor })
  }
}
