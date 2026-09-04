// Clause 6.1 (programs): a program consists of one or more source files, and a namespace
// declaration space is the union of what every file contributes to it. This file and
// LexProgramPartTwo.cs both open `Surface.Lexical.Programs` — one file-scoped, one
// block-scoped — so the namespace has two declaration sites and one identity.

namespace Surface.Lexical.Programs;

/// <summary>The half of <c>Surface.Lexical.Programs</c> that this file contributes.</summary>
public sealed class LexFirstUnit
{
    /// <summary>A value the other file's type reads, so the two halves are linked.</summary>
    public const int Contributed = 1;

    /// <summary>What this compilation unit is called, spelled out for a reader.</summary>
    public string Where() => "LexProgramPartOne.cs";
}
