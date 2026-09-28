# ee4v 開発資料

`docs~` の Markdown は現行の実装と設計を確認するための資料です。作業規則はリポジトリルートの [`AGENTS.md`](../AGENTS.md)、assembly と依存方向は [`src/Editor/ARCHITECTURE.md`](../src/Editor/ARCHITECTURE.md) を参照してください。

| 対象 | 資料 |
| --- | --- |
| Core の公開契約 | [core/README.md](./core/README.md) |
| Editor 機能 | [features/README.md](./features/README.md) |
| AssetManager と派生アセット | [asset-manager.md](./asset-manager.md) |
| Asset Protection | [asset-protection.md](./asset-protection.md) |
| ee4v MCP | [mcp.md](./mcp.md) |
| BOOTH・Eagle 連携 | [eagle.md](./eagle.md)、[scriptcat.md](./scriptcat.md) |
| Unity 6 移行時の確認 | [unity6.md](./unity6.md) |
| 既存テストの保証範囲 | [test.md](./test.md) |

機能を変更するときは該当する資料を更新します。公開 API、保存形式、副作用、失敗時の動作と、UI から確認できる現在の振る舞いを記載します。未実装の設計は実装済みの説明と混在させず、必要な場合だけ明示します。同じ契約は所有する資料に一度だけ記載し、他からはリンクします。
