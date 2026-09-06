// C# 10 — global using directives. This file declares no type: it exists so that the rest of
// the project can be read with no `using` block at all, which is what makes the directives
// observable. A global using alias is here too, because the alias and the plain form are
// separate constructs and an index that holds one need not hold the other.

global using System;
global using System.Collections.Generic;

// C# 10 — a global using *alias*.
global using Str = System.String;

// C# 10 — a global using *static* directive.
global using static System.Math;
