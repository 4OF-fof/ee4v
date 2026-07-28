# AssetManager

AssetManager は Booth / Eagle / ee4v 管理ファイルを統合して扱うためのドメインです。外部 datasource の情報は正本として直接使わず、AssetManager 側 DB に snapshot とアプリ独自情報を保持します。

## 設計方針

- Booth item ID を商品同一性の強いキーとして扱う
- Eagle / BLM は datasource として読み取り、AssetManager DB に取り込む
- ユーザー上書き可能な表示情報と datasource 由来 snapshot を分ける
- file 実体の解決は origin ごとに行い、Item とは論理的に分離する
- file は Item 配下の論理要素として扱い、必要に応じて Version Group / Variant Group で束ねる

## Module 構造

- `Contracts`: Unity 非依存の `IAssetManager`、request / response、change contract
- `Domain`: Unity・SQLite・Core に依存しない invariant と policy
- `Application`: use case と use case 単位の port。`IAssetManager` の instance 実装
- `Infrastructure`: SQLite、datasource、filesystem、network、Unity adapter
- `UI`: 注入された `IAssetManager` と UI state だけを扱う Presentation adapter
- `Composition`: Module の依存を組み立てる唯一の `[InitializeOnLoad]`

依存方向は `Composition -> UI / Infrastructure -> Application -> Domain` とし、UI は
Contracts 経由で Application facade を利用します。Contracts、Domain、Application は
`noEngineReferences: true` であり、UI と Infrastructure は互いを参照しません。

static `AssetManagerApi` と static change event は使用しません。composition root が生成した
`IAssetManager` instance を controller / presenter へ注入します。
旧 `api` directory、`Ee4v.AssetManager.Api` namespace、Api asmdef は残さず、
外部技術の実装は `Infrastructure`、BLM / Eagle reader は `Infrastructure/Datasources` に置きます。
SQLite は `Infrastructure/Persistence/SQLite`、filesystem / ZIP / persistent cache は
`Infrastructure/Files`、Unity package import は `Infrastructure/Unity` に分離します。

## 互換性

Clean Architecture 移行前の実装との後方互換は提供しません。

- 旧 `AssetManagerApi` の signature、namespace、型名、asmdef 名を維持しない
- 旧 static API / event や `api` directory 向けの互換 facade を置かない
- 既存 DB、cache、Module 固有 setting の migration を行わず、削除・再生成または既定値からの再作成を前提とする

現在の契約と機能要件は本ページ、[AssetManager API](../api/asset-manager.md)、
[DB Schema](./schema.md) を正本とします。

Import Target の path 正規化・file 配下制約・重複排除は `Domain` が所有します。
`Application` use case は検証後に transactional store へ一括置換を依頼し、commit 成功後だけ
`FileImportTargets`、`FileTree` の順で change を発行します。SQLite はこの規則を判断しません。

Item / Tag / Collection / Group の必須値、Dependency の自己参照・target 種別、
Smart Collection condition の成立条件も Domain policy が所有します。Application は
command port を呼ぶ前に policy を適用し、失敗を Contracts の安定した error code へ変換します。

Application port は read/query と command/write を分離しています。検索・thumbnail・file tree は
read store から read model を直接受け取り、command は atomic な write store の成功後だけ
notification を発行します。全面的な CQRS や汎用 repository は導入しません。

Unity への file import は Application の `ImportFileUseCase` が item/file の所属、path 解決、
Import Target の Domain policy を確認して `AssetImportPlan` を生成します。Infrastructure は
plan に従って filesystem / ZIP / Unity package import を実行します。

起動時 sync も Composition から SQLite helper を直接呼びません。Application の
prepare/apply use case を通し、Infrastructure の prepared state は opaque token として保持します。
conflict preview と overwrite 判断の契約は Application が所有し、通常 sync と起動時 sync は
同じ sync port を使用します。

UI の表示設定、File Tree の filesystem 読み取り、ZIP metadata 読み取りは Contracts の
`IAssetManagerUiPreferences`、`IAssetFileSystemReader`、`IAssetArchiveReader` として
注入します。background 実行と Unity main thread への復帰も
`IAssetManagerUiScheduler` を Composition が注入します。UI assembly は Infrastructure、
Application 実装、SQLite、filesystem具象、Core Settings、`Task.Run`、
`EditorApplication.delayCall` を参照しません。
`IAssetManager.Changed` は command の実行 thread で通知されるため、UI subscriber は
`IAssetManagerUiScheduler.RunOnMainThread(...)` を通してから UI Toolkit の state を更新します。

setting の定義と登録は Composition が所有します。Infrastructure は
`IAssetManagerInfrastructureSettings` の typed snapshot provider を受け取り、
`CoreSettings`、`ISettingsService`、Composition の setting 定義を直接参照しません。

## Unity メニュー

統合 AssetManager window は Unity メニューの `ee4v/Asset Manager` から開きます。
分割 window は `ee4v/Window` 配下から個別に開きます。

