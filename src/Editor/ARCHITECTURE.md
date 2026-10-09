# Editorのモジュール構成

`src/Editor/Core`と`src/Editor/UI`は、Editor機能が共通で使用する基盤です。Coreは契約、標準実装、Unity Editorとの接続、共通表示処理を所有します。UIは機能に依存しないUI Toolkit部品を所有し、Core.PresentationはUIを組み合わせて共通表示処理を提供します。

## 依存方向

機能モジュールはCoreとUIに依存できますが、原則として別の機能モジュールへ依存しません。機能間の状態や処理を直接参照せず、変更理由を各モジュール内へ閉じます。

```text
Feature ──→ Core
   ├──────→ UI ──→ Core
   └──────→ Core.Presentation ──→ UI
```

複数機能で共有する実装は`Feature/Shared`へ置きます。共有モジュールを追加または利用するときは、使用する機能と共有理由を`docs~`に記載します。ProjectStyleとHierarchyStyleは`Ee4v.ItemStyle.Editor`、AvatarPartsとAvatarMaterialsは`Ee4v.AvatarEditing.Editor`を共有します。AvatarEditingの対象・選択状態とホストサービスの契約は[`docs~/features/avatar-editing.md`](../../docs~/features/avatar-editing.md)を参照します。

AssetManagerは例外として、他の機能モジュールへ依存できます。AssetProtectionは`AssetManager/AssetProtection`に置くAssetManager内部モジュールです。

## assembly境界

- 機能モジュールはCoreの公開APIを使用します
- Coreから機能assemblyへの`InternalsVisibleTo`は追加しません
- 同じモジュール内の層分割とテストassemblyには、必要な範囲で`InternalsVisibleTo`を使用できます
- asmdefにはコンパイルで実際に使用するassemblyだけを記載します
- NDMFなど外部Pluginとの接続は専用asmdefへ分離します

`Core/Ndmf`の`Ee4v.Core.Ndmf.Editor`はNDMF Preview接続とMAメニュー・パラメーター・骨対応の読み取りを所有します。Core PreviewとExpression Menuが公開APIを使用し、機能の編集・保存処理やUIへ依存しません。外部APIのバージョン依存箇所はこのmoduleへ閉じます。

`Core/Preview`の`Ee4v.Core.Preview.Editor`は描画用overrideとCamera資源を所有し、NDMFの描画filterとCore.Ndmfの接続APIを使用します。UIのPrefabScenePreviewとFaceExpressionが公開APIを使用します。SceneのAvatarを全体複製せず、Play Modeでは既存の実行対象を描画します。

AssetManager内はContracts、Domain、Application、Infrastructure、UI、AssetProtectionに分割します。この層分割は一つの機能モジュール内の依存として扱います。

`Ee4v.Mcp.Editor`は外部クライアントから複数機能を呼び分けるintegration／composition rootです。MCP protocolとtransport、tool registry、JSON変換だけを所有し、Face ExpressionとAssetManagerの公開境界へ依存できます。この例外から機能module同士への依存は追加せず、domain処理は所有moduleの公開APIへ置きます。詳細は[`docs~/mcp.md`](../../docs~/mcp.md)を参照します。

## 単独機能とee4vウィンドウ

各機能は単独で操作できる入口を持ち、ee4vウィンドウでも同じview／controllerを組み合わせます。体型・パーツ、MaterialとAvatarInfoの単独Windowは各Feature内に置き、共有の`AvatarPrefabEditorWindow`でHierarchy上のPrefabインスタンス入力とPreviewを管理します。体型・パーツとMaterialの割り当ては元Prefabへ保存し、AvatarInfoは情報だけを表示します。単独WindowはSceneの保存やGitへの版保存を行わず、AssetManagerや他のWindowの起動・選択には依存しません。

`Feature/Avatar/AvatarParts`、`AvatarMaterials`、`AvatarInfo`がそれぞれパーツ・体型、Material、詳細を所有し、同階層のFaceExpressionとPlayModeComponentSuppressionも独立した機能として維持します。機能間のassembly参照は作りません。AvatarInfoのNDMF連携は専用assemblyに分けます。

AssetManager内部の`AssetModificationWorkflowView`は統合ホストとしてVariant選択・作成、Prefab構成、作業Scene、編集保護と版保存を扱います。`AvatarEditingContext`で編集対象と選択・通知を渡し、PartsとMaterialsの公開エディターAPIを統合と単独Windowで共有します。FaceExpressionの名前分類は両ホストが共有契約の`AvatarShapeNaming`で取得し、FaceExpressionの`AvatarShapeNamingProvider`が登録した分類器と変更通知を使用します。AvatarPartsからFaceExpressionを直接参照しません。

AssetManagerの検索・一覧・情報は`AssetManagerWorkspaceView`で既存の3つの`AssetManagerView`を共有sessionへ接続します。この3ペインをee4vウィンドウの素材選択と単独Libraryへ組み込みます。ワークフローは専用のAssetManagerViewStateを保持し、通常と同じ派生アセット追加カードから選択Item IDを作成開始callbackへ渡します。AssetManagerの画面構成と追加カードの描画は通常版と共有します。作成元をそのItemに固定してVariantを生成し、作成結果を編集画面へ渡します。
