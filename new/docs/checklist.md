# Phase 1

## 前提
- phase1は`old`のリファクタや段階移植ではなく、`new`の再実装に向けた基盤整備として進める
- `old`は要件や既存挙動を確認するための参照資料として扱い、そのままの構成・API・データ設計を持ち込まない
- phase1の完了条件は基盤APIの成立であり、既存機能の本実装移植は含めない

## i18n
- [x] JSONCベースのi18n基盤を実装する
    - [x] 機能ごとのscope単位でファイルを分割できる
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
