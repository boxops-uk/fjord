import { useMemo, useState } from 'react'
import { Banner } from '@astryxdesign/core/Banner'
import { Button } from '@astryxdesign/core/Button'
import { ButtonGroup } from '@astryxdesign/core/ButtonGroup'
import { Card } from '@astryxdesign/core/Card'
import { HStack, VStack } from '@astryxdesign/core/Stack'
import { StatusDot } from '@astryxdesign/core/StatusDot'
import { Table, TableCell, TableHeaderCell, TableRow } from '@astryxdesign/core/Table'
import { Text } from '@astryxdesign/core/Text'
import { Toolbar } from '@astryxdesign/core/Toolbar'
import { Spinner } from '@astryxdesign/core/Spinner'
import { useEngine } from '../engine'
import type { FuzzyStep, FuzzyWalk } from '../wasm'

type Spec = { term: string; candidate: string; distance: number }

/** One worked example, driven by the engine's Levenshtein automaton. */
export function DfaDemo({ source }: { source: string }) {
  const { engine, failure } = useEngine(true)
  const [at, setAt] = useState(0)
  const spec = useMemo(() => readSpec(source), [source])
  const walk = useMemo(
    () => (engine && spec ? engine.fuzzy(spec.term, spec.candidate, spec.distance) : null),
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

      <Toolbar
        className="dfa-transport"
        label="Walk the candidate"
        size="sm"
        startContent={
          <ButtonGroup label="Move through the candidate">
            <Button
              label="|◀ start"
              variant="secondary"
              onClick={() => setAt(0)}
              isDisabled={here === 0}
            />
            <Button
              label="◀ previous"
              variant="secondary"
              onClick={() => setAt(here - 1)}
              isDisabled={here === 0}
            />
            <Button
              label="next ▶"
              variant="secondary"
              onClick={() => setAt(here + 1)}
              isDisabled={here === end}
            />
            <Button
              label="end ▶|"
              variant="secondary"
              onClick={() => setAt(end)}
              isDisabled={here === end}
            />
          </ButtonGroup>
        }
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
    return { term: value.term, candidate: value.candidate, distance: value.distance }
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
  if (step.at === 0)
    return `Before reading the candidate, the row counts how many letters would have to be deleted from “${walk.term}” to match an empty string.`

  const transition = `After reading “${step.input}”, the automaton is in the state for the prefix “${step.consumed}”.`
  if (step.accepts !== null)
    return `${transition} Its final cell is ${step.accepts}, so this prefix is already within the allowed distance of ${walk.distance}.`
  if (step.live)
    return `${transition} At least one cell is no greater than ${walk.distance}, so adding more characters could still produce a match.`
  return `${transition} Every cell is now beyond ${walk.distance}. No suffix can rescue it, so every stored key beginning with “${step.consumed}” can be skipped.`
}
