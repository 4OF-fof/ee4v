# ee4v 開発資料

ee4v の実装を利用する開発者向け資料です。

## 資料

- [Core](./core/README.md)
  - 機能、公開インターフェース、副作用
- [Editor 機能](./features/README.md)
  - 各機能の概要
  - ProjectStyle、HierarchyStyle、ProjectTabs の API リファレンス
- [AssetManager](./asset-manager.md)
  - UI向けAPI、ドメイン規則、SQLite保存、Eagle同期
- [AssetManager DB 設計](./asset-manager-db.md)
  - 永続化する値、その目的、table ごとの制約
  - DB に保持しない一時データ
- [Unity 6 移行メモ](./unity6.md)
  - Unity 6 で不要にする仕組み
  - Unity に依存する主なファイル

## ソース側の資料

- [Core の概要](../src/Editor/Core/README.md)
- [Core / UI の設計方針](../src/Editor/ARCHITECTURE.md)
- [作業規則](../AGENTS.md)
