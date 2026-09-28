---
name: vrchat-avatar-concept-build
description: Unity と ee4v で VRChat アバターを改変する。素材の Prefab を確認してコンセプト画像を作り、承認後は ee4v の Variant 作成から派生アバターを制作する作業で使う。
---

# Prefab を確認して ee4v Variant に改変する

## 素材と実物を確認する

1. 接続中の Unity Project、ベースアバター、既存の派生アバターを確認する。利用者の Scene と元 Prefab を変更しない。
2. ee4v MCP で AssetManager を検索し、候補の File、依存素材、対象アバター用 Prefab を特定する。対応タグだけを根拠にしない。Unitypackage の名前で判別できない場合は取り込み前に内部の Prefab path を確認する。
3. 必要な素材を**画像生成より前に** import する。既に Project にある素材は再 import しない。Prefab の Renderer、Material、Shader、欠落参照、装着方法を調べる。
4. ベースと追加素材の Prefab を正面・背面・必要な側面から撮影する。撮影が失敗した場合は Unity の隔離 Preview で描画し、問題を記録する。画像生成への入力を禁じる利用条件が明記された素材画像は送信しない。

## コンセプトを提示して承認を待つ

撮影した Prefab 画像を画像生成ツールへ渡し、実際の形状、装着位置、パーツ数を尊重したコンセプト画像を作る。実素材にない形状や装飾は、制作で再現できると約束せず差分を示す。画像、使用素材、改変内容をユーザーに提示する。

**ユーザーの明示的な承認を得るまで制作を始めない。** 派生 Prefab の作成、パーツや Material の変更、生成画像の Unity Project への取り込みは承認後に行う。修正依頼には画像と作業案を更新して再提示する。

## 承認された案を制作する

1. **最初に ee4v の Variant 作成処理を実行する。** `ee4v_asset_get_item`でベースItemへ取り込み済みのPrefab GUIDを確認し、`ee4v_asset_create_variant`へItem ID・元Prefab GUID・名前を渡す。UIを使う場合はAssetManagerの「派生アセットを追加」から同じ処理を実行する。既存Variantの改変を再開する場合は、`ee4v_asset_list_variants`で登録を確認して再利用する。
2. 作成直後、Prefab が指定したベースの Variant であり、ee4v の派生アセット一覧で正しい親 Item に表示されることを確認する。`DerivedAssetCatalog.Read` と `FindByParentItem`、または ee4v の Item 取得結果で親 Item ID、元 Prefab GUID、派生 Prefab の GUID と path を照合する。ベース Prefab に Material 依存がある場合は、ee4v が作成した Material Variant も確認する。確認できるまでパーツ編集を始めない。作成処理を実行できなければ、その理由を報告して制作を止める。
3. ee4v が作成した派生 Prefab に、`ee4v_asset_add_prefab_to_variant`で対応素材を接続を保った nested Prefab として追加する。元の衣装など必要なパーツだけを派生側で切り替え、体型 BlendShape と Material を調整する。元の素材は直接編集しない。
4. Material の値を変える場合は派生フォルダー内の Material Variant を編集し、import した元 Material は編集しない。ee4v が生成した Material Variant を優先し、追加素材の Material に変更が必要なら`ee4v_asset_create_material_variant`で派生フォルダーに作る。改変後は`ee4v_asset_save_variant`で履歴を保存する。
5. 必要なら承認済み画像を Unity Project と派生アバターのギャラリーに登録する。
6. 正面・背面・斜めの実物 Preview、欠落参照、Console と AssetManager での表示を確認する。コンセプトとの差と作業中に見つけた不足機能を、再現条件とともに報告する。
