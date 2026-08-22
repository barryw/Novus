# AGA capabilities

Novus detects the classic Amiga chipset with
`amiga::sys::hardware::chipset::CHIPSET()`. APIs that expose AGA-only hardware
features check that result at runtime and return an error on OCS/ECS rather than
programming unsupported registers or layouts.

| Area | OCS/ECS | AGA | Novus API and guard |
| --- | --- | --- | --- |
| Hardware sprite fetch width | 16 pixels | 16, 32, or 64 pixels | `SpriteData::from_raw_width` and `AttachedSpriteData::from_raw_width`; extended widths return `BadDisplayMode` without AGA |
| Sprite vertical encoding | 0–511 | 0–1023 | Sprite position/control encoding selects the chipset limit |
| Copper bitplane pointers | planes 0–5 | planes 0–7 | `CopperList::bitplane` rejects planes 6–7 without AGA |
| Intuition screen depth | up to 5 lores or 4 hires in ordinary modes; 6 in EHB/HAM modes | up to 8 planes | `ScreenHandle` constructors and `ScreenBuilder::build` reserve planes 7–8 for AGA and let Intuition validate the selected legacy mode |
| Palette entries and precision | 32 entries, 4-bit components | 256 entries, 8-bit components | Palette setters select `SetRGB4` or `SetRGB32` from the detected chipset |

Both requested target configurations, A1200/020 and A4000/040, report AGA.
Their runtime suites exercise 16-, 32-, and 64-pixel regular and attached
sprites, planes 6 and 7 in copper lists, 8-plane Intuition screens, extended
vertical sprite positions, and 256-entry palette validation. Invalid widths,
planes, depths, and data layouts are also covered.

See [SPRITES.md](SPRITES.md) for the expanded hardware data layout and
[AMIGA_LIBRARY_DESIGN.md](AMIGA_LIBRARY_DESIGN.md) for the Tier 1/Tier 2
abstraction boundary.
