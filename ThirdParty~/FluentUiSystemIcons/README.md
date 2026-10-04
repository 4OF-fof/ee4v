# Fluent UI System Icons vendor layout

ee4vのEditor UIで使うMicrosoft Fluent UI System Iconsを、
必要なアイコンだけ保持します。

## Source

- Repository: `microsoft/fluentui-system-icons`
- Version: `1.1.334`
- Commit: `f2f75a6e4814153d5c049c0f06e197731718326b`
- Style: Filled、Regular（お気に入りの未選択Starのみ）
- Imported icons: `add`, `archive`, `arrow_clockwise`, `arrow_left`,
  `arrow_right`, `arrow_sort`, `checkmark`, `chevron_down`, `chevron_right`,
  `cube`, `dismiss`, `document`, `eye`, `eye_off`, `folder`, `folder_zip`,
  `image`, `info`, `library`, `mail_inbox`, `pin`, `search`, `star`,
  `subtract`, `tag`, `weather_moon`, `weather_sunny`
- Imported sizes: `selected-icons.txt`に生成PNGの一覧を記載します。

`Source/Filled/`と`Source/Regular/`のSVGが元データです。白色へ正規化したSVGから、
Unity用の透明なPNGを
`src/Editor/ThirdParty/FluentUiSystemIcons/Png512/`へ生成しています。
UI Toolkit側でテーマ色を適用できる単色マスクとして扱います。

お気に入りには20px用SVGから生成した`star_regular_20.png`と`star_20.png`を使用します。同じ輪郭のRegularとFilledを12pxで描画し、選択時はFilled Starを黄色にします。

`*_12.png`、`*_16.png`、`*_20.png`はそれぞれ12px、16px、20pxのPNGです。
元SVGを512pxで描画してからBoxフィルターで縮小し、細い線と穴の
画素ごとの被覆率を保持します。従来のサイズ接尾辞なしPNGは512pxです。
保存先の`Png512`は既存のアセット参照を維持するための名称です。

`FluentUiIcons.CreateState`とサイズ付き`LoadTexture`は、18px以下の表示で
16px用の元SVGから生成した`*_16.png`を選択します。
12px以下のAdd、Subtract、Arrow Clockwise、Arrow Left、Arrow Right、
Checkmark、Chevron Down、Chevron Right、Dismiss、Pinには
12px用の`*_12.png`を選択します。
Mail Inboxも16px用SVGから生成した`mail_inbox_16.png`を使用します。
サイズを付けない`LoadTexture`と、`*_12.png`や`*_16.png`を直接指定した
読み込みは、そのファイルを使用します。
`FluentUiIconPostprocessor`がこのディレクトリのPNGをGUI用Textureとして
非圧縮、ミップマップなし、透過エッジ補正あり、Bilinear、Clampで取り込みます。
`.meta`はUnityが生成・更新します。

このアイコンを含む配布物には`LICENSE.txt`と`NOTICE.txt`も含めます。
