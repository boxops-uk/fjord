// CompilationUnit and GlobalStatement — the two kinds that only exist in a file of
// top-level statements.
//
// The entry point the compiler synthesises for this file is `Program.<Main>$`, and its
// declaring syntax is the `CompilationUnitSyntax` itself: there is no method declaration to
// point at. `CompilationUnitSyntax` derives from none of the six bases the walk's
// declaration switch names, so the program's entry point is the one method in this project
// with no definition in the index — and the synthesised `Program` class it hangs off is the
// one type here that is not under `Surface.SyntaxForms`, because top-level statements are in
// the global namespace by construction and cannot be moved out of it.
//
// Each statement below is a `GlobalStatementSyntax` wrapping an ordinary statement, which is
// the only difference between this file and a `Main` body. The last one is a `return`, so
// the entry point's synthesised return type is `int` rather than `void`.

using System;
using Surface.SyntaxForms.Declarations;
using Surface.SyntaxForms.Expressions;
using Surface.SyntaxForms.Names;
using Surface.SyntaxForms.Namespaces;
using Surface.SyntaxForms.Statements;

var ledger = new SfMemberHost(10) { Label = "surface" };
Console.WriteLine(ledger.Describe());

var names = new SfNameForms();
Console.WriteLine(names.Predefined(4));
Console.WriteLine(names.AliasQualified());
Console.WriteLine(names.BothSpellings());

var occupants = new SfIdentifierOccupants();
Console.WriteLine(occupants.Named());
Console.WriteLine(occupants.Contextual());
Console.WriteLine(occupants.Inferred([new SfLedgerRecord(1, "one")]));

Console.WriteLine(new SfTypeSyntaxForms().Reach());
Console.WriteLine(new SfTypeSyntaxForms().Literal());
Console.WriteLine(new SfTypeSyntaxForms().Verdict());
Console.WriteLine(SfCrefForms.Anchor());

Console.WriteLine(SfOperatorForms.Every(3, 4));
Console.WriteLine(SfLiteralForms.Numeric());
Console.WriteLine(SfLiteralForms.Strings());
Console.WriteLine(SfLiteralForms.Singles());

// `SfLiteralForms.CallVarargs` is deliberately *not* called here: `__arglist` compiles to
// the vararg calling convention, which CoreCLR refuses to JIT
// (`InvalidProgramException`). The declaration and the call site exist to be parsed and
// indexed — the ArgListExpression row is about syntax the compiler accepts, and this is the
// one row in the slice whose code cannot also be executed.
Console.WriteLine(SfLiteralForms.PrimaryFunctions());

Console.WriteLine(SfAccessForms.Reach());
Console.WriteLine(SfCreationForms.Explicit());
Console.WriteLine(SfCreationForms.Implicit());
Console.WriteLine(SfCreationForms.Anonymous());
Console.WriteLine(SfCreationForms.Arguments());

Console.WriteLine(SfLambdaForms.AnonymousMethods());
Console.WriteLine(SfLambdaForms.SimpleLambdas());
Console.WriteLine(SfLambdaForms.ParenthesizedLambdas());
Console.WriteLine(SfLambdaForms.Nested());

Console.WriteLine(SfStatementForms.Walk());
Console.WriteLine(SfQueryForms.Report());

var subject = new SfPatternSubject { Slot = 2, Name = "second" };
Console.WriteLine(SfPatternForms.Classify(ledger.Total));
Console.WriteLine(SfPatternForms.Designations(subject));
Console.WriteLine(SfPatternForms.Recursive(subject));
Console.WriteLine(SfPatternForms.Arms(subject));

Console.WriteLine(Surface.SyntaxForms.Directives.SfDirectiveForms.Branch());
Console.WriteLine(Surface.SyntaxForms.Directives.SfDirectiveForms.Suppressed("name"));
Console.WriteLine(Surface.SyntaxForms.Directives.SfDirectiveForms.Aliased());

Console.WriteLine(new SfBlockScoped());
Console.WriteLine(new Surface.SyntaxForms.Namespaces.Inner.SfNestedNamespaceMember().Depth);

Console.WriteLine(new SfAttributeForms<string>("held").Every(2));
Console.WriteLine(new SfParameterPrimary("localhost", 5432).Endpoint());
Console.WriteLine(new SfParameterForms().Nested());
Console.WriteLine(new SfAccessorHost { Name = "accessors" }.Limit);
Console.WriteLine(new SfFieldKeywordHost { Counted = 3 }.Counted);
Console.WriteLine(21.Doubled);
Console.WriteLine(21.IsEven());
Console.WriteLine(new[] { 1, 2, 3 }.Final);
Console.WriteLine(new SfExplicitImplementor() is SfLedgerContract contract ? contract.Describe() : "none");

return ledger.Size < 0 ? 1 : 0;
