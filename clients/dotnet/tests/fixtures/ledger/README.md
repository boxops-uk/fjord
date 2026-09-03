# `ledger` — the frozen corpus, edited by no run

Two projects and six files, chosen to reach a wide slice of the schema rather than to be
small: types and nested types, an interface and an implementation, a generic method with a
constraint, a property, a field, an enum, a record, an operator, an indexer, doc comments,
and references that cross the project boundary.

**Nothing here is edited to make a test pass.** The sealed identity of a database built
from it is the assertion, so a file changed for any other reason moves every baseline that
was ever recorded against it. Anything a new test needs belongs in a new fixture.

Two properties this fixture is built to have, and both are asserted:

- **Every file belongs to a project.** No shared source, no file outside a project
  directory — so a run over it reports `0 file(s) no project compiles`, and a number other
  than zero means the attribution broke rather than that the fixture is unusual.
- **Every declaration is expressible.** No `dynamic`, no unresolved name — so
  `Inexpressible` is zero, and a run that drops declarations says so instead of quietly
  producing a smaller index that still looks plausible.
