# Editorのモジュール構成

`src/Editor/Core`と`src/Editor/UI`は、Editor機能が共通で使用する基盤です。Coreは契約、標準実装、Unity Editorとの接続、共通表示処理を所有します。UIは機能に依存しないUI Toolkit部品を所有し、Core.PresentationはUIを組み合わせて共通表示処理を提供します。

## 依存方向

機能モジュールはCoreとUIに依存できますが、原則として別の機能モジュールへ依存しません。機能間の状態や処理を直接参照せず、変更理由を各モジュール内へ閉じます。

```text
Feature ──→ Core
   ├──────→ UI ──→ Core
   └──────→ Core.Presentation ──→ UI
```

複数機能で共有する実装は`Feature/Shared`へ置きます。共有モジュールを追加または利用するときは、使用する機能と共有理由を`docs~`に記載します。現在はProjectStyleとHierarchyStyleが`Ee4v.ItemStyle.Editor`を共有します。

AssetManagerは例外として、他の機能モジュールへ依存できます。AssetProtectionは`AssetManager/AssetProtection`に置くAssetManager内部モジュールです。

## assembly境界

- 機能モジュールはCoreの公開APIを使用します
- Coreから機能assemblyへの`InternalsVisibleTo`は追加しません
- 同じモジュール内の層分割とテストassemblyには、必要な範囲で`InternalsVisibleTo`を使用できます
- asmdefにはコンパイルで実際に使用するassemblyだけを記載します
- NDMFなど外部Pluginとの接続は専用asmdefへ分離します

AssetManager内はContracts、Domain、Application、Infrastructure、UI、AssetProtectionに分割します。この層分割は一つの機能モジュール内の依存として扱います。

`Ee4v.Mcp.Editor`は外部クライアントから複数機能を呼び分けるintegration／composition rootです。MCP protocolとtransport、tool registry、JSON変換だけを所有し、Face Expression、PhysBone Collider、AssetManagerの公開境界へ依存できます。この例外から機能module同士への依存は追加せず、domain処理は所有moduleの公開APIへ置きます。詳細は[`docs~/mcp.md`](../../docs~/mcp.md)を参照します。

## 単独機能とee4vウィンドウ

各機能は単独で操作できる入口を持ち、ee4vウィンドウでも同じview／controllerを組み合わせます。ee4vウィンドウ専用だった体型・パーツ、Material、Prefab構成には単独Windowを用意します。

AssetManager内部の`AssetModificationWorkflowView`が改変の共通実装を所有し、各Windowは表示モードとlayout復元を扱います。AssetManagerの検索・一覧・情報は`AssetManagerWorkspaceView`で既存の3つの`AssetManagerView`を共有sessionへ接続します。この3ペインをee4vウィンドウの素材選択と単独Libraryへ組み込みます。ワークフローは専用のAssetManagerViewStateを保持し、通常と同じ派生アセット追加カードから選択Item IDを作成開始callbackへ渡します。AssetManagerの画面構成と追加カードの描画は通常版と共有します。作成元をそのItemに固定してVariantを生成し、作成結果を編集画面へ渡します。改変中のheader、カテゴリ、Preview、編集ペインは既存の構成を維持します。機能module間の新しい依存とCoreの機能登録APIは追加しません。
