# Porting libaom to C# (av1rt) — conventions

Read this whole document before porting anything. Every porter follows it so that code
written by different people in parallel compiles together and calls line up.

## 1. What we are porting

A **line-by-line port** of the libaom AV1 *encoder*, realtime path only, from `..\aom`
(sibling of this repo, read-only). The goal is that av1rt, given the same input and
settings, produces **byte-identical bitstreams** to the oracle build of libaom
(`tools/oracle`, see `docs/SUBSET.md`). That is how the port is verified, so fidelity
beats elegance everywhere under `src/Av1rt/Aom/`.

* Target: .NET 10, Native AOT compatible, **safe code only**.
* The subset (fixed configuration, speed 11, 8-bit 4:2:0, ...) is in `docs/SUBSET.md`.
* Which code actually runs in that subset is measured, not guessed: see §8.

A clean idiomatic public API is built later in `src/Av1rt/Api/` on top of the port.
Do not "improve" the port itself: no renames, no restructuring, no algorithm changes.

## 2. Files, classes, names

* C file `aom/<dir>/<stem>.c` → `src/Av1rt/Aom/<dir>/<stem>.c.cs`;
  header `<dir>/<stem>.h` → `src/Av1rt/Aom/<dir>/<stem>.h.cs`; `.inc` → `<stem>.inc.cs`.
* Everything lives in `namespace Av1rt.Aom;` (file-scoped). No sub-namespaces.
* Functions of `<stem>.c` and `<stem>.h` go into **one** `internal static partial class <stem>`
  (exact C stem, lowercase, e.g. `encodetxb`, `av1_common_int`, `blockd`). If the stem
  would clash with a member name or a C# keyword, append `_h` (e.g. class `mv` is fine).
* Each class is imported everywhere by one line in the **header's** file (or the `.c` file if
  there is no header), placed before the namespace:
  `global using static Av1rt.Aom.encodetxb;`
  so ported code calls `av1_write_coeffs_txb(...)` exactly as C does. Never repeat a
  `global using` that another file already declares.
* **Keep every C identifier verbatim**: functions, parameters, locals, struct names, fields,
  enum constants, macros. C# keywords get `@` (`@ref`, `@params`, `@base`, `@in`, `@out`,
  `@fixed`, `@checked`, `@object`, `@string`, `@event`, `@lock`, `@operator`, `@default`).
* C `static` (file-local) functions → `private static`; everything else → `internal static`.
  `static inline` functions in headers → `internal static` with
  `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
* Types (structs, enums, delegates) are top-level types in the namespace, not nested.
* Keep the C order of functions and keep libaom's comments that explain *why*; drop
  boilerplate and license headers (one line `// Port of libaom <path>` at the top is enough).

## 3. Types

### 3.1 Scalars

| C | C# |
|---|---|
| `uint8_t`, `unsigned char` | `byte` |
| `int8_t`, `char` used as number | `sbyte` |
| `uint16_t` / `int16_t` | `ushort` / `short` |
| `int`, `int32_t` | `int` |
| `unsigned int`, `uint32_t` | `uint` |
| `int64_t` / `uint64_t` | `long` / `ulong` |
| `size_t`, `ptrdiff_t`, `intptr_t` | `int` (use `long` only where values can exceed 2^31) |
| `bool` (stdbool) | `bool` |
| `double` / `float` | `double` / `float` |
| `tran_low_t` / `tran_high_t` | `int` / `long` (high bitdepth is compiled out) |
| `aom_cdf_prob` | `ushort` |

Keep C `int` flags as `int` (do **not** turn `int skip` into `bool`): it avoids casts and
keeps arithmetic like `ctx += skip` identical. Use `B(cond)` (from `CRuntime`) for `cond ? 1 : 0`.

### 3.2 Enums and scalar typedefs

C# casts between enums and ints would litter the port, so **every C enum type and every
scalar typedef becomes an `int` alias**, and the enumerators become `const int`:

```csharp
global using BLOCK_SIZE = int;          // in the owning header's file, before the namespace
...
internal static partial class enums {
    internal const int BLOCK_4X4 = 0;  ...
}
```

This applies to `BLOCK_SIZE, TX_SIZE, TX_TYPE, PREDICTION_MODE, UV_PREDICTION_MODE,
MV_REFERENCE_FRAME, PARTITION_TYPE, InterpFilter, FRAME_TYPE, TX_MODE, ...` and to
encoder-side enums (`RATE_CONTROL ...`, speed-feature enums, ...). A C typedef of a
pointer or struct is not an alias (see 3.3). If the C storage type matters for a value
(e.g. a `uint8_t` field that wraps), cast explicitly where C would truncate.

