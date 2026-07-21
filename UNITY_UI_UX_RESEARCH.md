# Unity Editor 2022.3 UI/UX 調査報告

## 1. 調査概要

この文書は、Unity Editor 本体のデスクトップ UI を再現・拡張するときに必要な視覚言語と操作原則を、実アプリ操作とスクリーンショット測定から整理したものです。対象プロジェクト固有のエディタ拡張、コード、USS、ドキュメントは根拠に含めていません。

再利用可能な規則は [DESIGN.md](./DESIGN.md) にまとめています。

### 調査環境

| 項目 | 値 |
|---|---|
| 製品 | Unity Editor |
| バージョン | `2022.3.22f1` |
| レンダラー表示 | `DX11` |
| OS | Windows、build `26200` |
| テーマ | Dark |
| UI 言語 | English |
| Unity ウィンドウ | 最大化、`1920 × 1080 px` |
| DPI | `96 DPI`、`100%` |
| 調査日 | 2026-07-21 |

### 権限境界

- Unity Editor の表示、メニュー、既存の標準オブジェクト選択、設定画面、About ダイアログだけを対象にした。
- シーン、アセット、Project Settings、Preferences の値は変更していない。
- 保存、削除、パッケージ操作、サインイン、ライセンス操作、外部サイト遷移は行っていない。
- キャプチャは Unity ウィンドウの表示範囲だけに限定した。
- 調査用キャプチャは成果物に含めず、測定後に削除する。

## 2. 調査方法

次の証拠クラスを分離して記録した。

- **Observed**: Unity を実際に操作して確認した状態と遷移。
- **Measured**: `1920 × 1080 px` のキャプチャから取得した寸法・色。
- **Defined**: Unity 2022.3.22f1 のインストール済み Editor リソースに存在するフォント。
- **Inferred**: 複数状態から導いた設計意図。公式トークンとは扱わない。

調査した代表状態は次のとおり。

1. 未選択の基本レイアウト。
2. Edit および Help のネイティブメニュー。
3. Project Settings のフローティングウィンドウ。
4. About Unity の中央ダイアログ。
5. Hierarchy の単一選択と Inspector フォーム。
6. Hierarchy 検索欄のキーボードフォーカス。
7. Edit メニュー内の無効項目とショートカット表示。

## 3. UI の全体構造

### 3.1 空間モデル

**Observed:** Unity は「アプリ全体の固定コマンド」と「作業領域ごとの文脈コマンド」を明確に分ける。

- 最上部に OS タイトルバーとネイティブメニューバー。
- その下に Play/Pause/Step、検索、Layer、Layout などのグローバルツールバー。
- メイン領域はドッキング可能なパネル群。
- 各パネルはタブ列、ローカルツールバー、コンテンツの順で構成。
- 画面下端は短いステータス領域。

**Measured:** 基本レイアウトの主要境界。

| 領域 | 座標または寸法 |
|---|---|
| OS タイトルバー | `y=0–30`、高さ `31 px` |
| ネイティブメニューバー | `y=31–50`、高さ `20 px` |
| グローバルツールバー | `y=51–80`、高さ `30 px` |
| 上段パネルのタブ列 | `y=81–99`、約 `19 px` |
| Scene ローカルツールバー | `y=100–125`、約 `26 px` |
| 上段コンテンツ | `y=126–675`、約 `550 px` |
| 下段タブ列 | `y=677–696`、約 `20 px` |
| 下段ローカルツールバー | `y=697–717`、約 `21 px` |
| 下段コンテンツ | `y=718–1064`、約 `347 px` |
| ステータス領域 | `y=1065–1079`、約 `15 px` |
| Hierarchy | `x=8–255`、約 `248 px` |
| Scene/Game 中央領域 | `x=258–1506`、約 `1249 px` |
| Inspector | `x=1509–1919`、約 `411 px` |

**Inferred:** ウィンドウを「ページ」に置き換えず、複数の専門ビューを常時並べることで、コンテキスト切り替えのコストを抑えている。新しい UI も既存パネルを押しのける全画面体験より、ドッキング可能な作業面として設計する方が Unity らしい。

### 3.2 安定性

**Observed:** 選択やフォーカスが変わっても、パネル境界、タブ列、ツールバー位置は動かない。Inspector の内容だけが文脈に応じて置き換わる。

