# Fluent UI System Icons vendor layout

This directory contains a curated set of Microsoft Fluent UI System Icons used
by `ee4v`.

## Source

- Repository: `microsoft/fluentui-system-icons`
- Version: `1.1.334`
- Commit: `f2f75a6e4814153d5c049c0f06e197731718326b`
- Style: Filled
- Imported upstream icons: 100
- Generated runtime icons: 102

The package keeps one source-size variant per icon. The 24px Filled SVG is used
when available; `Document Code` uses its available 16px Filled SVG.

## Files committed to this repository

- `Source/Filled/*.svg`
- `../../src/Editor/ThirdParty/FluentUiSystemIcons/Png512/*.png`
- `LICENSE.txt`
- `NOTICE.txt`
- `selected-icons.txt`

The SVG fill color is normalized from the upstream `#212121` to `#FFFFFF` so
the generated PNG remains a neutral mask and UI Toolkit can apply the ee4v
theme tint. Geometry and view boxes are unchanged.

`LICENSE.txt` applies to both the source SVGs and the generated PNG
derivatives. Keep `LICENSE.txt` and `NOTICE.txt` when redistributing the
package.

## Unity rendering

The SVG files under `Source` are the authoritative source data. The repository
root `ThirdParty~` directory keeps these regeneration-only files outside the
Unity package in `src`.
`src/Editor/ThirdParty/FluentUiSystemIcons/Png512` contains transparent
512 x 512 PNG derivatives rendered from those exact SVGs. UI Toolkit displays
the PNG through its standard `Image` element and applies the ee4v theme tint.
The package asset postprocessor imports them without texture compression or
mipmaps, clamps sampling at the texture edge, and enables alpha transparency.
These settings prevent Unity from selecting a smaller mip or introducing
compression artifacts when a card displays an icon at 88 px.

`folder_branch_fork.png` and `folder_layer.png` are single-color composite
derivatives made from the imported `Folder` plus `Branch Fork` or `Layer`
icons. A transparent separation around each overlaid glyph keeps it legible
when UI Toolkit applies one tint to the complete texture.

512 px covers the same card area as large thumbnails with ample HiDPI headroom,
while keeping the 102-icon runtime set compact. The package has no Vector
Graphics dependency, SVG importer, runtime SVG parser, or custom path
tessellation. This avoids Unity 2022.3 compound-path rendering differences.

## Update procedure

1. Clone the official repository at the intended release tag.
2. Keep the icon names listed in `selected-icons.txt`.
3. For each name, copy the 24px Filled SVG into `Source/Filled` when
   available, otherwise the nearest available Filled source size.
4. Normalize `fill="#212121"` to `fill="#FFFFFF"`.
5. Refresh `LICENSE.txt` and `NOTICE.txt`.
6. Update the source version, commit, and imported icon count above.
7. Render each SVG with a standards-compliant browser engine to a transparent
   512 x 512 PNG in
   `../../src/Editor/ThirdParty/FluentUiSystemIcons/Png512`. Do not use a
   renderer that loses the SVG nonzero compound-path fill semantics.
8. Regenerate `folder_branch_fork.png` and `folder_layer.png` from the
   corresponding imported source icons.
9. Run `UiIconTests` and confirm every `UiFluentIcon` resolves to a 512 x 512
   texture and the generated runtime icon count remains 102.

Any distribution that extracts `src` from this repository must also include
this directory's `LICENSE.txt` and `NOTICE.txt` with the PNG derivatives.