- `ee4v/Window/Navigation`: 単独 Navigation window
- `ee4v/Window/Infomation`: 単独 Infomation window
- `ee4v/Window/Main View`: 単独 Main View window

Navigation の `Library` 見出しにある同期操作から、設定済みの BLM / Eagle datasource を
手動同期できます。起動時の自動同期は user setting に従い、手動同期は同設定の有効・無効に
かかわらず、存在する datasource path を対象にします。どちらも同じ変更確認、競合確認、
background activity の経路を使用します。手動同期の完了時には Collection 一覧も再取得し、
schema 不整合を含む Navigation のエラー状態を現在の DB に対して再判定します。
同期 button は押下後に focus 枠を保持しません。

## UI の責務

- `MainViewController` は `MainViewHost` ごとに生成し、その表示セッションの navigation、履歴、grid 列数、取得済み一覧 cache、非同期 load と cancellation を所有する。統合 window と単独 window の controller instance は共有しない
- `MainView` は AssetManager 固有の UI controller として、検索文字列、一覧・詳細 mode、選択中 item などの画面状態を持ち、Core UI component へ描画 state を渡す
- `MainViewHost` は `MainToolbar`、`NavigationPanel`、`MainView` と controller の event 配線だけを担当する。toolbar と navigation panel は入力を通知し、渡された値を描画するだけで、設定や他 component を直接操作しない
- Navigation は `All`、Booth snapshot を持つ Item の `BOOTH Items`、通常 Collection 未所属かつ Smart Collection にも一致しない `Uncategorized`、Item grid とは別の tag 一覧ページを開く `Tags` を提供する
- Navigation の固定項目の下では通常 Collection と Smart Collection を 1 つの Collection tree に統合し、`ParentCollectionId` と `SortOrder` に従って両種別をまたいだ親子関係と兄弟順を表示する。Smart Collection は通常 Collection の子にできるが、Smart Collection 自身は通常・Smart のどちらの Collection も子に持てない。Collection 全体の Foldout は置かず、子を持つ各Collection行の disclosureで個別に開閉する。行は13px文字、14pxアイコン、22px高とし、親子をdepth lineで結ぶ。Asset Gridと同様にCtrl/Command+clickは個別の追加・解除、Shift+clickは表示中のanchorからの範囲選択を行う。最後の1件もmodifier clickで解除でき、treeの空白clickまたはEscapeで全選択を解除する。選択した複数行は表示順を保ったブロックとしてドラッグできる。ドラッグ先の行中央ではその子の末尾へ移動し、行の上端・下端では同じ階層の前後へ挿入する。treeの行以外からNavigation下端まで続く空き領域へドロップするとroot末尾へ移動し、root移動時の外枠は表示しない。cycle、Smart Collection 配下への配置、配置が変わらないdropは受け付けない。Collection構造変更は専用の`Collections` changeだけを発行し、Main Viewの再検索を起こさない
- 通常 Collection の表示アイコンは塗り付きの folder に固定し、Smart Collection は組み込みアイコンまたは任意 Texture asset を使用して区別する。Collection 見出しには `+` ボタンを 1 つだけ表示し、クリック時の context menu から通常 Collection または Smart Collection の作成を選ぶ。選択後は枠線付きのアンカー popup を開き、実測したコンテンツ高に合わせて高さを調整し、最大高を超えた場合だけ縦スクロールする。通常 Collection は名前だけを入力し、Smart Collection は名前、アイコン、match mode、条件一覧を入力する。条件追加buttonは左端に`+` iconを表示し、操作後のfocus枠を残さない。Smart Collection の条件 field は名前、説明、タグ、ファイル名、拡張子に限定する
- 単独 Navigation window の選択は standalone view session を介して単独 Main View window と共有し、`Tags` を含む固定ページと Collection の選択を Main View 側へ通知する
- `InfomationPanel` は渡された選択 state を描画し、詳細表示要求を通知する。統合 window は同じ layout 内で直接配線し、単独 Main View / Infomation window は Composition が生成する揮発性の standalone view session を介して選択 state と詳細 tab 要求だけを共有する。standalone session は現在値を保持するため、Infomation window を後から開いた場合も現在の選択を復元する
- Core の `ItemGrid` / `ItemImage` は渡された state の描画と UI Toolkit 固有の layout・resource 処理に限定する。AssetManager の cache key、履歴、設定値は保持しない
- grid 列数の実効値は controller ごとに独立した表示セッション state とする。user setting は新しい `MainViewHost` を生成するときのデフォルト値としてだけ読み取り、開いている他 window の実効値や slider へは反映しない

## 現在の実装状態

