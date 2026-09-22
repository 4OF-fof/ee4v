# AGENTS.md

## 対象環境

- VRChat
- Unity 2022.3
- 将来的なUnity 6への移行

## モジュール構成と依存

- `src/Editor/Core`と`src/Editor/UI`を機能横断の基盤とする
- 各機能モジュールはCoreとUIに依存してよいが、原則として別の機能モジュールへ依存しない
- 複数機能で共有する実装は`src/Editor/Feature/Shared`に置き、使用する機能と依存理由を`docs~`に記載する
- AssetManagerは例外として他の機能モジュールへ依存してよい
  - AssetProtectionは`src/Editor/AssetManager/AssetProtection`に置くAssetManager内部モジュールとして扱う
- 機能モジュールはCoreの公開APIを使用し、asmdefには実際に使用するassemblyだけを参照として記載する

## テスト実行

- テストは毎回実行せず、既存範囲へ大幅な変更があった場合に関連するテストだけを実行する
- Unity 6での動作確認は、ユーザーから指示があった場合だけ行う
- CodexからUnityテストを実行するときは、Licensing ClientのIPC接続に必要なため、`exec_command`へ`sandbox_permissions: "require_escalated"`を指定する
- Unity 2022.3のEditModeテストには次のコマンドを使用する。`-quit`はテスト開始前に終了するため指定しない

  ```powershell
  & 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe' -batchmode -nographics -projectPath '<workspace>\Temp~\VerifyProject~' -runTests -testPlatform EditMode -testResults '<result.xml>' -logFile '<test.log>'
  ```

- テスト結果はシェルの終了コードではなく、結果XMLの`test-run`にある`result`、`passed`、`failed`で判断する。結果XMLがなければ未実行として扱う
- 明示的に許可されている場合を除き、UI変更の確認目的でUnityを新規起動しない

## ファイルとデータ

- Unity の `.meta` ファイルは手動で作成しない
- DB の互換性とマイグレーションは原則不要。DB の削除と再生成を前提にschemaや取り込み処理を変更してよい
  - 開発段階なのでバージョンもv1で固定

## UI

- `Label`、`Button`、`Toggle`、入力フィールド、`HelpBox` など、文字を描画する可能性がある UI 要素は `UiTextFactory` 経由で作成する。表示文字の更新も同 Factory または Factory が返す要素の API を使用し、標準 UI 要素の `text` を直接操作しない
- UI component の Story には、Story 自身を除いた実際の使用ファイルを使用箇所として記載する。使用箇所がなくなった component は Story だけを維持せず削除する
- アイコンは`FluentUiSystemIcons`から使用するものだけを取得する。Unity固有のアイコンが必要な場合はBuilt-inアイコンを使用してよい

## ドキュメント

- 仕様と設計を確認するときは、agent向けの正本である`docs~`配下のMarkdownを参照する
  - 機能やテストの追加、変更があれば対応するMarkdownも併せて更新する
  - 利用者の操作、画面の入口、注意事項が変わる場合は`docs~/human`配下の対応するHTMLも更新する。ページの追加、削除、名称変更では`index.html`と全ページの共通ナビゲーションも更新する
