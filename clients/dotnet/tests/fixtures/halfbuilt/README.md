# `halfbuilt` — one multi-targeting project, one broken framework

`Shaky` targets `net8.0` and `net10.0`. Its `net8.0` inner build imports a file that is not
there, guarded by `Condition="'$(TargetFramework)' == 'net8.0'"` — so only that one inner
build's evaluation fails with MSBuild's own `MSB4019`, while `net10.0` evaluates and
compiles as normal.

One project, one design-time build, two inner builds, and only one of them usable: the
shape `Usable` can absorb without ever emitting a result for `net8.0`, because `net10.0`
alone already makes `Usable(results).Count > 0` — the project reads as built, and nothing
says the other half of it was not.
