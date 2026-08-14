# Classic AmigaOS NDK coverage

## Pinned baseline

`amiga::raw` targets the **Native Developer Kit for AmigaOS 3.9**, the 2001
classic 68k distribution whose README identifies it as based on the NDK 3.5
and Developer CD 2.1, with updated headers and SFD files.

The authoritative input is the NDK selected by `ndk_path` (currently
`/Users/barry/amiga-cc/NDK3.9`) with this layout:

- `README`
- `Include/sfd`
- `Include/fd`
- `Include/include_h`
- `Documentation/Autodocs` for API documentation

The manifest records a SHA-256 digest over the README and every SFD, FD, and
public header, so a different header pack cannot silently pass as this
baseline. Compiler-specific duplicate façades under `clib`, `defines`,
`inline`, `pragma`, `pragmas`, and `proto` are excluded from header symbol
inventory; `clib/alib_protos.h` is separately inventoried as `amiga.lib`.

No OS4 surface is included. No third-party header pack is mixed into the core
inventory. CAMD is not present in this NDK distribution and therefore is not a
core-baseline interface.

## Verified coverage

The checked-in machine-readable manifest is
[`Novus/std/amiga/raw/ndk_coverage.json`](../Novus/std/amiga/raw/ndk_coverage.json).
It accounts for every inventoried symbol as directly supported, represented by
a native Novus equivalent, or not applicable C preprocessor syntax.

| Category | Count | Accounting |
| --- | ---: | --- |
| Functions | 1,397 | 1,397 directly bound |
| Constants, flags, tags, and enum values | 7,016 | 7,016 directly bound |
| ABI aggregates and typedefs | 812 | 778 direct layouts/aliases; 34 native Novus ABI equivalents |
| Meaningful and C-only macros | 338 | 328 Novus equivalents; 10 not-applicable C syntax macros |
| **Total symbols** | **9,563** | **100% accounted** |

The inventory contains 112 interface records: 66 callable libraries, 25
device interfaces/header contracts, 20 resource interfaces/header contracts,
and the static `amiga.lib` support interface. Of the symbols, 8,123 are core
NDK and 1,440 are NDK-shipped ReAction surface. Opaque and forward-declared
aggregates referenced by public structures and callable signatures are counted
even when the NDK intentionally publishes no layout.

Before the verifier and gap work, 73 authoritative callable functions had no
raw binding: all 47 `amiga.lib` support calls and all 26 FD-only
`hdwrench.library` calls. Reconstructing the initial inventory gives
1,324/1,397 functions (94.8%). The current result is 1,397/1,397 (100%). No
percentage was claimed before the authoritative inventory existed.

## Special interface handling

- `amiga.lib` is a static support library. Its raw declarations have no
  `@library` metadata, create no library base, and are extracted by the linker
  only when referenced.
- `hdwrench.library` is the only public FD-only callable interface in this NDK.
  `Novus/ndk_overlays/hdwrench_lib.sfd` supplies typed declarations for the
  existing SFD generator, derived from the official FD, C header, pragmas, and
  autodoc. The official files remain the coverage authority.
- IORequest-only devices and structure-only resources do not acquire invented
  callable APIs. Their exact structures and constants live in
  `amiga::raw::structs` and `amiga::raw::consts`; operations use the documented
  Exec device/resource calls.
- Library bases are opened only for reachable raw library calls. The inventory
  does not make optional libraries, devices, resources, or ReAction classes
  mandatory startup dependencies.

## Macro classifications

The following are C declaration/preprocessor syntax with no runtime API
meaning in Novus: `CONST`, `EXTERN`, `FOREVER`, `GLOBAL`, `IMPORT`, `REGISTER`,
`STATIC`, `VOID`, `VOLATILE`, and `__CLIB_PROTOTYPE`.

All other inventoried object-like and function-like macros are listed in the
manifest as `NOVUS_EQUIVALENT` with their source header and original
definition. These cover field access, calculations, tag construction,
initializers, `sizeof`, call aliases, and hardware conveniences that Novus
expresses with its normal field, expression, call, tag, and `@sizeof` forms.

## Extensions excluded from core coverage

- `amiga::raw::amissl` — AmiSSL third-party extension
- `amiga::raw::bsdsocket` — Roadshow-compatible third-party extension
- `amiga::raw::mui_tags` — MUI third-party extension
- `amiga::raw::reaction_tags` — hand-maintained convenience aliases over the
  separately counted `NDK_REACTION` surface

The manifest also classifies individual Novus support symbols that live in a
core module but are not literal declarations at that location:
`InternalLoadSegFree`, the named form of an anonymous DOS callback signature;
`WA_SIZE_UNLIMITED`, a named sentinel for an unconstrained window dimension;
and the legacy `exec::BeginIO` compatibility declaration, whose canonical NDK
owner is `amiga.lib`.

## Verification and documentation

Regenerate and verify against an installed copy of the pinned NDK:

```sh
novusc verify-ndk --update --ndk-path /path/to/NDK3.9
novusc verify-ndk --ndk-path /path/to/NDK3.9
```

The verifier fails for changed authoritative inputs, missing or extra raw
functions/types/constants, duplicate bindings, conflicting definitions,
unclassified extensions, or any `UNSUPPORTED_NEEDS_WORK` entry.

Generate machine-readable and static web documentation under
`website/public/api`:

```sh
python3 tools/generate_api_docs.py Novus/std/amiga/raw \
  --ndk-path /path/to/NDK3.9 --output website/public/api --check
```

`api.json` is the stable extraction format. It includes functions, constants,
structs, unions, enums, types, traits, and classes, with member documentation
and raw-NDK status, scope, minimum version, interface, source headers, and
definitions. `index.html` and `api.css` are a dependency-free searchable
rendering of the same data. The documentation check requires every exported
raw declaration and member to have either NDK material, exact ABI/value
documentation, or Novus-authored documentation. Autodocs are resolved by
library as well as function name so duplicate device/library entry points
cannot silently receive another interface's contract.
