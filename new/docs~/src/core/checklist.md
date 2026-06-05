# Core 実装チェックリスト

- namespace は `Ee4v.<Scope>` になっているか
- bootstrap は `FeatureBootstrapContract.Initialize(...)` を通しているか
- definitions 型名が `<Scope>Definitions` になっているか
- 共通 API でない型を `public` にしていないか
- Unity internal API を feature 側で直接 reflection していないか
- setting を `RegisterAll()` で 1 回だけ登録しているか
- `User` / `Project` の保存先選択は妥当か
- settings 文言と validation 文言を localization したか
- 文言を `I18N.Get(...)` 経由にしたか
- localization を `Editor/<Scope>/Localization/<locale>` に置いたか
- Injector の `id` と `channel` は安定しているか
- Injector 描画後に `CurrentRect` の競合を起こしていないか
- setting 変更時に必要な `InjectorApi.Repaint(...)` を呼んでいるか
- `Test List` に出す registrar を追加したか
- test asmdef が必要なら `InternalsVisibleTo` を追加したか

## 補足

- settings provider は通常増やさない。定義追加で既存 provider に載せる
- localization scope と settings localization scope は namespace 依存なので、ファイル移動時は namespace の崩れに注意する
- feature 固有実装を `Core` へ入れると再利用ではなく依存逆転の崩れになる。迷ったらまず `Editor/<Feature>` に置く
