/**
 * **What scrolls, passed as the element rather than as a selector for it.**
 *
 * The frame fills the viewport and the regions scroll independently, so the
 * page's scroll position is not the document's — resetting it means resetting
 * one particular region. `Layout` already holds that region as a ref, because
 * the on-page outline needs the same one to know what a heading is scrolled
 * past; this hands it to whatever else has to move it.
 *
 * A ref and not a class selector. `astryx-layout-content` is on three elements
 * at once here — the shell's, the book column's and the drawer's — and the
 * first in document order is not the one that scrolls, which is a reset that
 * looks right, runs, and does nothing.
 */
import { createContext, use, type RefObject } from 'react'

const ScrollRootContext = createContext<RefObject<HTMLDivElement | null> | null>(null)

export const ScrollRootProvider = ScrollRootContext.Provider

/** The scrolling region this page sits in, or `null` outside a `Layout`. */
export function useScrollRoot(): RefObject<HTMLDivElement | null> | null {
  return use(ScrollRootContext)
}
