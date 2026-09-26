/** The anchor a heading's text earns, unchanged from the generated site. */
export function slugify(text: string): string

/** One heading, with the anchor it declares and the prose under it. */
export type Heading = { level: number; id: string; text: string; prose: string }

/**
 * Gives every heading its anchor and exports the page's list of them as
 * `headings`. See `mdx/headings.mjs` — this only describes it to `tsc`.
 */
export default function remarkHeadings(): (tree: unknown, file?: unknown) => void
