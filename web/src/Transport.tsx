import { Toolbar } from '@astryxdesign/core/Toolbar'
import { Button } from '@astryxdesign/core/Button'
import { Icon } from '@astryxdesign/core/Icon'
import { ButtonGroup } from '@astryxdesign/core/ButtonGroup'
import { Slider } from '@astryxdesign/core/Slider'
import { Text } from '@astryxdesign/core/Text'
import { HStack } from '@astryxdesign/core/Stack'
import type { Trace } from './wasm'

/**
 * **Play and pause, as shapes rather than words.**
 *
 * The one control whose label changed as you used it, so it changed width as
 * you used it — and the button that moves out from under the pointer is the one
 * being clicked. A square icon is fixed at both states, reads at a glance, and
 * gives the row back the width the word `play` was holding.
 *
 * Drawn here rather than named, because the registry has no `play`: the theme's
 * semantic names cover navigation and status, and a transport is neither. An
 * SVG component is what `Icon` takes for exactly this.
 */
function PlayMark() {
  return (
    <svg viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M5 3.4v9.2a.6.6 0 0 0 .92.5l7.2-4.6a.6.6 0 0 0 0-1l-7.2-4.6a.6.6 0 0 0-.92.5Z" />
    </svg>
  )
}

function PauseMark() {
  return (
    <svg viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <rect x="4" y="3" width="3.2" height="10" rx="0.8" />
      <rect x="8.8" y="3" width="3.2" height="10" rx="0.8" />
    </svg>
  )
}

/**
 * **The controls that move a run** — start, a row back, a transition either
 * way, on to the next row, play, the end, and a scrub bar over the whole trace.
 *
 * Two traps, both about a hand and a timer wanting the play head at once. Any
 * navigation stops the run, or a reader who steps back watches the machine step
 * forward over them. And the buttons keep their size as they change state: the
 * one being clicked is the one under the pointer.
 */
export function Transport({
  trace,
  at,
  onSeek,
  playback,
  sequence,
}: {
  trace: Trace
  at: number
  onSeek: (at: number) => void
  playback: { playing: boolean; setPlaying: (playing: boolean) => void }
  /** A nested playback sequence, whose `dfa` entries sit inside machine steps. */
  sequence?: { events: string[]; labels: string[] }
}) {
  const events = sequence?.events ?? trace.steps.map((step) => step.event)
  const here = Math.min(at, events.length - 1)
  const end = events.length - 1
  const seek = (to: number) => {
    playback.setPlaying(false)
    onSeek(to)
  }
  const nextYield = events.findIndex((event, index) => index > here && event === 'yield')
  const previousYield = findLast(events, here - 1, (event) => event === 'yield')

  return (
    <Toolbar
      className="transport"
      label="Run"
      size="sm"
      variant="muted"
      startContent={
        // **Words where a word says it, icons where a shape does.** The
        // original rule was that ⏮ and ⏩ are not in every monospace font and a
        // control rendering as a box is worse than a word — which is about
        // *text* glyphs. An SVG has no font to be missing from, so the controls
        // whose meaning is a direction are chevrons now: one for a transition,
        // one either side of the word `row` for stepping over to the next one,
        // and a doubled pair for the ends. That leaves exactly one triangle in
        // the row and it is the play mark — which is what makes it findable.
        // It is also what makes the row fit a phone rather than running half of
        // itself off the edge.
        <ButtonGroup label="Move the run">
          <Button
            variant="secondary"
            isIconOnly
            icon={<Icon icon="chevronsLeft" />}
            label="to the start"
            tooltip="to the start"
            onClick={() => seek(0)}
            isDisabled={here === 0}
          />
          <Button
            variant="secondary"
            icon={<Icon icon="chevronLeft" />}
            // The word is what a reader sees; the sentence is what a screen
            // reader says. Both buttons show `row`, so `label` alone would
            // announce the same name twice with nothing to tell them apart.
            label="back to the previous row"
            tooltip="back to the previous row"
            onClick={() => seek(previousYield)}
            isDisabled={previousYield < 0}
          >
            row
          </Button>
          {/* **Chevrons step, a triangle plays.** These were bare `◀` and `▶`,
              which put a second solid triangle two controls from the play mark
              and made the pair impossible to tell apart at a glance. A chevron
              is the registry's own word for moving one along; the triangle now
              belongs to the transport alone. */}
          <Button
            variant="secondary"
            isIconOnly
            icon={<Icon icon="chevronLeft" />}
            label="back one transition"
            tooltip="back one transition"
            onClick={() => seek(here - 1)}
            isDisabled={here === 0}
          />
          <Button
            variant="secondary"
            isIconOnly
            icon={<Icon icon="chevronRight" />}
            label="one transition"
            tooltip="one transition"
            onClick={() => seek(here + 1)}
            isDisabled={here >= end}
          />
          <Button
            variant="secondary"
            endContent={<Icon icon="chevronRight" />}
            label="on to the next row"
            tooltip="on to the next row — step over"
            onClick={() => seek(nextYield)}
            isDisabled={nextYield < 0}
          >
            row
          </Button>
          <Button
            variant={playback.playing ? 'primary' : 'secondary'}
            isIconOnly
            icon={playback.playing ? <PauseMark /> : <PlayMark />}
            label={playback.playing ? 'pause' : 'play'}
            tooltip={
              playback.playing ? 'pause' : here >= end ? 'play again from the start' : 'play'
            }
            onClick={() => {
              if (playback.playing) return playback.setPlaying(false)
              // Play from the end is play from the start: there is nowhere else
              // for it to mean.
              if (here >= end) onSeek(0)
              playback.setPlaying(true)
            }}
          />
          <Button
            variant="secondary"
            isIconOnly
            icon={<Icon icon="chevronsRight" />}
            label="to the end"
            tooltip="to the end"
            onClick={() => seek(end)}
            isDisabled={here >= end}
          />
        </ButtonGroup>
      }
      endContent={
        <HStack gap={3} align="center">
          <Slider
            label="step"
            isLabelHidden
            width={200}
            value={here}
            min={0}
            max={Math.max(end, 1)}
            onChange={((value: number) => seek(value)) as (value: number) => void}
            valueDisplay="none"
          />
          <Text className="count" type="supporting" hasTabularNumbers>
            {sequence?.labels[here] ?? `${here + 1}/${trace.steps.length}`}
          </Text>
        </HStack>
      }
    />
  )
}

function findLast<T>(items: T[], from: number, matches: (item: T) => boolean): number {
  for (let i = Math.min(from, items.length - 1); i >= 0; i--) if (matches(items[i])) return i
  return -1
}
