# AGENTS.md

## リポジトリ概要

このリポジトリは、Unity 2022.3 向けのエディタ拡張パッケージです。

- `src/` は現在の作業対象パッケージです。
- `old/` は旧実装・参照用のコードです。

## 作業方針

- `old/` は参照資料として扱い、書き換えたり削除したりしないでください。
- Unity の `.meta` ファイルは自動で生成されます。 `.meta` ファイルを手動で作成する必要はありません。
- 実装前にdocsを確認し、既存の手法に添うように実装を進めてください。
- ユーザーに見える表示テキストはコードへ直書きせず、ローカライズ定義を追加して `I18N.Get("key")` など既存のローカライズ経由で表示してください。
- DB に関する変更では、既存 DB との互換性やマイグレーションは原則考慮せず、DB の削除・再生成を前提として schema や取り込み処理を修正して構いません。

## UI・Unity 依存の分離

- UI は UI Toolkit（UIElements）を優先して実装し、UI Toolkit では実現できない場合に限って IMGUI を使用してください。
- UI Toolkit で通常の表示ラベルを実装する場合は、Unity 2022.3 のフォントキャッシュ問題を回避するため、`Label` を直接生成・継承せず `UiTextFactory.Create(...)` を使用してください。`Button.text` など操作 control 自身の text はこの制約の対象外です。
- `TextField(label)`、`Toggle(label)`、`IntegerField(label)`、`PopupField(label, ...)`、`BaseField<T>.label` など、Unity 標準 control が内部生成する label へ表示テキストを渡すことも禁止です。見た目が通常の label と同じでも `UiTextFactory` の代替にはなりません。
- `Foldout.text` も Unity 標準の内部 label を使用するため、表示文字列を設定しないでください。折り畳み見出しは `UiTextFactory.AttachToFoldout(...)` で追加してください。
- 特に Settings UI では、項目名を `UiTextFactory.Create(...)` で独立した要素として生成し、隣接する標準 field / custom drawer へ渡す label は必ず空文字列にしてください。これは `Toggle`、数値 field、文字列 field、enum / popup、custom setting drawer のすべてに適用します。
- `UiLabelAuditTests` はソース上の direct `Label` しか検出できず、標準 control 内部の label 利用を検出できません。Settings UI を追加・変更するときは、表示項目名が `UiTextElement` として存在し、配下の標準 field の `label` が空であることをテストで確認してください。
- UI は見た目の差し替えを容易にするため、state の表示と入力イベントの通知に責務を限定し、機能ロジックと分離してください。依存方向は `UI -> 機能ロジック` とします。
- Unity のバージョン更新時に変更箇所を特定しやすくするため、Unity 依存部分は Core の adapter / wrapper に集約してください。
- 特に Unity の非公開・internal API、reflection、内部フィールドへのアクセスは feature 側で直接行わず、原則 `Editor/Core/Internal/EditorAPI` の facade と `Backends` に分離してください。
- 非公開 API が利用できない場合も機能全体を壊さないよう、facade は `Try...` や fallback を提供してください。変更時は関連テストと `docs~/src/maintenance/unity-upgrade.md` も更新してください。

## クリーンアーキテクチャ・疎結合

- module は配置上のカテゴリではなく、単独で有効化・テスト・変更できる機能境界として定義してください。カテゴリディレクトリは module の assembly、namespace、設定、初期化処理を所有しません。
- たとえば `Editor/EditorEnhancements` は小規模な Editor 拡張 module をまとめるカテゴリに限定します。`Editor/EditorEnhancements/DepthIndicator` は `DepthIndicator` module であり、namespace、asmdef、Localization、Test、bootstrap、setting 定義を自身の配下で所有します。
- module の namespace と assembly 名はカテゴリ名を含めず、機能名を基準にしてください。例として `DepthIndicator` は `Ee4v.DepthIndicator` / `Ee4v.DepthIndicator.Editor`、`FolderContentOverlay` は `Ee4v.FolderContentOverlay` / `Ee4v.FolderContentOverlay.Editor` とします。
- module 間で具象型や static state を直接参照しないでください。module をまたぐ連携は Core の契約、明示的な interface、event、DTO など必要最小限の抽象を介し、asmdef の参照方向を一方向に保ってください。
- 依存方向は外側から内側へ向けます。UI / Presentation と Unity adapter は Application / Domain / Contracts を利用できますが、Application / Domain / Contracts から Unity、UI、filesystem、DB、network の具象へ依存してはいけません。
- Composition root は object の生成、依存注入、Unity lifecycle への登録だけを担当します。判断ロジック、描画ロジック、永続化処理を bootstrap や static facade に置かないでください。
- UI は state の描画と入力通知、renderer は描画に限定し、検索・集計・選択・validation・cache invalidation などの判断を分離してください。外部技術へ触れる処理は adapter として隔離します。
- public API は module の利用に必要な契約だけに限定し、実装型は原則 `internal` にしてください。将来使う可能性だけを理由に共通化や公開を行わないでください。
- 小規模 module に空の `Domain`、`Application`、`Infrastructure` directoryやassemblyを機械的に作る必要はありません。ただし責務と依存が増えた時点で層を分割できるよう、純粋な判断ロジックと Unity 依存処理を同じ型へ混在させないでください。
- setting、Localization、Test はそれを所有する module の配下へ置いてください。カテゴリ直下や無関係な共通 module に集約しないでください。
- 実装変更時は、module が兄弟 module を参照していないこと、内側のロジックが外部技術へ依存していないこと、bootstrap が composition 以外の責務を持っていないことを確認してください。可能な範囲で pure logic の unit test と Unity adapter の境界 test を追加してください。
