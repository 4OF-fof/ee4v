# AGENTS.md

## Target

- VRChat
- Unity 2022.3

## 作業方針

- コード変更には `tiny-code` skill を使用
- Unity の `.meta` ファイルは手動で作成しない
- DB の互換性とマイグレーションは原則不要。DB の削除と再生成を前提にschemaや取り込み処理を変更してよい
