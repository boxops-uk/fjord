import { Section } from '@astryxdesign/core/Section'
import { Text } from '@astryxdesign/core/Text'
import { VStack } from '@astryxdesign/core/Stack'
import type { Moment } from './run'
import type { PlanView, RegisterView, StepView, Trace, TraceStep } from './wasm'

/** Full-sentence narration of the executor transition the transport is on. */
export function StepDescription({
  trace,
  plan,
  at,
  moment,
}: {
  trace: Trace
  plan: PlanView
  at: number
  moment: Moment
}) {
  const here = Math.min(at, trace.steps.length - 1)
  const narration = describe(trace, plan, here, moment)

  return (
    <Section
      variant="muted"
      padding={3}
      dividers={['top', 'bottom']}
      data-testid="step-description"
      aria-live="polite"
      aria-atomic="true"
    >
      <VStack gap={1}>
        <Text type="supporting" color="secondary" hasTabularNumbers>
          Executor transition {here + 1} of {trace.steps.length}
        </Text>
        <Text type="label" weight="semibold">
          {narration.title}
        </Text>
        <Text>{narration.description}</Text>
      </VStack>
    </Section>
  )
}

type Narration = { title: string; description: string }

function describe(trace: Trace, plan: PlanView, here: number, moment: Moment): Narration {
  const transition = trace.steps[here]

  switch (transition.event) {
    case 'scan':
      return describeOpening(plan, transition)
    case 'reject':
      return describeRejection(plan, transition)
    case 'yield':
      return {
        title: 'The head yields one row.',
        description: `The head projects ${show(transition.row)} from the current bindings and hands that one row to the caller. Fjord does not materialise the rest of the result before returning it.`,
      }
    case 'done':
      return {
        title: 'The run is complete.',
        description: `The outermost level has no more candidates, so every nested loop is drained. The executor yielded ${moment.rows.length} ${plural(moment.rows.length, 'row')} after examining ${examined(transition)} stored ${plural(examined(transition), 'row')}.`,
      }
    default:
      return describeStep(plan, transition, trace.steps[here - 1] ?? null)
  }
}

function describeOpening(plan: PlanView, transition: TraceStep): Narration {
  const opening = transition.scanning
  const step = opening ? plan.steps[opening.step] : undefined
  const predicate = predicates(step)
  const access = step?.access[0]

  if (opening?.fetch) {
    return {
      title: `The executor fetches one ${predicate} fact.`,
      description: `An earlier register contains reference ${opening.fetch}, so the executor reads that fact directly. This is one point lookup rather than a walk through the predicate.`,
    }
  }

  if (access === 'seek') {
    return {
      title: `The executor opens a seek on ${predicate}.`,
      description: `The plan has encoded its constant and earlier bindings into this key range. The store can jump directly to matching candidates instead of walking unrelated rows.`,
    }
  }

  if (access === 'guided') {
    return {
      title: `The executor opens a guided seek on ${predicate}.`,
      description: `The fuzzy matcher opens only a key range that can still satisfy its edit-distance limit. It can jump forward and reopen the range when the intervening keys are provably unable to match.`,
    }
  }

  if (access === 'absent') {
    return {
      title: `The executor probes ${predicate} for a witness.`,
      description: `This is an absence test, so it needs to know only whether one matching row exists. The probe stops at its first witness and binds no register.`,
    }
  }

  return {
    title: `The executor opens a scan on ${predicate}.`,
    description: `No leading key field is fixed at this level, so the store starts at the beginning of the key range for ${predicate}. It walks candidate rows in stored order.`,
  }
}

function describeRejection(plan: PlanView, transition: TraceStep): Narration {
  const rejection = transition.rejected
  const fact = rejection?.row.fact ?? 'this candidate row'
  const filter = rejection
    ? residualOf(plan, rejection.step, rejection.residual)
    : 'the residual filter'

  return {
    title: `The executor drops ${fact}.`,
    description: `The source read this row, but ${filter} did not hold. The candidate counts as examined and is discarded before it can become an answer.`,
  }
}

