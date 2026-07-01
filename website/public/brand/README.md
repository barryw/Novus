# WHI shared brand assets (vendored)

These files implement the Walker Heavy Industries shared **download-latest**
control so it looks and behaves identically on every product site (WAL-39,
Build & Release Standard §6). They are **vendored copies** of the canonical
sources in the [`whi-brand`](https://github.com/barryw/whi-brand) monorepo
(`web/brand/`). Do **not** hand-edit them here — re-copy from `whi-brand` when
the shared component changes.

| File | Source in whi-brand | Notes |
|------|---------------------|-------|
| `css/tokens.css`        | `web/brand/tokens/tokens.css` | `:root` design tokens the control reads. Byte-identical. |
| `css/components.css`    | `web/brand/css/components.css` | `.whi-btn` / `.whi-download` / etc. All `.whi-*`-scoped. Byte-identical. |
| `css/whi-control.css`   | *(site-owned glue)* | Only the `.whi-icon` sizing rule (the icon README documents this as a consumer-supplied rule). |
| `js/download-latest.js` | `web/brand/js/download-latest.js` | Progressive-enhancement behavior. Byte-identical. |
| `icons/icons.svg`       | `web/build.sh` sprite output | Icon sprite; the control uses `#whi-download`. |

## Why only the component layer (not the full `whi.css` bundle)

Novus has its own complete design system (Tailwind + `novus-*` tokens). The full
`whi.css` bundle includes `base.css`, which resets global `body` / `a` / `h1`
typography and would clobber the Novus look (e.g. underline-on-hover links). The
parts that make the download control *identical across sites* are
`download-latest.js` + the `.whi-download` / `.whi-btn` / `.whi-icon`
presentation + the tokens they consume — so we vendor exactly those and skip the
chrome layers (`base.css`, `marketing.css`).

Novus is on the WHI **Amiga** line; the marketing-layer Amiga CTA guardrail
remaps `.whi-btn--line` to the neutral high-contrast house button (Amiga red
fails AA as a filled-button background). That is also the *default* rendering
when `--whi-line` is unset, so the control already matches the guardrail without
needing `marketing.css`.
