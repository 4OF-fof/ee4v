# Fluent UI System Icons vendor layout

ee4vのEditor UIで使うMicrosoft Fluent UI System Iconsを、
必要なアイコンだけ保持します。

## Source

- Repository: `microsoft/fluentui-system-icons`
- Version: `1.1.334`
- Commit: `f2f75a6e4814153d5c049c0f06e197731718326b`
- Style: Filled
- Imported icons: `add` (24px / 12px), `archive`, `arrow_clockwise`, `arrow_left`,
  `arrow_right`, `arrow_sort`, `checkmark`, `chevron_down`, `chevron_right`,
  `cube`, `dismiss`, `document`, `eye`, `eye_off`, `folder`, `folder_zip` (24px / 16px),
  `image`, `info` (24px / 16px), `library`, `mail_inbox` (16px), `pin`, `search`, `star`,
  `subtract` (24px / 12px), `tag`

`Source/Filled/`のSVGが元データです。白色へ正規化したSVGから、
Unity用の透明な512px PNGを
`src/Editor/ThirdParty/FluentUiSystemIcons/Png512/`へ生成しています。
UI Toolkit側でテーマ色を適用できる単色マスクとして扱います。

`add_12.png`と`subtract_12.png`は12px用の元SVGから生成し、
12px以下の表示では`FluentUiIcons.CreateState`が自動選択します。
それより大きい表示では24px用の画像を使用します。
`info_16.png`と`folder_zip_16.png`は16px用の元SVGから生成し、
File Treeの概要とArchive行で16px表示します。
`FluentUiIconPostprocessor`がこのディレクトリのPNGをGUI用Textureとして
非圧縮、ミップマップなし、透過エッジ補正あり、Bilinear、Clampで取り込みます。
`.meta`はUnityが生成・更新します。

このアイコンを含む配布物には`LICENSE.txt`と`NOTICE.txt`も含めます。
