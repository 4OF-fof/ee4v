# Fluent UI System Icons vendor layout

ee4vのEditor UIで使うMicrosoft Fluent UI System Iconsを、
必要なアイコンだけ保持します。

## Source

- Repository: `microsoft/fluentui-system-icons`
- Version: `1.1.334`
- Commit: `f2f75a6e4814153d5c049c0f06e197731718326b`
- Style: Filled
- Imported icons: `add`, `archive`, `arrow_clockwise`, `arrow_left`,
  `arrow_right`, `arrow_sort`, `chevron_down`, `chevron_right`, `code`,
  `cube`, `dismiss`, `document`, `eye_off`, `folder`, `folder_zip`, `image`,
  `info`, `library`, `music_note_2`, `pin`, `search`, `star`, `subtract`,
  `tag`, `video`

`Source/Filled/`のSVGが元データです。白色へ正規化したSVGから、
Unity用の透明な512px PNGを
`src/Editor/ThirdParty/FluentUiSystemIcons/Png512/`へ生成しています。
UI Toolkit側でテーマ色を適用できる単色マスクとして扱います。

このアイコンを含む配布物には`LICENSE.txt`と`NOTICE.txt`も含めます。
