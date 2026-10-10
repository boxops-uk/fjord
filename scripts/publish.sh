#!/usr/bin/env bash
# Publish a release to crates.io and nuget.org — the one step the release
# workflow deliberately leaves to a person.
#
# A GitHub Release can be deleted and a registry version cannot, so
# `AGENTS.md`'s repository rules keep this off the tag. That rule is about *who
# decides*, not about how much of it is typed by hand: a script a person runs is
# still a person's decision, and the alternative — a sequence remembered in shell
# history — is how a live API key came to sit in `~/.bash_history` in plaintext.
#
# **Nothing happens without `--execute`.** The default is a rehearsal: it
# resolves the version, says what is already on each registry, packs what needs
# packing and runs `cargo publish --dry-run`, then stops before every upload.
#
# **Resumable, because half a publish is the normal failure.** crates.io takes
# the four crates in dependency order and any one of them can fail on the index
# settling; a version already live is skipped rather than retried, so re-running
# finishes the job instead of erroring on what already worked.
#
#     scripts/publish.sh                                  # rehearse — needs no key
#     read -rs CRATES_API_KEY && export CRATES_API_KEY    # typed at a prompt, so the
#     read -rs NUGET_API_KEY  && export NUGET_API_KEY     # key never enters history
#     scripts/publish.sh --execute                        # upload
#     scripts/publish.sh --only nuget --execute
#
# **Not `KEY=... scripts/publish.sh`.** A leading assignment is part of the command
# line, and the shell's history keeps the whole line — which is how a live key came to
# sit in `~/.bash_history` in the first place.
#
# `--allow-dirty` passes through to cargo, for a tree with edits that do not reach
# any packaged file.
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root"

execute=0
only=both
version=
allow_dirty=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --execute) execute=1 ;;
        --allow-dirty) allow_dirty=1 ;;
        --only) only=${2:?--only needs crates or nuget}; shift ;;
        --version) version=${2:?--version needs X.Y.Z}; shift ;;
        -h|--help) sed -n '2,31p' "${BASH_SOURCE[0]}" | sed 's/^# \?//'; exit 0 ;;
        *) echo "publish: unknown argument $1" >&2; exit 2 ;;
    esac
    shift
done

case "$only" in
    crates|nuget|both) ;;
    *) echo "publish: --only takes crates or nuget, not $only" >&2; exit 2 ;;
esac

# The four published crates, **in dependency order**. `fjord-db` re-exports the
# three below it, so a later one cannot be accepted until the earlier ones are on
# the index — which is also why a failure here is resumed rather than restarted.
crates=(fjord-schema fjord-wire fjord-client fjord-db)

staging=$(mktemp -d)
trap 'rm -rf "$staging"' EXIT

# crates.io asks for a user agent that identifies the caller and answers 403
# without one, which reads as an auth failure rather than as a missing header.
ua="fjord-publish (https://github.com/boxops-uk/fjord)"

if [[ -z "$version" ]]; then
    # The workspace version, from `[workspace.package]` and not from whichever
    # `version =` happens to come first — several crate manifests carry one.
    version=$(awk '/^\[workspace\.package\]/{s=1; next} /^\[/{s=0} s && /^version/{gsub(/[",]/,""); print $3; exit}' Cargo.toml)
fi
[[ -n "$version" ]] || { echo "publish: could not resolve the version" >&2; exit 1; }

tag="v$version"
echo "version   $version"
echo "mode      $([[ $execute == 1 ]] && echo 'EXECUTE — uploads are permanent' || echo 'rehearsal (pass --execute to upload)')"
echo "scope     $only"

# **What is published should be what was released.** A registry version cannot be
# withdrawn, so a mismatch between the tree and the tag is worth stopping for
# rather than discovering afterwards.
if git rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
    if [[ "$(git rev-parse "$tag^{commit}")" != "$(git rev-parse HEAD)" ]]; then
        echo "publish: HEAD is not $tag — check out the tag before publishing" >&2
        exit 1
    fi
    echo "tag       $tag, and HEAD is on it"
else
    echo "publish: no local tag $tag; fetch it or pass --version deliberately" >&2
    if [[ $execute == 1 ]]; then exit 1; fi
