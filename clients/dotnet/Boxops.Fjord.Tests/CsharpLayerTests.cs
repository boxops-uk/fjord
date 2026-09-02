using System;
using System.Collections.Generic;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The C# semantic layer's thirty-one predicates, written and asked back.</b>
/// </para>
/// <para>
/// The deepest transcription in the set: three unions inlined at every use site, seven
/// optionals, and a type web whose recursion is broken by reference rather than by
/// nesting. The fingerprint says nothing about any of it — it is carried — so what proves
/// these shapes is the server decoding them against its own statement, which is what
/// writing a fact of each does.
/// </para>
/// <para>
/// The facts are built inline rather than through producer helpers on purpose: this is a
/// second statement of the shapes, and a helper shared with the emission would make the
/// two agree by construction.
/// </para>
/// </summary>
public sealed class CsharpLayerTests
{
    private static FjordValue R(FjordFact fact) => FjordValue.Of(FjordRef.To(fact));

    private static FjordValue Rec(params FjordValue[] fields) => FjordValue.Rec(fields);

    private static FjordValue Alt(uint disc, FjordValue payload) => FjordValue.Alt(disc, payload);

    /// <summary>An alternative with no payload — `false_`, `public`, `nothing`.</summary>
    private static FjordValue Tag(uint disc) => FjordValue.Alt(disc, FjordValue.Rec());

    private static readonly FjordValue False = Tag(0u);
    private static readonly FjordValue Nothing = Tag(0u);
    private static readonly FjordValue Public = Tag(9u);
    private static readonly FjordValue Private = Tag(3u);
    private static readonly FjordValue NoneRef = Tag(1u);

    [Fact]
    public void Every_csharp_predicate_round_trips_through_the_server()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");
        using var connection = FjordConnection.Connect(server.Socket, "dotnet", DotnetIndex.Schema);

        var file = DotnetIndex.FileFact("Fixture/Thing.cs");
        var symbol = DotnetIndex.SymbolFact("scip-csharp nuget Fixture 1.0.0.0 Fixture/Thing#Do().");

        // ---- names and namespaces ----------------------------------------------------

        FjordFact Name(string text) => new(DotnetIndex.Name, FjordValue.Of(text));

        var thing = Name("Thing");
        var fixture = Name("Fixture");
        var ns = new FjordFact(DotnetIndex.Namespace, Rec(R(fixture), Nothing));
        var thingName = new FjordFact(DotnetIndex.FullName, Rec(R(thing), R(ns)));

        FjordFact FullNameOf(string text) =>
            new(DotnetIndex.FullName, Rec(R(Name(text)), R(ns)));

        // ---- the named types ---------------------------------------------------------

        var cls = new FjordFact(DotnetIndex.Class, Rec(
            R(thingName), Nothing, Nothing, Public, False, False, False));
        var iface = new FjordFact(DotnetIndex.Interface, Rec(
            R(FullNameOf("IThing")), Nothing, Public, False));
        var record = new FjordFact(DotnetIndex.Record, Rec(
            R(FullNameOf("Point")), Nothing, Nothing, Public, False, False));
        var strct = new FjordFact(DotnetIndex.Struct, Rec(
            R(FullNameOf("Size")), Nothing, Public));

        // `class_ = 0` of `NamedType`, then `namedType = 1` of `AType`.
        var namedType = Alt(0u, R(cls));
        var aType = Alt(1u, namedType);

        var typeParameter = new FjordFact(DotnetIndex.TypeParameter, Rec(
            R(Name("T")), Nothing, False, False, False));

        // ---- members -----------------------------------------------------------------

        var method = new FjordFact(DotnetIndex.Method, Rec(
            R(Name("Do")), namedType, aType, False, Public, FjordValue.Of("M:Fixture.Thing.Do")));
        var parameter = new FjordFact(DotnetIndex.Parameter, Rec(
            R(Name("times")), aType, NoneRef, False, False, False));
        var field = new FjordFact(DotnetIndex.Field, Rec(
            R(Name("Count")), aType, namedType, Private, False, False, False));
        var local = new FjordFact(DotnetIndex.Local, Rec(
            R(Name("x")), aType, R(method), NoneRef, False));
        var property = new FjordFact(DotnetIndex.Property, Rec(
            R(Name("Label")), namedType, aType, Alt(1u, R(method)), Nothing, False, False,
            FjordValue.Of("P:Fixture.Thing.Label")));

        // ---- the remaining type shapes -----------------------------------------------

        var arrayType = new FjordFact(DotnetIndex.ArrayType, Rec(aType, FjordValue.Of(1L)));
        var pointerType = new FjordFact(DotnetIndex.PointerType, Rec(aType));
        var functionPointerType = new FjordFact(DotnetIndex.FunctionPointerType, Rec(
            R(FullNameOf("Callback")), R(method)));

