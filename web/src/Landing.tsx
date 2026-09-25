/**
 * **The landing page: what Fjord is, before the book explains it.**
 *
 * The root used to be the book's Overview, which is a docs page and reads like
 * one — it opens on predicates and keys because that is what the page after it
 * needs. Somebody arriving from a link has not agreed to that yet. So this says
 * what the thing is, shows it working, is honest about what it is not, and gets
 * out of the way.
 *
 * It is a component rather than an MDX page on purpose. Every page in
 * `src/content/` is prose the book's reading order owns, and this is neither.
 */
import { Button } from '@astryxdesign/core/Button'
import { Heading } from '@astryxdesign/core/Heading'
import { Link } from '@astryxdesign/core/Link'
import { Text } from '@astryxdesign/core/Text'
import { route } from './book/links'
import { HeroStory } from './HeroStory'
import { SearchDemo } from './SearchDemo'
import './landing.css'

const REPO = 'https://github.com/boxops-uk/fjord'

export function Landing() {
  return (
    <main className="landing" data-testid="landing">
      <section className="hero">
        <Heading level={1} type="display-1">
          A database of facts about your code
        </Heading>
        <Text as="p" size="lg" color="secondary">
          Fjord indexes a repository once, seals the result, and answers questions about it in a
          small typed query language. What you get is a directory: copy it, ship it, serve it
          from as many processes as you like.
        </Text>
        <div className="hero-actions">
          <Button variant="primary" label="Get started" href={route('getting-started')} />
          <Button variant="secondary" label="Read the docs" href={route('overview')} />
          <Button variant="ghost" label="GitHub" href={REPO} />
        </div>
        <HeroStory />
        <Text as="p" size="sm" color="secondary" className="hero-foot">
          That is the real engine, compiled to WebAssembly and running in this page — the same
          compiler, the same executor, and the index it is reading is a real one.
        </Text>
      </section>

      <SearchDemo />

      <section className="band">
        <Heading level={2}>The idea, in three parts</Heading>
        <div className="three">
          <article>
            <h3>Facts, grouped by predicate</h3>
            <p>
              A predicate is Fjord&rsquo;s word for a table. You describe the shape of your facts
              once and write them in. Part of each fact is the <b>key</b>, which is indexed and is
              what queries match on, so designing a schema is mostly deciding what the keys are.
            </p>
          </article>
          <article>
            <h3>Built once, then frozen</h3>
            <p>
              Create a database, write facts, and seal it. From then on it is read-only. There is
              no locking and no version to reconcile, writers never conflict, and every build
              produces a fresh database the way it produces a fresh binary.
            </p>
          </article>
          <article>
            <h3>Queries that can pause</h3>
            <p>
              A query compiles to a small plan and runs one row at a time. It can stop part-way,
              hand back a few bytes, and resume from exactly there — so paging holds nothing open
              and a different process can serve the next page.
            </p>
          </article>
        </div>
      </section>

      <section className="band">
        <Heading level={2}>The docs run the database</Heading>
        <Text as="p" color="secondary">
          Every demo in this documentation is the engine itself, compiled to WebAssembly. Nothing
          on these pages is a recording or a reimplementation in JavaScript, so a page cannot
          quietly go out of date about what the compiler does.
        </Text>
        <Text as="p" color="secondary">
          The workbench shows all of it at once: the tokens, the parse tree, the inferred types,
          the plan the executor will run, and the machine stepping one transition at a time with
          the database beside it.
        </Text>
        <div className="hero-actions">
          <Button variant="secondary" label="Open the workbench" href={route('playground')} />
        </div>
      </section>

      <section className="band">
        <Heading level={2}>What it is good at, and what it is not</Heading>
        <div className="two">
          <article className="good">
            <h3>Good at</h3>
            <ul>
              <li>Code intelligence: definitions, references, the build graph.</li>
              <li>A fresh sealed index per build, published as a directory.</li>
              <li>Serving many readers from copies of one immutable artifact.</li>
              <li>Paging a large result without holding anything open.</li>
              <li>Producers in any language: the wire protocol is documented and has a second implementation.</li>
            </ul>
          </article>
          <article className="not">
            <h3>Not for</h3>
            <ul>
              <li>Updating rows in place. A database is sealed and never changes.</li>
              <li>Transactions, or reading your own writes across a seal.</li>
              <li>Aggregation and recursion. Neither is in the language yet.</li>
              <li>Access control. The transport is the trust boundary, by design.</li>
              <li>Anything that needs a cost-based planner. Field order is yours to choose.</li>
            </ul>
          </article>
        </div>
        <Text as="p" size="sm" color="secondary">
          Fjord is pre-1.0 and the documentation says so page by page.{' '}
          <Link href={route('status')}>Status &amp; roadmap</Link> is the honest inventory of what
          is built, what is not, and the one decision still open.
        </Text>
      </section>

      <section className="band last">
        <Heading level={2}>Where to start</Heading>
        <div className="cards">
          <a className="card" href={route('getting-started')}>
            <b>Getting started</b>
            <span>Install the binary, write nine facts, and ask where a function is used. Five minutes.</span>
          </a>
          <a className="card" href={route('walkthrough')}>
            <b>A guided tour</b>
            <span>A real code index, end to end, with the output each command printed.</span>
          </a>
          <a className="card" href={route('concepts')}>
            <b>Concepts</b>
            <span>Facts, predicates, keys and values. The whole model on one page.</span>
          </a>
          <a className="card" href={route('query-language')}>
            <b>sigla reference</b>
            <span>Every construct in the query language, with the rows each one returns.</span>
          </a>
        </div>
      </section>
    </main>
  )
}
