//! The command tree — [operations §4](../../../website/content/operations.md).
//!
//! Common lifecycle verbs stay top-level because they are the daily drivers; admin
//! tooling nests one level. Every database-taking command is meant to accept any
//! address form from §2, so "local or remote" is a property of the *address* rather
//! than of the command — which is why there is no `--remote` flag anywhere here.
//!
//! # Every doc comment in this file is **product copy**
//!
//! clap renders them as `--help`, so they are read by someone who has the binary and
//! not the repository. They say what a flag does and what it costs, in the words that
//! person has; the *why* goes where AGENTS.md says every why goes — the design book,
//! and the module doc of the command that implements it. Markdown emphasis, a rustdoc
//! link, an invariant label or a path relative to this repository reaches a terminal
//! verbatim, where it resolves to nothing for the one person reading it.
//!
//! [`the_help_is_written_for_someone_who_has_only_the_binary`](tests::the_help_is_written_for_someone_who_has_only_the_binary)
//! is the guard, and it walks the whole tree: a design note added below fails it by
//! name rather than reaching a release.

use std::path::PathBuf;

use clap::{Parser, Subcommand, ValueEnum};

/// An immutable, embedded fact database.
#[derive(Debug, Parser)]
#[command(
    name = "fjord",
    version,
    long_about = None,
    after_help = AFTER_HELP,
)]
pub struct Cli {
    #[command(subcommand)]
    pub command: Command,

    /// Where databases live; the socket path is derived from it
    ///
    /// Also read from FJORD_DATA_DIR. Defaults to a directory under $XDG_DATA_HOME.
    #[arg(long, global = true, value_name = "PATH")]
    pub data_dir: Option<PathBuf>,

    /// A JSON config file holding `target` and `data_dir`
    ///
    /// Without this, ./fjord.json is read when it happens to be there — the working
    /// directory only, with no search of parent directories.
    #[arg(long, global = true, value_name = "PATH")]
    pub config: Option<PathBuf>,

    /// Where a schema's imports are looked for. Repeatable; first match wins
    ///
    /// Also read from FJORD_SCHEMA_PATH, separated the way PATH is. An entry file's
    /// own directory is always searched first, so a directory of schemas that import
    /// each other needs none of this.
    #[arg(long, global = true, value_name = "PATH")]
    pub schema_path: Option<Vec<PathBuf>>,

    /// Say more. Repeatable
    #[arg(short, long, global = true, action = clap::ArgAction::Count)]
    pub verbose: u8,
}

/// What a person who has only the binary still needs to be told.
///
/// The shape of the thing, and the one gap big enough to waste an afternoon looking
/// for: there is no file-import command, so a new user goes hunting for `insert` and
/// finds nothing. Named here rather than left to be discovered.
const AFTER_HELP: &str = "\
Databases live under a store root and are created against a schema. They start
Writable and are sealed with `finish`, after which they never change.

Facts are written by a producer speaking the wire protocol to `fjord serve`; there
is no import-from-a-file command yet. `fjord query` and `fjord shell` read.

Docs: https://boxops-uk.github.io/fjord/";