fi

# **A dirty tree matters only where it would change what is packaged.** `cargo
# publish` builds from the crate directories and `dotnet pack` from the client's,
# so an edit elsewhere in the repository cannot reach a registry — and refusing on
# it would mean stashing unrelated work to cut a release. cargo runs its own,
# correctly scoped refusal over the files it is about to package; this one names
# what is modified and leaves the judgement where it belongs.
dirty_flag=()
if [[ $allow_dirty == 1 ]]; then dirty_flag=(--allow-dirty); fi

dirty=$(git status --porcelain --untracked-files=no)
if [[ -n "$dirty" ]]; then
    echo "tree      modified, and cargo will refuse if any of it would be packaged:"
    sed 's/^/            /' <<<"$dirty"
    if [[ $allow_dirty == 1 ]]; then
        echo "            --allow-dirty given; cargo's own check is waived too"
    fi
fi
echo

# Is this version already on the registry? Answered before anything is uploaded,
# so a resumed run skips what worked rather than failing on it.
crate_is_live() {
    curl -fsS -A "$ua" "https://crates.io/api/v1/crates/$1/$2" >/dev/null 2>&1
}

nuget_is_live() {
    local id=${1,,}
    curl -fsS "https://api.nuget.org/v3-flatcontainer/$id/index.json" 2>/dev/null |
        grep -q "\"$2\""
}

# **The .NET packages version independently of the workspace, and of each other.**
# Lockstep was the arrangement until it was measured: one of the six published
# artifacts embeds a schema fingerprint, so a schema move is breaking for that one
# and invisible to the other five. Forcing them to re-release together is a cost
# with nothing on the other side — see `clients/dotnet/Directory.Build.props`.
#
# So a version is read per project: its own `<Version>` where it states one, and
# `Directory.Build.props`' default where it does not. The last match wins, which is
# how MSBuild resolves a property too.
dotnet_version() {
    local project=$1
    local own
    own=$(grep -oP '(?<=<Version>)[^<]+' "clients/dotnet/$project/$project.csproj" 2>/dev/null | tail -1)
    if [[ -n "$own" ]]; then
        echo "${own// /}"
        return
    fi
    grep -oP '(?<=<Version>)[^<]+' clients/dotnet/Directory.Build.props | tail -1 | tr -d ' '
}

publish_crates() {
    echo "── crates.io ──"
    if [[ $execute == 1 && -z "${CRATES_API_KEY:-}" ]]; then
        echo "publish: CRATES_API_KEY is not set" >&2
        exit 1
    fi

    local pending=0
    for crate in "${crates[@]}"; do
        if crate_is_live "$crate" "$version"; then
            echo "  $crate $version — already published, skipping"
            continue
        fi

        if [[ $execute == 0 ]]; then
            # **Only the lowest unpublished crate can be rehearsed.** A dry run
            # resolves dependencies from the registry, so `fjord-wire` cannot be
            # verified while the `fjord-schema` it names is still local-only.
            # Reporting that is the honest answer; failing on it would make a
            # rehearsal look like a broken release.
            if [[ $pending == 1 ]]; then
                echo "  $crate $version — cannot be rehearsed until the crates below it are live"
                continue
            fi
            echo "  $crate $version — would publish; rehearsing:"
            if ! cargo publish --locked --dry-run "${dirty_flag[@]}" \
                -p "$crate" 2>&1 | sed 's/^/    /'; then
                echo "publish: $crate would not be accepted as it stands" >&2
                exit 1
            fi
            pending=1
            continue
        fi

        echo "  $crate $version — publishing"
        # **The token goes in the environment, never in argv.** `--token` would
        # put it in this process's command line, where `ps` and any shell history
        # that captured the invocation can read it. `CARGO_REGISTRY_TOKEN` is
        # cargo's own name for it and is read without being echoed.
        CARGO_REGISTRY_TOKEN="$CRATES_API_KEY" \
            cargo publish --locked "${dirty_flag[@]}" -p "$crate"
    done
    echo
}

