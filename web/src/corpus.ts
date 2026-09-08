/**
 * **The code index, in the page.**
 *
 * `wasm.ts` loads the engine; this loads the *corpus* — a real checkout walked by
 * Roslyn, exported by `fjord export` as a store image, fetched as a static asset and
 * put back into a `MemStore`. Nothing here interns a fact, because nothing compiled
 * to WebAssembly can: the ids were assigned once, offline, and travel with the rows.
 *
 * **The types come from the module, not from here.** `wasm-bindgen` writes the
 * `.d.ts` from the Rust, so `Blob`, `Definition`, `Reference` and `Hit` are the
 * Rust types with getters — re-exported rather than restated, because a second
 * statement of them is a second thing to keep in step.
 */
import init, {
  type Blob,
  type Definition,
  type Hit,
  type Reference,
  definitions,
  files,
  load_corpus,
  open,
  outline,
  references,
  search,
  xrefs,
} from './wasm/fjord_wasm.js'
import wasmUrl from './wasm/fjord_wasm_bg.wasm?url'

export type { Blob, Definition, Hit, Reference }

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
  open: (path: string) => Blob
  outline: (path: string) => Definition[]
  xrefs: (path: string) => Reference[]
  definitions: (symbol: string) => Definition[]
  references: (symbol: string) => Reference[]
  search: (prefix: string) => Hit[]
}

/**
 * Where the asset lives. Under the base, because a page here is a *path* and the
 * base it is served from is compiled in — `SITE_BASE`, which the Pages build sets
 * from the repository name.
 */
const base = import.meta.env.BASE_URL
const IMAGE = `${base}corpus/code.fjmem`
const SCHEMA = `${base}corpus/code.sigla`

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

    const [image, schema] = await Promise.all([
      fetch(IMAGE).then((response) => {
        if (!response.ok) {
          throw new Error(
            `no corpus at ${IMAGE} (${response.status}) — run scripts/build-corpus.sh`,
          )
        }
        return response.arrayBuffer()
      }),
      fetch(SCHEMA).then((response) => {
        if (!response.ok) throw new Error(`no schema at ${SCHEMA} (${response.status})`)
        return response.text()
      }),
    ])

    const loaded = JSON.parse(
      load_corpus(new Uint8Array(image), schema),
    ) as Loaded

    if (!loaded.ok) throw new Error(loaded.problem ?? 'the corpus was refused')

    return {
      rows: loaded.rows,
      fingerprint: loaded.fingerprint ?? '',
      files,
      open,
      outline,
      xrefs,
      definitions,
      references,
      search,
    }
  })()

  return corpus
}
