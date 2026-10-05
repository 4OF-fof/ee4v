---
name: blendshape-preset-create
description: ee4v の FBX 別 BlendShape Preset を作成・修正し、表情、体型、衣装、髪型の形状を用途に沿って role、左右、見た目の部位・グループへ分類するときに使う。
---

# BlendShape Preset を作成・整備する

## 対象を確定する

1. [分類ルール](references/classification-rules.md)を読む。現行の保存形式と画面での用途は`docs~/features/face-expression.md`と`docs~/asset-manager.md`で確認する。
2. 対象アバターのベースFBXと、BlendShapeを持つ追加FBXを確認する。プリセットはFBXごとに別であり、アバター名やRenderer名だけで対象を決めない。
3. 接続中のee4v Editorがあれば`ee4v_list_blendshape_presets`と`ee4v_get_blendshape_preset`で保存済みデータを読む。新規FBXは表情エディターでアバターを選ぶと初期プリセットを自動生成する。Preferencesの「FBX別BlendShapeプリセット」から保存フォルダーを開ける。共通データの保存先はUser Settingsから確認する。

## 分類する

- 全mappingの`meshName`、`shapeName`、区切り見出し、Renderer上の見た目、対応する衣装やアバター別FBXを見て用途を判断する。名前の接頭辞・末尾だけから一括決定しない。
- `role`、`side`、`mouthMorph`、`appearancePart`、`appearanceGroup`は別の意味を持つ。各フィールドの決め方と代表例は分類ルールに従う。
- 体型・衣装は`Breast`、`Nipple`、`Shrink`、`Waist`、`Hip`、髪型や小物など実際に選ぶ用途で役割カードを作る。縮小用BlendShapeは部位に関係なく`Shrink`へまとめ、見た目側では各部位の`Shrink`見出しに置く。ただし`_OFF`をすべて縮小と見なさない。
- 表情の役割は別機能を混ぜず、対応付け可能な粒度を維持する。左右で同じ操作だけ役割を揃えて`side`で区別する。
- 既存データを修正するときは、指定された範囲の全プリセット・全mappingを点検する。FBXのGUID、mesh local ID、shape名、区切り見出し、口形状指定を無関係な分類変更で書き換えない。

## 保存して確認する

- 接続中のEditorでは`ee4v_update_blendshape_preset`で`meshLocalId`と`shapeName`を指定し、取得したrevisionを渡して部分更新できる。まず`dryRun`で検証し、保存後に再取得する。大量更新をJSONで行う場合は元ファイルを退避し、保存先と元のrevision・ファイル内容を確認してから反映する。
- 保存後は、対象FBXのmapping数と識別子が変わっていないこと、非ヘッダーに意図した役割と部位が入り、`Shrink`の役割と見た目グループが揃っていることを確認する。Body mesh上の表情が見た目一覧へ混ざらず、顔立ちや体型調整が必要な部位へ表示されるかも確認する。
- 表情クリップの再マッピングは役割と左右を使う。複数形状を一つの役割にした結果、アニメーションで使う形状の対応先が曖昧になる場合は、役割をより具体的に分ける。
