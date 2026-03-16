# Phase 1

## 前提
- phase1は`old`のリファクタや段階移植ではなく、`new`の再実装に向けた基盤整備として進める
- `old`は要件や既存挙動を確認するための参照資料として扱い、そのままの構成・API・データ設計を持ち込まない
## i18n
- [x] JSONCベースのi18n基盤を実装する
    - [x] 機能ごとのscope単位でファイルを分割できる
    - [x] scopeごとに独立したキー空間を持ち、別scopeと衝突しない
    - [x] scope解決は`namespace Ee4v.<Scope>...`とLocalization配置が一致する
    - [x] fallbackは`選択言語 -> fallback選択言語 -> en-US -> key`とする

## SettingAPI
- [x] 設定の登録・取得・保存を担うSettingAPIを実装する
    - [x] `User`と`Project`の2スコープを扱える
    - [x] 各設定はメタデータで登録できる
    - [x] 設定定義からPreferences / Project SettingsのUIを自動生成できる
    - [x] 通常項目は自動描画し、特殊なUIだけcustom drawerを許可する
    - [x] validatorで入力値を検証できる
    - [x] feature側は保存先を意識せずSettingAPI経由で利用できる

## InjectorAPI
- [x] Unity Editor拡張描画の登録とdispatchを担うInjectorAPIを実装する
    - [x] `Hierarchy`
        - [x] `Item`
        - [x] `Header`
    - [x] `Project`
        - [x] `Item`
        - [x] `Toolbar`
    - [x] 上記のHierarchy / Project配下の各チャネルを統一された登録モデルで扱える
    - [x] priorityで描画順を制御できる
    - [x] 有効化の判定はSettingAPI側の責務とし、InjectorAPIはその結果に従ってdispatchできる
    - [x] Unityのcallback購読と再描画要求をAPI側で一元管理する
    - [x] phase1では本番機能の移植ではなく、動作確認用の最小stubで成立性を検証する


# Phase 2
- phase2ではphase1で整備した基盤を活用して、既存機能の移植と新機能の実装を進める
- 既存機能の移植は、要件や挙動を踏まえつつも、`new`の設計原則やAPIに合わせて再構築する
- 新機能の実装は、ユーザビリティや拡張性を考慮し、`new`のビジョンに沿ったものとする

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
- [] FolderStyle
    - [] Projectのアイテムのアイコンや背景色を変更できる機能を実装する
- [] ContentOverlay
    - [] Folderのアイコンに重ねてフォルダ内に存在するアイテムの種類を示すアイコンを表示する機能を実装する

