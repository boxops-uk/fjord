# `renamed` — two projects reach one assembly by being renamed to it

The sibling of [`identity`](../identity), and the same subject spelled the other way.
`identity` files two `Engine.csproj` in two directories; here the project files are
`Fast.csproj` and `Portable.csproj`, and both set `<AssemblyName>Engine</AssemblyName>`.

Both declare `Engine.Rotor.Spin` with different documentation, so every symbol they share
is one `codemarkup.SymbolInfo` key wanted twice with two values — `ops-I4` — exactly as
`identity`'s pair is.

**Why the pair needs both spellings.** The loader used to name a compilation after its
project *file*, so this fixture's two projects arrived as assemblies `Fast` and `Portable`:
distinct identities, no collision detected, both walked, and every symbol in them named
after a project rather than after the assembly it is actually compiled into
([#84](https://github.com/boxops-uk/fjord/issues/84)). Nothing failed. The conflict the
`identity` pair exists to catch was not absent here, only hidden — and `find references`
from anything that referenced `Engine.dll` resolved to neither copy.

So `identity` proved the mechanism for the spelling where the two names already agreed,
and this one proves it for the spelling where they did not.
