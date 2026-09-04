// The fourth UsingDirective spelling: a global using, which is a using directive whose scope
// is the compilation rather than the file. It has to sit before every other using in its own
// file and outside any namespace, so it gets a file of its own.
//
// The alias it declares is a `SymbolKind.Alias` like any other, and like any other the walk
// drops it — but unlike a file-scoped alias it is in scope in every file of the project, so
// a reference to `SfClock` anywhere here binds to a declaration in this file.

global using SfClock = System.DateTimeOffset;
global using SfNumerics = System.Numerics;
