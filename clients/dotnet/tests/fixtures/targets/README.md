# `targets` — three projects, two frameworks, and no overlap

`Multi` targets `net8.0` and `net10.0`; `Single` targets only `net10.0`; `Old` only
`net8.0`. So neither framework covers the solution, which is the shape that makes a
fan-out mean something: index `net10.0` and `Old` has nothing to contribute, index `net8.0`
and `Single` does not.

`Multi` compiles differently per framework — a member behind `#if NET10_0_OR_GREATER` — so
"each database holds exactly one target's facts" is a claim with an observable difference
behind it rather than two identical indexes with different names.
