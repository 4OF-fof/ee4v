# ee4v agent development reference

この配下のMarkdownは、agentが実装、変更、レビューを行うときに参照する開発資料です。利用者向けの案内は [`human/index.html`](./human/index.html) に分離します。

## 読む順序

1. リポジトリルートの [`AGENTS.md`](../AGENTS.md) で対象環境と作業規則を確認する。
2. [`src/Editor/ARCHITECTURE.md`](../src/Editor/ARCHITECTURE.md) でassemblyと依存方向を確認する。
3. 変更対象に対応する下表の契約を読む。
4. 振る舞いまたはテストを変更する場合は [`test.md`](./test.md) で既存の保証範囲を確認する。

## 変更対象から引く

| 変更対象 | 主資料 | 確認する内容 |
|---|---|---|
| Coreの契約、標準実装、Unity接続部 | [`core/README.md`](./core/README.md) | 公開API、責務、副作用、失敗時の動作 |
| Editorの各機能 | [`features/README.md`](./features/README.md) | 機能の所有範囲、実装場所、機能別契約 |
| AssetManager | [`asset-manager.md`](./asset-manager.md) | UI契約、domain規則、Source同期、DB schema、未実装範囲 |
| Asset Protection | [`asset-protection.md`](./asset-protection.md) | 保護対象、編集抑止、AssetManagerとの境界 |
| ee4v MCP | [`mcp.md`](./mcp.md) | 接続、tool catalog、Avatar改変・AssetManager操作、安全境界 |
| BOOTHとEagleの連携 | [`scriptcat.md`](./scriptcat.md) | userscriptの画面別動作とbridge呼び出し |
| Unity 6への移行 | [`unity6.md`](./unity6.md) | 影響を受ける接続部と移行時の確認事項 |
| EditModeテスト | [`test.md`](./test.md) | テストが保証する契約と残す理由 |
| コードベース監査 | [`codebase-audit.md`](./codebase-audit.md) | 横断監査の結果、適用済み改善、残る構造課題 |

## Markdownに記録する情報

- 公開APIと入力、戻り値、通知、副作用、失敗時の動作
- UIから観測できる振る舞いと、実装が守る必要のある制約
- assembly、module、外部Plugin、保存先の境界
- DB schema、同期、importなどデータを失わないための規則
- 実装済み範囲と未実装範囲
- テストが保証する契約と、そのテストを残す理由

利用手順、機能紹介、画面を探すための案内など、人が製品を使うための情報はHTMLへ記録します。公開仕様を変更した場合はMarkdownを正として更新し、利用手順に影響する場合だけ対応するHTMLも同時に更新します。

## 記載規則

- 現在の実装、合意済みの仕様、未実装の設計を区別する。
- 未実装の内容は該当文の近くで明示し、実装済みと読める形で混在させない。
- 型名、menu path、保存先、数値はコードまたは設定で確認してから記載する。
- 同じ契約を複数のMarkdownへ複製せず、所有する資料へリンクする。
- 機能間共有を追加した場合は、利用する機能と共有理由を [`features/README.md`](./features/README.md) に記載する。
- StoryにはStory自身を除く実使用箇所を記載し、対応する機能資料にも実画面との関係を残す。

## Windowのメニュー登録

統合ウィンドウの表示名は日英ともに`ee4v`とし、`ee4v/ee4v`から開きます。単独ウィンドウは`ee4v/Window`配下で機能別にまとめ、Asset Managerの分割ペインは`Panes`、表情とジェスチャーは`Avatar`配下へ配置します。各Windowの所有moduleで`MenuItem`のpathとpriorityを明示し、次の順序で登録します。

| メニュー | priority |
|---|---|
| `ee4v/ee4v` | 0 |
| `ee4v/Window/Asset Manager/Library` | 100 |
| `ee4v/Window/Asset Manager/Open All` | 101 |
| `ee4v/Window/Asset Manager/Panes/Navigation` | 120 |
| `ee4v/Window/Asset Manager/Panes/Main` | 121 |
| `ee4v/Window/Asset Manager/Panes/Information` | 122 |
| `ee4v/Window/Avatar/Shape and Parts` | 200 |
| `ee4v/Window/Avatar/Materials` | 201 |
| `ee4v/Window/Avatar/Prefab Composition` | 202 |
| `ee4v/Window/Avatar/Face Expression/Editor` | 220 |
| `ee4v/Window/Avatar/Face Expression/Groups` | 221 |
| `ee4v/Window/Avatar/Face Expression/BlendShape Presets` | 222 |
| `ee4v/Window/Avatar/Gesture Assignment/Assignments` | 240 |
| `ee4v/Window/Avatar/Gesture Assignment/Settings` | 241 |
| `ee4v/Window/Window Groups` | 300 |
| `ee4v/Debug/Catalog` | 1100 |

## 人間向け資料

人間向けの入口は [`human/index.html`](./human/index.html) です。詳細はMCP、Asset Manager、Asset Protection、BOOTH / Eagle、Face Expression、Play Mode、Project、Hierarchy、Window Groups、設定のHTMLへ分割し、全ページを共通ナビゲーションで相互にリンクします。ビルド処理や外部CDNを必要としない静的HTMLとして保持します。
