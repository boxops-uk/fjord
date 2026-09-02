using Boxops.Fjord.Client;

// A non-Rust producer writing facts to Fjord over the wire protocol.
//
// The point of this program is what it *does not* do: it holds no fact ids, keeps no
// map from entities to identities, and emits in whatever order it likes. Every
// reference it writes is the target fact itself, nested inline, and the server interns
// it. That is the whole reason a reference on the way in is a fact rather than an id —
// an indexer walking a syntax tree knows the file when it reaches the declaration, and
// should not have to remember what the server called it.

// One address rather than a socket and a name: `[where//]db[@instance]`, the same
// grammar the `fjord` CLI takes. `--socket` and `--database` still work, and compose
// into one.
const string DefaultSocket = "/tmp/fjord.sock";

var address = FjordAddress
    .Parse(Args("--at") ?? $"{Args("--socket") ?? DefaultSocket}//{Args("--database") ?? "code"}")
    .OrSocket(DefaultSocket);

// With `--golden <path>` this program connects to nothing: it encodes a fixed corpus
// and writes the bytes out, for the Rust client's test to compare itself against. See
// EmitGolden below for why that file exists.
var goldenPath = Args("--golden");
var unionGoldenPath = Args("--golden-unions");
var bytesGoldenPath = Args("--golden-bytes");

string? Args(string flag)
{
    var argv = Environment.GetCommandLineArgs();
    for (var i = 1; i < argv.Length - 1; i++)
    {
        if (argv[i] == flag)
        {
            return argv[i + 1];
        }
    }
    return null;
}

// ---- the schema, written down because a client must have it -------------------
//
// The transport codec sends no field names, no types and no arities: the server has
// them and so does this. Two rules are load-bearing, and the handshake below is what
// checks them rather than something failing later:
//
//   * a predicate's id IS its position here;
//   * a record's fields are in the schema's declared order, which is sorted by name,
//     because that order is part of the encoding.
//
// This mirrors `fjord::code_index` on the Rust side. It is written twice on
// purpose — that is the whole point of the fingerprint.

const uint File = 0;
const uint Decl = 1;
const uint Reference = 2;
const uint Span = 3;
const uint Extent = 4;
const uint Kind = 5;
const uint KindOf = 6;
const uint Resolves = 7;
const uint Digest = 8;
const uint Extends = 9;
const uint Note = 10;

// `schemas/demo.sigla`'s fingerprint, as `fjord schema check` prints it. Carried, not
// computed: a second implementation of the canonical form in every client is a port
// every future client pays for and a drift every one of them can cause.
const ulong SchemaFingerprint = 0x03678fcd1e7924e3;

// `{ line : int, col : int }`, which the schema names `Position` — a named type is
// structural, so the name does not enter the canonical form and this side need not have
// one for it.
var position = FjordType.Rec(("line", FjordType.Integer), ("col", FjordType.Integer));

// `{ data : string = 5 | func : int = 2 }`. **The tags are written down, not inferred
// from position**: a client numbering alternatives 0 and 1 would write `data` where the
// schema says `func`, and nothing on the wire would object.
var what = FjordType.OneOf(
    ("data", 5u, FjordType.String),
    ("func", 2u, FjordType.Integer));

// A union whose alternatives carry a payload of every kind: none, a reference, a
// reference to a *different* predicate, and a record.
var target = FjordType.OneOf(
    ("unresolved", 0u, FjordType.Rec()),
    ("decl", 1u, FjordType.Reference(Decl)),
    ("file", 2u, FjordType.Reference(File)),
    ("external", 3u, FjordType.Rec(
        ("name", FjordType.String),
        ("assembly", FjordType.String))));

// A single-alternative union, which needs the trailing `|` in the schema to be one at
// all — here it is just a union with one arm.
var rendered = FjordType.OneOf(("html", 0u, FjordType.String));

