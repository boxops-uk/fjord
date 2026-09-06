# `identity` — two implementations, one assembly

One library and two projects, there for one shape: **two real implementations of one
assembly**. `Engine/fast` and `Engine/portable` both declare `Engine.Rotor.Spin`, with
different bodies and different documentation, and both produce assembly `Engine` — which
is what `src/coreclr/System.Private.CoreLib` and `src/mono/System.Private.CoreLib` are to
each other, one per runtime.

**It killed the run.** The package coordinate in a `src.Symbol` is the containing
assembly's identity, so both halves mint the same string for every member they share.
`codemarkup.SymbolInfo` is keyed `{symbol}` with `{signature, doc, modifiers}` on the
value side, the two disagree, and ingest refuses one key with two values (`ops-I4`):

```
Unhandled exception. System.InvalidOperationException: the fact writer failed while
writing codemarkup.SymbolInfo
 ---> Boxops.Fjord.Client.FjordServerException: Conflict: predicate PredicateId(8)
      already holds a different fact under this key, as FactId(8796093022209)
EXIT=134
```

**Not [`refimpl`](../refimpl/README.md), and not its answer.** These are the same symptom
— one symbol, two declarations, two values under one key — and the two cases differ in
what is lost by picking one. A reference assembly is an API surface restated for the
compiler, so dropping it costs nothing and it is dropped silently. These are two programs
somebody wrote, so the one left out is **named**, and `--strict` fails the run over it: the
same reading a project that would not build already gets. Indexing both means two
databases, which is the decision `--framework` already makes for a checkout that compiles
twice.

| Shape | What it is here for |
|---|---|
| two projects of one **file name**, in two solution folders | the identity collision itself: Buildalyzer names a compilation's assembly from the project file, and `.slnx` refuses two projects of one name in one folder — the layout `sfx.slnx` uses, and the one `dotnet/runtime` files its two CoreLibs under |
| different bodies **and** different documentation on `Spin` | so the value side differs in `doc` as well as `signature`, and the conflict does not depend on `--no-docs` being off |
| neither project marked a reference assembly | which is what makes this the case `refimpl`'s rule must **not** answer: `IsReferenceAssembly` is false for both, so the identity rule is the only thing standing between this fixture and a dead run |

`SharedAssemblyTests` is what runs over this: the real program, a real socket, a real
database, and the exit code as the conflict assertion. Then the shape of what was written —
one `codemarkup.SymbolInfo` for the shared symbol, both projects still in the build graph
with only one of them walked, and `--strict` returning 1.

**The rule's order is asserted against `refimpl` rather than left to two `if`s.**
`A_reference_assembly_listed_first_does_not_claim_the_assembly` is that gate: `refimpl`
lists its `ref/` project first, so an identity rule that ran before the reference-assembly
rule would keep the restatement and leave the implementation out — exiting 0, answering
every documentation query with the empty string, and saying nothing about either.
