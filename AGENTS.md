# AGENTS.md

## リポジトリ概要

このリポジトリは、Unity 2022.3 向けのエディタ拡張パッケージです。

- `src/` は現在の作業対象パッケージです。
- `old/` は旧実装・参照用のコードです。

## 作業方針

- `old/` は参照資料として扱い、書き換えたり削除したりしないでください。
- Unity の `.meta` ファイルは自動で生成されます。 `.meta` ファイルを手動で作成する必要はありません。
- 実装前にdocsを確認し、既存の手法に添うように実装を進めてください。
- ユーザーに見える表示テキストはコードへ直書きせず、ローカライズ定義を追加して `I18N.Get("key")` など既存のローカライズ経由で表示してください。
- DB に関する変更では、既存 DB との互換性やマイグレーションは原則考慮せず、DB の削除・再生成を前提として schema や取り込み処理を修正して構いません。

## UI・Unity 依存の分離

- UI は UI Toolkit（UIElements）を優先して実装し、UI Toolkit では実現できない場合に限って IMGUI を使用してください。
- UI Toolkit で通常の表示ラベルを実装する場合は、Unity 2022.3 のフォントキャッシュ問題を回避するため、`Label` を直接生成・継承せず `UiTextFactory.Create(...)` を使用してください。
- `UiTextFactory.Create(...)` を呼ぶだけではフォントキャッシュ問題の回避を保証できません。構造用 class しか渡さない場合は通常の UI Toolkit `Label` 実装へ fallback します。問題回避が必要な text は `UiClassNames` に typography class を定義し、`TypographyStyleResolver` で `RequiresImgui = true` として登録した class を `UiTextFactory.Create(...)` または `UiTextFactory.AttachToFoldout(...)` へ必ず渡してください。追加・変更時は生成された `UiTextElement` が IMGUI fallback を使用することをテストで確認してください。
- 新規追加または変更する文字付き button で `Button.text` を使用しないでください。共通の `UiButton` を使い、表示文字列を内部の `UiTextFactory` と登録済み typography class 経由で描画してください。標準 `Button` を直接使えるのは、共通 component では表現できない低レベル操作や文字を持たない特殊 control に限ります。その場合も `Button.text` は空にし、例外理由を関連テストまたはコード上の構造から確認できるようにしてください。
- `TextField(label)`、`Toggle(label)`、`IntegerField(label)`、`PopupField(label, ...)`、`BaseField<T>.label` など、Unity 標準 control が内部生成する label へ表示テキストを渡すことも禁止です。見た目が通常の label と同じでも `UiTextFactory` の代替にはなりません。
- `Foldout.text` も Unity 標準の内部 label を使用するため、表示文字列を設定しないでください。折り畳み見出しは `UiTextFactory.AttachToFoldout(...)` で追加してください。
- 特に Settings UI では、項目名を `UiTextFactory.Create(...)` で独立した要素として生成し、隣接する標準 field / custom drawer へ渡す label は必ず空文字列にしてください。これは `Toggle`、数値 field、文字列 field、enum / popup、custom setting drawer のすべてに適用します。
- `UiLabelAuditTests` はソース上の direct `Label` しか検出できず、標準 control 内部の label 利用を検出できません。Settings UI を追加・変更するときは、表示項目名が `UiTextElement` として存在し、配下の標準 field の `label` が空であることをテストで確認してください。
- UI は見た目の差し替えを容易にするため、state の表示と入力イベントの通知に責務を限定し、機能ロジックと分離してください。依存方向は `UI -> 機能ロジック` とします。
- Unity のバージョン更新時に変更箇所を特定しやすくするため、Unity 依存部分は Core の adapter / wrapper に集約してください。
- 特に Unity の非公開・internal API、reflection、内部フィールドへのアクセスは feature 側で直接行わず、原則 `Editor/Core/Internal/EditorAPI` の facade と `Backends` に分離してください。
- 非公開 API が利用できない場合も機能全体を壊さないよう、facade は `Try...` や fallback を提供してください。変更時は関連テストと `docs~/src/maintenance/unity-upgrade.md` も更新してください。

## テスト方針

