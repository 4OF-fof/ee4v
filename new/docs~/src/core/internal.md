# 可視性と Internal

## 基本方針

- feature 内で閉じる型は `internal`
- `Core` から他 feature が使う共通 API だけを `public`
- test から触りたいだけの型を `public` にしない

test 用アクセスは `Editor/AssemblyInfo.cs` の `InternalsVisibleTo` で許可します。

## `Core/Internal` に置くもの

`Editor/Core/Internal` は「公開したくないが複数箇所から必要な基盤」を置く場所です。

置いてよいもの:

- package ルートや namespace 解決の補助
- Unity の internal / private API を叩く薄いラッパー
- bootstrap 制約の共通チェック

置かないもの:

- feature 固有ロジック
- domain 仕様
- 設定値や UI 状態

## Unity 内部 API

Unity の private / internal 実装に触る必要がある場合は、feature 側で直接 reflection しません。

- `Editor/Core/Internal/EditorAPI` に用途別 facade を作る
- reflection は `Backends` 側へ閉じる
- feature 側は facade だけを使う
