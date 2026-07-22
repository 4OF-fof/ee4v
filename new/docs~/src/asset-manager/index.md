# AssetManager

AssetManager は Booth / Eagle / ee4v 管理ファイルを統合して扱うためのドメインです。外部 datasource の情報は正本として直接使わず、AssetManager 側 DB に snapshot とアプリ独自情報を保持します。

## 設計方針

- Booth item ID を商品同一性の強いキーとして扱う
- Eagle / BLM は datasource として読み取り、AssetManager DB に取り込む
- ユーザー上書き可能な表示情報と datasource 由来 snapshot を分ける
- file 実体の解決は origin ごとに行い、Item とは論理的に分離する
- file は Item 配下の論理要素として扱い、必要に応じて Version Group / Variant Group で束ねる

## 現在の実装状態

- schema v2、Item/File/Tag/Collection/Dependency/Import Target API を実装済み
- BLM `data.db` と Eagle library の読み取り同期、安定した source identity、datasource tag、欠落 origin の reconciliation を実装済み
- Main View と File Tree は DB / filesystem 読み込みを background で行い、前回 load の cancellation に対応
- File Tree は完成済みツリーを Unity Editor のメモリ上に最大 64 件共有し、同一 item / file の再表示では background 確認と loading 表示を省略する。Import Target と Version Group の代表変更は cache 上の表示 state へ反映して再構築せず、構造を含む AssetManager の変更時だけ全件を破棄する。cache は Unity 終了または domain reload で揮発する
- File Tree の Variant Group と Version Group は異なる accent で表示し、行末の localized meta label で group 種別を識別できる
- Version Group 配下の file root は context menu から代表ファイルに設定でき、現在の代表は Import Target と同じ配色で識別できる。設定後は Main View 全体を再読み込みせず File Tree の行 state だけを更新する。file root 自体は Import Target にできない
- File Tree の context menu から Import Target、または個別の実 file を Unity へ取り込める。Version Group は代表 file root と同じ対象を使う。`.unitypackage` は Unity package import を実行し、user setting `assetManager.showUnityPackageImportDialog` で内容選択画面の表示を切り替えられる。それ以外は `Assets/<asset name>/<file name>/` 配下へ相対 path を維持して copy する
- File Tree の ZIP metadata は、thumbnail と同じ cache root の `<ee4v global path>/cache/file-tree` に永続化し、更新日時と file size の検証を background で行う。ZIP 全体が ZIP と同名の単一 root folder に包まれている場合は、その folder を File Tree と import 先の相対 path から省略する
- Unity Editor session 開始時の BLM / Eagle datasource sync は background で変更確認を先行し、`cache/sync` の前回成功 fingerprint と一致する source は DB sync と UI reload を省略する
- Unity側の item 情報が同期元より新しい競合は `DiffConfirmationOverlay` で現在値と同期元値を比較し、上書きまたは今回の同期キャンセルを選択できる
- 起動時に自動同期する source は user setting から個別に有効・無効を選択できる
- background activity 中は統合 AssetManager の Main View と単独 Main View window の右下だけに `StatusOverlay` を表示する
- File Tree の構築状況は File Tree 内の loading message だけで表示し、`StatusOverlay` には追加しない
- thumbnail は最大 4 並列取得に対応
- Eagle Booth bridge は loopback bind、BOOTH origin 制限、session token、1 MiB request body 上限を適用

未完了なのは全件を辿る pagination、toolbar 検索・絞り込みの接続です。Smart Collection は現状 Item ごとの条件評価を含むため、大規模 DB 向けの query 一括化も今後の課題です。

## Pages

- [AssetManager API](../api/asset-manager.md)
- [DB Schema](./schema.md)
- [Datasource Data Elements](./data-elements.md)
- [BLM data.db Structure](../datasource/blm_db_structure.md)