- テストは、非自明な判断、invariant、状態遷移、外部技術との境界、障害時 fallback、Localization・Architecture・UI実装規約など、壊れたことを自動検出する必要がある契約に限定してください。
- テストを追加する前に、同じ保証が別レイヤーや別assemblyの既存テストにないか確認してください。同じinvariantをUI、Application、Infrastructureで重ねて確認せず、原則として判断を所有する最も内側のレイヤーで1回だけテストしてください。境界固有の変換、通知順序、永続化、failure handlingが別に壊れ得る場合だけ、その境界のテストを追加してください。
- getter、既定値の転記、constructor引数の保持、privateな要素数やclass構造など、実装をそのままなぞるだけのテストは追加しないでください。列挙値や表示variantごとの同型テストも、固有のfailure modeがなければdata-driven testへまとめるか削除してください。
- UI CatalogにstoryまたはscreenがあるUIについて、見た目や通常操作をNUnit / Unity Testで重複確認しないでください。座標、寸法、余白、色、USS class、表示順、text・iconの見え方、通常のclick、focus、foldout、drag、context menu、popup操作はCatalogで確認し、必要なstateや操作presetをCatalog側へ追加してください。
- UIの重要な判断はUI component内へ置かずpure logicへ分離し、そのlogicだけをunit testしてください。Catalogで再現できないUnity lifecycle、external adapter、fallback、複雑なfailure handlingに固有の回帰がある場合に限り、UI境界の自動テストを追加してください。
- Catalogで見える結果だけでは保証できない実装規約は自動テストを維持してください。具体的にはLocalization key監査、asmdefと依存方向の監査、`UiTextFactory`利用、標準field / `Foldout`の空label、`Button.text`禁止、typography classのIMGUI fallback、design token整合性、Catalog登録・stylesheet網羅性が該当します。
- UIの不具合を修正するときは、まずCatalogのstory / presetで再現可能にしてください。見た目や通常操作だけの回帰テストは追加せず、原因がpure logic、実装規約、Unity固有境界のいずれかに分離できる場合だけ、その最小単位へテストを追加してください。
- 実装変更で既存テストの保証が別のテストやCatalogへ移った場合は、重複した旧テストを同じ変更内で削除してください。

## クリーンアーキテクチャ・疎結合

- module は配置上のカテゴリではなく、単独で有効化・テスト・変更できる機能境界として定義してください。カテゴリディレクトリは module の assembly、namespace、設定、初期化処理を所有しません。
- たとえば `Editor/EditorEnhancements` は小規模な Editor 拡張 module をまとめるカテゴリに限定します。`Editor/EditorEnhancements/DepthIndicator` は `DepthIndicator` module であり、namespace、asmdef、Localization、Test、bootstrap、setting 定義を自身の配下で所有します。
- module の namespace と assembly 名はカテゴリ名を含めず、機能名を基準にしてください。例として `DepthIndicator` は `Ee4v.DepthIndicator` / `Ee4v.DepthIndicator.Editor`、`FolderContentOverlay` は `Ee4v.FolderContentOverlay` / `Ee4v.FolderContentOverlay.Editor` とします。
- module 間で具象型や static state を直接参照しないでください。module をまたぐ連携は Core の契約、明示的な interface、event、DTO など必要最小限の抽象を介し、asmdef の参照方向を一方向に保ってください。
- 依存方向は外側から内側へ向けます。UI / Presentation と Unity adapter は Application / Domain / Contracts を利用できますが、Application / Domain / Contracts から Unity、UI、filesystem、DB、network の具象へ依存してはいけません。
- Composition root は object の生成、依存注入、Unity lifecycle への登録だけを担当します。判断ロジック、描画ロジック、永続化処理を bootstrap や static facade に置かないでください。
- UI は state の描画と入力通知、renderer は描画に限定し、検索・集計・選択・validation・cache invalidation などの判断を分離してください。外部技術へ触れる処理は adapter として隔離します。
- public API は module の利用に必要な契約だけに限定し、実装型は原則 `internal` にしてください。将来使う可能性だけを理由に共通化や公開を行わないでください。
- 小規模 module に空の `Domain`、`Application`、`Infrastructure` directoryやassemblyを機械的に作る必要はありません。ただし責務と依存が増えた時点で層を分割できるよう、純粋な判断ロジックと Unity 依存処理を同じ型へ混在させないでください。
- setting、Localization、Test はそれを所有する module の配下へ置いてください。カテゴリ直下や無関係な共通 module に集約しないでください。
- 実装変更時は、module が兄弟 module を参照していないこと、内側のロジックが外部技術へ依存していないこと、bootstrap が composition 以外の責務を持っていないことを確認してください。可能な範囲で pure logic の unit test と Unity adapter の境界 test を追加してください。

# Tiny Code

依存を外側へ寄せて中心の処理を疎結合に保ち、目的を満たす最小のコードを書く。

## 基本姿勢

実装前に依頼と関連コードを読み、入力から出力までの流れと依存関係を確認する。
変更対象だけでなく、判断を行う処理と外部へ接続する処理の境界を把握する。

解決策は次の順番で探し、成立した時点で止める。

1. 変更せずに目的を達成できないか
2. 依存を増やさず既存の処理を再利用できないか
3. 言語や実行環境の標準機能で処理できないか
4. 導入済みのライブラリや基盤の機能に任せられないか
5. 既存の境界へ小さな処理を追加できないか
6. どれにも該当しない場合だけ新しい境界と処理を書く

新しく作ることより作らずに済ませることを優先する。
ただしコード量を減らすために中心の処理へ外部依存を持ち込まない。

## 依存の方向

業務上の判断や計算を中心に置く。
表示と通信、保存などの都合は外側に置く。
外側の処理は内側を利用してよいが、内側の処理から外側の実装を直接参照しない。