### 3.3 Structs: class or struct?

* **Small** C structs (roughly ≤ 64 bytes, no big arrays) that C copies by value, stores in
  arrays, or saves/restores (`struct buf_2d orig = pd->dst; ... pd->dst = orig;`)
  → C# `struct`. Examples: `MV`, `FULLPEL_MV`, `int_mv`, `CANDIDATE_MV`, `POSITION`,
  `buf_2d`, `TileInfo`, `RD_STATS`, `int_interpfilters`, `scale_factors`, `FULLPEL_MV_STATS`.
  Taking their address becomes `ref` (`ref buf_2d dst = ref pd.dst;`).
* **Large** structs that are used through pointers (`AV1_COMP`, `AV1_COMMON`, `MACROBLOCK`,
  `MACROBLOCKD`, `MB_MODE_INFO`, `FRAME_CONTEXT`, `RATE_CONTROL`, `SPEED_FEATURES`,
  `YV12_BUFFER_CONFIG`, `RefCntBuffer`, `PC_TREE`, `PICK_MODE_CONTEXT`, `ThreadData`,
  `TileDataEnc`, ...) → C# `sealed class`. Embedded sub-structs that are themselves large
  are classes too, created in the constructor (`public readonly FeatureFlags features = new();`
  is wrong if C ever assigns the whole member; then use a non-readonly field).
* If C ever copies a class-mapped struct by value (`*a = *b`, `memcpy(a, b, sizeof(*a))`,
  `x = *y` of that type), give the class `public void CopyFrom(T other)` that copies **every**
  field (deep-copying arrays, `CopyFrom` on nested classes) and use it at those sites.
  Grep the in-scope C code for such copies when deciding.
* Fields are **fields, never properties**, so `cm.features.allow_x = 1` and `ref` work.
* Unions: if C type-puns (e.g. `int_mv` read as `as_int` and `as_mv`), use
  `[StructLayout(LayoutKind.Explicit)]` with `[FieldOffset]` (unmanaged fields only).
  If only one member is ever used in the subset, keep just that member.
* Bitfields → plain fields of the declared type.
* Fixed arrays inside a **struct** → `ArrN<T>` from `InlineArrays.cs` (value semantics).
  Fixed arrays inside a **class** → either `ArrN<T>` (preferred when the class has a
  `CopyFrom`, since it copies by value) or a `T[]` allocated in the constructor.
  Multi-dimensional arrays inside classes → jagged arrays (`T[][]`) so `a[i][j]` reads like C.
* Arrays of struct pointers / grids (`MB_MODE_INFO **mi_grid_base`) → `MB_MODE_INFO[]` of
  references, with pointers into them as `Ptr<MB_MODE_INFO>` (see §4).
* Function pointers → a `delegate` type with the C typedef name, fields of that type;
  create delegates once (static readonly), never per call.

### 3.4 Constants and tables

* `#define NAME value` and `static const` scalars → `internal const` in the header's class.
  Use the narrowest C#-legal type that keeps C arithmetic the same (usually `int`).
* `static const T tbl[]` → `internal static readonly T[] tbl = { ... };`
  (or `private static ReadOnlySpan<byte> tbl => new byte[] { ... };` for hot 1-D primitive
  tables). Multi-dimensional tables → jagged arrays, preserving C indexing. Generate big
  tables with a script rather than by hand (see `tools/gen/`); never retype numbers manually.
* Never define a constant or table that belongs to another header — reference it.

## 4. Pointers

* Pointer to a class instance → the reference. Pointer to a value struct → `ref` (`in` for
  `const T *` parameters). Out-parameters `T *out` of scalars → `ref T` (never `out`, so the
  signature can be derived mechanically from the C prototype).
* **Any pointer into an array of primitives or structs → `Ptr<T>`** (`src/Av1rt/Aom/Ptr.cs`): an
  array plus offset with C semantics — `p[i]`, `p[-1]`, `p + n`, `p - q`, `p += stride`,
  `p.Value` for `*p`, `p.IsNull`. This is mandatory for every function parameter and struct
  field whose C type is `T *` / `const T *` with T a primitive (e.g. `uint8_t *dst` →
  `Ptr<byte> dst`, `tran_low_t *coeff` → `Ptr<int> coeff`, `const int16_t *` → `Ptr<short>`),
  so callers and callees agree without coordination. `const` is dropped.
