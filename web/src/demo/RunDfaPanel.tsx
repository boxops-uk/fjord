import { HStack, VStack } from '@astryxdesign/core/Stack'
import { Section } from '@astryxdesign/core/Section'
import { StatusDot } from '@astryxdesign/core/StatusDot'
import { Text } from '@astryxdesign/core/Text'
import type { Engine, FuzzyWalk, PlanView } from '../wasm'
import { DfaStateTable } from './DfaDemo'
import type { GuidedFrame } from './fuzzyTimeline'

/** The inner state machine, shown only for a query that has a fuzzy plan step. */
export function RunDfaPanel({
  engine,
  plan,
  frame,
}: {
  engine: Engine
  plan: PlanView
  frame: GuidedFrame
}) {
  const evaluation = frame.evaluation
  const fallback = plan.steps.flatMap((step) => step.fuzzy)[0]
  const walk = evaluation?.walk ?? (fallback ? engine.fuzzy(fallback.term, '', fallback.distance) : null)
  if (!walk) return null

  const dfaAt = frame.kind === 'dfa' ? frame.dfaAt : evaluation ? walk.steps.length - 1 : 0
  const step = walk.steps[dfaAt]
  const state = evaluation ? result(step) : 'idle until the executor reaches fuzzy matching'

  return (
    <Section
      className="run-dfa"
      padding={0}
      dividers={['top', 'bottom']}
      data-testid="run-dfa"
    >
      <VStack gap={0}>
        <HStack padding={3} gap={2} align="center">
          <StatusDot variant={variant(step, evaluation !== null)} label={state} />
          <VStack gap={1}>
            <Text type="label" weight="semibold">
              Inner machine · Levenshtein DFA
            </Text>
            <Text type="supporting">
              {frame.kind === 'dfa'
                ? `The executor is paused inside its fuzzy step. DFA transition ${dfaAt + 1} of ${walk.steps.length} has consumed “${step.consumed || '∅'}” from database candidate “${frame.evaluation.candidate}”.`
                : evaluation
                  ? `The DFA has returned ${result(step)} to executor transition ${frame.machineAt + 1}.`
                  : `The DFA is idle. It will start at this row when the outer executor reaches “${fallback.term}”~${fallback.distance}.`}
            </Text>
          </VStack>
        </HStack>
        <DfaStateTable walk={walk} steps={walk.steps.slice(0, dfaAt + 1)} active={dfaAt} />
      </VStack>
    </Section>
  )
}

/** Full-sentence narration while playback is inside a DFA transition. */
export function DfaStepDescription({ frame }: { frame: Extract<GuidedFrame, { kind: 'dfa' }> }) {
  const walk = frame.evaluation.walk
  const step = walk.steps[frame.dfaAt]
  const detail =
    step.at === 0
      ? `The inner machine starts before reading “${frame.evaluation.candidate}”. Its row contains the deletion cost from an empty candidate to every prefix of “${walk.term}”.`
      : `It consumes “${step.input}” and moves to the one DFA state for prefix “${step.consumed}”. ${explain(step, walk.distance)}`

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
          DFA transition {frame.dfaAt + 1} of {walk.steps.length} · inside executor transition{' '}
          {frame.evaluation.outcomeAt + 1}
        </Text>
        <Text type="label" weight="semibold">
          The executor is paused inside fuzzy matching.
        </Text>
        <Text>{detail}</Text>
      </VStack>
    </Section>
  )
}

function explain(step: FuzzyWalk['steps'][number], distance: number): string {
  if (step.accepts !== null)
    return `The final cell is ${step.accepts}, so the candidate is accepted within the limit of ${distance}.`
  if (step.live)
    return `At least one cell remains within ${distance}, so another character could still reach a match.`
  return `Every cell is beyond ${distance}; the state is dead and no suffix can rescue this candidate.`
}

function result(step: { live: boolean; accepts: number | null }): string {
  if (step.accepts !== null) return `accepted at distance ${step.accepts}`
  return step.live ? 'not accepted yet, but still live' : 'dead, so the candidate is rejected'
}

function variant(
  step: { live: boolean; accepts: number | null },
  evaluated: boolean,
): 'success' | 'accent' | 'error' {
  if (!evaluated || (step.accepts === null && step.live)) return 'accent'
  return step.accepts === null ? 'error' : 'success'
}