var schema = new FjordSchema([
    new FjordPredicate("code.File", FjordType.String, null),

    new FjordPredicate("code.Decl", FjordType.Rec(
            ("file", FjordType.Reference(File)),
            ("name", FjordType.String),
            ("line", FjordType.Integer)),
        FjordType.String),

    new FjordPredicate("code.Ref", FjordType.Rec(
        ("from", FjordType.Reference(Decl)),
        ("to", FjordType.Reference(Decl))), null),

    new FjordPredicate("code.Span", FjordType.Rec(
        ("decl", FjordType.Reference(Decl)),
        ("at", position)), null),

    new FjordPredicate("code.Extent",
        FjordType.Rec(("decl", FjordType.Reference(Decl))),
        FjordType.Rec(("from", position), ("to", position))),

    new FjordPredicate("code.Kind", FjordType.Rec(
        ("decl", FjordType.Reference(Decl)),
        ("what", what)), null),

    new FjordPredicate("code.KindOf", FjordType.Rec(
        ("what", what),
        ("decl", FjordType.Reference(Decl))), null),

    new FjordPredicate("code.Resolves", FjordType.Rec(
        ("at", FjordType.Integer),
        ("to", target)), null),

    new FjordPredicate("code.Digest",
        FjordType.Rec(("file", FjordType.Reference(File))),
        FjordType.Rec(("sha256", FjordType.Blob))),

    new FjordPredicate("code.Extends", FjordType.Rec(
        ("type", FjordType.Reference(Decl)),
        ("base", FjordType.Reference(Decl))), null),

    new FjordPredicate("code.Note", FjordType.Rec(
        ("decl", FjordType.Reference(Decl)),
        ("text", rendered)), null),
], SchemaFingerprint);

if (goldenPath is not null)
{
    EmitGolden(goldenPath);
    return;
}

if (bytesGoldenPath is not null)
{
    EmitBytesGolden(bytesGoldenPath);
    return;
}

if (unionGoldenPath is not null)
{
    EmitUnionGolden(unionGoldenPath);
    return;
}

Console.WriteLine($"connecting to {address}");
Console.WriteLine($"  our schema fingerprint {schema.Fingerprint:x16}");

using var connection = FjordConnection.Connect(
    address,
    schema,
    SessionMode.ReadWrite,
    // A claim, not a question: if the server's schema differs, the handshake refuses
    // before a byte of data flows.
    assertSchema: true);

Console.WriteLine($"  connected: protocol {connection.Hello.Version}, "
    + $"{connection.Hello.Predicates} predicates, schema {connection.Hello.SchemaFingerprint:x16}");
Console.WriteLine();

// ---- writing facts that hold no ids -------------------------------------------

// A declaration names its file, and a reference names two declarations — so a
// reference is two levels of nesting, and this program never learns what any of them
// were called.
FjordFact FileFact(string path) =>
    new(File, FjordValue.Of(path));

// Fields in the schema's order — file, name, line — with the signature on the value
// side. **The order is the schema's, not alphabetical**: it is part of the encoding, so
// a client that sorted them would produce bytes the server reads as a different fact.
FjordFact DeclFact(string path, string name, long line, string signature) =>
    new(Decl,
        FjordValue.Rec(
            FjordValue.Of(FjordRef.To(FileFact(path))),
            FjordValue.Of(name),
            FjordValue.Of(line)),
        FjordValue.Of(signature));

var declarations = new List<FjordFact>
{
    DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"),
    DeclFact("store/keys.py", "key_prefix", 48, "def key_prefix(row)"),
    DeclFact("store/keys.py", "key_successor", 77, "def key_successor(row)"),
    DeclFact("store/codec.py", "encode_key", 7, "def encode_key(k)"),
    DeclFact("store/codec.py", "CodecError", 31, "class CodecError"),
    DeclFact("query/plan.py", "Plan", 5, "class Plan"),
};

Console.WriteLine($"writing {declarations.Count} declarations, every reference nested");

