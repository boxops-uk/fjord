# `propsbroken` — one project whose `TargetFrameworks` lives in an imported props file

`Hidden.csproj` names no target framework of its own; `Hidden/Directory.Build.props`
sets `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>` instead — an ordinary layout
for a repository that factors its target set out of individual project files. Its
`net8.0` inner build imports a file that is not there, guarded by
`Condition="'$(TargetFramework)' == 'net8.0'"`, so only that inner build's evaluation
fails with MSBuild's own `MSB4019`, the same shape `halfbuilt` uses.

The shape this guards: reading the project's *own* XML for `TargetFrameworks` returns
nothing when the list is set only by an import, so a declared-vs-achieved comparison
that trusts the raw XML sees no declared target to compare against and the dropped
`net8.0` target reaches neither the log nor `Skipped` nor `--strict` — the issue's exact
complaint, surviving for this one layout.
