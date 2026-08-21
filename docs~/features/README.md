# Editor 機能

`Editor/Feature` にある機能の概要です。公開 API を持つ機能は個別のリファレンスへリンクします。

検索、追加、閉じる、固定などの汎用操作・状態アイコンにはMicrosoft Fluent UI System Iconsを使用します。Scene、GameObject、FolderなどUnityの実体やEditor概念を指すアイコンにはUnity組み込みアイコンを使用します。Project内アセットのサムネイルと利用者が指定したTextureは実体表現として維持します。

## Project

| 機能 | 概要 | API リファレンス |
|---|---|---|
| Asset Protection | AssetManagerから取り込んだAssetをGUIDで判定し、直接編集と保存を防ぎます。 | [Asset Protection](./asset-protection.md) |
| FolderContentOverlay | Project のフォルダーへ配下の代表的なアセットアイコンを重ねて表示します。アセット変更時は対象フォルダーと祖先のキャッシュを更新します。 | なし |
| ProjectStyle | Project のフォルダーへ背景色とアイコンを設定します。 | [ProjectStyle](./project-style.md) |
| ProjectTabs | Project ウィンドウへフォルダータブ、移動履歴、固定タブを追加します。 | [ProjectTabs](./project-tabs.md) |

## Hierarchy

| 機能 | 概要 | API リファレンス |
|---|---|---|
| DepthIndicator | GameObject の親子関係を示すガイド線を Hierarchy に描画します。`HideInHierarchy` の対象は階層計算から除きます。 | なし |
| HierarchyDecoration | `---` で始まる空の GameObject を区切り線として描画します。`GameObject/HierarchyDecoration/div` から区切りを作成できます。 | なし |
| HierarchyStyle | GameObject の背景色とアイコンを設定します。Hidden Objects の一覧表示、非表示、再表示もこの機能に含みます。 | [HierarchyStyle](./hierarchy-style.md) |
| SceneSwitcher | Hierarchy のシーン見出しからシーンを検索し、置換、追加、作成を行います。新規シーンは共通アセットルート配下の`Scene`へ保存します。 | なし |

## Avatar

| 機能 | 概要 | API リファレンス |
|---|---|---|
| Face Expression | BlendShape表情を作成し、標準ハンドジェスチャーへ割り当てます。アバター複製を保持する軽量プレビューを使用します。 | [Face Expression](./face-expression.md) |

## Editor

| 機能 | 概要 | API リファレンス |
|---|---|---|
| WindowGroup | 同じグループのウィンドウをまとめて前面へ移します。 | [WindowGroup](./window-group.md) |

## 共有実装

ProjectStyle と HierarchyStyle は `Ee4v.ItemStyle.Editor` の値、保存処理、編集ウィンドウを共有します。外部コードから各機能を操作する場合は `ProjectStyleApi` または `HierarchyStyleApi` を使用します。
