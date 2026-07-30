# AGENTS.md

## リポジトリ

Unity 2022.3 向けのエディタ拡張パッケージです。

- `src/` は作業対象です。
- `old/` は参照専用です。変更や削除をしないでください。

## 作業方針

- コード変更には `tiny-code` skill を使用し、このファイルではリポジトリ固有の規則を優先してください。
- 実装前に関連する docs と既存コードを確認してください。
- Unity の `.meta` ファイルは手動で作成しないでください。
- 表示テキストはコードへ直書きせず、module 所有のローカライズ定義と `I18N.Get("key")` などを使用してください。
- DB の互換性とマイグレーションは原則不要です。DB の削除と再生成を前提に schema や取り込み処理を変更できます。

## UI と Unity 依存

- UI は UI Toolkit を優先し、実現できない場合だけ IMGUI を使用してください。UI は state の表示と入力通知に限定し、判断や機能ロジックを分離してください。
- 通常の表示テキストには `Label` を直接生成または継承せず、`UiTextFactory.Create(...)` を使用してください。
- Unity 2022.3 のフォントキャッシュ問題を回避する text には、`UiClassNames` の typography class を渡してください。その class は `TypographyStyleResolver` で `RequiresImgui = true` として登録し、生成された `UiTextElement` が IMGUI fallback を使うことをテストしてください。
- 文字付き button は `UiButton` を使用し、`Button.text` を設定しないでください。標準 `Button` は文字を持たない特殊 control など共通 component で表現できない場合だけ使用できます。
- 標準 field の label は空にし、項目名を `UiTextFactory.Create(...)` で別要素として配置してください。`TextField`、`Toggle`、数値 field、popup、enum、custom drawer を含みます。
- `Foldout.text` は空にし、見出しは `UiTextFactory.AttachToFoldout(...)` で追加してください。Settings UI も同じ規則に従います。
- `UiLabelAuditTests` は標準 control 内部の label を検出できません。Settings UI では項目名が `UiTextElement` であり、配下 field の label が空であることを確認してください。
- Unity 依存処理は Core の adapter または wrapper に集約してください。非公開 API、internal API、reflection、内部 field へのアクセスは `Editor/Core/Internal/EditorAPI` の facade と `Backends` に置いてください。
- internal API が利用できない場合に備えて `Try...` または fallback を提供してください。変更時は関連テストと `docs~/src/maintenance/unity-upgrade.md` を更新してください。

## Module と依存方向

- module は単独で有効化、テスト、変更できる機能境界です。カテゴリ directory は assembly、namespace、設定、初期化処理を所有しません。
- `Editor/EditorEnhancements` は物理カテゴリです。各子 module が namespace、asmdef、設定、ローカライズ、テスト、bootstrap を所有してください。
- namespace と assembly 名はカテゴリ名を含めず機能名を基準にしてください。例: `Ee4v.DepthIndicator`、`Ee4v.DepthIndicator.Editor`。
- module 間で具象型や static state を直接参照しないでください。Core の契約、interface、event、DTO など必要最小限の抽象を介し、asmdef の参照方向を一方向に保ってください。
- 依存方向は `UI / Unity adapter -> Application / Domain / Contracts` とします。Application、Domain、Contracts から Unity、UI、filesystem、DB、network の具象を参照しないでください。
- Composition root は生成、依存注入、Unity lifecycle への登録だけを担当し、判断、描画、永続化を置かないでください。
- module 利用に不要な実装型は `internal` にしてください。将来利用する可能性だけを理由に公開または共通化しないでください。
- 小規模 module に空のレイヤーを作る必要はありません。ただし純粋な判断と外部技術への接続を同じ型へ混在させないでください。
- 実装変更時は兄弟 module への参照、内側から外部技術への参照、composition root の責務を確認してください。

## テスト

- 非自明な判断、invariant、状態遷移、外部境界、障害時 fallback、Localization・Architecture・UI 規約など、自動検出が必要な契約だけをテストしてください。
- 同じ保証は判断を所有する最も内側のレイヤーで一度だけ確認してください。境界固有の変換、通知順序、永続化、failure handling が別に壊れ得る場合だけ境界テストを追加してください。
- getter、既定値の転記、constructor 引数の保持、private な構造など実装をなぞるテストは追加しないでください。同型の列挙値テストは data-driven test へまとめてください。
- UI Catalog に story または screen がある UI は、見た目と通常操作を NUnit / Unity Test で重複確認しないでください。必要な状態や操作は Catalog の preset として追加してください。
- UI の判断は pure logic へ分離して unit test してください。UI 境界テストは Catalog で再現できない Unity lifecycle、external adapter、fallback、複雑な failure handling に限定してください。
- Localization key、asmdef と依存方向、`UiTextFactory`、空 label、`Button.text` 禁止、typography fallback、design token、Catalog 登録と stylesheet の監査は維持してください。
- UI 不具合は先に Catalog で再現してください。原因を pure logic、実装規約、Unity 固有境界へ分離できる場合だけ最小単位のテストを追加してください。
- 保証を別テストまたは Catalog へ移した場合は、重複する旧テストを同じ変更で削除してください。
