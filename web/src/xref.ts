/**
 * **What is under the cursor**, and where it is declared.
 *
 * `codemarkup.FileXRef` is described in the schema as "what a renderer asks once
 * per file to splice links over text" — this is that splice, done by hit-testing
 * rather than by wrapping every reference in an element. The code is rendered by
 * `CodeBlock`, which owns its own DOM; reaching in to wrap six hundred spans would
 * fight it and would have to be redone on every scroll.
 *
 * **So the arrays are flat and the search is a binary search.** A code view asks
 * this question on every mouse move, and the alternative — an array of objects
 * whose every field is a getter crossing into WebAssembly — is hundreds of
 * crossings per move. `FileRefs` hands over two `Int32Array`s once per file and
 * everything here happens in the page.
 */
import type { Blob } from './corpus'

/** Bytes, because a span in the index counts UTF-8 and a JS string counts UTF-16. */
const encoder = new TextEncoder()

/**
 * The index of the entry whose `[start, start + length)` contains `offset`, or
 * `-1`.
 *
 * `stride` is how many numbers each entry occupies — two for a global reference,
 * four for a local one — and both arrays are ordered by `start`, which is what
 * makes this a binary search rather than a scan.
 */
export function spanAt(flat: Int32Array, stride: number, offset: number): number {
  let low = 0
  let high = flat.length / stride - 1

  while (low <= high) {
    const mid = (low + high) >> 1
    const start = flat[mid * stride]

    if (offset < start) high = mid - 1
    else if (offset >= start + flat[mid * stride + 1]) low = mid + 1
    else return mid
  }

  return -1
}

/** Where a line and a UTF-16 column fall in the file, counted in bytes. */
export function byteOffsetOf(blob: Blob, line: number, column: number): number | null {
  const start = blob.start(line)
  const text = blob.text(line)
  if (start === undefined || text === undefined) return null

  return start + encoder.encode(text.slice(0, column)).length
}

/** A line and a UTF-16 column, from a point on the screen. */
export function positionAt(
  root: HTMLElement,
  x: number,
  y: number,
): { line: number; column: number } | null {
  const caret = caretAt(x, y)
  if (!caret) return null

  const { node, offset } = caret
  if (!root.contains(node)) return null

  const element = node.nodeType === Node.TEXT_NODE ? node.parentElement : (node as Element)
  const holder = element?.closest('[data-line]')
  if (!holder) return null

  const line = Number(holder.getAttribute('data-line'))
  if (!Number.isFinite(line)) return null

  // The column by measuring, not by counting nodes: the line may be one text node
  // or a dozen spans depending on how the block chose to paint, and a range from
  // the line's start to the caret is right either way.
  const range = document.createRange()
  range.setStart(holder, 0)
  range.setEnd(node, offset)

  // An empty line renders as a zero-width space, which is a character the file
  // does not have; counting it would shift every column on that line by one.
  const column = range.toString().replace(/​/g, '').length

  return { line, column }
}

/**
 * The two caret APIs, because neither is everywhere: `caretPositionFromPoint` is
 * the standard one and `caretRangeFromPoint` is what WebKit and older Chrome ship.
 */
function caretAt(x: number, y: number): { node: Node; offset: number } | null {
  const withPosition = document as Document & {
    caretPositionFromPoint?: (x: number, y: number) => { offsetNode: Node; offset: number } | null
  }

  const position = withPosition.caretPositionFromPoint?.(x, y)
  if (position) return { node: position.offsetNode, offset: position.offset }

  const withRange = document as Document & {
    caretRangeFromPoint?: (x: number, y: number) => Range | null
  }

  const range = withRange.caretRangeFromPoint?.(x, y)
  return range ? { node: range.startContainer, offset: range.startOffset } : null
}