#[derive(Debug, Subcommand)]
pub enum Command {
    /// Run the server over a store root
    ///
    /// The server owns the store root while it runs: one process, and every database
    /// under it. Lifecycle commands find it on its socket and go through it.
    Serve {
        /// Where to bind. Defaults to <data-dir>/fjord.sock
        #[arg(long, value_name = "PATH")]
        socket: Option<PathBuf>,

        /// Also listen on TCP, at host:port
        ///
        /// Off unless you pass it: there is no config-file entry and no environment
        /// variable, so a port can only appear because somebody typed one.
        ///
        /// This opts in to reachability, not to access control. The handshake accepts
        /// anonymous connections, so whoever passes this owns the gateway in front of
        /// it.
        #[arg(long, value_name = "HOST:PORT")]
        listen_tcp: Option<String>,

        /// Write this file once the listener is accepting
        ///
        /// A signal to wait on rather than a sleep: anything that sees the file can
        /// connect. It is removed again when the server stops.
        #[arg(long, value_name = "PATH")]
        ready_file: Option<PathBuf>,

        /// Serve at most this many connections at once; refuse the rest
        ///
        /// Defaults to half the process's soft descriptor limit (ulimit -n). The other
        /// half is not spare — it is the store's files and the listeners. Without a cap
        /// a burst of connections takes every descriptor and the server is alive and
        /// unreachable, which looks exactly like a crash from outside.
        ///
        /// Past the cap a connection is told the server is busy and closed, so a client
        /// can back off rather than guess.
        #[arg(long, value_name = "N")]
        max_connections: Option<usize>,

        /// Commit a write stream's facts once per block instead of once per fact
        ///
        /// Faster to ingest, and a crash mid-ingest may cost the index. A fact's id is
        /// handed out before its bytes are durable, so a database may be left holding a
        /// reference to a fact that was never written. `finish` walks every reference
        /// and refuses to seal such a database: the cost is re-running the index, never
        /// a wrong answer from one that sealed.
        #[arg(long)]
        commit_per_block: bool,
    },

    /// Create a Writable database
    Create {
        /// The name to create. Creating the same name twice makes a second instance
        name: String,

        /// The schema to create it against
        ///
        /// An entry file, whose imports are resolved from its own directory and then
        /// --schema-path. Required, and frozen for the database's lifetime: this is the
        /// one moment the schema can be chosen, and it cannot be changed afterwards.
        #[arg(long, value_name = "FILE")]
        schema: PathBuf,
    },

    /// Write facts from JSONL files
    ///
    /// One JSON object per line: `{"id": "1", "predicate": "src.File", "fact": "a.cs"}`.
    /// A reference field carries the id of a fact written on an earlier line, and a
    /// forward reference is refused rather than held over.
    ///
    /// The simple way in, not the fast one. A producer writing at volume speaks the wire
    /// protocol through a client library; this is for a person or an agent writing a few
    /// facts by hand. Needs a running server.
    Write {
        /// The database to write to, as `name` or `name@instance`
        name: String,

        /// The files to read. Blank lines and lines starting with `#` are skipped
        #[arg(value_name = "FILE", required = true)]
        files: Vec<PathBuf>,
    },

    /// Seal a database: Writable to Complete, and immutable thereafter
    Finish {
        /// The database to seal, as `name` or `name@instance`
        name: String,

        /// Seal a database holding no facts
        ///
        /// Refused without this, because a silently-empty sealed artifact is the
        /// classic CI failure that looks like success.
        #[arg(long)]
        allow_zero_facts: bool,
    },

    /// List the databases in the store root
    List {
        /// How to print the listing
        #[arg(long, value_enum, default_value_t = Format::Table)]
        format: Format,
    },

    /// Write a database out as a portable file — every fact, in dependency order
    ///
    /// The same grammar `fjord write` reads, so an export can be written back. A
    /// reference carries the local id of a line written earlier, so the file is read in
    /// one forward pass and the ids it names are its own.
    ///
    /// Reads the store directly, so it needs a data directory no server is holding:
    /// stop the server first, or export from a copy.
    Export {
        /// The database to write out, as `name` or `name@instance`
        name: String,

        /// Where to write it
        #[arg(long, value_name = "PATH")]
        to: PathBuf,
    },

    /// Show a database's metadata and schema
    Describe {
        /// The database to describe, as `name` or `name@instance`
        name: String,

        /// How to print the description
        #[arg(long, value_enum, default_value_t = Format::Table)]
        format: Format,

        /// Print the embedded schema itself — the text `create --schema` would take
        #[arg(long)]
        schema: bool,
    },

