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
