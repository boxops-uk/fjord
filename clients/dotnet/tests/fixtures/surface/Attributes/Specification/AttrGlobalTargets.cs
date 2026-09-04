// Clause 23.3 (attribute specification), the two global targets.
//
// `assembly` and `module` are the only targets whose attribute section attaches to
// something no source file declares. There is no assembly declaration and no module
// declaration in C#, so an index that models an application as an edge from a section to a
// declared symbol has no node at this end of the edge — and a query for "what does
// AttrMarkAttribute annotate" must either name the assembly or say nothing here.
//
// This file holds global attributes and nothing else, because a global attribute section
// must precede every other element of its file except using and extern alias directives
// (CS1730 otherwise). Names are fully qualified for the same reason: a using directive
// would be legal, but the qualified form keeps the reference text and the declaration in
// two different files, which is the cross-file case.

[assembly: Surface.Attributes.Classes.AttrMark("the whole assembly")]
[assembly: Surface.Attributes.Classes.AttrRepeatable(1)]
[assembly: Surface.Attributes.Classes.AttrRepeatable(2)]

[module: Surface.Attributes.Classes.AttrMark("this module")]
[module: Surface.Attributes.Classes.AttrRepeatable(3)]

// 23.3: a reserved attribute at assembly scope, whose effect is on the compiler rather
// than on any declaration. Applied to the assembly, it makes every unmarked type in it
// implicitly non-CLS-compliant, so the warnings that do *not* appear are the evidence.
[assembly: System.CLSCompliant(false)]
