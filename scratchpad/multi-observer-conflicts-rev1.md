# Multi-observer conflicts — an uninformed compilation must abstain, not guess

| | |
|---|---|
| **Issue** | [#82](https://github.com/boxops-uk/fjord/issues/82) |
| **Area** | `clients/dotnet` (`FactSink`, `Program`, the loader), `schemas/msbuild.sigla`, `schemas/codemarkup.sigla`, and whatever else the sweep in step 2 names |
| **Invariants** | **`ops-I5`** is *correct* and stays — the reject is the bug reporting itself. `ops-I4` is why "last writer wins" is not available |
| **Format** | unchanged |
| **Protocol** | unchanged |
| **Fingerprint** | **moves.** A value shape changes, so this is a flag day: `clients/dotnet/README.md`'s ordered checklist applies |

## The class, stated once

A predicate keyed on an entity, valued with a description of it, **written by every
compilation that can see the entity**. Two compilations see the same entity through
different windows, so they describe it differently, and the second write is the same key
with a different value.

The server is right to refuse. The producer is asserting a functional dependency
(entity → one description) that it does not honour.

## What was reproduced

The issue's two-project repro fails as described. Two things it gets wrong, and both
change the fix.

### The first conflict is `msbuild.Project`, not `codemarkup.SymbolInfo`

```
the fact writer failed while writing msbuild.ProjectReference
 ---> Conflict: predicate PredicateId(47) already holds a different fact under this key,
      as FactId(51677046505473)
```

**Predicate 47 is `msbuild.Project`.** `msbuild.ProjectReference` is 49. And
`FactId(51677046505473)` decodes to predicate 47, sequence 1 — `msbuild.Project#1`, which
answers the issue's open question: the rejected fact is a *project*, not a symbol, which is
why its side-by-side `SymbolInfo` comparison could not show what failed.

**With `--writers 1` the attribution is correct.** So the message names whichever predicate
*that* writer happened to be on rather than the one the server rejected — a concurrency
artefact, and the reason the investigation went to the wrong predicate.

### Measured divergence, `msbuild.Project`, for `Lib/Lib.csproj`

| field | `Lib` indexed (design-time built) | `App` indexed (`Lib` discovered only) |
|---|---|---|
| `sdk` | `just "Microsoft.NET.Sdk"` | **`nothing`** |
| `outputType` | `just "Library"` | **`nothing`** |
| `rootNamespace` | `just "Lib"` | **`nothing`** |
| `targetFramework`, `assemblyName` | `just …` | `just …` — agree |

Symmetrically for `App.csproj`. The indexer says so itself: *"build layer: 2 project(s), 1
from a design-time build"*. A project the run built gets a full description; one it merely
discovered by glob gets a degraded one.

`codemarkup.SymbolInfo` diverges too, exactly as reported — `doc` present against `""`,
every other field byte-identical. So it is at least two predicates, and `SymbolInfo` is what
fails next.

## Why neither fix in the issue is quite right

**"Make absence representable" does not work, and we already have the experiment.**
`msbuild.Project`'s varying fields are *already* `nothing | just`. It still conflicts,
because the uninformed observer writes `nothing` as a **claim** ("this project has no sdk")
rather than as abstention ("I cannot see"). Optionality in the type does not make a producer
abstain.

**"Let exactly one compilation describe it" is right in shape but would delete a tested
property.** For projects, the build layer is deliberately *every* project under the root,
not only the built ones — `LoaderTests.The_build_layer_holds_the_project_the_workspace_does_not`
asserts that by count. Gating the whole fact on authority would drop the discovered ones.

**So: split by authority, not by optionality.** The fields only the authority can know move
into a predicate only the authority writes. Everyone else keeps writing what they can
actually see.

## Step 1 · The misattribution

Cheap, independent, and worth landing first because every later diagnosis depends on it.

`FactSink`'s failure path names the predicate the writer was on. Make it name the predicate
the **server** rejected, decoded to a schema name rather than a number. The server already
sends the id; the client already has the schema it learned at handshake.

**Red test:** two conflicting writes across two predicates on separate writers, asserting
the message names the one the server rejected. It fails today with >1 writer and passes with
`--writers 1`, which is the test's own control.

## Step 2 · Sweep the class, as a permanent guard

Not a one-off investigation. The sweep *is* the guard, and it is a differential with an
independent oracle — the other database:

> Index each project of a multi-project solution into **its own** database. For every
> predicate, every key present in more than one database must carry the same value.

That finds the whole class now and stops it coming back, and it needs no judgement about
which predicates are at risk. **This is the red test for the PR**: it fails today on at
least `msbuild.Project` and `codemarkup.SymbolInfo`, and the fix is done when it is green.

The candidate set it will be ranging over, from the schema — every predicate with a value
side whose key an entity more than one compilation can see:

| schema | predicates with a value side | notes |
|---|---|---|
| `msbuild` | `Project`, `PackageReference`, `ProjectCompilation` | `Project` **confirmed**; the other two are keyed on a project and plausibly incomplete for an unbuilt one |
| `codemarkup` | `SymbolInfo`, `Definition`, `FileDefinition` | `SymbolInfo` **confirmed**; `Definition` carries `qualified` and `name`, which a metadata view may render differently |
| `src` | `FileLanguage`, `FileDigest`, `FileOrigin`, `FileInfo`, `FileLine`, `FileLineStyles` | keyed on a file. Content-derived, so observers *should* agree — which is exactly the kind of "should" this sweep exists to check |

`bundle`, `npm` and `typescript` have 41 more between them. Out of scope here, but the same
shape, so the guard should be written to range over whatever schema it is given rather than
over a hard-coded list.

**A second, cheaper sweep worth running alongside:** the same solution indexed once with
`--sln` against the same solution indexed project-by-project. The issue reports a conflict
*within* one `--sln` run on a real repository, which the two-run case does not explain — so
there may be a second mechanism, and this is what would show it.

## Step 3 · The schema and indexer change

Once the sweep names the set, for each predicate in it:

- **Keep** in the existing predicate what every observer can see — for `msbuild.Project`,
  what is readable from the `.csproj` XML; for `SymbolInfo`, what is in the assembly
  (`signature`, `modifiers`, `qualified`, `package`, `kind` all agreed byte-for-byte in the
  repro).
- **Move** what only the authority can know into a new predicate only the authority writes —
  the design-time-build fields for a project; `doc` for a symbol.
- **The authority test is local**, which is what keeps the indexer free of a symbol→info
  map: `symbol.ContainingAssembly == compilation.Assembly` for a symbol, and "this run
  design-time-built this project" for a project, which the loader already knows because it
  already reports the count.

A value shape changes, so the fingerprint moves and the flag day applies. Both halves of the
golden (`byte_identical_with_the_dotnet_client`) and each client's own schema constant are on
that checklist.

## Acceptance criteria

1. **Two runs into one database succeed** — the issue's repro, as an integration test that
   drives a real server and real MSBuild, which is what `clients/dotnet`'s gates already do.
2. **The differential guard of step 2 is green**, and was red before the change — recorded,
   because a guard nobody saw fail is a guard nobody knows works.
3. **The conflict message names the predicate the server rejected**, asserted with more than
   one writer.
4. **The build layer still holds every project under the root.**
   `LoaderTests.The_build_layer_holds_the_project_the_workspace_does_not` stays green — it is
   the property the obvious fix would have broken.
5. **No existing golden moves except the ones the value-shape change forces**, and each of
   those is regenerated deliberately, named in the commit.
6. **`--no-docs` stops conflicting**, which the issue reports as a separate symptom on a
   different symbol. If the sweep does not explain it, say so rather than assuming the fix
   covered it.
7. **The flag day is walked** — `clients/dotnet/README.md`'s ordered steps, each client's
   constant checked against its own schema by name.

## Traps

- **Do not relax `ops-I5`.** The reject is the bug announcing itself. Last-writer-wins breaks
  `ops-I4`'s order independence, and a merge rule would make the database depend on write
  order.
- **Do not put the observing compilation in the key.** It resolves the conflict and
  multiplies rows for something a reader calls one fact.
- **Do not trust the exception's predicate name** while writing step 2 or 3 — fix it in step
  1 first, or every observation in the sweep is suspect for the same reason the issue's was.
- **An empty string is not an abstention**, and neither is `nothing`. The only abstention is
  not writing the fact.

## Not in scope

- **Assembly-version identity.** The issue's aside is real — a SCIP symbol carries the
  assembly version, everything unset is `1.0.0.0`, so identity rests on the assembly name and
  two builds of one name mint colliding symbols. It is a different bug with a different fix
  and wants its own issue.
- **`bundle`, `npm` and `typescript`.** Same class, not this producer; the guard should be
  able to range over them when someone gets there.
- **Incremental indexing as a feature.** This unblocks it; it does not build it.

---

## What the implementation changed about this plan

Recorded rather than edited in, because a plan that is quietly corrected afterwards teaches
nobody where the reasoning was thin.

**The authority test for a symbol is not `symbol.ContainingAssembly == compilation.Assembly`.**
Step 3 proposed that, and it is wrong in a way the two-project repro cannot show.
`Loader.Document()` deliberately attaches documentation providers for assemblies *outside*
the compilation, because a reference to `IDisposable` or to a package type has to get a
description from somewhere and no run here holds its source. Gating on the compilation's own
assembly would have deleted every description of every external symbol — the layer's whole
reason for describing symbols it does not own.

So the rule is three-way, not two:

| the symbol's assembly | who describes it | why |
|---|---|---|
| this compilation's own | this walk | it has the source |
| another project **in this run** | that project's walk | it has the source, and this view does not |
| outside the run entirely | every observer, identically | nobody has the source, so every view is the same one and `ops-I5` dedups it |

`ProjectIndex.Produces(assembly)` is the middle row, asked of *every* project in the run
rather than of the ones walked so far — asking the latter would make the answer depend on
the order projects were handed over, which is what `ops-I4` forbids.

**The assembly-version aside became a defect of its own, found from the other end.** The
plan put it out of scope. The guard then found it first: on a fixture that sets
`AssemblyName`, the two views mint *different* names for one method — the walk uses the
project name and the metadata reader uses the assembly name — so they never collide, and
the divergence is invisible rather than refused. That is [#84](https://github.com/boxops-uk/fjord/issues/84),
it is sharper than the conflict, and it is why the `observers` fixture exists: `graph` could
not show #82 at all while #84 masked it.

**`--no-docs` is explained, not asserted.** Acceptance criterion 6 asked for one or the
other. `doc` was the only `SymbolInfo` field that ever diverged — every other field was
byte-identical in the measurement above — so suppressing it removes the conflict for the
same reason abstention does. The guard asserts the abstention; nothing asserts the switch,
and a test over a flag that works by making the values agree would be testing the
measurement rather than the fix.

**The predicate count is the part of a flag day nothing warns about.** `DotnetIndex` keeps
a hand-written array of ids beside the schema statement, and appending a predicate to one
and not the other is invisible until `PredicateCensusTests` compares the two sets — which it
did, on the second assertion rather than the first. Worth knowing before the next one.
