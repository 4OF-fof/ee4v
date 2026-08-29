# AGENTS.md

## Target

- VRChat
- Unity 2022.3
- 将来的なUnity 6への移行

## モジュールと依存

- `src/Editor/Core`と`src/Editor/UI`を機能横断の基盤とする
- 各機能モジュールはCoreとUIに依存してよいが、原則として別の機能モジュールへ依存しない
- 複数機能で共有する実装は`src/Editor/Feature/Shared`に置き、使用する機能と依存理由を`docs~`に記載する
- AssetManagerは例外として他の機能モジュールへ依存してよい
  - AssetProtectionは`src/Editor/AssetManager/AssetProtection`に置くAssetManager内部モジュールとして扱う
- 機能モジュールはCoreの公開APIを使用し、asmdefには実際に使用するassemblyだけを参照として記載する

## 作業方針

- コード変更には `tiny-code` skill を使用
- テストの実行は毎回行わない。既存範囲に大幅な変更があった場合、新規テストを追加した場合に関連するテストのみを実行する。修正の再発防止テストは実装しない。
  - Unity6での動作確認は指示があった場合のみ行う
  - Codex から Unity のテストを実行するときは、`exec_command` に `sandbox_permissions: "require_escalated"` を指定する。通常のサンドボックス内では Licensing Client の IPC 接続がタイムアウトするため、Unity を起動しない
  - Unity 2022.3 の EditMode テストには `& 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe' -batchmode -nographics -projectPath '<workspace>\Temp~\VerifyProject~' -runTests -testPlatform EditMode -testResults '<result.xml>' -logFile '<test.log>'` を使用する。`-quit` を付けるとテスト開始前に終了するため指定しない
  - テストの成否はシェルの終了コードだけで判断せず、生成された結果 XML の `test-run` にある `result`、`passed`、`failed` を確認する。結果 XML が生成されなかった場合はテスト未実行として扱う
  - 明示的に許可されている場合を除いてUIを変更してもUnityを新規起動して確認しない。
- Unity の `.meta` ファイルは手動で作成しない
- DB の互換性とマイグレーションは原則不要。DB の削除と再生成を前提にschemaや取り込み処理を変更してよい
  - 開発段階なのでバージョンもv1で固定
- `Label`、`Button`、`Toggle`、入力フィールド、`HelpBox` など、文字を描画する可能性がある UI 要素は `UiTextFactory` 経由で作成する。表示文字の更新も同 Factory または Factory が返す要素の API を使用し、標準 UI 要素の `text` を直接操作しない
- UI component の Story には、Story 自身を除いた実際の使用ファイルを使用箇所として記載する。使用箇所がなくなった component は Story だけを維持せず削除する
- 仕様と設計を確認するときは、agent向けの正本である`docs~`配下のMarkdownを参照する
  - 機能やテストの追加、変更があれば対応するMarkdownも併せて更新する
  - 利用者の操作、画面の入口、注意事項が変わる場合は`docs~/human`配下の対応するHTMLも更新する。ページの追加、削除、名称変更では`index.html`と全ページの共通ナビゲーションも更新する
- アイコンを使用する際は`FluentUiSystemIcons`のアイコンを使用する。使うものだけをGitHubから取得すること
  - Unity特有のアイコンを用いる必要がある場合はBuiltiンのアイコンを使用してもよい
