# `propvar` — one project whose `TargetFrameworks` is a property reference

`Prop` writes `<TargetFrameworks>$(MyTargets)</TargetFrameworks>` rather than a literal
list — an ordinary pattern for a repository that defines its target set once and reuses
it. Nothing is broken: both `net8.0` and `net10.0` build clean.

The shape this guards: reading `TargetFrameworks` as unevaluated XML text returns the
literal string `$(MyTargets)`, which is never among the frameworks that actually built —
so a declared-vs-achieved comparison that trusts the raw XML reports two targets dropped
from a project that dropped nothing, and `--strict` fails a run with nothing wrong in it.
