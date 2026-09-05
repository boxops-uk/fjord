# `rescue` — a project the solution lists and the glob cannot see

The build layer reads every `.csproj` under the index root. The solution is under it
too, and it is free to list a project that is not: `app/App.slnx` names `../lib/Lib.csproj`,
which sits beside `app/` rather than inside it.

So `Lib` is a project that **built** — MSBuild resolved it, it has a target framework, an
assembly name and a source list — and that the glob never saw. Nothing about it is
uncertain; it was simply looked for in one place and found in another.

`--root` is the fixture root, so `lib/Lib.csproj` is a name this index can use. A project
whose path does not resolve under the root is a different case and still skipped: that one
has no name two runs would agree on.

**Rooted at `app/` instead, the same fixture is the other case.** `lib/Lib.csproj` then comes
back as `../lib/Lib.csproj`, gets no `src.File` and no `msbuild.Project` — so it is a project
the solution *lists* that no `msbuild.SolutionToProject` edge can point at. The solution and
the edge to `Main` are still written; the missing one is named in the log and counted in
`ProjectIndex.Unlinked`, because a database holding half a solution's membership looks exactly
like one holding all of it.