    /// Run a query and print its rows
    ///
    /// Needs a running server: `fjord serve` starts one over the store root.
    Query {
        /// The database to query, as `name` or `name@instance`
        name: String,

        /// The query, for example: X where demo.Person X
        query: String,

        /// How to print the rows
        #[arg(long, value_enum, default_value_t = RowFormat::Table)]
        format: RowFormat,

        /// Give up after this many seconds, cancelling in band
        ///
        /// A deadline on this command's patience rather than a promise about the
        /// server: the cancel lands between rows, so a query stuck inside one chunk is
        /// stopped when that chunk ends.
        #[arg(long, value_name = "SECONDS")]
        timeout: Option<f64>,

        /// Stop after this many rows, cancelling the rest in band
        ///
        /// Not a LIMIT clause: the query is unchanged and the server does the work up
        /// to the point the cancel lands. What this bounds is what crosses the socket.
        #[arg(long, value_name = "N")]
        limit: Option<u64>,

        /// Print the row count and elapsed time to stderr, so it survives a pipe
        #[arg(long)]
        timing: bool,

        /// Report what the query examined, per step, to stderr
        ///
        /// The outcome to a plan's intent: a plan says which field narrowed the scan,
        /// and this says how many rows that came to.
        #[arg(long)]
        profile: bool,

        /// Print how many rows, and none of them
        ///
        /// The same plan and the same executor; the server counts instead of encoding.
        /// That is the part that costs, so this is much cheaper than piping the rows to
        /// wc -l.
        #[arg(long, conflicts_with_all = ["limit", "profile", "format"])]
        count: bool,

        /// Show the fact a reference names, instead of its id — recursively
        ///
        /// A row carries a reference as an id, like #3:7, because that is what one is
        /// once stored. This replaces each with the fact it names, and each reference
        /// in that with the fact it names, so {"to": "#3:7"} becomes
        /// {"to": {"module": ..., "name": "encode", "line": 12}}.
        ///
        /// Bare, it follows every reference to the end of the chain; with a number,
        /// that many hops. It costs one point read per distinct reference, so it is off
        /// unless asked for.
        #[arg(
            long,
            value_name = "HOPS",
            num_args = 0..=1,
            default_missing_value = "16",
            conflicts_with = "count"
        )]
        expand: Option<usize>,
    },

    /// An interactive REPL
    ///
    /// Needs a running server, and works over the socket like any other client. Type
    /// :help inside it for the shell's own commands — :type, :plan, :schema and the
    /// rest.
    Shell {
        /// The database to connect to, as `name` or `name@instance`
        database: String,
    },

    /// Read schemas as files, before any database holds one
    #[command(subcommand)]
    Schema(SchemaCommand),

    /// Administrative commands
    #[command(subcommand)]
    Db(DbCommand),
}

/// The three questions a schema can be asked away from a database
/// ([operations §5](../../../website/content/operations.md)).
///
/// All three take **files**, and `diff` takes a database name just as happily: what is
/// being compared is a schema, and where it was read from is the caller's business.
#[derive(Debug, Subcommand)]
pub enum SchemaCommand {
    /// Resolve a schema and report what it does not like
    ///
    /// Follows the imports, unions the blocks and lowers the result, which answers the
    /// three things a schema can be wrong about before anything writes a fact: an
    /// import nothing resolves, a syntax error in any file, and a redeclaration.
    Check {
        /// The entry file. Its imports are resolved from its own directory, then
        /// --schema-path
        file: PathBuf,
    },

    /// Print a schema's fingerprint, and each predicate's
    ///
    /// This is the number a client carries. A client holds what this prints rather
    /// than computing one, and a stale constant is refused at the handshake by name.
    Fingerprint {
        /// The entry file
        file: PathBuf,

        /// How to print the fingerprints
        #[arg(long, value_enum, default_value_t = Format::Table)]
        format: Format,

        /// Print the canonical form the fingerprint is taken over
        ///
        /// What a second implementation is written against, and what to diff when two
        /// ends disagree about a schema they believe they share.
        #[arg(long)]
        canonical: bool,
    },

    /// Print a schema resolved into one source, with its imports followed and inlined
    ///
    /// This is what `create` sends over the wire. Resolution reads files, so it happens
    /// where the files are rather than on the server.
    Compose {
        /// The entry file. Its imports are resolved from its own directory, then
        /// --schema-path
        file: PathBuf,
    },

