# `graph` — a solution, a project it does not list, and one reference across the line

Three projects and two edges: `A → B` inside the solution, `B → C` out of it. `Graph.slnx`
lists A and B only, and C sits outside `src/` so a glob rooted there misses it too.

That shape is the one the loader's gates are about:

- **A → B resolves to source.** Both are in the workspace, so a symbol used in A has a
  location in `B.cs` and a reference to it is not counted external.
- **C is outside the indexed set.** The loader no longer builds it to find out — which is
  what made workspace loading spawn MSBuild — so C resolves to its *assembly* where the
  checkout has been built, and to nothing where it has not. Either way the walk indexes
  what was built and nothing else.
- **C is discovered but not built**, which is the build layer's own case: `Discover` globs
  every `.csproj` under the source, so C gets a project fact from its XML while A and B get
  theirs from a design-time build.
- **C is the control for the solution's membership.** It has a project fact and the solution
  does not name it, so `msbuild.SolutionToProject` naming it would mean the membership had
  been written from what is on disk rather than from what `Graph.slnx` lists.

Nothing here references a package: a fixture that needs a restore is a fixture that needs a
network, and these run in the same job as everything else.