**Inferred:** 状態変化でレイアウトを動かすより、既存の矩形を保ったまま色、内容、開閉で差を出すことが中心原則である。

## 4. 色と表面

### 4.1 測定パレット

色は平坦な領域の内部ピクセルから測定した。Scene ビューの 3D 背景色やユーザーコンテンツ色は UI パレットに含めていない。

| 役割 | 値 | 根拠 | 信頼度 |
|---|---:|---|---|
| 最深部、分割線、グローバル帯 | `#191919` | 基本画面で反復測定 | 高 |
| 非選択タブ、濃いヘッダー | `#282828` | タブ列・ヘッダーで測定 | 高 |
| 入力面 | `#2A2A2A` | 検索欄・Inspector field で測定 | 高 |
| 標準パネル面 | `#383838` | Hierarchy、Inspector、Console で反復測定 | 高 |
| 選択タブ、ダイアログ面 | `#3C3C3C` | active tab、About 本文で測定 | 高 |
| 標準ボタン・ツールボタン | `#585858` | Scene toolbar で測定 | 中 |
| アクティブツール | `#46607C` | Scene toolbar の選択済みボタン | 高 |
| 行選択 | `#2C5D87` | Hierarchy の選択行内部 | 高 |
| 入力フォーカス | `#3A79BB` | 検索欄のフォーカス境界 | 高 |
| 標準テキスト | `#C2C2C2` | タブ・検索ラベル内部 | 中 |
| ネイティブメニュー面 | `#F9F9F9` | Edit/Help メニュー | 高 |

### 4.2 表面の階層

**Observed:** Dark テーマでは、影よりも隣接するグレーの差と 1–3 px の暗い分割線で階層を作る。

1. `#191919`: アプリの骨格、帯、スプリッター。
2. `#282828` / `#2A2A2A`: タブ、入力、コントロールの沈んだ面。
3. `#383838`: 主作業面。
4. `#3C3C3C` / `#585858`: 選択中の面や操作可能な面。
5. 青: 選択、フォーカス、アクティブ状態に限定。

**Inferred:** 彩度は「状態」と「3D コンテンツ」に予約されている。装飾目的で広い青面や多色カードを置くと Unity の作業道具としての密度を壊す。

### 4.3 ネイティブ UI との境界

**Observed:** Windows の Edit/Help メニューは Editor の Dark テーマに追従せず、`#F9F9F9` の明るいネイティブ面で表示された。ショートカットは右揃え、区切り線でコマンド群を分ける。

**Inferred:** Unity 内で再現するカスタムメニューを Windows ネイティブメニュー風に偽装する必要はない。EditorWindow 内のポップアップは Unity の Dark 面を使い、OS 管轄のメニューだけを OS に任せるのが一貫する。

## 5. タイポグラフィ

### 5.1 フォント

**Defined:** Unity 2022.3.22f1 の Editor リソースには次が同梱されている。

- Inter: Regular、Medium、SemiBold、Bold と italic variants。
- Roboto Mono: Regular、Bold と italic variants。

**Observed:** Editor 本文、タブ、Inspector はコンパクトなサンセリフ。ネイティブメニューと OS タイトルバーは Windows のシステム書体で描画される。

**推奨:** Editor 内 UI は `Inter` を第一候補にし、OS フォールバックを持たせる。コード、ログ、固定幅値だけに `Roboto Mono` を使う。

### 5.2 役割

| 役割 | 推定サイズ | ウェイト | 行高/領域 | 用途 |
|---|---:|---:|---:|---|
| compact | `11 px` | 400 | `16 px` 前後 | 補助情報、ステータス、細いツールバー |
| body | `12 px` | 400 | `18–20 px` | タブ、行、フォーム、メニュー |
| section | `12 px` | 500–600 | `20–24 px` | Inspector component header |
| page-title | `~20 px` | 500–600 | `26 px` 前後 | Settings ページ見出し |
| mono | `11–12 px` | 400 | `16–18 px` | Console、技術値、コード断片 |

サイズはキャプチャ上の描画範囲からの近似であり、公式な全 Editor トークンではない。

## 6. 密度、余白、形状

### 6.1 密度

**Measured/Observed:** 標準的な行とコントロールはおおむね `18–22 px`。グローバルツールバーは `30 px`、パネルタブは約 `20 px`。

