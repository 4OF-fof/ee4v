# I18N / Localization

永続表示文言は `I18N.Get("key")` を使います。

`I18N` は `Ee4v.UI.Editor` に置くpresentation向け短縮APIです。解決本体はUnity非依存の
`LocalizationService` instanceであり、Core内側やfeatureのDomain / Applicationから
`I18N`を呼びません。

- caller namespace から scope を解決する
- `Ee4v.<Scope>` にいれば `<Scope>` の localization を引く
- format が必要なら `I18N.Get("key", args...)`

## 配置

localization は以下に置きます。

- `Editor/<Scope>/Localization/ja-JP/*.jsonc`
- `Editor/<Scope>/Localization/en-US/*.jsonc`

scope は `Localization` フォルダの親 directory 名から決まります。`Editor/<Scope>` 以外へ置くと意図した scope とずれます。

## key 管理

- jsonc は object を flatten して `a.b.c` 形式で読まれる
- 同一 locale / scope で duplicate key はエラー
- code 側で未定義 key を引くと key 文字列がそのまま返る

Core には localization 静的監査があり、duplicate / missing / unused が `Core` suite で監査されます。

## 再読込

`Localization` 配下のasset変更時は `LocalizationAssetPostprocessor` が
`CoreLocalization.Current.Reload()` を呼びます。serviceはcatalog cacheの破棄とevent通知だけを行い、
Injectorや全Viewの再描画はUI assemblyがeventを購読して実行します。

## assembly境界

- `Ee4v.Core.Contracts.Editor`: catalog model、`ILocalizer`、service/source/language/diagnostics契約
- `Ee4v.Core.Services.Editor`: scope、fallback、format、cache、reloadを扱う `LocalizationService`
- `Ee4v.Core.Editor`: package JSONC source、Settings language provider、Unity diagnostics、AssetPostprocessor
- `Ee4v.UI.Editor`: caller scope adapter、`I18N`、reload後の再描画

`LocalizationService` はfilesystem、Newtonsoft、Settings、Unity APIを参照しません。
