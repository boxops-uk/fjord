namespace Surface.Synthesised;

/// <summary>
/// <c>SymbolKind.ErrorType</c> / <c>TypeKind.Error</c> — an unresolved type in a program
/// that compiles.
/// </summary>
/// <remarks>
/// An error type normally means the compilation failed, and a corpus that must build
/// cannot hold one that way. A documentation-comment <c>cref</c> is the exception: the
/// compiler binds it, reports at most a <em>warning</em> (CS1574) when it cannot, and the
/// unbound name still produces an <c>IErrorTypeSymbol</c> from any semantic model asked
/// about it. So the crefs on this type are the corpus's only error types, and they are the
/// answer to "what does the index do with a name that binds to nothing" —
/// <c>CandidateReason</c>, an arity that does not exist, and a member on a type that does.
///
/// The project sets <c>&lt;GenerateDocumentationFile&gt;</c> so that these are visible as
/// warnings in the build log rather than being taken on trust.
/// </remarks>
/// <seealso cref="SynNoSuchTypeAtAll"/>
public class SynErrorRefs
{
    /// <summary>A cref to a type that does not exist: <see cref="SynNoSuchTypeAtAll"/>.</summary>
    public int Missing { get; set; }

    /// <summary>
    /// A cref to a real type at an arity it does not have:
    /// <see cref="SynEntry{TNotThere}"/>.
    /// </summary>
    public int WrongArity { get; set; }

    /// <summary>
    /// A cref to a member that does not exist on a type that does:
    /// <see cref="SynEntry.NoSuchMember"/>.
    /// </summary>
    public int MissingMember { get; set; }

    /// <summary>
    /// A cref to a real method at a signature it does not have:
    /// <see cref="SynOverloads.Measure(string, string, string)"/>.
    /// </summary>
    public int WrongSignature { get; set; }

    /// <summary>
    /// A cref that resolves, for contrast: <see cref="SynEntry.Key"/> and
    /// <see cref="SynOverloads.Measure(int, int)"/> and
    /// <see cref="SynAccessors.this[int]"/> and
    /// <see cref="SynOperators.op_Addition"/>.
    /// </summary>
    public int Resolves { get; set; }

    /// <summary>A <c>param</c> naming a parameter that does not exist.</summary>
    /// <param name="notAParameter">Nothing is called this.</param>
    /// <returns>Zero.</returns>
    public int Mismatched(int actual) => actual - actual;
}
