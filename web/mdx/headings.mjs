/**
 * **Heading anchors, and the list of them a page declares.**
 *
 * A heading is a link target. The book cites `storage.html#keys` from its own
 * pages, from the crates, from the plan and from the changelog, so the id a
 * heading carries is published surface rather than a rendering detail — and it
 * is computed here the way the Markdown book computed it: marks off, lowercased,
 * everything but letters, digits, spaces and dashes dropped, runs collapsed to
 * one dash. Every link written against the generated site still lands.
 *
 * The same walk hangs the list off the module as `headings`. That is what the
 * search index reads and what the drift gate indexes, so the anchors a page
 * declares are computed **once** — a second parser over the same pages is the
 * arrangement that let the two renderers disagree, and it is not repeated here.
 */
import { parse } from 'acorn'

/** The nodes whose text survives into a heading's plain form. */
const LITERAL = new Set(['text', 'inlineCode'])

/**
 * A node's text with its marks off — the mdast equivalent of stripping `` ` ``,
 * `*`, `~` and link syntax, which is what the Markdown renderer did before it
 * slugified. Emphasis, strong, delete and link are containers, so descending
 * through them drops the syntax and keeps the words.
 */
function plain(node) {
  if (LITERAL.has(node.type)) return node.value
  if (!Array.isArray(node.children)) return ''
  return node.children.map(plain).join('')
}

/** The anchor a heading's text earns, unchanged from the generated site. */
export function slugify(text) {
  const stripped = text
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '')
  return stripped.replace(/[\s-]+/g, '-').replace(/^-+|-+$/g, '') || 'section'
}

/** How much prose a heading carries into the search index. */
const SNIPPET = 600

export default function remarkHeadings() {
  return (tree, file) => {
    const seen = new Map()
    const found = []

    // The prose *before* the first heading belongs to the page rather than to a
    // section of it, and it is what a search for the page's subject should find.
    // It is collected under the empty anchor, which is the page's own top.
    let section = { level: 1, id: '', text: '', prose: '' }
    found.push(section)

    const visit = (node) => {
      if (node.type === 'heading') {
        const text = plain(node).trim()
        // **A page may say the same thing twice.** `invariants` has a "Why" under
        // every entry. The second one cannot take the first one's anchor, so it
        // takes a numbered one — the rule the Markdown renderer used, kept so a
        // link to `#why-3` still means the third.
        const base = slugify(text)
        const count = seen.get(base)
        const id = count === undefined ? base : `${base}-${count + 1}`
        seen.set(base, (count ?? 0) + 1)

        node.data ??= {}
        node.data.hProperties = { ...node.data.hProperties, id }

        section = { level: node.depth, id, text, prose: '' }
        found.push(section)
        return
      }

      if (LITERAL.has(node.type)) {
        if (section.prose.length < SNIPPET) section.prose += `${node.value} `
        return
      }

      for (const child of node.children ?? []) visit(child)
    }

    for (const child of tree.children) visit(child)
    for (const entry of found) entry.prose = entry.prose.trim().slice(0, SNIPPET)

    const source = `export const headings = ${JSON.stringify(found)}`
    tree.children.unshift({
      type: 'mdxjsEsm',
      value: source,
      data: { estree: parse(source, { ecmaVersion: 'latest', sourceType: 'module' }) },
    })

    if (file) file.data.headings = found
  }
}
