// Clause 7.3 (declarations) and 7.7.1 (scopes, general). 7.3 lists the declaration spaces
// a program has — the global namespace, each namespace, each type, each member's
// parameters and type parameters, each block's locals and local constants, and each
// block's labels — and the rule that makes them worth a fixture is that a name may be
// declared once *per space*. Every declaration below is spelled `Slot` or `Retry`, so the
// only thing telling any two of them apart is the space they are in.

// Two namespaces in one file, so both are the block form: a file-scoped
// namespace declaration has to be the only one in its file (CS8954).
namespace Surface.Lexical.Concepts.Spaces
{
    /// <summary>7.3: a type in a namespace whose last component is <c>Spaces</c>.</summary>
    public sealed class LexSlot
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 1;
    }
}

namespace Surface.Lexical.Concepts
{
    /// <summary>
    /// 7.3: one type's declaration space, holding a differently-named member of each kind. A
    /// type may not declare two members of one name whatever their kinds, which is why this
    /// type is the one place in the file where the names differ.
    /// </summary>
    public sealed class LexTypeSpace
    {
        /// <summary>A constant.</summary>
        public const int Fixed = 1;

        /// <summary>A field.</summary>
        public int Held;

        /// <summary>A property.</summary>
        public int Computed { get; set; }

        /// <summary>An event.</summary>
        public event System.Action? Raised;

        /// <summary>A nested type, which is in the same space as the members.</summary>
        public sealed class Nested
        {
            /// <summary>Something to reference.</summary>
            public const int Tag = 2;
        }

        /// <summary>A method, whose parameters are a space of their own.</summary>
        public int Act(int given) => given + Fixed + Held + Computed + Nested.Tag;

        /// <summary>Raises the event, so it is written as well as declared.</summary>
        public void Raise() => Raised?.Invoke();
    }

    /// <summary>
    /// 7.3: the parameter and type parameter spaces. <c>Slot</c> is the parameter here and
    /// <c>TSlot</c> the type parameter; a parameter that shared a name with a method type
    /// parameter would be CS0412, so the two spaces are only *almost* independent.
    /// </summary>
    public sealed class LexParameterSpace
    {
        /// <summary>A method whose parameter is named for the same slot as everything else.</summary>
        public TSlot Echo<TSlot>(TSlot Slot) => Slot;

        /// <summary>A second method reusing both names, which is a second pair of spaces.</summary>
        public TSlot EchoAgain<TSlot>(TSlot Slot) => Slot;
    }

    /// <summary>
    /// 7.3 and 7.7.1: the local variable, local constant and label spaces. Four declarations
    /// named <c>Slot</c> and two named <c>Retry</c> live in this one method, in sibling blocks
    /// — which is legal because a block is a space, and is the shape that a name-keyed index
    /// of locals collapses.
    /// </summary>
    public sealed class LexBlockSpaces
    {
        /// <summary>The field that every local below hides in its own block.</summary>
        private readonly int Slot = 100;

        /// <summary>
        /// Walks the sibling blocks. <c>this.Slot</c> reaches the field; the bare <c>Slot</c>
        /// inside each block reaches that block's own declaration.
        /// </summary>
        public int Walk()
        {
            int total = this.Slot;

            {
                /* a local variable named for the field it hides */
                int Slot = 1;
                total += Slot;
            }

            {
                /* a local constant of the same name in a sibling block */
                const int Slot = 2;
                total += Slot;
            }

            {
                /* a third, whose scope is the whole of this block including the loop */
                for (int Slot = 0; Slot < 3; Slot++)
                {
                    total += Slot;
                }
            }

            {
                /* a fourth, bound by a pattern rather than a declarator */
                if (total is int Slot)
                {
                    total += Slot % 2;
                }
            }

            {
                Retry:
                total += 1;
                if (total < 0)
                {
                    goto Retry;
                }
            }

            {
                /* a second label of the same name, in a sibling block */
                Retry:
                total += 2;
                if (total < 0)
                {
                    goto Retry;
                }
            }

            return total + Spaces.LexSlot.Tag;
        }
    }
}
