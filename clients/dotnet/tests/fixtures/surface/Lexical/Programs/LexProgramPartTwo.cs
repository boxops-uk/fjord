// Clause 6.1 (programs): the second declaration site of `Surface.Lexical.Programs`, written
// in the block form so the two spellings of one namespace differ as tokens and agree as
// declaration spaces. `LexFirstUnit` needs no qualification from here.

namespace Surface.Lexical.Programs
{
    /// <summary>The half of <c>Surface.Lexical.Programs</c> that the second file contributes.</summary>
    public sealed class LexSecondUnit
    {
        /// <summary>Reads across the file boundary within one namespace.</summary>
        public int FromTheOtherFile() => LexFirstUnit.Contributed + 1;

        /// <summary>What this compilation unit is called, spelled out for a reader.</summary>
        public string Where() => "LexProgramPartTwo.cs";
    }
}
