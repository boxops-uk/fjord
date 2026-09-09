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
 * **Every followable byte range in a file**, ordered, as `[start, end)` pairs.
 *
 * The two kinds of reference are one affordance: a global one is followed by a
 * query for its symbol's definition and a file-local one by a span in this same
 * file, but a reader sees a name they can click either way — so what a renderer
 * asks for is the union, and both arrays are already ordered by `start`, which
 * makes this a merge rather than a sort.
 *
 * An overlap between the two is folded, because a stretch of text underlined
 * twice is a stretch cut in two for no reason a reader can see.
 */
export function links(refs: { spans: Int32Array; locals: Int32Array }): number[] {
  const ranges: number[] = []
  let global = 0
  let local = 0

  const add = (start: number, length: number) => {
    const last = ranges.length - 1
    if (ranges.length > 0 && start <= ranges[last])
      ranges[last] = Math.max(ranges[last], start + length)
    else ranges.push(start, start + length)
  }

  while (global < refs.spans.length || local < refs.locals.length) {
    const next = global < refs.spans.length ? refs.spans[global] : Infinity
    const here = local < refs.locals.length ? refs.locals[local] : Infinity

    if (next <= here) {
      add(refs.spans[global], refs.spans[global + 1])
      global += 2
    } else {
      add(refs.locals[local], refs.locals[local + 1])
      local += 4
    }
  }

  return ranges
}

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
 * **Where a byte range sits on the screen**, for hanging a card off it.
 *
 * The inverse of `positionAt`, and it exists because the code has no element to
 * anchor to: `CodeBlock` paints with the CSS Custom Highlight API, so a line is
 * one text node and every colour on it is a `Range`, not a `<span>`. A card
 * anchored to the line would sit at the start of the line; a card anchored to the
 * pointer would wander while a reader holds still. A `Range` over the same bytes
 * the highlight covers puts it on the name.
 *
 * `null` where the line is not rendered — the block virtualises long files, and a
 * reference above or below the window has no rectangle to speak of.
 */
export function rectOf(
  root: HTMLElement,
  blob: Blob,
  line: number,
  start: number,
  length: number,
): DOMRect | null {
  const holder = root.querySelector(`[data-line="${line}"]`)
  if (!holder) return null

  const lineStart = blob.start(line)
  const text = blob.text(line)
  if (lineStart === undefined || text === undefined) return null

  // Bytes on the way in, UTF-16 on the way out: the index counts a span in UTF-8
  // and a DOM offset is a JS string index, and the two part company at the first
  // character outside ASCII.
  const from = utf16Of(text, start - lineStart)
  const to = utf16Of(text, start - lineStart + length)
  if (from === null || to === null) return null

  const range = document.createRange()
  const set = (pos: number, apply: (node: Node, offset: number) => void): boolean => {
    let left = pos
    const walk = document.createTreeWalker(holder, NodeFilter.SHOW_TEXT)
    while (walk.nextNode()) {
      const node = walk.currentNode as Text
      if (left <= node.data.length) {
        apply(node, left)
        return true
      }
      left -= node.data.length
    }
    return false
  }
  if (!set(from, range.setStart.bind(range)) || !set(to, range.setEnd.bind(range))) return null
  const rect = range.getBoundingClientRect()
  return rect.width === 0 && rect.height === 0 ? null : rect
}

/** How many UTF-16 units of `text` the first `bytes` bytes of it occupy. */
function utf16Of(text: string, bytes: number): number | null {
  if (bytes <= 0) return 0

  let seen = 0
  for (let at = 0; at < text.length; at++) {
    // A surrogate pair is one code point and two units, and `encoder` counts the
    // pair once — so step by the pair rather than by the unit.
    const code = text.codePointAt(at)
    if (code === undefined) return null
    const units = code > 0xffff ? 2 : 1
    seen += encoder.encode(String.fromCodePoint(code)).length
    if (seen > bytes) return at
    at += units - 1
    if (seen === bytes) return at + 1
  }

  return text.length
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