    /// Compare two schemas: identical, compatible, or breaking
    ///
    /// Each side is a schema file or the name of a database in the store root, in any
    /// combination — comparing what a build would produce against what an artifact
    /// already holds is the question this is for.
    Diff {
        /// The schema file or database name to compare from
        before: String,

        /// The schema file or database name to compare to
        after: String,
    },
}

#[derive(Debug, Subcommand)]
pub enum DbCommand {
    /// Delete a database
    Rm {
        /// The database to delete, as `name` or `name@instance`
        name: String,

        /// Confirm the delete
        ///
        /// Required. This command never prompts, and without it nothing is deleted and
        /// the command fails.
        #[arg(short = 'y', long)]
        yes: bool,
    },
}

/// How to render output.
///
/// **Client-side, always.** The wire carries the binary format and the server never
/// produces JSON — a decision from the original brief, and the reason this is a flag
/// on the command rather than a field in a request.
#[derive(Debug, Clone, Copy, PartialEq, Eq, ValueEnum)]
pub enum Format {
    /// Aligned columns for a person
    Table,
    /// One JSON document, for a script
    Json,
}

/// How to render a query's rows.
///
/// Its own enum rather than [`Format`]'s, because the shapes a *result* wants are not
/// the shapes a listing wants: `raw` and `count` are meaningless for `list`, and the
/// distinction between a shape that streams and one that cannot is a property of
/// results alone.
#[derive(Debug, Clone, Copy, PartialEq, Eq, ValueEnum)]
pub enum RowFormat {
    /// Aligned columns for a person. The one shape that buffers
    Table,
    /// One JSON document, written incrementally
    Json,
    /// One JSON value per line (JSON Lines)
    ///
    /// The same values `json` writes without the array around them, which is what a
    /// consumer reading row by row wants and what a paged result has to be.
    Jsonl,
    /// Tab-separated fields, one row per line. Streams
    Raw,
    /// The row count and nothing else
    ///
    /// For measuring the server: rendering is this command's cost, and a throughput
    /// number that includes it is measuring the wrong process.
    Count,
}

#[cfg(test)]
mod tests {
    use clap::CommandFactory;

    use super::*;

    /// **`--expand`'s bare depth is [`fjord_client::FULL_DEPTH`]**, restated as a
    /// literal.
    ///
    /// clap needs `default_missing_value` as a string at attribute position, so the
    /// number is written twice: once in `fjord-client`, where the walk is, and once
    /// above. This is the check that they agree — the sort of drift nothing else would
    /// notice, since a wrong number here still expands, just not as far as the flag's own
    /// help says.
    ///
    /// Parsed rather than read off the `Arg`, so what is asserted is the value the
    /// command actually receives.
    #[test]
    fn the_bare_expand_depth_is_the_clients_full_depth() {
        let parsed = Cli::parse_from(["fjord", "query", "code", "F where src.File F", "--expand"]);

        let Command::Query { expand, .. } = parsed.command else {
            panic!("that is a query");
        };

        assert_eq!(
            expand,
            Some(fjord_client::FULL_DEPTH),
            "`--expand` with no number should follow a chain as far as the expander does"
        );

        // And with a number it is that number, which is the form the bare one defaults.
        let parsed = Cli::parse_from([
            "fjord",
            "query",
            "code",
            "F where src.File F",
            "--expand",
            "2",
        ]);
        let Command::Query { expand, .. } = parsed.command else {
            panic!("that is a query");
        };
        assert_eq!(expand, Some(2));

        // Absent is absent: ids, and no point reads.
        let parsed = Cli::parse_from(["fjord", "query", "code", "F where src.File F"]);
        let Command::Query { expand, .. } = parsed.command else {
            panic!("that is a query");
        };
        assert_eq!(expand, None);
    }

