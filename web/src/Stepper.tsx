/**
 * **The controls that move a machine, in a debugger's vocabulary.**
 *
 * Three of these had grown independently — the executor's transport, the
 * isolated automaton's own bar, and the hero's play button — with three sets of
 * words for the same six ideas and glyphs that were text in one and absent in
 * another. They are one component now, so the language cannot drift again.
 *
 * **The words are a debugger's because the thing is a debugger.** A reader
 * stepping a query has done this before, in an editor, over a program: `restart`
 * goes to the top, `step` advances one transition, `step over` runs the
 * transitions inside a row and stops at the next one, `continue` runs until
 * something stops it, and `run to end` does not stop. Borrowing those five
 * names costs nothing and means the control bar explains itself to anybody who
 * has ever set a breakpoint.
 *
 * **The marks are drawn rather than named.** The icon registry covers navigation
 * and status; a transport is neither, and the two that matter most — step and
 * step over — are the shapes every debugger draws and no icon set carries. Step
 * is an arrow landing *on* the line it will execute. Step over arcs above it.
 * Once those two exist the rest have to match, or the row is half one language.
 */
import { useEffect, useRef, type ReactNode } from 'react'
import { Button } from '@astryxdesign/core/Button'
import { ButtonGroup } from '@astryxdesign/core/ButtonGroup'
import { Toolbar } from '@astryxdesign/core/Toolbar'

/** The shape of a control, which is also what it is called. */
export type Mark = 'restart' | 'back' | 'step' | 'backOver' | 'over' | 'play' | 'pause' | 'end'

const paint = { fill: 'none', stroke: 'currentColor', strokeWidth: 1.6, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const }

/** A circular arrow: back to the top and ready to go again. */
function Restart() {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true">
      <path d="M13 8a5 5 0 1 1-1.6-3.7" {...paint} />
      <path d="M13.2 2.6v2.6h-2.6" {...paint} />
    </svg>
  )
}

/**
 * **Step**: an arrow coming down onto the line it is about to run.
 *
 * Backwards is the same arrow reversed *vertically* — it leaves the line going
 * up. Mirroring it horizontally, which is what the arc beside it wants, leaves
 * this one identical to itself: the shape is symmetric about the axis the flip
 * is around, so the two controls came out indistinguishable.
 */
function Step({ back = false }: { back?: boolean }) {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true" style={back ? { transform: 'scaleY(-1)' } : undefined}>
      <path d="M8 2v7" {...paint} />
      <path d="M5.4 6.6 8 9.2l2.6-2.6" {...paint} />
      <circle cx="8" cy="12.6" r="1.5" fill="currentColor" stroke="none" />
    </svg>
  )
}

/** **Step over**: the same arrow, arcing above the line instead of landing on it. */
function Over({ back = false }: { back?: boolean }) {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true" style={back ? { transform: 'scaleX(-1)' } : undefined}>
      <path d="M3.2 10.4a5.4 5.4 0 0 1 9.6 0" {...paint} />
      <path d="M10.2 10.8h2.8V8" {...paint} />
      <circle cx="8" cy="13" r="1.5" fill="currentColor" stroke="none" />
    </svg>
  )
}

/** Continue, and the same button once it is running. */
function Play() {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true">
      <path d="M5 3.4v9.2a.6.6 0 0 0 .92.5l7.2-4.6a.6.6 0 0 0 0-1l-7.2-4.6a.6.6 0 0 0-.92.5Z" fill="currentColor" />
    </svg>
  )
}

function Pause() {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true" fill="currentColor">
      <rect x="4" y="3" width="3.2" height="10" rx="0.8" />
      <rect x="8.8" y="3" width="3.2" height="10" rx="0.8" />
    </svg>
  )
}

/** Run to the end, which is continue with nothing left to stop it. */
function ToEnd() {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true">
      <path d="M3.4 3.4v9.2a.6.6 0 0 0 .92.5l5.6-4.6a.6.6 0 0 0 0-1L4.32 2.9a.6.6 0 0 0-.92.5Z" fill="currentColor" />
      <rect x="11.4" y="3" width="1.8" height="10" rx="0.7" fill="currentColor" />
    </svg>
  )
}