- schema v6、Item/File/Tag/Collection/Dependency/Import Target API を実装済み
- BLM `data.db` と Eagle library の読み取り同期、安定した source identity、datasource tag、欠落 origin の reconciliation を実装済み
- BLM snapshot に任意の `preferences` table がない場合も item metadata の同期を継続し、item directory path だけを未設定として扱う
- Main View と File Tree は DB / filesystem 読み込みを background で行い、前回 load の cancellation に対応
- Main View のグリッドサイズ変更は、その `MainViewHost` の controller が持つ実効値と取得済み item state の再配置だけを更新し、user setting、他 window、DB、thumbnail、表示 cacheへ影響させない。`ItemGridItemsPerRow` user setting は新規 controller のデフォルト値であり、静的な列数範囲（1〜12）は同 setting の `SettingRange<int>` が所有する。実効下限は各 `ItemGrid` の viewport 幅と高さから、card が高さ制限によって縮小されて右側に未使用領域を生じない最大 card 幅と column gap 16 px を使って必要な列数を切り上げて求める。window の拡大または高さの縮小で下限が現在値を超えた場合は controller、grid、toolbar slider を同じ値へ引き上げ、下限未満の入力を許可しない。下限が下がった場合は現在値を維持し、その後の slider 操作で新しい下限まで選択できる。狭い表示領域では column gap と card 幅を縮め、高さが不足する場合は card 幅と行高を抑える fallback により表示領域へ収める
- File Tree は完成済みツリーを Unity Editor のメモリ上に最大 64 件共有し、同一 item / file の再表示では background 確認と loading 表示を省略する。Import Target と Version Group の代表変更は cache 上の表示 state へ反映して再構築せず、構造を含む AssetManager の変更時だけ全件を破棄する。cache は Unity 終了または domain reload で揮発する
- File Tree の Variant Group と Version Group は異なる accent で表示し、行末の localized meta label で group 種別を識別できる
- Version Group 配下の file root は context menu から代表ファイルに設定でき、現在の代表は Import Target と同じ配色で識別できる。設定後は Main View 全体を再読み込みせず File Tree の行 state だけを更新する。file root 自体は Import Target にできない
- File Tree の context menu から Import Target、または個別の実 file を Unity へ取り込める。Version Group は代表 file root と同じ対象を使う。`.unitypackage` は Unity package import を実行し、user setting `assetManager.showUnityPackageImportDialog` で内容選択画面の表示を切り替えられる。それ以外は `Assets/<asset name>/<file name>/` 配下へ相対 path を維持して copy する
- File Tree の ZIP metadata は、thumbnail と同じ cache root の `<ee4v global path>/cache/file-tree` に永続化し、更新日時と file size の検証を background で行う。ZIP 全体が ZIP と同名の単一 root folder に包まれている場合は、その folder を File Tree と import 先の相対 path から省略する
- File Tree の PNG / JPEG / PSD file に hover すると `ImageTooltip` で preview と file 名を表示する。通常 file と ZIP 内 entry の両方を background で読み込み、PSD の合成画像は Raw / RLE compression の 8 / 16 bit Gray / RGB / CMYK を thumbnail size に縮小しながら最大 1 GiB まで stream decode する。PNG / JPEG の encoded data は 64 MiB を上限とする。user setting `assetManager.showFileTreeImageTooltip` を無効にすると通常の text tooltip に切り替わる
- File Tree の要素、または Main View の file card をダブルクリックすると、通常 file、directory、group、ZIP 内 entry を同じ経路で Main View の詳細表示へ開く。詳細は Main View の履歴に入り、戻る・進むで一覧と往復できる。ZIP 内 entry のパンくずには所属 ZIP 名を item と target の間に表示する。単独 Infomation window からも単独 Main View window へ表示する。現時点の詳細内容は要素名のみ
- Unity Editor session 開始時の BLM / Eagle datasource sync は background で変更確認を先行し、DB 内にも同じ source の成功状態があり、かつ `cache/sync` の前回成功 fingerprint と一致する場合だけ DB sync と UI reload を省略する。DB を削除・再生成した場合は fingerprint が残っていても再同期する
- Unity側の item 情報が同期元より新しい競合は `DiffConfirmationOverlay` で現在値と同期元値を比較し、上書きまたは今回の同期キャンセルを選択できる
- 起動時に自動同期する source は user setting から個別に有効・無効を選択できる
- background activity 中は統合 AssetManagerと単独 Main View windowの右下に `StatusOverlay` を表示する。単独 Navigation windowには表示しない
- File Tree の構築状況は File Tree 内の loading message だけで表示し、`StatusOverlay` には追加しない
- Main View の一覧検索は関連 tag / file / Booth snapshot を構築しない軽量 summary API を使う。thumbnail URL は一覧分を一度の DB 接続で解決して最大 4 並列取得する。描画用 Texture cache は画像内容を含む key と LRU 上限を持つ
- Eagle Booth bridge は loopback bind、BOOTH origin 制限、session token、1 MiB request body 上限を適用

未完了なのは全件を辿る pagination、toolbar 検索・絞り込みの接続です。Smart Collection は現状 Item ごとの条件評価を含むため、大規模 DB 向けの query 一括化も今後の課題です。

## Pages

- [AssetManager API](../api/asset-manager.md)
- [DB Schema](./schema.md)
- [Datasource Data Elements](./data-elements.md)
- [BLM data.db Structure](../datasource/blm_db_structure.md)