* Inside a function body you may turn a `Ptr<T>` into a `Span<T>` (`p.AsSpan(n)`) for loops,
  `CopyTo`, `Fill` and SIMD. Local scratch buffers that the C code declares on the stack
  (`DECLARE_ALIGNED(16, uint8_t, buf[N])`):
  * leaf functions (do not recurse and are not re-entered while the buffer is live) may use a
    `[ThreadStatic] private static T[] s_name;` lazily allocated once per thread;
  * otherwise allocate (`new T[N]`) and mark `// TODO(perf): per-call allocation`;
  * `stackalloc` into a `Span<T>` is fine when the buffer never needs to become a `Ptr<T>`.
* `memcpy/memmove/memset` → `CRuntime.memcpy/memmove/memset` with **element counts**, or span
  `CopyTo` / `Fill` / `Clear`. `aom_malloc/aom_calloc/aom_memalign` → `new T[n]`
  (zeroed); `aom_free(p)` → `p = null` (or nothing).
* Reinterpreting casts (`(uint16_t *)buf8`) — only for high bitdepth, which is compiled out.
  If one remains, use `MemoryMarshal.Cast` on spans.

## 5. C semantics you must preserve

* **Mixed signed/unsigned arithmetic.** In C, `int op unsigned` is done in `unsigned`; in C#
  `int op uint` is done in `long`. Cast to reproduce C exactly:
  `(uint)a + b`, `(int)(x - y)`. Watch comparisons like `if (u < i)` and `(unsigned)x < n`
  range checks (C# needs `(uint)x < (uint)n`).
* `uint8_t`/`int16_t` arithmetic promotes to `int` in both languages, but assigning back
  needs an explicit cast in C# — that cast must truncate the same way C does.
* Shifts: C# masks the shift count (`x << 33 == x << 1`); C does not. Shifting negative
  values right is arithmetic in both. `1 << n` with n ≥ 31 must be `1L << n` if C used a
  64-bit type.
* Integer overflow wraps in C# (unchecked by default); keep it that way. Integer division
  and `%` truncate toward zero in both.
* Floating point: keep `double` vs `float` exactly as C declares; keep the operation order;
  `(int)` truncation is the same. Use `Math.Pow/Log/Exp/Sqrt` (they call the CRT on Windows).
  `log2` → `Math.Log2`, `floor/ceil/fabs/round` → `Math.Floor/Ceiling/Abs/Round` — but C's
  `round` rounds half away from zero: use `Math.Round(x, MidpointRounding.AwayFromZero)`.
* C's `x ? a : b` with mixed int/unsigned also converts; keep the C result type.
* `assert(...)` → `Debug.Assert(...)`.
* `aom_internal_error(...)` / `aom_internal_error_copy` → `throw new AomException(code, msg)`.
* `goto` is allowed (C# supports it within a method); labels keep their C names.
* Function-local `static` variables → `private static` fields (note thread-safety).

## 6. Configuration and pruning

The build is configured like the oracle (`.oracle/build/config/aom_config.h`):
`CONFIG_REALTIME_ONLY=1`, `CONFIG_AV1_HIGHBITDEPTH=0`, `CONFIG_AV1_TEMPORAL_DENOISING=0`,
`CONFIG_MULTITHREAD=1`, `CONFIG_QUANT_MATRIX=1`, `CONFIG_DENOISE=1`,
`CONFIG_DISABLE_FULL_PIXEL_SPLIT_8X8=1`, `CONFIG_LIBYUV=1`, and **0** for every debug /
stats / timing / ML / TFLITE / TUNE_* / BITSTREAM_DEBUG / RD_DEBUG / MISMATCH_DEBUG /
COLLECT_* / ENTROPY_STATS / FPMT_TEST / PARTITION_SEARCH_ORDER / RT_ML_PARTITIONING /
SALIENCY_MAP / THREE_PASS / BITRATE_ACCURACY / OUTPUT_FRAME_SIZE flag.
Resolve every `#if` accordingly and port only the live branch. Drop all high-bitdepth code
(`is_cur_buf_hbd`, `highbd_*`, `CONVERT_TO_*`) — the subset is 8-bit only. Drop all SIMD
(`*_sse2`, `*_avx2`, `*_neon`, ...) — port the `_c` versions.

**RTCD.** C code calls dispatched names (`aom_sad16x16`, `av1_inv_txfm2d_add`, ...) declared in
`aom_dsp_rtcd.h` / `av1_rtcd.h` / `aom_scale_rtcd.h`. Port the `_c` function verbatim
(`aom_sad16x16_c`) and add the dispatched name next to it as a one-line forwarder
(`internal static uint aom_sad16x16(...) => aom_sad16x16_c(...);`). Callers always use the
dispatched name. SIMD versions come later behind these forwarders.

**Pruning by measurement.** `.oracle/cov/` holds line coverage of the oracle running the
subset on varied content (4K/1080p/odd sizes/352x288, 1–8 threads, high/low bitrate, forced
key frames, frame dropping, desktop content):

* `.oracle/cov/summary/functions.txt` — every executed function with its line range;
  `unexecuted.txt` — compiled but never executed functions in the same files;
* `.oracle/cov/show/coverage/C/Users/LOST/Projects/Media/aom/<path>.txt` — the source with an
  execution count on every line (`0` = never ran).

Rules:
1. Port every **executed** function. Do not port functions that never ran, unless they are
   trivially small and obviously reachable from data-dependent branches (say why in a comment).
2. Inside an executed function, a branch that never ran is either
   * **configuration-dependent** (speed features, `oxcf` settings, `use_svc`, `mode != REALTIME`,
     screen content, high bitdepth, ...) whose condition is constant in the subset →
     drop it, leaving `// [subset] <what was dropped and why>`; or
   * **data-dependent** (depends on pixels, motion, bitrate, frame size, buffer state) →
     port it. When unsure, port it.
   For a dropped branch that should be impossible, add `Debug.Assert(false, "...")` or
   `throw new NotSupportedException(...)` rather than silently doing something else.
3. Fields of structs: port the fields that in-scope code touches, plus anything cheap that a
   later porter will obviously need. Fields that only feed dropped features may be omitted.

## 7. Safe code, AOT, performance

* No `unsafe`, no pointers, no `fixed`, no `stackalloc` into pointers, no
  `System.Runtime.CompilerServices.Unsafe`, no `MemoryMarshal.GetReference/CreateSpan/
  GetArrayDataReference`, no `Marshal`. `MemoryMarshal.Cast`, `BinaryPrimitives`,
  `Span<T>`, `Vector128/256` with `Create(span)` / `CopyTo(span)` are fine.
* No reflection, no `dynamic`, no LINQ in encoder code, no `List<T>` in place of C arrays.
* Hot paths (per block / per pixel) must not allocate per call (see §4 scratch rules).
* Don't optimise yet beyond that; correctness first. SIMD comes after bit-exactness.

## 8. Workflow (phase 2 onwards)

* You work in your own **git worktree** (a full checkout on its own branch). Run
  `dotnet restore src/Av1rt/Av1rt.csproj` once, then build normally:
  `dotnet build src/Av1rt/Av1rt.csproj`. The tree builds with 0 errors at the start; keep it so.
* Paths outside your worktree (not committed, so only in the main checkout):
  * libaom sources (read-only): `C:/Users/LOST/Projects/Media/aom`
  * coverage: `C:/Users/LOST/Projects/Media/av1rt/.oracle/cov/` (`summary/functions.txt`,
    `summary/unexecuted.txt`, `show/coverage/C/Users/LOST/Projects/Media/aom/<path>.txt`)
  * pure-C libaom build: `C:/Users/LOST/Projects/Media/av1rt/.oracle/build`
    (generated `config/aom_dsp_rtcd.h`, `config/av1_rtcd.h`)
  * test clips: `C:/Users/LOST/Projects/Media/av1rt/.testdata`
* **Stubs.** `src/Av1rt/Aom/_stubs/<dir>_<name>_c.cs` holds placeholders for functions of
  `<dir>/<name>.c` that already-ported code calls. When you port `<name>.c`, delete its stub
  file and make your real signatures match how the callers use them. If a stub signature was
  wrong, fix the call site (a minimal edit in someone else's file is allowed for this; list it in
  your report). If you call a function owned by another package that has no stub yet, add it to
  that C file's stub file (create it in the same format if missing), derived mechanically from
  the C prototype per §4.
* **Missing fields.** Every port type is `partial`. If a shared type lacks a field you need,
  add it in `src/Av1rt/Aom/_fields/<you>.cs`
  (`internal partial class MACROBLOCK { internal int foo; }`) rather than editing the type's
  own file. If the type has a `CopyFrom`, say so in your report so the copy can be extended.
* **Tests are part of the job** for self-contained functions (DSP kernels, transforms,
  quantisers, filters, context derivation, table builders): differential tests against libaom's
  C code through a shim, see `docs/TESTING.md`. Name your shim after your package id.
* Never touch files owned by another package beyond the minimal call-site fixes above.
* Mark anything uncertain with `// PORT-NOTE: ...`.
* At the end, `git add -A` and `git commit` on your branch (message: what you ported). Final
  report: branch name, files written, functions ported, functions deliberately skipped (and
  why), stubs deleted/added, fields added, edits to others' files, test results, open questions.
