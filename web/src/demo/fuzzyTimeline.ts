import { fold, type Moment } from '../run'
import type {
  Engine,
  FuzzyPlan,
  FuzzyWalk,
  PlanView,
  RegisterView,
  Trace,
  TraceStep,
} from '../wasm'

export type FuzzyEvaluation = {
  pattern: FuzzyPlan
  candidate: string
  key: string | null
  walk: FuzzyWalk
  planStep: number
  outcomeAt: number
}

export type GuidedFrame =
  | { kind: 'machine'; machineAt: number; evaluation: FuzzyEvaluation | null }
  | {
      kind: 'dfa'
      machineAt: number
      dfaAt: number
      evaluation: FuzzyEvaluation
    }

export type GuidedTimeline = {
  frames: GuidedFrame[]
  events: string[]
  labels: string[]
}

/** Expand each fuzzy evaluation inside the executor transition that owns it. */
export function fuzzyTimeline(engine: Engine, plan: PlanView, trace: Trace): GuidedTimeline {
  const frames: GuidedFrame[] = []

  for (const transition of trace.steps) {
    const evaluation = evaluationAt(engine, plan, transition)
    if (evaluation) {
      for (const dfaStep of evaluation.walk.steps) {
        frames.push({
          kind: 'dfa',
          machineAt: Math.max(transition.at - 1, 0),
          dfaAt: dfaStep.at,
          evaluation,
        })
      }
    }
    frames.push({ kind: 'machine', machineAt: transition.at, evaluation })
  }

  return {
    frames,
    events: frames.map((frame) =>
      frame.kind === 'machine' ? trace.steps[frame.machineAt].event : 'dfa',
    ),
    labels: frames.map((frame) =>
      frame.kind === 'machine'
        ? `machine ${frame.machineAt + 1}/${trace.steps.length}`
        : `DFA ${frame.dfaAt + 1}/${frame.evaluation.walk.steps.length} · inside machine ${frame.evaluation.outcomeAt + 1}`,
    ),
  }
}

/** Fold the outer machine only as far as it had moved when this frame occurred. */
export function momentAt(trace: Trace, frame: GuidedFrame): Moment {
  const moment = fold(trace, frame.machineAt)
  return frame.kind === 'dfa'
    ? { ...moment, testing: frame.evaluation.key }
    : moment
}

function evaluationAt(
  engine: Engine,
  plan: PlanView,
  transition: TraceStep,
): FuzzyEvaluation | null {
  const patterns = plan.steps.flatMap((step) =>
    step.fuzzy.map((pattern) => ({ step, pattern })),
  )

  if (transition.rejected) {
    const found = patterns.find(
      ({ step, pattern }) =>
        step.index === transition.rejected?.step &&
        pattern.residual === transition.rejected?.residual,
    )
    if (found)
      return makeEvaluation(engine, found.step.index, found.pattern, transition.rejected.row, transition.at)
  }

  for (const register of transition.registers) {
    if (register.kind !== 'fact') continue
    const found = patterns.find(({ step }) => step.register === `r${register.address}`)
    if (found) return makeEvaluation(engine, found.step.index, found.pattern, register, transition.at)
  }

  return null
}

function makeEvaluation(
  engine: Engine,
  planStep: number,
  pattern: FuzzyPlan,
  register: RegisterView,
  outcomeAt: number,
): FuzzyEvaluation | null {
  const text = candidate(register, pattern)
  if (text === null) return null
  const walk = engine.fuzzy(pattern.term, text, pattern.distance)
  return walk
    ? { pattern, candidate: text, key: register.key, walk, planStep, outcomeAt }
    : null
}

function candidate(register: RegisterView, pattern: FuzzyPlan): string | null {
  let value = register.value
  for (const key of pattern.path) {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return null
    value = (value as Record<string, unknown>)[key]
  }
  return typeof value === 'string' ? value : null
}
