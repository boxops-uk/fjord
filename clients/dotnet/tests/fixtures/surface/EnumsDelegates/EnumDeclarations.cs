// Clause 20.2 — enum declarations. An `enum-declaration` is attributes, modifiers, the
// `enum` keyword, an identifier, an optional `enum-base`, a body, and an optional trailing
// semicolon. The enum-base names an integral type in a position that is neither a base-class
// list nor an ordinary type usage: the type it names becomes the enum's *underlying* type,
// while the enum's base *class* is System.Enum, which no token here names.

namespace Surface.EnumsDelegates
{
    /// <summary>20.2 — no enum-base, so the underlying type is the implicit <c>int</c>.</summary>
    public enum EdBaseImplicit
    {
        Only = 1,
    }

    /// <summary>20.2 — enum-base <c>sbyte</c>, at the ends of its range.</summary>
    public enum EdBaseI8 : sbyte
    {
        Min = -128,
        Zero = 0,
        Max = 127,
    }

    /// <summary>20.2 — enum-base <c>byte</c>.</summary>
    public enum EdBaseU8 : byte
    {
        Zero = 0,
        Max = 255,
    }

    /// <summary>20.2 — enum-base <c>short</c>.</summary>
    public enum EdBaseI16 : short
    {
        Min = -32768,
        Max = 32767,
    }

    /// <summary>20.2 — enum-base <c>ushort</c>.</summary>
    public enum EdBaseU16 : ushort
    {
        Max = 65535,
    }

    /// <summary>20.2 — enum-base <c>int</c>, written out where it would be implied.</summary>
    public enum EdBaseI32 : int
    {
        Min = -2147483648,
        Max = 2147483647,
    }

    /// <summary>20.2 — enum-base <c>uint</c>.</summary>
    public enum EdBaseU32 : uint
    {
        Max = 4294967295,
    }

    /// <summary>20.2 — enum-base <c>long</c>.</summary>
    public enum EdBaseI64 : long
    {
        Min = -9223372036854775808,
        Max = 9223372036854775807,
    }

    /// <summary>20.2 — enum-base <c>ulong</c>, the widest underlying type the clause allows.</summary>
    public enum EdBaseU64 : ulong
    {
        Max = 0xFFFFFFFFFFFFFFFFUL,
    }

    /// <summary>
    /// 20.2 hazard — an enum body with no members. The type exists and declares nothing, so
    /// an index that reaches enums through their members holds no row for it at all.
    /// </summary>
    public enum EdEmptyBody
    {
    }

    /// <summary>20.2 — the optional semicolon the grammar allows after an enum body.</summary>
    public enum EdTrailingSemicolon
    {
        Present = 1,
    };

    /// <summary>
    /// 20.2 hazard — two enums with the same underlying type and the same member names. The
    /// member identity has to be keyed on the containing enum, not on the name and value.
    /// </summary>
    public enum EdDeclaredLeft : byte
    {
        Same = 1,
        Other = 2,
    }

    /// <summary>20.2 — the twin of <see cref="EdDeclaredLeft"/>, member for member.</summary>
    public enum EdDeclaredRight : byte
    {
        Same = 1,
        Other = 2,
    }

    /// <summary>20.2 — an enum nested in a class, so its container is a type.</summary>
    public sealed class EdEnumClassHost
    {
        /// <summary>20.2 — the nested enum declaration.</summary>
        public enum EdNestedInClass
        {
            Idle,
            Busy,
        }

        /// <summary>20.2 — a use of the nested enum from inside its container.</summary>
        public EdNestedInClass State { get; set; } = EdNestedInClass.Idle;
    }

    /// <summary>20.2 — an enum nested in a struct.</summary>
    public readonly struct EdEnumStructHost
    {
        /// <summary>20.2 — the nested enum declaration, inside a value type.</summary>
        public enum EdNestedInStruct
        {
            Absent,
            Present,
        }
    }

    /// <summary>20.2 — an enum nested in an interface, which the language permits.</summary>
    public interface IEdEnumInterfaceHost
    {
        /// <summary>20.2 — the nested enum declaration, inside an interface.</summary>
        enum EdNestedInInterface
        {
            Unset,
            Set,
        }

        /// <summary>20.2 — an interface member typed by the enum nested beside it.</summary>
        EdNestedInInterface Status { get; }
    }
}

namespace Surface.EnumsDelegates.Deep
{
    /// <summary>
    /// 20.2 — an enum in a nested namespace, so its fully qualified name has four parts and
    /// its container is a namespace rather than a type.
    /// </summary>
    public enum EdDeepEnum
    {
        Shallow,
        Deeper,
    }
}
