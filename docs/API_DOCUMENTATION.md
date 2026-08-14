# API documentation

Public Novus APIs are documented in their source with `///` comments. The
same comments drive editor hovers, the machine-readable API index, and the
static website reference; there is no separate documentation database to
drift out of date.

## Required coverage

Every exported function, type, trait, class, enum, constant, field, variant,
and trait member must have a useful summary. Function documentation should
explain ownership or safety requirements that are not obvious from the type,
and may use these structured sections:

```novus
/// Opens a device request for the selected unit.
///
/// # Parameters
/// * `unit` - The device unit number.
///
/// # Returns
/// The owned request on success, or the reason the device could not be opened.
///
/// # Ownership
/// The returned handle owns the request and closes it when dropped.
pub fn open(unit: u32) -> Result<DeviceRequest, DeviceError>
```

The extractor also records parameter names, types, modifiers, method
receivers, return types, ownership text, source locations, and NDK metadata as
separate JSON fields. NDK raw functions use the pinned official autodocs where
available and Novus-authored documentation otherwise.

## Generate and verify

From `website/`:

```sh
npm run docs       # strict amiga::raw reference
npm run docs:all   # strict complete standard-library reference
npm run build      # copy the reference into the static website build
```

Output is written to `website/public/api`:

- `api.json` — stable machine-readable schema
- `index.html` — dependency-free searchable reference
- `api.css` — responsive presentation

Both strict commands fail when a public declaration or member lacks
documentation. Internal modules under `Novus/std/tests` are intentionally not
part of the public API index.
