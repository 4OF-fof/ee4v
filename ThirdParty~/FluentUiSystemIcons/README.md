# Fluent UI System Icons vendor layout

AssetManagerの再読み込みボタンで使うMicrosoft Fluent UI System Iconsを、
必要な1点だけ保持します。

## Source

- Repository: `microsoft/fluentui-system-icons`
- Version: `1.1.334`
- Commit: `f2f75a6e4814153d5c049c0f06e197731718326b`
- Style: Filled
- Imported icon: `arrow_clockwise`

`Source/Filled/ic_fluent_arrow_clockwise_24_filled.svg`が元データです。
白色へ正規化したSVGから、Unity用の透明な512px PNG
`src/Editor/ThirdParty/FluentUiSystemIcons/Png512/arrow_clockwise.png`
を生成しています。UI Toolkit側でテーマ色を適用できる単色マスクとして扱います。

このアイコンを含む配布物には`LICENSE.txt`と`NOTICE.txt`も含めます。
