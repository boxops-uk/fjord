# `observers` — one symbol, two windows

Two projects and one documented method: `Lib.Widget.Measure` carries a doc comment, and
`App` calls it. Indexing `Lib` design-time-builds it and reads the comment from source;
indexing `App` reaches `Measure` through `Lib.dll`, where the comment is not.

**Neither project sets `AssemblyName`**, and that is the whole reason this fixture exists
beside [`graph`](../graph). `graph` sets it, so the two views mint *different* symbol
names for one method ([#84](https://github.com/boxops-uk/fjord/issues/84)) and never
collide — the divergence is there but invisible. Here the project name and the assembly
name coincide, the two views agree on what the method is *called*, and so they can
disagree about its description:

| | `Lib` indexed | `App` indexed, `Lib` from its build output |
|---|---|---|
| `doc` | `Lives in source, and not in the assembly.` | `` (empty) |
| everything else | identical | identical |

That is [#82](https://github.com/boxops-uk/fjord/issues/82)'s minimal repro as a fixture:
`codemarkup.SymbolInfo` is keyed `{symbol}`, the two values differ, and a database cannot
be filled by both runs. `ObserverAgreementTests` is what runs over it.

**`doc` is empty rather than absent, which is the defect in one line.** A compilation that
cannot see the comment asserts that there is none, instead of abstaining — so the schema
states a functional dependency (symbol → one description) that the producer does not
honour.
