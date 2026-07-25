# Roadmap

phase 1 は `old` のリファクタや段階移植ではなく、`new` の再実装に向けた基盤整備として進める。`old` は要件や既存挙動を確認するための参照資料として扱い、そのままの構成・API・データ設計は持ち込まない。

## AssetManager

- [x] Database
    - [x] Booth Library Manager Helper
        - 公式クライアントであるBooth Library Managerとの連携を担うための実装
        - metadata は `%appdata%/pm.booth.library-manager/data.db` に保存されており、これを操作することによって連携を行う
        - 詳細は [BLM data.db Structure](./datasource/blm_db_structure.md) を参照
        - アセットの保存先はユーザーが指定
        - [x] `%appdata%/pm.booth.library-manager/data.db`の存在確認
        - [x] dbファイルにアクセスし、各種操作を行うAPIを実装
            - まずは読み取り操作のみ実装
            - [x] 商品情報
                - [x] 商品名
                - [x] 商品URL
                - [x] 商品説明
                - [x] 商品サムネイル(URL)
                - [x] ショップ名
                - [x] ショップURL
                - [x] ショップサムネイル(URL)
                - [x] タグ
    - [x] Eagle Data Source
        - Eagle のローカル API と `_boothmeta.json` item を組み合わせて連携する
        - AssetManager 側は読み取りのみを前提とし、Eagle への書き戻しは行わない
        - Booth 情報は folder 本体ではなく `_boothmeta.json` 本文に保持する
        - `TargetRoot` 未指定時は `VRCAsset` を同期対象 root とする
        - `TargetRoot` 配下の descendant folder を AssetManager item として同期する
        - folder 内の通常 Eagle item を AssetManager file として同期する
        - `_boothmeta.json` は item 情報として使い、file としては登録しない
        - `_boothmeta.json` を持たない folder も対象には含め、folder 名と空 description で item 化する
        - [x] folder ID / item ID を安定 identity として保持する
        - [x] rename と snapshot からの欠落を reconciliation する
        - [x] datasource tag を user tag と分離して保持する
        - [x] Booth bridge に origin 制限、session token、request size 上限を追加する
    - [ ] AssetManager UI completion
        - [ ] toolbar search / filter を API query に接続する
        - [ ] 200 件を超える一覧の pagination を追加する
        - [x] import target に基づく Unity import 実行を追加する
        - [ ] Smart Collection の Item ごとの query を一括化する

## Injector

- [ ] HierarchyItemStyle
    - [ ] HierarchyのGameObjectのアイコンや背景色を変更できる機能を実装する
    - 専用ダミーコンポーネントをGameObjectにアタッチすることで、スタイルを適用する
- [ ] ComponentVisualizer
    - [ ] Hierarchy上でGameObjectにアタッチされたComponentのアイコンを表示する機能を実装する
    - [ ] アイコンクリック時に該当ComponentのInspectorを別ウィンドウで表示する機能を実装する
- [x] DepthIndicator
    - [x] Hierarchy上でGameObjectの階層の深さを示すインジケーターを表示する機能を実装する
- [ ] HiddenObject
    - [ ] Hierarchyから特定のGameObjectを非表示にする機能を実装する
    - [ ] 非表示のGameObjectを管理するUIを提供し、復元を可能にする
- [ ] SceneSwitcher
    - [ ] HierarchyのScene名をクリックするとSceneを切り替えられるウィンドウを開く機能を実装する
    - [ ] 複数Sceneを開いている場合は、クリックしたSceneを切り替え他のSceneはそのままにする
- [ ] StyledObject
    - [ ] `:div`や`:header`などの特定の名前を持つGameObjectを特別なスタイルで表示する機能を実装する

- [x] ProjectTab
    - [x] ProjectToolbarにタブを追加する機能を実装する
    - [x] タブごとに履歴を管理し、進む・戻るができるようにする
    - [x] CoreのEditorAPIを利用して現在位置を追跡し、履歴管理を実装する
    - [x] ドラッグとキーボード操作でタブを並び替える
    - [x] Project folderをタブ領域へdropしてタブを追加する
    - [x] folder locationを固定するpin tabとAssets Home tabを追加する
- [x] FolderStyle
    - [x] Alt操作でProject folderのアイコン色やアイコンを変更できる機能を実装する
- [x] ContentOverlay
    - [x] Folderのアイコンに重ねてフォルダ内に存在するアイテムの種類を示すアイコンを表示する機能を実装する
