/**
 * **Routing, in one file.**
 *
 * A page is a path, not a fragment: `#i5` has to stay the anchor a heading
 * links to, and the book is full of them. The pages are known at build time and
 * there are twenty of them, so this is a URL, a listener and a lookup — a
 * router library would be more code than the site it routes.
 *
 * The trap is the served copy: a path route needs the host to answer every path
 * with the same document. `dist/404.html` is that answer for GitHub Pages, and
 * `vite preview` and the dev server do it by themselves.
 */
import { useSyncExternalStore } from 'react'

const listeners = new Set<() => void>()

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  window.addEventListener('popstate', listener)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('popstate', listener)
  }
}

function snapshot(): string {
  return window.location.pathname + window.location.hash
}

/** The current path and fragment, re-read whenever either changes. */
export function useLocation(): string {
  return useSyncExternalStore(subscribe, snapshot, () => '/')
}

/**
 * **The query string**, for a page whose state lives in one.
 *
 * A sibling of `useLocation` rather than a part of it. The book's pages are
 * paths and know nothing about a query; folding the search into that snapshot
 * would re-render twenty of them for one panel's business, and `slugOf` would
 * have to learn to ignore it. Both subscribe to the same set, so an entry
 * pushed by either is one Back moves through.
 */
export function useSearch(): string {
  return useSyncExternalStore(
    subscribe,
    () => window.location.search,
    () => '',
  )
}

/**
 * Write the query string on the current path, keeping the path and fragment.
 *
 * `replace` for a change that is not somewhere to come back to — a filter being
 * typed, rather than a place being opened. Writing the query it already has is
 * not history: the guard is what lets a caller state where it is on every
 * render without stacking entries.
 */
export function setSearch(query: string, replace = false): void {
  const search = query ? `?${query}` : ''
  if (search === window.location.search) return
  const url = window.location.pathname + search + window.location.hash
  window.history[replace ? 'replaceState' : 'pushState'](null, '', url)
  for (const listener of listeners) listener()
}

export function navigate(to: string, replace = false): void {
  const url = new URL(to, window.location.href)
  if (url.origin !== window.location.origin) {
    window.location.href = to
    return
  }
  const same = url.pathname === window.location.pathname
  if (same && url.hash === window.location.hash) {
    scrollTo(url.hash)
    return
  }
  window.history[replace ? 'replaceState' : 'pushState'](null, '', url)
  for (const listener of listeners) listener()
  // The document, which on this site never scrolls — the frame fills the viewport
  // and the regions scroll inside it. Kept as the fallback for a layout that does
  // scroll the page; the reset that actually moves a reader is `PageView`'s, which
  // has the scrolling region itself and can tell a fragment from a fresh page.
  if (!same) window.scrollTo({ top: 0 })
  scrollTo(url.hash)
}

/** Bring a fragment into view, after the page it names has rendered. */
export function scrollTo(hash: string): void {
  if (!hash) return
  requestAnimationFrame(() => {
    document.getElementById(hash.slice(1))?.scrollIntoView({ block: 'start' })
  })
}

/** Which page a path names. The site's root is the book's first page. */
export function slugOf(pathname: string): string {
  const base = import.meta.env.BASE_URL
  const path = (pathname.startsWith(base) ? pathname.slice(base.length) : pathname.replace(/^\//, ''))
    .replace(/\/$/, '')
    // A path the generated site would serve, followed in from somewhere older.
    .replace(/\.html$/, '')
  return path === '' ? 'index' : path
}
