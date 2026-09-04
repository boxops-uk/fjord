# `Entry` — the entry point nobody declared

Three source files. The project exists for one construct: **top-level statements**, whose
containing type and entry-point method are both invented by the compiler and appear in no
source file. `Program.cs`'s statements become the body of `<Main>$` — a name no C# identifier
can spell — on a class named `Program` in the global namespace, and `args` is a parameter whose
declaration is nowhere at all.

A class library cannot produce that pair: `Program.cs` in one is CS8805. So this is the third
project of the slice, `OutputType=Exe`, and it is the only one of the three that runs.

| File | Holds |
|---|---|
| `Program.cs` | the top-level statements: `args`, an `await` that makes the synthesized method async, a `return` that is the exit code, and a local function declared after it |
| `EntryLog.cs` | an ordinary type for the statements to call, so the corpus has an edge *out of* a synthesized method into a declared one |
| `IgnoredDirectives.cs` | C# 14's `#:` ignored directives, and a declaration after them |

## The two properties this project is built to have

- **Every declaration in `Program.cs` is one the compiler wrote.** There is nothing else in
  the file. An index that keys a declaration by its name in source has nothing to key `Program`
  or `<Main>$` by; an index that records a containing type for those statements has to invent
  the same type the compiler did. Which of those happened is the question the file exists to
  ask, and it can only be asked where no hand-written type is available to answer it by
  accident.
- **`Features=FileBasedProgram` is load-bearing, and it is a trap.** C# 14's ignored
  directives (`#:property`, `#:sdk`, `#:package`) are rejected outside a file-based program
  with CS9298, and `dotnet run app.cs` is not something a checked-in fixture can be — so the
  project sets by hand the compiler flag that route sets. If that flag does not reach the
  indexer's own compilation, `IgnoredDirectives.cs` does not parse and every declaration in it
  disappears with no other symptom. The top-level statements are in a *different* file for
  exactly this reason: the two rows fail independently.

`dotnet run --project Entry/Entry.csproj -- alpha beta` prints three lines and exits 4, which
is the cheapest available check that the synthesized entry point is real.
