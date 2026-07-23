# UI 実装チェックリスト

- 配置カテゴリは妥当か
- `Ee4v.UI` namespace になっているか
- `Label` を直接使っていないか
- text 表示を `UiTextFactory` 経由にしたか
- typography が必要な class 名だけを `UiClassNames` に追加したか
- typography 不要な class を component 内に閉じたか
- 必要なタイポグラフィ定義を `TypographyStyleResolver` に追加したか
- 色、余白、角丸、font size、共通 control size を design token 経由にしたか
- USS と C# の両方で使う token は `UiDesignTokenTests` の対応表へ追加したか
- 画面固有の寸法を、再利用される共通 size token と混同していないか
- 文言を `I18N.Get(...)` に寄せたか
- `Editor/UI/Localization` を更新したか
- built-in icon を直接引かず `Icon` 経由にしたか
- Catalog registrar で story を登録したか
- Catalog registrar で必要な stylesheet を登録したか
- `Editor/UI/Catalog/Stories` の `<name>.story.cs` に preview 実装を追加したか
- Catalog 上で最低限の使い方が確認できるか

## 補足

- 既存コンポーネントを組み合わせて作れるなら、まず module 専用 UI ではなく汎用カテゴリへの追加を検討する
- 1 画面専用でも、今後の再利用が見込める責務なら先に汎用コンポーネントとして切り出す
- Catalog に載せにくい実装は、責務が曖昧か state の切り方が不十分なことが多い
