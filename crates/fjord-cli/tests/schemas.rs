//! **The schemas this repository ships, and the reader that embeds them.**
//!
//! An integration test rather than a unit one because the claim spans the tool and the
//! files beside it: `schemas/` is what `fjord create --schema` is pointed at, what the
//! .NET clients state independently, and what `sample_schema` compiles in — so a change
//! to one of them that nothing here notices is a change somebody finds out about at a
//! handshake.

use fjord_schema::{fingerprint, syntax::resolve};

/// **The embedded reader follows imports**, and it is handed more than one file here so
/// that it is not only ever handed one.
///
/// `sample_schema::SOURCES` holds a single entry today, so nothing in the production
/// path exercises the multi-file case — and a resolving reader that has only ever seen
/// one file is a reader whose import handling is untested. This hands the same function
/// a genuine two-file set: an entry that declares nothing but an import, and the file
/// that answers it.
///
/// The two assertions are what would fail on the two plausible wrong implementations:
/// lowering only the entry (no `dep.Thing`, and `app.Use` unresolvable), and lowering
/// each file separately (`app.Use`'s reference could not resolve across the boundary).
#[test]
fn the_embedded_reader_follows_imports() {
    const ENTRY: &str = "schema app { import dep\n predicate Use : { of : dep.Thing } }";
    const DEP: &str = "schema dep { predicate Thing : string }";

    let resolved = resolve::resolve_from([("app.sigla", ENTRY), ("dep.sigla", DEP)])
        .expect("a two-file schema resolves");

    assert_eq!(resolved.files.len(), 2, "{:?}", resolved.files);

    let names: Vec<&str> = (0..resolved.schema.len())
        .filter_map(|index| {
            resolved
                .schema
                .get(fjord_schema::schema::PredicateId(index as u32))?
                .name()
        })
        .collect();

    // Sorted by qualified name, `app` before `dep` — both files' predicates, in one
    // schema, numbered together.
    assert_eq!(names, ["app.Use", "dep.Thing"]);
}

/// **The source layer breaks exactly one predicate, and it is `src.Line`.**
///
/// The flag day's whole claim, asserted rather than argued: eight predicates arrive, one
/// is deleted, and **every survivor is byte-identical**. A second broken name, or a
/// surviving predicate whose fingerprint moved, is a different change from the one that
/// was reviewed — which is the failure this is here to make loud rather than to leave to
/// somebody reading a diff of 34 predicates.
///
/// The eight is not nine: `src.File` **moves** from `code.sigla` into `src.sigla` rather
/// than arriving, and a `Predicate` carries no file for the canonical form to read, so it
/// is the same predicate in the same place with the same fingerprint.
#[test]
fn the_source_layer_breaks_exactly_one_predicate() {
    use fjord_schema::fingerprint::Compatibility;

    let root = std::path::PathBuf::from(concat!(env!("CARGO_MANIFEST_DIR"), "/../.."));

    // The shipped schema as it was before the source layer, kept as a golden copy: the
    // comparison has to be against what was *released*, not against whatever a previous
    // commit happens to hold.
    let before = resolve::resolve(
        &std::path::PathBuf::from(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/tests/schemas/code-before-src.sigla"
        )),
        &[],
    )
    .expect("the previous schema resolves");

    let after = resolve::resolve(&root.join("schemas/code.sigla"), &[root.join("schemas")])
        .expect("the shipped schema resolves");

    let (before, after) = (
        fingerprint::identity(&before.schema),
        fingerprint::identity(&after.schema),
    );

    match before.compatibility(&after) {
        Compatibility::Breaking { broken } => assert_eq!(
            broken,
            ["src.Line"],
            "the source layer was supposed to break exactly `src.Line`"
        ),
        other => panic!("expected Breaking, got {other:?} — `src.Line` should be gone"),
    }

    // Eight added, counted as the arithmetic rather than asserted as a total: 34 = 27
    // − 1 removed + 8.
    assert_eq!(
        after.predicates().len(),
        before.predicates().len() - 1 + 8,
        "the source layer added something other than eight predicates"
    );

    // **And every survivor is byte-identical**, which is the half `Breaking` alone does
    // not say: it names what broke and is silent about what did not.
    for (name, was) in before.predicates() {
        if name == "src.Line" {
            continue;
        }
        assert_eq!(
            after.predicates().get(name),
            Some(was),
            "`{name}` survived the source layer with a different fingerprint, so this is \
             not the additive change it was reviewed as"
        );
    }
}

/// **A schema split across files is fingerprint-identical to the same declarations in
/// one**, so the split W6 lands costs nothing at the predicate level.
///
/// The mechanism is in the tree rather than in luck: an alias is expanded at lowering,
/// so its name never reaches a `PredicateTy`, and a `Predicate` carries no file for the
/// canonical form to read. This is that stated as a test, because every later schema
/// file in `schemas/` rests on it.
#[test]
fn moving_a_declaration_into_an_imported_file_moves_no_fingerprint() {
    let together = resolve::resolve_from([(
        "one.sigla",
        "schema app { type Flag = { no : {} = 0 | yes : {} = 1 }\n \
         predicate P : { flag : Flag } }",
    )])
    .expect("one file resolves");

    let apart = resolve::resolve_from([
        (
            "entry.sigla",
            "schema app { import base\n predicate P : { flag : base.Flag } }",
        ),
        (
            "base.sigla",
            "schema base { type Flag = { no : {} = 0 | yes : {} = 1 } }",
        ),
    ])
    .expect("two files resolve");

    assert_eq!(
        fingerprint::of(&together.schema),
        fingerprint::of(&apart.schema),
        "moving a named type into an imported file moved the schema fingerprint"
    );
    assert_eq!(
        fingerprint::identity(&together.schema).predicates(),
        fingerprint::identity(&apart.schema).predicates(),
        "moving a named type into an imported file moved a predicate fingerprint"
    );
}

