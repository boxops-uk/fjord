/**
 * **The code index, in the page.**
 *
 * `wasm.ts` loads the engine; this loads the *corpus* — a real checkout walked by
 * Roslyn, exported by `fjord export` as JSONL, fetched as a static asset and read back
 * into a `MemStore`. Nothing here interns a fact, because nothing compiled to
 * WebAssembly can: a reference in the file names an earlier line, so the load counts
 * out a sequence per predicate instead.
 *
 * **The types come from the module, not from here.** `wasm-bindgen` writes the
 * `.d.ts` from the Rust, so `Blob`, `Definition`, `Reference` and `Hit` are the
 * Rust types with getters — re-exported rather than restated, because a second
 * statement of them is a second thing to keep in step.
 */
import init, {
  type Blob,
  type Definition,
  type Entry,
  type FileRefs,
  type Hit,
  type Info,
  type PackageRef,
  type Project,
  type Reference,
  children,
  definitions,
  files,
  load_corpus_jsonl,
  open,
  outline,
  packages,
  project,
  references,
  refs,
  search,
  symbol_info,
  xrefs,
} from './wasm/fjord_wasm.js'
import wasmUrl from './wasm/fjord_wasm_bg.wasm?url'

export type { Blob, Definition, Entry, FileRefs, Hit, Info, PackageRef, Project, Reference }

/** What a load answered — the shape `fjord_inspect::corpus::Loaded` serialises to. */
type Loaded = {
  ok: boolean
  rows: number
  fingerprint: string | null
  problem: string | null
}

export type Corpus = {
  /** Rows in the store — the whole index, as one number. */
  rows: number
  /** The schema both sides agreed on, as `0x…`. */
  fingerprint: string
  files: () => string[]
  /** One directory's immediate entries. `''` is the root; a directory ends in `/`. */
  children: (prefix: string) => Entry[]
  /** What the build layer holds about a project, or `undefined` for any other file. */
  project: (path: string) => Project | undefined
  /** The packages that project asks for, with the range its file wrote. */
  packages: (path: string) => PackageRef[]
  open: (path: string) => Blob
  outline: (path: string) => Definition[]
  xrefs: (path: string) => Reference[]
  /** One file's references, flat, for hit-testing what is under the cursor. */
  refs: (path: string) => FileRefs
  definitions: (symbol: string) => Definition[]
  references: (symbol: string) => Reference[]
  search: (prefix: string) => Hit[]
  /** What a hover card needs, or `undefined` where this index only names the symbol. */
  info: (symbol: string) => Info | undefined
}

/**
 * Where the asset lives. Under the base, because a page here is a *path* and the
 * base it is served from is compiled in — `SITE_BASE`, which the Pages build sets
 * from the repository name.
 */
const base = import.meta.env.BASE_URL
const FACTS = `${base}corpus/corpus.jsonl`
const SCHEMA = `${base}corpus/corpus.sigla`

let corpus: Promise<Corpus> | null = null

/**
 * The corpus, loaded once and shared.
 *
 * **A refusal is thrown rather than swallowed.** The commonest cause is a schema
 * that moved since the asset was built, and an index that answers nothing looks
 * exactly like one that loaded — so the page has to be able to say which happened.
 */
export function loadCorpus(): Promise<Corpus> {
  corpus ??= (async () => {
    await init({ module_or_path: wasmUrl })

    const [facts, schema] = await Promise.all([
      fetch(FACTS).then((response) => {
        if (!response.ok) {
          throw new Error(
            `no corpus at ${FACTS} (${response.status}) — run scripts/build-corpus.sh`,
          )
        }
        return response.text()
      }),
      fetch(SCHEMA).then((response) => {
        if (!response.ok) throw new Error(`no schema at ${SCHEMA} (${response.status})`)
        return response.text()
      }),
    ])

    const loaded = JSON.parse(load_corpus_jsonl(facts, schema)) as Loaded

    if (!loaded.ok) throw new Error(loaded.problem ?? 'the corpus was refused')

    return {
      rows: loaded.rows,
      fingerprint: loaded.fingerprint ?? '',
      files,
      children,
      project,
      packages,
      open,
      outline,
      xrefs,
      refs,
      definitions,
      references,
      search,
      info: symbol_info,
    }
  })()

  return corpus
}