function describeStep(
  plan: PlanView,
  transition: TraceStep,
  previous: TraceStep | null,
): Narration {
  const written = transition.registers.filter((register) => register.kind !== 'empty')
  const cleared = transition.registers.filter((register) => register.kind === 'empty')

  if (written.length === 1) return describeWrite(plan, transition, written[0])

  if (written.length > 1) {
    const registers = written.map((register) => `r${register.address}`).join(', ')
    return {
      title: `The executor fills ${registers}.`,
      description: `This plan step binds the rows it found to ${registers} and advances to the next step. Each fact register keeps the whole stored row so fields can be decoded only if a later operation reads them.`,
    }
  }

  if (cleared.length > 0) {
    const registers = cleared.map((register) => `r${register.address}`).join(', ')
    return {
      title: `The executor clears ${registers}.`,
      description: `Those bindings belonged to work the machine has now left. It backtracks to plan step ${transition.depth + 1}, where an earlier level can look for another candidate.`,
    }
  }

  if (transition.depth >= plan.steps_count) {
    return {
      title: 'The head is ready.',
      description: `Every plan step has advanced successfully, so the register file contains a complete set of bindings. The next transition can project the query's head from those registers.`,
    }
  }

  const step = plan.steps[transition.depth]
  if (previous && transition.depth < previous.depth) {
    return {
      title: `The executor backtracks to plan step ${transition.depth + 1}.`,
      description: `The deeper work has no next row, so the machine returns to ${stepName(step)}. That earlier step can now produce another candidate without rebuilding the whole run.`,
    }
  }

  if (step?.kind === 'Test') {
    return {
      title: 'The test passes.',
      description: `The test binds no register; it only decides whether the current bindings survive. Because it passed, the executor advances to the next plan step.`,
    }
  }

  return {
    title: `The executor advances to ${stepName(step)}.`,
    description: `The previous transition completed without changing a register. The machine keeps its current bindings and continues through the ordered plan.`,
  }
}

function describeWrite(plan: PlanView, transition: TraceStep, register: RegisterView): Narration {
  const address = `r${register.address}`
  const next =
    transition.depth >= plan.steps_count
      ? ' All plan steps now have a binding, so the head is ready to project an answer.'
      : ` The machine advances to ${stepName(plan.steps[transition.depth])}.`

  if (register.kind === 'fact') {
    return {
      title: `The executor binds ${address}.`,
      description: `The current level reads ${register.fact ?? 'a fact'} and binds the whole stored row to ${address}. Merely holding the row does not decode all of its fields.${next}`,
    }
  }

  return {
    title: `The executor computes ${address}.`,
    description: `A derive step computes ${show(register.value)} and stores that value in ${address}. Derived values are recomputed after resume rather than stored in the cursor.${next}`,
  }
}

function residualOf(plan: PlanView, step: number, residual: number): string {
  const lines = plan.steps[step]?.text.split('\n').filter((line) => line.includes('where')) ?? []
  return lines[residual]?.trim().replace(/^where\s+/, '') ?? `residual ${residual + 1}`
}

function predicates(step: StepView | undefined): string {
  if (!step || step.predicates.length === 0) return 'the current predicate'
  return step.predicates.join(' or ')
}

function stepName(step: StepView | undefined): string {
  if (!step) return 'the next plan step'
  const predicate = predicates(step)
  return step.predicates.length > 0
    ? `the ${step.kind.toLowerCase()} over ${predicate}`
    : `the ${step.kind.toLowerCase()} step`
}

function examined(transition: TraceStep): number {
  return transition.examined.reduce((total, count) => total + count, 0)
}

function plural(count: number, noun: string): string {
  return count === 1 ? noun : `${noun}s`
}

function show(value: unknown): string {
  return typeof value === 'string' ? `“${value}”` : JSON.stringify(value) ?? 'the projected value'
}