    /// Every command in the tree, and what it prints for `-h` and for `--help`.
    ///
    /// Rendered rather than read off the source, because what reaches a terminal is
    /// clap's work: the value names, the defaults and the possible values are printed
    /// too, and a marker could arrive from any of them.
    fn rendered_help() -> Vec<(String, String)> {
        let mut out = Vec::new();
        let mut pending = vec![(String::new(), Cli::command())];

        while let Some((path, mut command)) = pending.pop() {
            let path = match path.is_empty() {
                true => command.get_name().to_owned(),
                false => format!("{path} {}", command.get_name()),
            };

            for sub in command.get_subcommands() {
                pending.push((path.clone(), sub.clone()));
            }

            out.push((
                format!("{path} --help"),
                command.render_long_help().to_string(),
            ));
            out.push((format!("{path} -h"), command.render_help().to_string()));
        }

        out
    }

    /// **The help is read by someone who has the binary and not the repository.**
    ///
    /// Markdown emphasis, a rustdoc link, an invariant label and a path relative to
    /// this repository all render verbatim into a terminal, where none of them resolve
    /// to anything. They arrive the same way every time: a doc comment in this file is
    /// *both* rustdoc and product copy, and left to itself the design note wins.
    ///
    /// The markers rather than a style opinion, because a marker is the part a test can
    /// hold. Where a why is worth keeping it goes in the module doc above, which clap
    /// never renders.
    #[test]
    fn the_help_is_written_for_someone_who_has_only_the_binary() {
        const FORBIDDEN: &[(&str, &str)] = &[
            ("**", "Markdown emphasis"),
            ("[`", "a rustdoc link"),
            ("](", "a Markdown link"),
            ("../", "a path inside this repository"),
            ("crate::", "a Rust path"),
            ("ops-I", "an operational invariant's label"),
            (".md", "a file in this repository"),
            ("§", "a section of the design book"),
        ];

        for (command, help) in rendered_help() {
            for (marker, what) in FORBIDDEN {
                assert!(
                    !help.contains(marker),
                    "`fjord {command}` prints {what} (`{marker}`):\n{help}"
                );
            }

            // An invariant by number — `I13`, `(I9)` — which has no marker to match on
            // beyond the shape itself.
            let bytes = help.as_bytes();
            for (at, window) in bytes.windows(2).enumerate() {
                let labelled = window[0] == b'I' && window[1].is_ascii_digit();
                let starts_a_word = at == 0 || !bytes[at - 1].is_ascii_alphanumeric();

                assert!(
                    !(labelled && starts_a_word),
                    "`fjord {command}` prints an invariant by number:\n{help}"
                );
            }
        }
    }

    /// **Every argument says what it is**, including the positional ones.
    ///
    /// clap prints a positional with no help as a bare `<NAME>` under `Arguments:`,
    /// which tells a reader the shape of the command line and nothing about what to put
    /// there — and it is the failure mode nobody notices, because the flags around it
    /// are documented.
    #[test]
    fn every_argument_says_what_it_is() {
        let mut pending = vec![(String::new(), Cli::command())];

        while let Some((path, command)) = pending.pop() {
            let path = match path.is_empty() {
                true => command.get_name().to_owned(),
                false => format!("{path} {}", command.get_name()),
            };

            for sub in command.get_subcommands() {
                pending.push((path.clone(), sub.clone()));
            }

            // `help` is clap's own, and its wording is not ours to state.
            if command.get_name() == "help" {
                continue;
            }

            assert!(
                command.get_about().is_some(),
                "`fjord {path}` does not say what it does"
            );

            for argument in command.get_arguments() {
                assert!(
                    argument.get_help().is_some(),
                    "`fjord {path}` takes `{}` and does not say what it is",
                    argument.get_id()
                );
            }
        }
    }

    /// The one line a person who downloaded a binary and no documentation can act on.
    ///
    /// Pinned because it is the sort of line that reads as decoration to whoever is
    /// tidying the file next.
    #[test]
    fn the_root_help_says_where_the_documentation_is() {
        let help = Cli::command().render_long_help().to_string();

        assert!(
            help.contains("https://boxops-uk.github.io/fjord/"),
            "the help does not say where the documentation is:\n{help}"
        );

        // ...and the gap a new user otherwise spends an afternoon looking for.
        assert!(
            help.contains("no import-from-a-file command"),
            "the help does not say how facts get in:\n{help}"
        );
    }
}
