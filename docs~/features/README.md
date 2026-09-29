# Editor feature map

`src/Editor/Feature`配下の所有範囲と、変更時に読む契約の索引です。機能モジュールは原則として互いに依存せず、CoreとUIの公開APIを利用します。

機能 UI の共通部品は [Core UI](../core/ui.md) を参照します。

## Project

| module | 所有する振る舞い | 実装 | 契約 |
|---|---|---|---|
| FolderContentOverlay | Projectのフォルダーへ配下の代表的なAssetアイコンを重ねる。Assetパスを一度索引化して各フォルダーの直下だけを集計する。Asset変更時は索引と対象フォルダー・祖先の集計cacheを更新する。 | `Feature/Project/FolderContentOverlay` | この表 |
| ProjectStyle | Projectのフォルダーへ背景色とアイコンを設定する。 | `Feature/Project/ProjectStyle` | [ProjectStyle](./project-style.md) |
| ProjectTabs | Project Windowへフォルダータブ、移動履歴、固定タブを追加する。 | `Feature/Project/ProjectTabs` | [ProjectTabs](./project-tabs.md) |

## Hierarchy

| module | 所有する振る舞い | 実装 | 契約 |
|---|---|---|---|
| DepthIndicator | GameObjectの親子関係を示すガイド線を描画する。`HideInHierarchy`の対象は階層計算から除く。 | `Feature/Hierarchy/DepthIndicator` | この表 |
| HierarchyDecoration | `---`で始まる空のGameObjectを区切り線として描画する。描画時のComponent確認は名前が一致する行に限る。 | `Feature/Hierarchy/HierarchyDecoration` | この表 |
| HierarchyStyle | GameObjectの背景色とアイコン、Hidden Objectsの非表示と復元を扱う。 | `Feature/Hierarchy/HierarchyStyle` | [HierarchyStyle](./hierarchy-style.md) |
| SceneSwitcher | HierarchyのScene見出しからSceneを検索し、置換、追加、作成する。 | `Feature/Hierarchy/SceneSwitcher` | この表 |

## Avatar

| module | 所有する振る舞い | 実装 | 契約 |
|---|---|---|---|
| Face Expression | BlendShape表情の作成、表情Group、preset、Gesture割り当てを扱う。 | `Feature/Avatar/FaceExpression` | [Face Expression](./face-expression.md) |
| Play Mode Component Suppression | Play Mode向けNDMF処理で指定componentをbuild対象Avatarから除く。 | `Feature/Avatar/PlayModeComponentSuppression` | [Play Mode Component Suppression](./play-mode-component-suppression.md) |

## Editor

| module | 所有する振る舞い | 実装 | 契約 |
|---|---|---|---|
| WindowGroup | 同じGroupのEditorWindowをまとめて前面へ移す。 | `Feature/WindowGroup` | [WindowGroup](./window-group.md) |

## 共有実装

| assembly | 使用する機能 | 共有理由 | 実装 |
|---|---|---|---|
| `Ee4v.ItemStyle.Editor` | ProjectStyle、HierarchyStyle | 背景色とアイコンの値、保存処理、編集Windowが同じ契約を持つため | `Feature/Shared/ItemStyle` |

外部コードから各機能を操作する場合は、共有実装を直接参照せず`ProjectStyleApi`または`HierarchyStyleApi`を使用します。

`src/Editor/Mcp`は機能moduleではなく外部integration層です。Face Expressionの公開APIとAssetManager Contractsを組み合わせますが、機能module間の参照は作りません。MCP固有の契約は[`../mcp.md`](../mcp.md)で管理します。

AssetManager UIは例外として、取り込み先フォルダーの初期アイコン設定に`ProjectStyleApi`、改変画面の埋め込み表情エディターとBlendShape分類にFace Expressionの公開APIを使用します。`ItemStyle`の直接参照は不要です。ProjectStyleとFace ExpressionからAssetManagerへの逆方向参照はありません。