/// **The constants the .NET clients carry are the fingerprint `schemas/code.sigla`
/// actually has.**
///
/// A client sends one whole-schema number at the handshake and the server checks it for
/// equality, so a stale constant is a **refused connection** — and until this test the
/// first sign of one was somebody else's site. There is no mechanism that could keep the
/// two in step on its own: the number is deliberately *carried* rather than computed,
/// because a client that computed it would need this crate's fingerprint implementation
/// and then the two sides would agree by construction, which is the whole thing the
/// golden exists to avoid.
///
/// So it is a grep rather than a build. Two constants, restated independently on purpose
/// — `CodeIndex.cs`'s is what the indexer sends and `Program.cs`'s is what the demo sends
/// — and both are checked, because "we updated the client" has meant one of them before.
#[test]
fn the_dotnet_clients_carry_the_fingerprint_the_schema_has() {
    let root = concat!(env!("CARGO_MANIFEST_DIR"), "/../..");

    let resolved = resolve::resolve(
        &std::path::PathBuf::from(root).join("schemas/code.sigla"),
        &[std::path::PathBuf::from(root).join("schemas")],
    )
    .expect("the shipped schema resolves");
    let actual = format!("0x{:016x}", fingerprint::of(&resolved.schema));

    // Where each constant lives, and the line that declares it. Named rather than
    // globbed: a new file carrying a third copy should be a decision, and adding it here
    // is how that decision gets made.
    let carried = [
        (
            "clients/dotnet/Boxops.Fjord.Indexer/CodeIndex.cs",
            "public const ulong SchemaFingerprint = ",
        ),
        (
            "clients/dotnet/Boxops.Fjord.Demo/Program.cs",
            "const ulong SchemaFingerprint = ",
        ),
    ];

    for (file, declaration) in carried {
        let source = std::fs::read_to_string(std::path::PathBuf::from(root).join(file))
            .unwrap_or_else(|err| panic!("{file}: {err}"));

        let found: Vec<&str> = source
            .lines()
            .filter_map(|line| {
                let at = line.find(declaration)? + declaration.len();
                Some(line[at..].trim_end().trim_end_matches(';'))
            })
            .collect();

        assert_eq!(
            found.len(),
            1,
            "{file} should declare `SchemaFingerprint` exactly once, and declares it \
             {} times — if the constant moved, this test has to move with it",
            found.len()
        );

        assert_eq!(
            found[0], actual,
            "{file} carries a stale fingerprint. `schemas/code.sigla` is now {actual}, \
             so this client would be refused at the handshake. Re-paste it and follow \
             the rest of `clients/dotnet/README.md`'s flag-day checklist — the goldens \
             do not regenerate themselves."
        );
    }
}

/// **Every schema this repository ships has its fingerprint recorded here.**
///
/// A shipped schema's number is what a client carries as a constant, so an accidental
/// edit to one is a refused handshake at somebody's site — a red suite is cheaper. The
/// golden is regenerated by reading the failure, never by hand.
#[test]
fn every_shipped_schema_has_a_recorded_fingerprint() {
    let root = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas");

    let mut recorded: Vec<(String, String)> = std::fs::read_dir(root)
        .expect("the schemas directory")
        .map(|entry| entry.expect("a directory entry").path())
        .filter(|path| path.extension().is_some_and(|ext| ext == "sigla"))
        .map(|path| {
            let name = path
                .file_name()
                .expect("a file name")
                .to_string_lossy()
                .into_owned();
            let resolved = resolve::resolve(&path, &[std::path::PathBuf::from(root)])
                .unwrap_or_else(|reason| panic!("{name} does not resolve:\n{reason}"));

            (name, format!("{:#018x}", fingerprint::of(&resolved.schema)))
        })
        .collect();

    recorded.sort();

    let expected = [
        // Moved by the source layer, which is a flag day — see
        // `the_source_layer_breaks_exactly_one_predicate` and
        // `clients/dotnet/README.md`'s checklist.
        ("code.sigla", "0xe044df7620885507"),
        ("codemarkup.sigla", "0x32adb52110a42dcc"),
        ("config.sigla", "0xac3c414ab7ff574f"),
        ("demo.sigla", "0x026d61be0818f394"),
        ("msbuild.sigla", "0xbda9e53fc3c35113"),
        ("src.sigla", "0x0f2fe69be726b41d"),
    ];

    assert_eq!(
        recorded
            .iter()
            .map(|(name, hash)| (name.as_str(), hash.as_str()))
            .collect::<Vec<_>>(),
        expected,
        "a shipped schema's fingerprint moved, or a schema arrived without one"
    );
}