反復して現れる間隔は `2 / 4 / 6 / 8 px`。構造的な余白でも `12–16 px` 程度に留まり、広いカード余白は使わない。

### 6.2 角丸

**Observed:** ドッキングされた作業面はほぼ直角。入力と小ボタンは `2–3 px` 程度、ネイティブメニューと独立ウィンドウは `4–10 px` 程度の丸みを持つ。

**Inferred:** 角丸は操作部品の境界を和らげるための補助であり、製品全体のシグネチャではない。大きな `12–24 px` radius のカードは避ける。

### 6.3 奥行き

**Observed:** ドッキング面ではシャドウをほとんど使わない。Project Settings の独立ウィンドウだけが周囲へ柔らかい影を落とす。About ダイアログは中央に置かれるが、背景を暗転する scrim はなかった。

## 7. コンポーネント調査

### 7.1 パネルとタブ

- active tab は `#3C3C3C`、inactive tab は `#282828`。
- タブは約 `20 px` 高で、内容面と連続して見える。
- タブ列の右端にロック、メニュー、閉じるなどの小さい icon-only action を置く。
- panel content は `#383838` を基準にする。
- パネル間は `2–3 px` の濃いスプリッターで区切る。

### 7.2 ツールバー

- global toolbar は `30 px` 高。
- アイコンボタンは正方形に近い `22–30 px` の hit area。
- 関連操作を隣接させ、中央 Play/Pause/Step のように短いグループを作る。
- active tool は青みのある `#46607C`。色だけでなく、ボタン面全体の変化で示す。
- icon-only control はツールチップを前提とするが、今回ツールチップ表示は未検証。

### 7.3 Tree/List row

- 行高は約 `20 px`。
- 開閉三角、種類アイコン、ラベル、補助アクションの順。
- 単一選択は行全幅を `#2C5D87` にする。
- Hierarchy は深さを左インデントで示し、カードや罫線を足さない。
- 選択後もツリー幅や隣接パネル位置は変えない。

### 7.4 検索フィールド

- 背景 `#2A2A2A`、高さ約 `20 px`。
- 左に検索アイコン、中央に文字、右に補助アイコン。
- フォーカスは `#3A79BB` の 1 px 境界で明示。
- フォーカス時にもサイズは変えない。

### 7.5 Inspector form

- ラベル列と値列の 2 カラムが基本。
- 値の型に応じて text field、dropdown、checkbox、slider、object picker を同じ行高に揃える。
- `Transform` や `Camera` は高さ約 `24 px` の折りたたみ section header。
- section header に enable checkbox、help、preset、more actions を収める。
- 関連する複数値は X/Y/Z や Near/Far のように同一行または連続行へ圧縮。
- 主要アクション `Add Component` は下端に横長で置かれるが、幅いっぱいにはせず中央に余白を残す。

### 7.6 ネイティブメニュー

**Measured:** Edit menu は約 `276 px` 幅。Help menu は約 `239 px` 幅。行は約 `22 px`。

- ラベルは左、shortcut は右。
- destructive/non-destructive を色で派手に分けず、グルーピングと文言で示す。
- disabled item は低コントラストのグレー。
- separator で意味単位を作る。
- submenu は右向き chevron。

### 7.7 Project Settings window

**Measured:** 約 `1203 × 613 px`、画面中央付近の `x=406–1608`, `y=342–954`。

- 左に約 `100 px` のカテゴリナビゲーション、右に設定フォーム。
- 最上部に横長検索。
- 選択カテゴリを濃淡差で示す。
- 背景の Editor は暗転せず保持される。
- 独立ウィンドウとして閉じる操作を持つ。

### 7.8 About dialog

**Measured:** 約 `640 × 296 px`、`x=640–1279`, `y=399–694`。

- 中央配置。
- 白い OS title bar と `#3C3C3C` の本文。
- ブランド、version、license/credits の情報だけに絞る。
- 背景 scrim はない。
- blocking/focus trap の詳細は未検証。

## 8. 状態モデル

