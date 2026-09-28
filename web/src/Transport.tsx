import { Slider } from '@astryxdesign/core/Slider'
import { Text } from '@astryxdesign/core/Text'
import { HStack } from '@astryxdesign/core/Stack'
import { Stepper } from './Stepper'
import type { Trace } from './wasm'

/**
 * **The controls that move a run**, in the vocabulary of the debugger this is.
 *
 * Restart, step back over, step back, step, step over, continue and run to end
 * — a reader who has stepped a program in an editor already knows every one of
 * them, and what they mean here is what they mean there. A row is this
 * machine's unit of work, so the transitions that build one are what `step
 * over` steps over.
 *
 * Two traps, both about a hand and a timer wanting the play head at once. Any
 * navigation stops the run, or a reader who steps back watches the machine step
 * forward over them. And the controls are square icons, so the one being
 * clicked is the one under the pointer whatever it says.
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
    <Stepper
      className="transport"
      label="Run"
      controls={[
        {
          mark: 'restart',
          name: 'restart',
          key: 'Home',
          hint: 'back to the first transition',
          onClick: () => seek(0),
          isDisabled: here === 0,
        },
        {
          mark: 'backOver',
          name: 'step back over',
          key: 'shift+ArrowLeft',
          hint: 'back to the previous row',
          onClick: () => seek(previousYield),
          isDisabled: previousYield < 0,
        },
        {
          mark: 'back',
          name: 'step back',
          key: 'ArrowLeft',
          hint: 'one transition',
          onClick: () => seek(here - 1),
          isDisabled: here === 0,
        },
        {
          mark: 'step',
          name: 'step',
          key: 'ArrowRight',
          hint: 'one transition',
          onClick: () => seek(here + 1),
          isDisabled: here >= end,
        },
        {
          // **The debugger's own word for it.** A row is what this machine
          // returns, and the transitions that build one are what a reader
          // stepping through them is usually not asking about — which is the
          // whole of what `step over` means anywhere else.
          mark: 'over',
          name: 'step over',
          key: 'shift+ArrowRight',
          hint: 'on to the next row',
          onClick: () => seek(nextYield),
          isDisabled: nextYield < 0,
        },
        {
          mark: playback.playing ? 'pause' : 'play',
          name: playback.playing ? 'pause' : 'continue',
          key: ' ',
          hint: playback.playing
            ? 'stop where it is'
            : here >= end
              ? 'again, from the first transition'
              : 'until something stops it',
          isOn: playback.playing,
          onClick: () => {
            if (playback.playing) return playback.setPlaying(false)
            // Continue from the end is continue from the start: there is
            // nowhere else for it to mean.
            if (here >= end) onSeek(0)
            playback.setPlaying(true)
          },
        },
        {
          mark: 'end',
          name: 'run to end',
          key: 'End',
          hint: 'the last transition, without playing it',
          onClick: () => seek(end),
          isDisabled: here >= end,
        },
      ]}
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
