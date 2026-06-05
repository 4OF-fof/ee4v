# I18N / Localization

永続表示文言は `I18N.Get("key")` を使います。

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

`Localization` 配下の asset 変更時は `LocalizationAssetPostprocessor` が `I18N.Reload()` を呼びます。feature 側で独自の localization reload 実装を持たないでください。