        // ---- locations and cross-references ------------------------------------------

        var span = Rec(FjordValue.Of(0L), FjordValue.Of(7L));
        var location = Rec(R(file), span);

        // `method = 1` of `Definition`.
        var definition = Alt(1u, R(method));

        var memberAccess = new FjordFact(DotnetIndex.MemberAccessLocation, Rec(
            Alt(4u, R(method)), location));

        var facts = new (uint Predicate, FjordFact Fact)[]
        {
            (DotnetIndex.Name, thing),
            (DotnetIndex.NameLowerCase, new FjordFact(DotnetIndex.NameLowerCase, Rec(
                FjordValue.Of("thing"), R(thing)))),
            (DotnetIndex.Namespace, ns),
            (DotnetIndex.FullName, thingName),
            (DotnetIndex.Class, cls),
            (DotnetIndex.Interface, iface),
            (DotnetIndex.Record, record),
            (DotnetIndex.Struct, strct),
            (DotnetIndex.Implements, new FjordFact(DotnetIndex.Implements, Rec(namedType, R(iface)))),
            (DotnetIndex.TypeTypeParameter, new FjordFact(DotnetIndex.TypeTypeParameter, Rec(
                namedType, FjordValue.Of(0L), R(typeParameter)))),
            (DotnetIndex.Method, method),
            (DotnetIndex.MethodParameter, new FjordFact(DotnetIndex.MethodParameter, Rec(
                R(method), FjordValue.Of(0L), R(parameter)))),
            (DotnetIndex.MethodTypeParameter, new FjordFact(DotnetIndex.MethodTypeParameter, Rec(
                R(method), FjordValue.Of(0L), R(typeParameter)))),
            (DotnetIndex.Parameter, parameter),
            (DotnetIndex.Field, field),
            (DotnetIndex.TypeParameter, typeParameter),
            (DotnetIndex.Local, local),
            (DotnetIndex.Property, property),
            (DotnetIndex.PropertyParameter, new FjordFact(DotnetIndex.PropertyParameter, Rec(
                R(property), FjordValue.Of(0L), R(parameter)))),
            (DotnetIndex.ArrayType, arrayType),
            (DotnetIndex.PointerType, pointerType),
            (DotnetIndex.FunctionPointerType, functionPointerType),
            (DotnetIndex.DefinitionLocation, new FjordFact(DotnetIndex.DefinitionLocation, Rec(
                definition, location))),
            (DotnetIndex.ObjectCreationLocation, new FjordFact(DotnetIndex.ObjectCreationLocation, Rec(
                aType, R(method), location))),
            (DotnetIndex.MemberAccessLocation, memberAccess),
            (DotnetIndex.MethodInvocationLocation, new FjordFact(DotnetIndex.MethodInvocationLocation, Rec(
                R(method), location, Alt(1u, Rec(R(memberAccess)))))),
            (DotnetIndex.TypeLocation, new FjordFact(DotnetIndex.TypeLocation, Rec(aType, location))),
            (DotnetIndex.EntityXRef, new FjordFact(DotnetIndex.EntityXRef, Rec(
                R(file), span, definition))),
            (DotnetIndex.EntityRef, new FjordFact(DotnetIndex.EntityRef, Rec(
                definition, R(file), span))),
            (DotnetIndex.SymbolOf, new FjordFact(DotnetIndex.SymbolOf, Rec(definition, R(symbol)))),
            (DotnetIndex.DefinitionBySymbol, new FjordFact(DotnetIndex.DefinitionBySymbol, Rec(
                R(symbol), definition))),
        };

        Assert.Equal(31, facts.Length);

        // **A wrong shape is refused here**, by the server decoding against its own
        // statement — that is what this loop asserts, and the exception is the assertion.
        // The count is `Seen` rather than `Created` because a fact nested inside an
        // earlier one has already been interned: `MethodParameter` interns its parameter,
        // so writing `csharp.Parameter` afterwards dedupes rather than creates.
        foreach (var (predicate, fact) in facts)
        {
            var written = connection.Write(predicate, [fact]);
            Assert.True(written.Seen >= 1, DotnetIndex.NameOf(predicate));
        }

        foreach (var (predicate, _) in facts)
        {
            var name = DotnetIndex.NameOf(predicate);
            Assert.NotEmpty(connection.Query($"X where X = {name} _").Rows);
        }

        // **A union-typed variable shared by two generators**, which is the query the
        // schema's own comment names as what W1 bought — and the reason `Definition` can
        // be a union in a key at all.
        var shared = connection.Query(
            "{use = U, at = L} where "
            + "csharp.EntityXRef {file = F, use = U, target = D}; "
            + "csharp.DefinitionLocation {definition = D, location = L}").Rows;

        Assert.NotEmpty(shared);
    }
}
