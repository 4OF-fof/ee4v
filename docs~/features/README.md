# Editor feature map

`src/Editor/Feature`配下の所有範囲と、変更時に読む契約の索引です。機能モジュールは原則として互いに依存せず、CoreとUIの公開APIを利用します。

## 共通のUI契約

- 検索、追加、閉じる、固定などの汎用操作・状態アイコンにはMicrosoft Fluent UI System Iconsを使用する。
- Scene、GameObject、FolderなどUnityの実体やEditor概念を指すアイコンにはUnity組み込みアイコンを使用する。
- Project内アセットのサムネイルと利用者が指定したTextureは実体表現として維持する。
- 文字を描画する要素は`UiTextFactory`経由で作成する。詳細な規則はルートの`AGENTS.md`を参照する。
- Domain固有の入力、一覧、Card、PanelもCore UIと同じ境界線、面、角丸、hover、選択状態を使用する。機能固有の配置と意味を持つ状態色は維持する。

## Project

| module | 所有する振る舞い | 実装 | 契約 |
|---|---|---|---|
| FolderContentOverlay | Projectのフォルダーへ配下の代表的なAssetアイコンを重ねる。Asset変更時は対象フォルダーと祖先のcacheを更新する。 | `Feature/Project/FolderContentOverlay` | この表 |
| ProjectStyle | Projectのフォルダーへ背景色とアイコンを設定する。 | `Feature/Project/ProjectStyle` | [ProjectStyle](./project-style.md) |
| ProjectTabs | Project Windowへフォルダータブ、移動履歴、固定タブを追加する。 | `Feature/Project/ProjectTabs` | [ProjectTabs](./project-tabs.md) |

## Hierarchy

| module | 所有する振る舞い | 実装 | 契約 |
|---|---|---|---|
| DepthIndicator | GameObjectの親子関係を示すガイド線を描画する。`HideInHierarchy`の対象は階層計算から除く。 | `Feature/Hierarchy/DepthIndicator` | この表 |
| HierarchyDecoration | `---`で始まる空のGameObjectを区切り線として描画する。 | `Feature/Hierarchy/HierarchyDecoration` | この表 |
| HierarchyStyle | GameObjectの背景色とアイコン、Hidden Objectsの非表示と復元を扱う。 | `Feature/Hierarchy/HierarchyStyle` | [HierarchyStyle](./hierarchy-style.md) |
| SceneSwitcher | HierarchyのScene見出しからSceneを検索し、置換、追加、作成する。 | `Feature/Hierarchy/SceneSwitcher` | この表 |

## Avatar

| module | 所有する振る舞い | 実装 | 契約 |
|---|---|---|---|
| Face Expression | BlendShape表情の作成、表情Group、preset、Gesture割り当てを扱う。 | `Feature/Avatar/FaceExpression` | [Face Expression](./face-expression.md) |
| PhysBone Collider | Collider候補の生成、preview、PhysBoneへの割り当て、Prefab生成を扱う。 | `Feature/Avatar/PhysBoneCollider` | [PhysBone Collider](./physbone-collider.md) |
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

`src/Editor/Mcp`は機能moduleではなく外部integration層です。Face ExpressionとPhysBone Colliderの公開API、AssetManager Contractsを組み合わせますが、機能module間の参照は作りません。MCP固有の契約は[`../mcp.md`](../mcp.md)で管理します。

## 更新条件

- moduleの所有範囲、実装場所、機能間共有が変わった場合はこの索引を更新する。
- 公開API、永続化、副作用、失敗時の動作が変わった場合は対応する個別資料を更新する。
- UIから観測できる操作や注意事項が変わった場合は`../human`配下の対応する機能ページも更新する。ページ構成が変わる場合は[`../human/index.html`](../human/index.html)と全ページの共通ナビゲーションも更新する。
