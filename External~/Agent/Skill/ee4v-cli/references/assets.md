# 素材Libraryと制作物

asset-searchは名前・Tag・拡張子、Collectionまたはfilter treeで検索する。Collectionと自由filterは同時指定しない。asset-get-itemでFile、依存素材、取り込み済みPrefabを確認する。

Libraryへの登録・同期・取り込みと、Unity Projectへのimportは別処理。Prefab調査やpreviewに必要な素材を勝手にimportしない。asset-inspect-prefabとasset-render-prefab-previewは取り込み済みPrefabを対象にする。

## 制作

- 元Itemの派生を作る場合はasset-create-variant。新しい所有Itemを持つ制作物はasset-create-work。ベースなしは通常Prefab。
- 追加素材はasset-add-prefab-to-variant。Material編集は生成された派生Materialを使用し、必要な場合だけasset-create-material-variant。
- 汎用Unity CLIで制作Sceneを編集後、asset-save-variantで履歴を保存する。Scene保存だけでは制作履歴は作られない。
- asset-get-variantは版一覧・編集元・差分・ギャラリーを返す。未保存Assetがある場合のchanges=nullを変更なしと扱わない。

## 復元と削除

過去版はasset-restore-variant、編集元へ戻す場合はasset-discard-variant-changes。dirtyな制作Sceneを捨てる必要がある場合だけdiscardWorkingChangesを明示する。操作結果が部分適用を示す場合は、完了した変更を照合する。

Item/File/Variantの削除は既存のArchive・所有権条件を満たす必要がある。Gallery画像の削除・並び替えは登録済みimageIdへ行う。IDを推測しない。

Library、Source同期、制作履歴・Gallery、FBX分類は共通data rootへ永続する。複数Projectで共有されるため、単なる接続確認やScene編集の検証にこれらのwriterを使わない。
