import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import mdx from '@mdx-js/rollup'
import remarkGfm from 'remark-gfm'
import remarkHeadings from './mdx/headings.mjs'
import { copyFileSync, mkdirSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'

/** The reading order, which is also the list of paths a host has to answer. */
function pages(): string[] {
  const nav = JSON.parse(readFileSync(resolve(__dirname, 'src/content/nav.json'), 'utf8')) as {
    groups: { pages: { slug: string }[] }[]
  }
  const slugs = nav.groups.flatMap((group) => group.pages.map((page) => page.slug))
  // The workbench and the code browser are routes with no page behind them, so the
  // nav does not name them.
  return [...slugs.filter((slug) => slug !== 'index'), 'playground', 'browse']
}

// The book lives in `src/content/`: one MDX page per page, compiled into this
// application like any other module. There is no second renderer and no
// generated copy, so a page cannot be published in two states.
export default defineConfig({
  base: process.env.SITE_BASE ?? '/',
  plugins: [
    // **Before `react()`**, which is a `transform` over JSX: MDX has to have
    // turned the page into JSX by the time it runs, or the plugin sees prose.
    { enforce: 'pre', ...mdx({ remarkPlugins: [remarkGfm, remarkHeadings] }) },
    react({ include: /\.(mdx|jsx|tsx)$/ }),
    {
      // **A page is a path, and a path has to be a file.** A host that has never
      // heard of `/storage` will still answer with the application if it is given
      // a fallback document — `404.html`, which is what GitHub Pages uses — but it
      // answers **404**, and a docs site whose every page is a 404 to anything
      // reading status codes (a link preview, a crawler, a link checker) is live
      // only to a reader who starts at the root.
      //
      // So every known route gets a document of its own, in both shapes a static
      // host resolves an extensionless path through: `storage.html` and
      // `storage/index.html`. They are copies of `index.html`, which is legitimate
      // here because the application decides what to render from the path — and
      // its asset URLs are absolute under the base, so the same bytes work at any
      // depth. `404.html` stays, for a path nothing knows about.
      name: 'fjord-routes-as-files',
      closeBundle() {
        const out = resolve(__dirname, 'dist')
        const index = resolve(out, 'index.html')
        copyFileSync(index, resolve(out, '404.html'))
        for (const slug of pages()) {
          copyFileSync(index, resolve(out, `${slug}.html`))
          mkdirSync(resolve(out, slug), { recursive: true })
          copyFileSync(index, resolve(out, slug, 'index.html'))
        }
      },
    },
  ],
})
