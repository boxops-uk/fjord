# `rescue` — a project the solution lists and the glob cannot see

The build layer reads every `.csproj` under `--source`. The solution is under `--source`
too, and it is free to list a project that is not: `app/App.slnx` names `../lib/Lib.csproj`,
which sits beside `app/` rather than inside it.

So `Lib` is a project that **built** — MSBuild resolved it, it has a target framework, an
assembly name and a source list — and that the glob never saw. Nothing about it is
uncertain; it was simply looked for in one place and found in another.

`--root` is the fixture root, so `lib/Lib.csproj` is a name this index can use. A project
whose path does not resolve under the root is a different case and still skipped: that one
has no name two runs would agree on.