# The two NuGet packages come from **different places, on purpose**.
#
# `Boxops.Fjord.Indexer` is a release asset: CI built it, `SHA256SUMS` covers it
# and SLSA provenance names it, so pushing that file publishes the artifact that
# was actually attested. Repacking it here would publish a different build with
# the same version number.
#
# `Boxops.Fjord.Client` has no release asset — the workflow packs it only to
# prove it packs — so it is built here, from the tag the checks above pinned.
push_nuget() {
    echo "── nuget.org ──"
    if [[ $execute == 1 && -z "${NUGET_API_KEY:-}" ]]; then
        echo "publish: NUGET_API_KEY is not set" >&2
        exit 1
    fi

    # Each package's own number, which is what `dotnet pack` stamps into the file
    # name — so the release asset is named after the package version and not after
    # the tag, and the two are deliberately allowed to differ.
    local indexer_version client_version
    indexer_version=$(dotnet_version Boxops.Fjord.Indexer)
    client_version=$(dotnet_version Boxops.Fjord.Client)

    [[ -n "$indexer_version" && -n "$client_version" ]] || {
        echo "publish: could not resolve a version for the .NET packages" >&2
        exit 1
    }

    local indexer="Boxops.Fjord.Indexer.$indexer_version.nupkg"
    local client="Boxops.Fjord.Client.$client_version.nupkg"

    if nuget_is_live "Boxops.Fjord.Indexer" "$indexer_version"; then
        echo "  Boxops.Fjord.Indexer $indexer_version — already published, skipping"
        indexer=
    else
        echo "  Boxops.Fjord.Indexer $indexer_version — taking the attested release asset"
        gh release download "$tag" --pattern "$indexer" --pattern SHA256SUMS --dir "$staging"
        # The checksum is checked here rather than trusted, because this is the
        # one point where a release artifact becomes a permanent registry version.
        (cd "$staging" && grep " $indexer\$" SHA256SUMS | sha256sum -c -) || {
            echo "publish: $indexer does not match SHA256SUMS" >&2
            exit 1
        }
    fi

    if nuget_is_live "Boxops.Fjord.Client" "$client_version"; then
        echo "  Boxops.Fjord.Client $client_version — already published, skipping"
        client=
    else
        echo "  Boxops.Fjord.Client $client_version — packing from this checkout"
        dotnet pack clients/dotnet/Boxops.Fjord.Client -c Release -o "$staging" \
            --nologo -v quiet
        [[ -f "$staging/$client" ]] || {
            echo "publish: dotnet pack did not produce $client" >&2
            exit 1
        }
    fi

    for package in "$indexer" "$client"; do
        [[ -n "$package" ]] || continue
        if [[ $execute == 0 ]]; then
            echo "  would push $package ($(stat -c %s "$staging/$package") bytes)"
            continue
        fi
        echo "  pushing $package"
        # **The key is on this command line, and there is no way around it.**
        # `dotnet nuget push` reads no environment variable for it, and the
        # alternative — writing it into a NuGet.config — puts it on disk instead.
        # It is passed as a variable so it never reaches shell history; what it
        # does still reach is this process's argv for the length of the upload.
        #
        # **`--skip-duplicate`, or a resumed run stops one package short.** nuget.org
        # lists a version in `index.json` only once it has indexed it, minutes after
        # accepting the push — so a re-run inside that window reads the package it just
        # pushed as not live, pushes it again, gets 409, and `set -e` ends the script
        # before the package that actually failed. With the flag the 409 is a warning.
        dotnet nuget push "$staging/$package" \
            --source https://api.nuget.org/v3/index.json \
            --api-key "$NUGET_API_KEY" \
            --skip-duplicate
    done
    echo
}

[[ "$only" == "crates" || "$only" == "both" ]] && publish_crates
[[ "$only" == "nuget" || "$only" == "both" ]] && push_nuget

if [[ $execute == 1 ]]; then
    # The crates share the workspace version; the .NET packages were reported with
    # their own above, so this names the set rather than claiming one number for all
    # six artifacts.
    echo "published the crates at $version, and the .NET packages at the versions above."
    echo "Registry versions cannot be withdrawn; yank with"
    echo "  cargo yank --version $version -p <crate>    (which hides, it does not delete)"
else
    echo "rehearsal only — nothing was uploaded. Re-run with --execute."
fi
