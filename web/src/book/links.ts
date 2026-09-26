/**
 * **Where a link in the book points, once the book is one application.**
 *
 * The pages cite each other as `storage.html#keys`. That is what the generated
 * site served, it is what the crates, the plan and the changelog were written
 * against, and rewriting every one of them into a route would break every link
 * outside this repository that ever pointed at a page. So the form stays and
 * the resolution moves: `.html` is read as *a page of the book*, and turned into
 * whatever path this site is being served from.
 */

/** A page's path under whatever base the site is served from. */
export function route(slug: string): string {
  const base = import.meta.env.BASE_URL
  return slug === 'index' ? base : `${base}${slug}`
}

/** Where a link in the content points once the site is one application. */
export function href(target: string): string {
  if (/^(https?:|mailto:|#)/.test(target)) return target
  const [page, anchor] = target.split('#')
  if (page.endsWith('.html')) {
    const slug = page.slice(0, -'.html'.length)
    return route(slug) + (anchor ? `#${anchor}` : '')
  }
  return target
}