var summary = connection.Write(Decl, declarations);

Console.WriteLine($"  created {summary.Created}, deduped {summary.Deduped} "
    + $"(of {summary.Seen} facts touched)");
Console.WriteLine($"  {declarations.Count} declarations + 3 files = 9 distinct facts");
Console.WriteLine();

// A reference: two references to one predicate, and both declarations it names are
// already written, so it dedups rather than creating a second copy.
var references = new List<FjordFact>
{
    new(Reference, FjordValue.Rec(
        FjordValue.Of(FjordRef.To(
            DeclFact("query/plan.py", "Plan", 5, "class Plan"))),
        FjordValue.Of(FjordRef.To(
            DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"))))),
};

// A nested *record* in a key, which is a different shape from a nested fact: the span
// is spliced into the key rather than interned as a fact of its own.
var spans = new List<FjordFact>
{
    new(Span, FjordValue.Rec(
        FjordValue.Of(FjordRef.To(
            DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"))),
        FjordValue.Rec(FjordValue.Of(12L), FjordValue.Of(4L)))),
};

var refs = connection.Write(Reference, references);
Console.WriteLine($"a reference between two declarations: created {refs.Created}, "
    + $"deduped {refs.Deduped} (both declarations were already there)");

var spanned = connection.Write(Span, spans);
Console.WriteLine($"a span, whose nested record is spliced into the key rather than "
    + $"interned: created {spanned.Created}, deduped {spanned.Deduped}");
Console.WriteLine();

// Writing the same block again writes nothing: interning is idempotent, which is what
// makes a retry after a dropped connection safe.
var again = connection.Write(Decl, declarations);
Console.WriteLine($"the same declarations again: created {again.Created}, deduped {again.Deduped}");
Console.WriteLine();

// ---- reading them back, on the same connection --------------------------------

void Run(string sigla)
{
    Console.WriteLine($"sigla> {sigla}");

    var result = connection.Query(sigla);
    Console.WriteLine($"  : {Describe(result.Shape)}");

    foreach (var row in result.Rows)
    {
        Console.WriteLine($"  {Render(row)}");
    }

    Console.WriteLine($"  {result.Rows.Count} row(s)");
    Console.WriteLine();
}

Run("F where code.File F");
Run("{at = D.line, what = D.name} where D = code.Decl _");
// The value side, which a query can read but never match on (I6).
Run("D.value where D = code.Decl _");

// The denial: declarations whose name does not start with `key`.
Run("N where code.Decl {name = N}; N != \"key\"..");

// Reaching through a reference — the join that makes a fact database worth having.
Run("{from = R.from.name, to = R.to.name} where R = code.Ref _");

// The span's nested-record key, read back.
Run("{line = S.at.line, col = S.at.col} where S = code.Span _");

try
{
    connection.Query("this is not sigla");
}
catch (FjordServerException error)
{
    Console.WriteLine($"a bad query fails its stream, by code: {error.Code}");
    Console.WriteLine($"  {error.ServerMessage.Split('\n')[0]}");
    Console.WriteLine();
}

// ...and the connection is still usable afterwards, which is the point of failing a
// stream rather than a connection.
Run("F where code.File F");

Console.WriteLine("done");

// ---- the golden corpus ---------------------------------------------------------
//
// Phase 9e's acceptance criterion is that the Rust and C# clients produce **byte
// identical** blocks for the same facts. Interoperating today does not prove that:
// the two could disagree about something the server happens to tolerate, or about a
// case neither demo exercises, and a fact file written by one would then not be the
// file the other writes.
//
// So this mode encodes a fixed corpus and writes the bytes out. `fjord-client`'s
// test reads the file, encodes the same facts from its own independent statement of
// the same schema, and compares. Neither side can be changed alone without the other
// noticing — which is the whole reason there is a second implementation at all.
//
// The corpus is chosen for what it *reaches* rather than for what it means: scalars,
// a value side, two levels of nesting, a record inside a key, two references to two
// different predicates, and integers on both sides of the varint's one-byte boundary.
void EmitGolden(string path)
{
    (string Name, uint Predicate, IReadOnlyList<FjordFact> Facts)[] blocks =
    [
        ("code.File", File, [FileFact("store/keys.py"), FileFact("query/plan.py")]),

        ("code.Decl", Decl,
        [
            DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"),
            // Zero, and a value past a single varint byte: zigzag is where a codec
            // that agrees on small numbers can still disagree.
            DeclFact("store/keys.py", "zero", 0, "def zero()"),
            DeclFact("query/plan.py", "Plan", 2147483648, "class Plan"),
            // A negative, for the other half of zigzag. A declaration is never on line
            // -1; this corpus is chosen for what it reaches, not for what it means.
            DeclFact("store/keys.py", "before", -1, "def before()"),
        ]),

        // **Two levels of nesting**: a reference names two declarations, and each names
        // its file.
        ("code.Ref", Reference,
        [
            new(Reference, FjordValue.Rec(
                FjordValue.Of(FjordRef.To(
                    DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"))),
                FjordValue.Of(FjordRef.To(
                    DeclFact("query/plan.py", "Plan", 5, "class Plan"))))),
        ]),

        // A reference followed by a **nested record** in the same key — spliced in
        // rather than framed, and neither client may quietly start framing it.
        ("code.Span", Span,
        [
            new(Span, FjordValue.Rec(
                FjordValue.Of(FjordRef.To(
                    DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"))),
                FjordValue.Rec(FjordValue.Of(12L), FjordValue.Of(4L)))),
        ]),

        // **A union leading a key**, with a tag that is neither 0 nor 1 and a payload
        // that differs per alternative. A client numbering alternatives by position
        // writes `data` where this says `func`.
        ("code.KindOf", KindOf,
        [
            new(KindOf, FjordValue.Rec(
                FjordValue.Alt(2u, FjordValue.Of(1L)),
                FjordValue.Of(FjordRef.To(
                    DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"))))),
            new(KindOf, FjordValue.Rec(
                FjordValue.Alt(5u, FjordValue.Of("class")),
                FjordValue.Of(FjordRef.To(
                    DeclFact("query/plan.py", "Plan", 5, "class Plan"))))),
        ]),

        // **A union with an empty payload, and one carrying a record** — the two shapes
        // an alternative can take that a scalar payload does not reach.
        ("code.Resolves", Resolves,
        [
            new(Resolves, FjordValue.Rec(
                FjordValue.Of(7L),
                FjordValue.Alt(0u, FjordValue.Rec()))),
            new(Resolves, FjordValue.Rec(
                FjordValue.Of(19L),
                FjordValue.Alt(3u, FjordValue.Rec(
                    FjordValue.Of("Deserialize"),
                    FjordValue.Of("serde"))))),
        ]),

        // **`bytes`, in a value side.** On this wire it costs its length and nothing
        // else — the storage codec's escaping is storage's business — so a client that
        // reused its string path would produce the same bytes here and the wrong ones on
        // disk. The payload holds a NUL and two UTF-8 continuation bytes, which no
        // `string` could carry.
        ("code.Digest", Digest,
        [
            new(Digest,
                FjordValue.Rec(FjordValue.Of(FjordRef.To(FileFact("store/keys.py")))),
                FjordValue.Rec(FjordValue.Of(
                    new byte[] { 0x00, 0x53, 0x80, 0xBF, 0xFF }.AsMemory()))),
        ]),

        // A **record value side**, which a scalar one does not reach: two nested records
        // behind the `->`.
        ("code.Extent", Extent,
        [
            new(Extent,
                FjordValue.Rec(FjordValue.Of(FjordRef.To(
                    DeclFact("query/plan.py", "Plan", 5, "class Plan")))),
                FjordValue.Rec(
                    FjordValue.Rec(FjordValue.Of(5L), FjordValue.Of(1L)),
                    FjordValue.Rec(FjordValue.Of(41L), FjordValue.Of(2L)))),
        ]),

        // **The same union after a reference rather than before it.** `code.Kind` and
        // `code.KindOf` hold the same pair in the two orders, so this pins that a tag
        // encodes the same wherever it sits in a key — which is not obvious, and is the
        // kind of thing one client could get right in one position and wrong in the
        // other.
        ("code.Kind", Kind,
        [
            new(Kind, FjordValue.Rec(
                FjordValue.Of(FjordRef.To(
                    DeclFact("store/keys.py", "key_of", 12, "def key_of(row)"))),
                FjordValue.Alt(2u, FjordValue.Of(1L)))),
        ]),

        // **`type` as a field name**, which the grammar allows and no other block here
        // reaches: a client that treated the schema's keywords as reserved would refuse
        // to state this predicate at all.
        ("code.Extends", Extends,
        [
            new(Extends, FjordValue.Rec(
                FjordValue.Of(FjordRef.To(
                    DeclFact("store/codec.py", "CodecError", 31, "class CodecError"))),
                FjordValue.Of(FjordRef.To(
                    DeclFact("query/plan.py", "Plan", 5, "class Plan"))))),
        ]),

        // **A single-alternative union**, whose tag is still written rather than elided:
        // one arm is not no arms, and a client that skipped the discriminant here would
        // produce a shorter run that decodes as something else.
        ("code.Note", Note,
        [
            new(Note, FjordValue.Rec(
                FjordValue.Of(FjordRef.To(
                    DeclFact("query/plan.py", "Plan", 5, "class Plan"))),
                FjordValue.Alt(0u, FjordValue.Of("<p>An ordered list of steps.</p>")))),
        ]),
    ];

    List<string> lines =
    [
        "# Blocks produced by the .NET client, as hex — Phase 9e's acceptance criterion.",
        "# `fjord-client` encodes the same facts and must produce the same bytes.",
        "#",
        "# Regenerate with ./clients/dotnet/emit-golden.sh. A diff here is either a",
        "# deliberate format change — in which case both clients move together — or a",
        "# divergence, which is the thing this file exists to catch.",
        $"schema-fingerprint {schema.Fingerprint:x16}",
    ];

    foreach (var (name, predicate, facts) in blocks)
    {
        var bytes = Block.Encode(schema, predicate, facts);
        lines.Add($"block {name} {predicate} {Convert.ToHexString(bytes).ToLowerInvariant()}");
    }

    // `System.IO.File`, spelled out: `File` is the predicate id above.
    System.IO.File.WriteAllLines(path, lines);
    Console.WriteLine($"wrote {blocks.Length} golden blocks to {path}");
}

// ---- the union golden (8.6) ------------------------------------------------------
//
// A **second** corpus, over a schema of its own. The shared fixture carries unions now
// and the golden above exercises them, so this is no longer the only place a tag is
// pinned — what it still does that the fixture cannot is push the *tag space*: 3, 0,
// 40000 and 7, declared in that order, where the fixture's tags are small and tidy. A
// client numbering alternatives by position, or truncating a varint, fails here and
// nowhere else.
//
// The schema is written down here and again in `fjord-client`'s test, and neither
// statement is derived from the other. That is the point: a shared statement would make
// the two encoders agree by construction.
void EmitUnionGolden(string path)
{
    const uint Thing = 0;
    const uint Tagged = 1;
    const uint Labelled = 2;

    // **The tags are not positions.** 3, 0, 40000 and 7, declared in that order: a
    // client numbering alternatives by position writes `num` where this says `text`,
    // and one truncating a varint cannot write 40000 at all.
    const uint Num = 3;
    const uint Text = 0;
    const uint ThingAlt = 40_000;
    const uint None = 7;

    // One union, used in a key field *and* on a value side, so the same alternatives go
    // through both paths.
    FjordType Alternatives() => FjordType.OneOf(
        ("num", Num, FjordType.Integer),
        ("text", Text, FjordType.String),
        ("thing", ThingAlt, FjordType.Reference(Thing)),
        ("none", None, FjordType.Rec()));

    var unionSchema = new FjordSchema(
        [
            new FjordPredicate("uni.Thing", FjordType.Rec(("id", FjordType.Integer)), null),
            // The union leads, which on the wire changes nothing and in storage changes
            // everything — stated the same way on both sides so the schemas match field
            // for field.
            new FjordPredicate("uni.Tagged", FjordType.Rec(
                ("what", Alternatives()),
                ("id", FjordType.Integer)), null),
            new FjordPredicate("uni.Labelled",
                FjordType.Rec(("id", FjordType.Integer)),
                Alternatives()),
        ],
        // Carried, not computed — `fjord schema fingerprint`'s job, and for a corpus
        // with no schema file of its own, `print_the_union_schema_fingerprint`'s.
        0x84c63c4ec408796eUL);

    FjordFact Thing_(long id) =>
        new(Thing, FjordValue.Rec(FjordValue.Of(id)));

    FjordFact Tagged_(FjordValue what, long id) =>
        new(Tagged, FjordValue.Rec(what, FjordValue.Of(id)));

    (string Name, uint Predicate, IReadOnlyList<FjordFact> Facts)[] blocks =
    [
        ("uni.Thing", Thing, [Thing_(1), Thing_(2)]),

        ("uni.Tagged", Tagged,
        [
            Tagged_(FjordValue.Alt(Num, FjordValue.Of(5L)), 10),
            Tagged_(FjordValue.Alt(Text, FjordValue.Of("a")), 20),
            // A nested reference **inside a payload** — the case a walk that stops at a
            // union misses, and the one that would leave a fact uninterned.
            Tagged_(FjordValue.Alt(ThingAlt, FjordValue.Of(FjordRef.To(Thing_(1)))), 30),
            // An alternative whose payload is the empty record, which is what an
            // alternative declared with no type comes to: zero bytes after the tag.
            Tagged_(FjordValue.Alt(None, FjordValue.Rec()), 40),
        ]),

        ("uni.Labelled", Labelled,
        [
            new(Labelled, FjordValue.Rec(FjordValue.Of(1L)),
                FjordValue.Alt(Num, FjordValue.Of(7L))),
            new(Labelled, FjordValue.Rec(FjordValue.Of(2L)),
                FjordValue.Alt(Text, FjordValue.Of("b"))),
        ]),
    ];

    List<string> lines =
    [
        "# Union blocks produced by the .NET client, as hex — Phase 8.6's half of 9e's",
        "# criterion. `fjord-client` encodes the same facts and must produce the same bytes.",
        "#",
        "# A schema of its own, so a tag past a varint byte costs the shared fixture",
        "# no flag day.",
        "# Regenerate with ./clients/dotnet/emit-golden.sh.",
        $"schema-fingerprint {unionSchema.Fingerprint:x16}",
    ];

    foreach (var (name, predicate, facts) in blocks)
    {
        var bytes = Block.Encode(unionSchema, predicate, facts);
        lines.Add($"block {name} {predicate} {Convert.ToHexString(bytes).ToLowerInvariant()}");
    }

    System.IO.File.WriteAllLines(path, lines);
    Console.WriteLine($"wrote {blocks.Length} golden union blocks to {path}");
}

void EmitBytesGolden(string path)
{
    const uint Digest = 0;
    const uint Blob = 1;

    // **Payloads no `string` could hold**, which is the whole reason the type exists:
    // a NUL, the storage codec's escape byte, and two UTF-8 continuation bytes. On
    // this wire they cost their own length and nothing else — the escaping is
    // storage's business — so a client that reused its string path would produce the
    // same bytes here and the wrong ones on disk. What this golden pins is that both
    // clients agree on the *length prefix and the run*, and that neither validates it.
    var bytesSchema = new FjordSchema(
        [
            new FjordPredicate("blob.Digest", FjordType.Rec(
                ("digest", FjordType.Blob),
                ("path", FjordType.String)), null),
            // A `bytes` value side as well as a key field, so the same run goes through
            // both paths.
            new FjordPredicate("blob.Blob",
                FjordType.Rec(("id", FjordType.Integer)),
                FjordType.Blob),
        ],
        // Carried, not computed — `print_the_bytes_schema_fingerprint`'s job on the
        // Rust side.
        0x7cf603845973ed44UL);

    FjordFact Digest_(byte[] digest, string path) =>
        new(Digest, FjordValue.Rec(FjordValue.Of(digest), FjordValue.Of(path)));

    (string Name, uint Predicate, IReadOnlyList<FjordFact> Facts)[] blocks =
    [
        ("blob.Digest", Digest,
        [
            // The empty run, which a length prefix of zero is the whole encoding of.
            Digest_([], "empty"),
            Digest_([0x00], "nul"),
            Digest_([0x00, 0xFF, 0xFF, 0x00, 0x80, 0xC0], "everything a string cannot"),
            Digest_([0xFF, 0xFF, 0xFF], "escape bytes"),
        ]),

        ("blob.Blob", Blob,
        [
            new(Blob, FjordValue.Rec(FjordValue.Of(1L)), FjordValue.Of(new byte[] { 0xED, 0xA0, 0x80 })),
            new(Blob, FjordValue.Rec(FjordValue.Of(2L)), FjordValue.Of(Array.Empty<byte>())),
        ]),
    ];

    List<string> lines =
    [
        "# `bytes` blocks produced by the .NET client, as hex. `fjord-client` encodes the",
        "# same facts and must produce the same bytes.",
        "#",
        "# A schema of its own, so a payload no `string` could hold costs the shared",
        "# fixture no flag day.",
        "# Regenerate with ./clients/dotnet/emit-golden.sh.",
        $"schema-fingerprint {bytesSchema.Fingerprint:x16}",
    ];

    foreach (var (name, predicate, facts) in blocks)
    {
        var bytes = Block.Encode(bytesSchema, predicate, facts);
        lines.Add($"block {name} {predicate} {Convert.ToHexString(bytes).ToLowerInvariant()}");
    }

    System.IO.File.WriteAllLines(path, lines);
    Console.WriteLine($"wrote {blocks.Length} golden bytes blocks to {path}");
}

static string Describe(FjordType type) => type switch
{
    FjordType.Int => "int",
    FjordType.Str => "string",
    FjordType.Bytes => "bytes",
    FjordType.Fact fact => $"fact({fact.Predicate})",
    FjordType.Record record =>
        "{" + string.Join(", ", record.Fields.Select(f => $"{f.Name} : {Describe(f.Type)}")) + "}",
    FjordType.Union union =>
        "{" + string.Join(" | ", union.Alternatives.Select(
            a => $"{a.Name} : {Describe(a.Type)} = {a.Disc}")) + "}",
    _ => "?",
};

static string Render(FjordValue value) => value switch
{
    FjordValue.Int n => n.Value.ToString(),
    FjordValue.Str s => $"\"{s.Value}\"",
    FjordValue.Bytes b => "0x" + Convert.ToHexString(b.Value.Span).ToLowerInvariant(),
    FjordValue.Ref { Value: FjordRef.Id id } => $"#{id.FactId >> 40}:{id.FactId & 0xFFFFFFFFFF}",
    FjordValue.Ref { Value: FjordRef.Nested nested } => $"<{Render(nested.Fact.Key)}>",
    FjordValue.Record record => "{" + string.Join(", ", record.Fields.Select(Render)) + "}",
    FjordValue.Union chosen => "{" + chosen.Disc + " = " + Render(chosen.Value) + "}",
    _ => "?",
};
