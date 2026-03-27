# 前提
- phase1は`old`のリファクタや段階移植ではなく、`new`の再実装に向けた基盤整備として進める
- `old`は要件や既存挙動を確認するための参照資料として扱い、そのままの構成・API・データ設計を持ち込まない

# AssetManager

- [x] Database
    - [x] Booth Library Manager Helper
        - 公式クライアントであるBooth Library Managerとの連携を担うための実装
        - metadataは`%appdata%/pm.booth.library-manager/data.db`に保存されており、これを操作することによって連携を行う(`docs/blm_db_structure.md`を参照)
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
    - [] Eagle Data Source
        - Eagle のローカル API と Eagle plugin の attachment JSON を組み合わせて連携する
        - v1 は読み取りのみを前提とし、Eagle への書き戻しは行わない
        - Booth 情報は folder 本体ではなく plugin 管理データに保持する
        - Booth attachment 付き folder は同期対象とし、配下 item は `VRCAsset` タグ不要で扱う
        - standalone item は `VRCAsset` タグ付きのみ同期対象とする
        - 詳細設計は `docs/datasource/eagle/EagleDataSource.md` と `docs/datasource/eagle/EaglePlugin.md` を参照

# Injector

- [] HierarchyItemStyle
    - [] HierarchyのGameObjectのアイコンや背景色を変更できる機能を実装する
    - 専用ダミーコンポーネントをGameObjectにアタッチすることで、スタイルを適用する
- [] ComponentVisualizer
    - [] Hierarchy上でGameObjectにアタッチされたComponentのアイコンを表示する機能を実装する
    - [] アイコンクリック時に該当ComponentのInspectorを別ウィンドウで表示する機能を実装する
- [] DepthIndicator
    - [] Hierarchy上でGameObjectの階層の深さを示すインジケーターを表示する機能を実装する
- [] HiddenObject
    - [] Hierarchyから特定のGameObjectを非表示にする機能を実装する
    - [] 非表示のGameObjectを管理するUIを提供し、復元を可能にする
- [] SceneSwitcher
    - [] HierarchyのScene名をクリックするとSceneを切り替えられるウィンドウを開く機能を実装する
    - [] 複数Sceneを開いている場合は、クリックしたSceneを切り替え他のSceneはそのままにする
- [] StyledObject
    - [] `:div`や`:header`などの特定の名前を持つGameObjectを特別なスタイルで表示する機能を実装する

- [] ProjectTab
    - [] ProjectToolbarにタブを追加する機能を実装する
    - [] タブごとに履歴を管理し、進む・戻るができるようにする
    - [] CoreのEditorAPIを利用して現在位置を追跡し、履歴管理を実装する
- [] FolderStyle
    - [] Projectのアイテムのアイコンや背景色を変更できる機能を実装する
- [] ContentOverlay
    - [] Folderのアイコンに重ねてフォルダ内に存在するアイテムの種類を示すアイコンを表示する機能を実装する
