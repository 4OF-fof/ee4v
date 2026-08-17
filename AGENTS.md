# AGENTS.md

## Target

- VRChat
- Unity 2022.3
- 将来的なUnity 6への移行

## 作業方針

- コード変更には `tiny-code` skill を使用
- テストの実行は毎回行わない。既存範囲に大幅な変更があった場合、新規テストを追加した場合にテストを実行する。
  - Unity6での動作確認は指示があった場合のみ行う
- Unity の `.meta` ファイルは手動で作成しない
- DB の互換性とマイグレーションは原則不要。DB の削除と再生成を前提にschemaや取り込み処理を変更してよい
- `Label`、`Button`、`Toggle`、入力フィールド、`HelpBox` など、文字を描画する可能性がある UI 要素は `UiTextFactory` 経由で作成する。表示文字の更新も同 Factory または Factory が返す要素の API を使用し、標準 UI 要素の `text` を直接操作しない
- UI component の Story には、Story 自身を除いた実際の使用ファイルを使用箇所として記載する。使用箇所がなくなった component は Story だけを維持せず削除する
