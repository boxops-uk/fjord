// Clause 7.4.2 (namespace members): the second declaration of
// `Surface.Lexical.Members.Nest.Inner`, spelled as three nested declarations rather than
// one dotted one. `LexNamespaceClass` needs no qualification from in here, which is what
// makes the two spellings one declaration space.

namespace Surface.Lexical.Members
{
    namespace Nest
    {
        namespace Inner
        {
            /// <summary>7.4.2: declared in the nested spelling of the dotted namespace.</summary>
            public sealed class LexNestedSpellingMember
            {
                /// <summary>Reaches a type declared in the other file's spelling.</summary>
                public int FromTheDottedSpelling() => LexNamespaceClass.Tag;

                /// <summary>Reaches the rest of the namespace's members.</summary>
                public int Everything() =>
                    LexNamespaceClass.Tag
                    + LexNamespaceStruct.Tag
                    + (int)LexNamespaceEnum.Only
                    + new LexNamespaceRecord(4).Tag
                    + new LexNamespaceDelegate(value => value)(5);
            }
        }
    }
}
