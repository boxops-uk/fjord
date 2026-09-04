// C# 9 — Top-level statements, and the reason this project exists at all.
//
// Every declaration in this file is one the compiler writes rather than one anybody wrote:
// the statements below become the body of a method named `<Main>$` — a name no C# identifier
// can spell — on a class named `Program` in the global namespace. Neither the class nor the
// method has a declaring syntax node of its own: `Program`'s declaring syntax is the whole
// compilation unit, and `args` is a parameter whose declaration is nowhere. An index that
// keys a declaration by its name in source has nothing to key these by, and an index that
// records a containing type for the statements has to invent the same type the compiler did.
using System;
using System.Threading.Tasks;

using Surface.Entry;

var log = new EntryLog();

// `args` is in scope with no declaration in this file.
log.Add($"arguments: {args.Length}");

// An `await` among top-level statements makes the synthesized entry point async, which
// changes its return type from `int` to `Task<int>` without changing anything in source.
await Task.Yield();

var doubled = Doubled(args.Length);
log.Add($"doubled: {doubled}");

log.Add(IgnoredDirectives.Note);

Console.WriteLine(log.Render());

// A `return` from top-level statements is the process's exit code.
return doubled;

// A local function among top-level statements: it is a local of `<Main>$`, so its containing
// symbol is the invented method, and it may be declared *after* the return that precedes it.
static int Doubled(int value) => value * 2;
