import { useMemo, useState } from 'react'
import { Banner } from '@astryxdesign/core/Banner'
import { Card } from '@astryxdesign/core/Card'
import { HStack, VStack } from '@astryxdesign/core/Stack'
import { StatusDot } from '@astryxdesign/core/StatusDot'
import { Table, TableCell, TableHeaderCell, TableRow } from '@astryxdesign/core/Table'
import { Stepper } from '../Stepper'
import { Text } from '@astryxdesign/core/Text'
import { Toolbar } from '@astryxdesign/core/Toolbar'
import { Spinner } from '@astryxdesign/core/Spinner'
import { useEngine } from '../engine'
import type { FuzzyStep, FuzzyWalk } from '../wasm'

type Spec = { term: string; candidate: string; distance: number; anchored: boolean }

/** One worked example, driven by the engine's Levenshtein automaton. */
export function DfaDemo({ source }: { source: string }) {
  const { engine, failure } = useEngine(true)
  const [at, setAt] = useState(0)
  const spec = useMemo(() => readSpec(source), [source])
  const walk = useMemo(
    () =>
      engine && spec
        ? engine.fuzzy(spec.term, spec.candidate, spec.distance, spec.anchored)
        : null,
    [engine, spec],
  )

  if (failure)
    return <Banner status="error" title="the engine did not load" description={failure} />

  if (!engine)
    return (
      <HStack gap={2} padding={3} align="center">
        <Spinner size="sm" />
        <Text type="supporting">loading the automaton…</Text>
      </HStack>
    )

  if (!spec || !walk)
    return (
      <Banner
        status="error"
        title="this DFA example is not valid"
        description="A worked example needs a term, a candidate, and an edit distance from 0 to 3."
      />
    )

  const here = Math.min(at, walk.steps.length - 1)
  const step = walk.steps[here]
  const end = walk.steps.length - 1

  return (
    <Card padding={0} className="demo demo-dfa">
      <Toolbar
        label="DFA worked example"
        size="sm"
        variant="muted"
        startContent={
          <Text type="label" color="secondary">
            the real fuzzy matcher, one character at a time
          </Text>
        }
        endContent={
          <Text type="code">
            “{walk.candidate}” against “{walk.term}”~{walk.distance}
          </Text>
        }
      />

      {/* **The same bar as the executor's**, because it is the same act. This
          machine has no rows and no timer, so it shows the four controls that
          mean something here — and it shows them in the words and the shapes
          the transport uses, rather than the second vocabulary it had grown. */}
      <Stepper
        className="dfa-transport"
        label="Walk the candidate"
        controls={[
          {
            mark: 'restart',
            name: 'restart',
            key: 'Home',
            hint: 'back to the empty candidate',
            onClick: () => setAt(0),
            isDisabled: here === 0,
          },
          {
            mark: 'back',
            name: 'step back',
            key: 'ArrowLeft',
            hint: 'give back one character',
            onClick: () => setAt(here - 1),
            isDisabled: here === 0,
          },
          {
            mark: 'step',
            name: 'step',
            key: 'ArrowRight',
            hint: 'read one character',
            onClick: () => setAt(here + 1),
            isDisabled: here === end,
          },
          {
            mark: 'end',
            name: 'run to end',
            key: 'End',
            hint: 'read the rest of the candidate',
            onClick: () => setAt(end),
            isDisabled: here === end,
          },
        ]}
        endContent={
          <Text type="supporting" hasTabularNumbers data-testid="dfa-count">
            state {here + 1}/{walk.steps.length}
          </Text>
        }
      />

      <DfaStateTable walk={walk} steps={[step]} />

      <VStack gap={2} padding={3} className="dfa-description" data-testid="dfa-description">
        <HStack gap={2} align="center">
          <StatusDot variant={status(step).variant} label={status(step).label} />
          <Text type="label">{status(step).label}</Text>
        </HStack>
        <Text>{description(walk, step)}</Text>
        <Text type="supporting">
          Each column compares the consumed candidate with the term prefix above it. A value of{' '}
          {walk.cap} means “{walk.cap} or more”; the automaton does not need a larger number.
        </Text>
      </VStack>
    </Card>
  )
}

/** The automaton's state table, shared by isolated and database-backed demos. */
export function DfaStateTable({
  walk,
  steps = walk.steps,
  active,
}: {
  walk: FuzzyWalk
  steps?: FuzzyStep[]
  active?: number
}) {
  return (
    <Table density="compact" dividers="grid" textOverflow="wrap">
      <TableRow isHeaderRow>
        <TableHeaderCell>candidate prefix</TableHeaderCell>
        {walk.columns.map((column) => (
          <TableHeaderCell key={column}>{column}</TableHeaderCell>
        ))}
      </TableRow>
      {steps.map((step) => (
        <TableRow key={step.at} className={step.at === active ? 'dfa-active' : undefined}>
          <TableCell>
            <Text type="code">{step.consumed || '∅'}</Text>
          </TableCell>
          {step.row.map((cell, index) => (
            <TableCell key={walk.columns[index]}>
              <Text
                type="code"
                color={cell <= walk.distance ? 'accent' : 'secondary'}
                hasTabularNumbers
              >
                {cell}
              </Text>
            </TableCell>
          ))}
        </TableRow>
      ))}
    </Table>
  )
}

function readSpec(source: string): Spec | null {
  try {
    const value = JSON.parse(source) as Partial<Spec>
    if (
      typeof value.term !== 'string' ||
      typeof value.candidate !== 'string' ||
      !Number.isInteger(value.distance) ||
      value.distance === undefined ||
      value.distance < 0 ||
      value.distance > 3
    )
      return null
    return {
      term: value.term,
      candidate: value.candidate,
      distance: value.distance,
      // Absent means the whole-string question, so every example written before
      // `~<` existed keeps meaning what it meant.
      anchored: value.anchored === true,
    }
  } catch {
    return null
  }
}

function status(step: FuzzyStep): {
  variant: 'success' | 'accent' | 'error'
  label: string
} {
  if (step.accepts !== null)
    return { variant: 'success', label: `accepted at distance ${step.accepts}` }
  if (step.live) return { variant: 'accent', label: 'still able to match' }
  return { variant: 'error', label: 'dead: no continuation can match' }
}

function description(walk: FuzzyWalk, step: FuzzyStep): string {
  if (step.at === 0) {
    const empty = `Before reading the candidate, the row counts how many letters would have to be deleted from “${walk.term}” to match an empty string.`
    return walk.anchored && step.accepts !== null
      ? `${empty} That is already within ${walk.distance}, so the empty prefix matches — and every stored string starts with it.`
      : empty
  }

  const transition = `After reading “${step.input}”, the automaton is in the state for the prefix “${step.consumed}”.`
  if (step.accepts !== null)
    return walk.anchored
      ? `${transition} Its final cell is ${step.accepts}, within the allowed distance of ${walk.distance} — so “${step.consumed}” matches, and so does every stored key that begins with it. The walk stops here; the rest of the key cannot change the answer.`
      : `${transition} Its final cell is ${step.accepts}, so this prefix is already within the allowed distance of ${walk.distance}.`
  if (step.live)
    return `${transition} At least one cell is no greater than ${walk.distance}, so adding more characters could still produce a match.`
  return `${transition} Every cell is now beyond ${walk.distance}. No suffix can rescue it, so every stored key beginning with “${step.consumed}” can be skipped.`
}