```text
表示・通信・保存 → 接続部 → 用途の処理 → 中心の規則
```

矢印は依存する方向を表す。
中心の規則は枠組み、データベース、外部サービスを知らない状態にする。

外部の機能が必要な場合は中心側が必要な操作だけを契約として定め、外側で実装する。
実装は組み立て箇所から渡し、処理の途中で生成しない。

## 境界の作り方

次の依存は中心の処理から分離する。

- データベースやファイルへの読み書き
- 通信や外部サービスの呼び出し
- 現在時刻、乱数、環境変数
- 表示形式や通信形式への変換
- 枠組み固有の型や大域的な状態

境界を越える値は用途に必要な項目だけを持つ単純な形にする。
外部の応答や保存形式を中心まで流さず、接続部で意味のある値へ変換する。

失敗も外部固有の例外のまま渡さない。
用途の処理が判断できる失敗へ置き換え、表示や再試行は外側で決める。

依存の組み立ては入口に集める。
各処理が隠れて外部実装を取得する状態や、どこからでも変更できる共有状態を作らない。

## 小さく保つ原則

- 依頼されていない機能、設定項目、互換層は追加しない。
- 境界は外部依存や変更理由が分かれる場所にだけ設ける。
- 層の数や名前を形式的にそろえるためのファイルは作らない。
- 実装の差し替えが不要でも、外部技術を中心から剥がすための契約は作ってよい。
- 単純な値の受け渡しに汎用の基底型や登録機構を持ち込まない。
- 将来必要になるかもしれない処理を先回りして置かない。
- 巧妙な短縮より読み手がすぐ理解できる平凡な実装を選ぶ。
- 不要になった依存とコードは削除する。ただし依頼範囲外の整理まで広げない。

新しいファイルを避けること自体を目的にしない。
責務や依存方向が異なる処理は分け、同じ理由で変わる小さな処理はまとめる。

## 変更の進め方

既存コードを変更するときは次の順番で考える。

1. 中心に残す判断と外側へ出す依存を分ける
2. 中心の処理が受け取る値と必要な操作を定める
3. 外部固有の値を接続部で変換する
4. 入口で実装を組み立てて中心へ渡す
5. 不要になった直接参照と中継処理を削除する

局所的な回避を呼び出し側へ重ねない。
同じ原因が複数の経路へ影響する場合は、共有される中心の処理か境界で一度だけ直す。

ただし大規模な置き換えを前提にしない。
既存の依存を一度に剥がせない場合は変更対象の周囲へ境界を置き、新しい直接依存を増やさない。

## 不具合修正

報告された内容は症状として扱い、対象の関数とデータの流れを調べる。
同じ原因で別の経路にも不具合が生じる場合は原因に最も近い場所で修正する。

外部入力の解釈が原因なら接続部で直し、業務上の判断が原因なら中心で直す。
複数の呼び出し側へ同じ防御処理を追加して終わらせない。

## 省略してはいけないもの

次の要素はコード量や依存数を減らす目的で取り除かない。

- 外部入力や権限境界での検証
- データの消失や破損を防ぐ異常時の処理
- 認証、認可、秘密情報の保護
- 基本的な操作性や利用しやすさに必要な要素
- 実機差、時刻のずれ、センサー誤差への調整手段
- 利用者が明示した要件

簡潔であることと脆弱であることを混同しない。

## 意図的な割り切り

現在の規模では十分でも既知の上限がある実装を選ぶ場合がある。
その場合はコードの近くに `tiny-code:` で始まる注記を残し、制約と見直す条件を一行で記録する。

```text
tiny-code: 全体ロックで十分。競合待ちが観測されたら利用者単位へ分ける
```

単に「後で改善する」とは書かず、実装の限界と見直す条件を示す。
外部依存を中心へ持ち込む割り切りには使わない。

## 確認

中心の規則は外部サービスを起動せず確認できる形にする。
計算、状態の変化、権限の判断には小さな実行可能な確認を残す。

接続部は値の変換と外部との契約を確認する。
実際のデータベースや通信を使う確認は境界に絞り、中心の確認と混ぜない。

既存の試験方式があればそれに合わせる。
明白な一行変更のために新しい試験基盤や大量の試験用データは作らない。

## 要件が大きすぎる場合

小さな方法で目的を満たせるならその方法で完了し、省略した内容を短く伝える。
安全に決められる内容について質問のために作業を止めない。

小さい案と依頼された案で結果が大きく変わる場合や元に戻せない判断を伴う場合は実装前に確認する。
利用者が完全な実装を明示した場合は同じ簡略案を繰り返し主張しない。

## 応答

結果を先に示し、変更内容と意図的に作らなかったものを説明する。
追加が必要になる条件があればそれも伝える。
依頼されていない設計文書や機能一覧によって、削った複雑さを文章として持ち込まない。

短いコードではなく、依存が少なく保守できるコードを必要な分だけ書くことが目的である。
