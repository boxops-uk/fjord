// Clause 7.8.3 (fully qualified names): a namespace or type's fully qualified name is
// built by walking out through its containers, joining each step with a dot — so a nested
// type's fully qualified name and a namespaced type's are the same *shape* of string, and
// the string alone does not say which dots were namespace steps and which were nesting.
//
// `Surface.Lexical.Names.Fqn.Bridge.Leaf` below is a type nested in a type nested in a
// namespace; `Surface.Lexical.Names.Fqn.Span.Leaf` is a type in a namespace one level
// deeper. The two dotted strings differ in one identifier and in nothing structural.
//
// The exact collision — one dotted string reached two ways — is unbuildable: a namespace
// and a type of one name in one enclosing namespace is CS0101, so C# forbids the shape
// before an index can see it. That is recorded here rather than written.

namespace Surface.Lexical.Names.Fqn
{
    /// <summary>
    /// 7.8.3: a type whose fully qualified name is
    /// <c>Surface.Lexical.Names.Fqn.Bridge</c>, holding a nested type whose own is
    /// <c>Surface.Lexical.Names.Fqn.Bridge.Leaf</c>.
    /// </summary>
    public sealed class Bridge
    {
        /// <summary>Something to reference on the containing type.</summary>
        public const int Tag = 1;

        /// <summary>7.8.3: nested, so one of the dots in its name is a nesting step.</summary>
        public sealed class Leaf
        {
            /// <summary>Something to reference.</summary>
            public const int Tag = 2;

            /// <summary>Its fully qualified name, written out for a reader.</summary>
            public const string Fqn = "Surface.Lexical.Names.Fqn.Bridge.Leaf";

            /// <summary>7.8.3: nested twice, so two of the dots are nesting steps.</summary>
            public sealed class Deepest
            {
                /// <summary>Something to reference.</summary>
                public const int Tag = 3;

                /// <summary>Its fully qualified name.</summary>
                public const string Fqn = "Surface.Lexical.Names.Fqn.Bridge.Leaf.Deepest";
            }
        }
    }

    /// <summary>
    /// 7.8.3: a generic type, whose fully qualified name drops the type argument list —
    /// so <c>Holder&lt;int&gt;</c> and <c>Holder&lt;string&gt;</c> have one fully
    /// qualified name between them.
    /// </summary>
    /// <typeparam name="THeld">What it holds.</typeparam>
    public sealed class Holder<THeld>
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 4;

        /// <summary>The fully qualified name, which says nothing about the argument.</summary>
        public const string Fqn = "Surface.Lexical.Names.Fqn.Holder";

        /// <summary>A nested type inside a generic one, whose name inherits the parameter.</summary>
        public sealed class Nested
        {
            /// <summary>Something to reference.</summary>
            public const int Tag = 5;
        }
    }
}

namespace Surface.Lexical.Names.Fqn.Span
{
    /// <summary>
    /// 7.8.3: a type whose fully qualified name is the same *shape* as
    /// <c>Bridge.Leaf</c>'s and whose last dot is a namespace step rather than a
    /// nesting one.
    /// </summary>
    public sealed class Leaf
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 6;

        /// <summary>Its fully qualified name.</summary>
        public const string Fqn = "Surface.Lexical.Names.Fqn.Span.Leaf";
    }
}

namespace Surface.Lexical.Names.Fqn.Uses
{
    /// <summary>
    /// 7.8.3: every reference here is written as a fully qualified name with the
    /// <c>global::</c> qualifier, which is the one spelling that cannot be changed by a
    /// <c>using</c> or by a nearer declaration.
    /// </summary>
    public static class LexFullyQualifiedUses
    {
        /// <summary>Reaches the nested type through its full name.</summary>
        public static int NestedLeaf() => global::Surface.Lexical.Names.Fqn.Bridge.Leaf.Tag;

        /// <summary>Reaches the twice-nested type through its full name.</summary>
        public static int Deepest() =>
            global::Surface.Lexical.Names.Fqn.Bridge.Leaf.Deepest.Tag;

        /// <summary>Reaches the namespaced type whose name is the same shape.</summary>
        public static int NamespacedLeaf() => global::Surface.Lexical.Names.Fqn.Span.Leaf.Tag;

        /// <summary>Reaches the containing type, whose name is a prefix of the nested one's.</summary>
        public static int Bridge() => global::Surface.Lexical.Names.Fqn.Bridge.Tag;

        /// <summary>Reaches the generic type at two instantiations, which share one full name.</summary>
        public static int Holders() =>
            global::Surface.Lexical.Names.Fqn.Holder<int>.Tag
            + global::Surface.Lexical.Names.Fqn.Holder<string>.Tag
            + global::Surface.Lexical.Names.Fqn.Holder<int>.Nested.Tag;

        /// <summary>Reads the declared name strings, so each is referenced.</summary>
        public static int Lengths() =>
            global::Surface.Lexical.Names.Fqn.Bridge.Leaf.Fqn.Length
            + global::Surface.Lexical.Names.Fqn.Bridge.Leaf.Deepest.Fqn.Length
            + global::Surface.Lexical.Names.Fqn.Span.Leaf.Fqn.Length
            + global::Surface.Lexical.Names.Fqn.Holder<int>.Fqn.Length;
    }
}