const MARKS: Record<Mark, ReactNode> = {
  restart: <Restart />,
  back: <Step back />,
  step: <Step />,
  backOver: <Over back />,
  over: <Over />,
  play: <Play />,
  pause: <Pause />,
  end: <ToEnd />,
}

/**
 * One of the marks on its own, for a control this bar does not own.
 *
 * The hero's loop has its own two buttons and no stepper around them, and a
 * third drawing of a play triangle is how a site ends up with three of them.
 */
export function Mark({ of }: { of: Mark }) {
  return <>{MARKS[of]}</>
}

/** One control: what it looks like, what it is called, and what it does. */
export type Control = {
  mark: Mark
  /** The key that does it, while the machine this bar moves has focus. */
  key?: string
  /** The accessible name, and the tooltip. A debugger's word for the action. */
  name: string
  /** Extra words a tooltip adds, where the action needs saying in this machine. */
  hint?: string
  onClick: () => void
  isDisabled?: boolean
  /** `play` lights up while it is running; nothing else does. */
  isOn?: boolean
}

/**
 * The bar itself.
 *
 * Every control is icon-only and therefore square, which is what stops a label
 * changing under the pointer and is what makes the row fit a phone. Where it
 * still does not fit, the bar scrolls rather than drawing a control outside
 * itself — `app.css` owns that, because it is the same rule for every bar.
 */
export function Stepper({
  label,
  controls,
  endContent,
  className,
}: {
  label: string
  controls: Control[]
  endContent?: ReactNode
  className?: string
}) {
  const bar = useRef<HTMLDivElement>(null)

  /**
   * **The keys a debugger has.** Stepping with a pointer means finding a 28px
   * target between each look at the machine, and the whole act is *look, step,
   * look* — which is why every debugger binds these and why a reader who has
   * used one will try them here.
   *
   * Bound on the panel this bar belongs to rather than on the window: a page
   * carries several of these, arrow keys already mean something to a slider and
   * a text box, and a key that moved a machine a reader was not looking at
   * would be worse than no key at all. The listener asks for the nearest panel,
   * so a demo is armed exactly while something inside it has focus.
   */
  useEffect(() => {
    const panel = bar.current?.closest('.demo, .run, .playground') ?? bar.current?.parentElement
    if (!panel) return

    const onKey = (event: Event) => {
      const press = event as KeyboardEvent
      if (press.metaKey || press.ctrlKey || press.altKey) return
      // A key typed into the query editor is a character, not a command.
      const target = press.target as HTMLElement | null
      if (target?.closest('input, textarea, [contenteditable="true"]')) return

      const wanted = press.shiftKey ? `shift+${press.key}` : press.key
      const control = controls.find((control) => control.key === wanted)
      if (!control || control.isDisabled) return
      press.preventDefault()
      control.onClick()
    }

    panel.addEventListener('keydown', onKey)
    return () => panel.removeEventListener('keydown', onKey)
  }, [controls])

  return (
    <Toolbar
      ref={bar}
      className={['stepper', className].filter(Boolean).join(' ')}
      label={label}
      size="sm"
      variant="muted"
      startContent={
        <ButtonGroup label={label}>
          {controls.map((control) => (
            <Button
              key={control.name}
              variant={control.isOn ? 'primary' : 'secondary'}
              isIconOnly
              icon={MARKS[control.mark]}
              label={control.name}
              tooltip={[
                control.name,
                control.hint ? `— ${control.hint}` : '',
                control.key ? `(${said(control.key)})` : '',
              ]
                .filter(Boolean)
                .join(' ')}
              onClick={control.onClick}
              isDisabled={control.isDisabled}
            />
          ))}
        </ButtonGroup>
      }
      endContent={endContent}
    />
  )
}

/** A key as a reader would say it, for a tooltip. */
function said(key: string): string {
  return key
    .replace('shift+', 'Shift ')
    .replace('ArrowRight', '→')
    .replace('ArrowLeft', '←')
    .replace(' ', 'Space')
}
