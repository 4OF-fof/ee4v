# Avatar、体型、MA装着、実行時

find-avatarsでSceneまたはPrefab StageのAvatar参照を取得し、inspect-avatarとaudit-avatarで現状と問題を区別する。数量は現Hierarchyの値であり、最終ビルドの性能ではない。最終構成はmeasure-avatar-performanceのNDMF測定を使う。

## 体型

inspect-avatar-authoring --avatar-ref <ref> のscope、scale parts、rendererPath、shapeName、categoryを使う。

- edit-avatar-shape --avatar-ref <ref> --action setScale --part wholeBody --multipliers 1.05 --multipliers 1.05 --multipliers 1.05 --dry-run true は元Prefab基準の倍率。1は100%。適用時は同じ入力のdryRunをfalseへ変更する。
- 身長は --action setHeight --value <メートル>。Avatar全体のViewPositionも共通APIが調整する。
- BlendShapeは --action setBlendshape --renderer-path <path> --shape-name <name> --value <0–100>。Group編集は検出済みcategoryとshapeNameを使う。対応付けは元FBXと分類に依存する。

書き込み対象は利用者のScene上のAvatar instance。元Prefab assetとPrefab Stageの設定を直接書き換えない。書き込みはUndo対象だが、Prefabや制作履歴の保存とは別。

## MA装着

edit-avatar-attachmentに渡すpartPathはAvatar直下のnested Prefab。inspectのstate、reviewReasons、canApplyと接続候補を先に確認する。kindはauto / MergeArmature / BoneProxy。autoはcanApplyの場合だけ未接続部位の提案をまとめて1つのUndoグループで追加し、ownerPathで1件に限定できる。targetPathの上書きには1件の選択が必要。要確認時はownerPath、kind、targetPathを明示し、dryRunで入力を検証する。既存接続の編集では省略したtargetPathに現在の接続先を使用する。

Avatar外の装着先、part自身への追従、既存の別方式への暗黙置換をしない。装着処理は既存bone名や姿勢・scale追従を共通APIで扱う。汎用SerializedPropertyの編集で同じ処理を再実装しない。

## Play Mode

Play Mode開始・停止はUnity CLIへ任せる。切り替え後にAvatar参照を再取得する。get-avatar-runtimeは読取、control-avatar-runtime --action connectは既存GestureManagerへの接続、setParametersは実行時値の変更。

render-lighting-previewのpreviews配列はargs-fileで指定し、--output <path> で画像を保存する。Scene照明は描画後に復元される。保存する照明presetはProjectをまたぐユーザーデータなので、明示的な作業対象のときだけupsert/deleteを使う。