| 状態 | 観察された表現 |
|---|---|
| Default | `#383838` 面、`#C2C2C2` 付近のテキスト |
| Hover | ボタン/field が `#515151`〜`#585858` 方向へ明るくなる |
| Focus | 入力境界を `#3A79BB` に変更 |
| Selected row | 行全幅 `#2C5D87` |
| Active tool | ボタン面 `#46607C` |
| Disabled | テキストとアイコンのコントラストを下げる |
| Expanded | 下向き disclosure と本文表示 |
| Collapsed | 右向き disclosure と本文非表示 |

pressed、dragging、loading、success、warning、error、toast、validation error は今回直接検証していない。

## 9. 操作フロー

### 9.1 メニュー

1. top-level label をクリック。
2. menu が label 直下にアンカーされる。
3. item を意味グループ単位で走査できる。
4. `Esc` で閉じ、元の Editor 状態へ戻る。

**Observed:** menu を開いても Editor のレイアウトは変化しない。menu は一時的な overlay として表示される。

### 9.2 選択から編集

1. Hierarchy で対象を選ぶ。
2. 選択行が青へ変わる。
3. Scene に gizmo/selection visualization が現れる。
4. Inspector が対象の component form に置き換わる。

**Inferred:** 同じ selection model を複数ビューが共有し、色と内容を同期することが Unity の中心的 UX パターンである。

### 9.3 設定

1. Edit menu から Project Settings を開く。
2. 独立ウィンドウ内でカテゴリを選ぶ。
3. 右ペインでフォームを編集する。
4. ウィンドウを閉じて作業面へ戻る。

**Observed:** 設定は専用の二ペイン構造で、メイン Editor のドッキング構造を壊さない。

## 10. 強み

- 空間が安定しており、選択やフォーカスで作業面が跳ねない。
- 高密度でもタブ、帯、スプリッターの明度差で階層を追える。
- 同じ青系でも row selection、tool active、input focus の役割を面と境界で分ける。
- novice は可視メニュー、expert は右揃え shortcut から同じ機能へ到達できる。
- Inspector の折りたたみと 2 カラム form が大量の設定を圧縮する。

## 11. リスクとアクセシビリティ

- `11–12 px` の文字と `18–22 px` の control は高密度だが、低視力・高 DPI 環境では負担になり得る。
- `#383838` と `#3C3C3C` の差は小さい。境界線や位置関係を併用しないと区別しにくい。
- icon-only action が多く、ツールチップと一貫した icon semantics が必須。
- 選択とアクティブ状態は青への依存が強い。アイコン、check、境界、形状を併用するべき。
- ネイティブメニューだけが明るく、Dark Editor とのテーマ断絶がある。
- キーボード tab order、screen reader、high contrast、color vision、reduced motion は未検証。

## 12. 証拠台帳

| 主張 | 値/結果 | 証拠 | 状態 | 信頼度 |
|---|---|---|---|---|
| 標準パネル面 | `#383838` | Measured | base/selection/settings | 高 |
| 構造帯 | `#191919` | Measured | base | 高 |
| active tab | `#3C3C3C` | Measured | base | 高 |
| field | `#2A2A2A` | Measured | Inspector/search | 高 |
| row selection | `#2C5D87` | Measured | Main Camera selection | 高 |
| input focus | `#3A79BB` | Measured | focused search | 高 |
| panel geometry | 3-column docked layout | Observed + Measured | base | 高 |
| settings geometry | `~1203 × 613 px` | Measured | Project Settings | 高 |
| about geometry | `~640 × 296 px` | Measured | About Unity | 高 |
| primary font candidate | Inter | Defined by bundled Editor fonts + visual match | installation/base | 中 |
| mono font candidate | Roboto Mono | Defined by bundled Editor fonts | installation | 中 |
| chroma reserved for state | blue concentrated in active/selected/focus | Inferred from multiple states | multiple | 中 |

## 13. 未検証範囲

- Light theme。
- macOS/Linux のネイティブ chrome。
- 日本語 UI と CJK fallback font。
- 非最大化、小さいウィンドウ、複数 DPI の responsive behavior。
- drag & drop、resize 中の feedback。
- confirmation dialog、error、warning、progress、toast。
- keyboard-only navigation と accessibility tree。
- animation duration、easing、reduced-motion behavior。

したがって、[DESIGN.md](./DESIGN.md) の exact value は Windows/Dark/English/100%/1920×1080 の範囲で用い、他環境では host Editor の resolved style を優先する。
