# `broken` — one project that will not build, beside one that will

A real repository has projects that do not restore on this machine: a Windows-only target,
a pinned SDK, a missing feed. One of them must not cost the other four hundred, and the
reason the run prints has to be the real one — a reader who is told the wrong reason looks
in the wrong place, and the two candidate messages here are one real error and one that is
wrong by construction.

`Bad` imports a file that is not there, which MSBuild reports as `MSB4019`. It is
single-targeted, so the run's second attempt asks it for `DispatchToInnerBuilds` and is
told that target does not exist — which is true, uninteresting, and exactly what the report
must not say.
