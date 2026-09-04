// Clause 14.5.1 (using directives, general) and 14.2 (compilation units).
//
// Two facts at once.
//
// The first is scope. An ordinary using directive is scoped to the compilation unit or the
// namespace body that immediately contains it; a `global using` is scoped to *every*
// compilation unit of the program. So the three directives below are in force in all eighteen
// files of this project, and in no file is there anything to point at that says so. That is
// the hazard: a reference resolved through one of these has a directive as its reason, and the
// directive is in a file the reference never mentions.
//
// The second is 14.2 again, from its emptiest corner. Every one of a compilation unit's four
// sections is optional, and this file exercises the case that nothing but using_directives is
// present — a compilation unit that declares no type, no namespace, and no member. It is a
// file with no declarations at all.

global using System.Collections.Generic;

global using static System.Math;

global using NsGlobalPair = System.Collections.Generic.KeyValuePair<string, int>;
