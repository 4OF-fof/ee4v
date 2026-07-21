---
version: "alpha"
name: "unity-editor-2022-dark-design"
description: |
  Unity Editor 2022.3 の Windows Dark theme を再現する、高密度で作業中心のデザイン仕様。直角に近いドッキング面、狭いツールバー、低彩度の表面階層を基調とし、青は選択・フォーカス・アクティブ状態に限定する。
scope: "Unity Editor 2022.3.22f1, Windows, Dark, English, 96 DPI, 1920x1080 maximized"
source_basis:
  - "Direct interaction with the running Unity Editor"
  - "Pixel measurement from Unity-window-only captures"
  - "Bundled Unity Editor font resources"
colors:
  chrome-deep: "#191919"
  tab-idle: "#282828"
  field: "#2A2A2A"
  panel: "#383838"
  surface-raised: "#3C3C3C"
  control: "#585858"
  tool-active: "#46607C"
  selection: "#2C5D87"
  focus: "#3A79BB"
  text-primary: "#C2C2C2"
  menu-native: "#F9F9F9"
typography:
  compact:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "11px"
    fontWeight: 400
    lineHeight: "16px"
  body:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 400
    lineHeight: "18px"
  section:
    fontFamily: "Inter, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 600
    lineHeight: "20px"
  mono:
    fontFamily: "Roboto Mono, Consolas, monospace"
    fontSize: "11px"
    fontWeight: 400
    lineHeight: "16px"
spacing:
  xxs: "2px"
  xs: "4px"
  sm: "6px"
  md: "8px"
  lg: "12px"
  xl: "16px"
rounded:
  docked: "0px"
  control: "2px"
  popup: "4px"
  floating-window: "8px"
dimensions:
  row: "20px"
  field: "20px"
  panel-tab: "20px"
  component-header: "24px"
  global-toolbar: "30px"
  splitter: "2px"
components:
  docked-panel:
    backgroundColor: "{colors.panel}"
    tabHeight: "{dimensions.panel-tab}"
    splitterColor: "{colors.chrome-deep}"
    rounded: "{rounded.docked}"
  input:
    backgroundColor: "{colors.field}"
    textColor: "{colors.text-primary}"
    height: "{dimensions.field}"
    focusColor: "{colors.focus}"
    rounded: "{rounded.control}"
  tree-row:
    height: "{dimensions.row}"
    selectedBackground: "{colors.selection}"
    textColor: "{colors.text-primary}"
  toolbar:
    backgroundColor: "{colors.chrome-deep}"
    height: "{dimensions.global-toolbar}"
    activeBackground: "{colors.tool-active}"
---

# Unity Editor Design System

## Overview

この仕様は Unity Editor 2022.3.22f1 の Windows Dark theme と調和する Editor UI を設計するためのものです。調査の詳細と証拠は [UNITY_UI_UX_RESEARCH.md](./UNITY_UI_UX_RESEARCH.md) を参照してください。

適用範囲は Editor 内のツール、パネル、Inspector、設定画面、ポップアップです。ゲーム内 UI、ランチャー、マーケティングサイトには使いません。

## Design Philosophy

1. **作業面を安定させる。** 状態変化でパネルを移動させず、同じ矩形の中で内容、明度、開閉を変える。
2. **密度を価値として扱う。** 行高 `20px`、本文 `12px` を基準に、頻繁な操作を短い視線移動で完結させる。
3. **階層はグレーの段差で作る。** 大きな shadow、gradient、装飾 card を使わない。
4. **青は状態に予約する。** 選択、focus、active tool 以外に広い青面を使わない。
5. **novice と expert を同じ入口へつなぐ。** 可視 label、menu、tooltip と keyboard shortcut を対立させない。
6. **Editor が host であることを尊重する。** theme、DPI、font、標準 control の resolved style を可能な限り継承する。

## Colors

### Surface ladder

| Token | Value | Use |
|---|---:|---|
| `{colors.chrome-deep}` | `#191919` | global bar、splitter、最深部 |
| `{colors.tab-idle}` | `#282828` | inactive tab、濃い header |
| `{colors.field}` | `#2A2A2A` | search、text field、numeric field |
| `{colors.panel}` | `#383838` | panel body、main work surface |
| `{colors.surface-raised}` | `#3C3C3C` | active tab、dialog body、軽い強調面 |
| `{colors.control}` | `#585858` | toolbar button、標準 button |

### State colors

| Token | Value | Rule |
|---|---:|---|
| `{colors.selection}` | `#2C5D87` | 選択行の全幅面。装飾に使わない |
| `{colors.focus}` | `#3A79BB` | focused input の 1px border |
| `{colors.tool-active}` | `#46607C` | active mode/tool の button surface |

### Text

- primary text は `{colors.text-primary}` を基準にする。
- secondary text は host Editor の disabled/secondary text color を継承する。固定値が必要なら primary の opacity を下げる。
- disabled は彩度を足さず、文字・icon・control 全体の contrast を下げる。
- error/warning/success は host Editor の semantic color を継承する。この調査で未測定のため独自値を定義しない。

## Typography

### Roles

- `compact`: toolbar の補助情報、status、metadata。
- `body`: tab、tree row、field、button、menu 相当の本文。
- `section`: Inspector component header、折りたたみ見出し。
- `mono`: Console、code、固定幅で比較すべき技術値。

Editor 本文は Inter 系、OS chrome はシステム書体、技術情報は Roboto Mono 系を使う。太字は section title と現在状態の短い強調に限定する。

### Text behavior

- 単一行 label は原則 nowrap + ellipsis。
- 長い説明だけ wrap を許可する。
- tab と button label は中央揃え、tree と form label は左揃え。
- 数値列は比較しやすい alignment を保つ。
- font size を縮めて情報を詰めず、先に label の短縮、grouping、foldout を検討する。

## Layout

### Editor shell

- global command は上部 `{dimensions.global-toolbar}` に置く。
- feature 固有 command は対象 panel の tab 直下に local toolbar として置く。
- main work area は docked panel の組み合わせにする。
- panel は tab strip、local toolbar、content の 3 層を基本とする。
- panel 同士は `{dimensions.splitter}` 前後の濃い境界で区切る。

### Spacing

- icon と label: `{spacing.xs}`〜`{spacing.sm}`。
- row 内 padding: 水平 `{spacing.sm}`〜`{spacing.md}`、垂直 `{spacing.xxs}`。
- field 間: `{spacing.xxs}`〜`{spacing.xs}`。
- section 間: `{spacing.md}`〜`{spacing.lg}`。
- window content inset: `{spacing.lg}`〜`{spacing.xl}`。

24px を超える大きな余白は、empty state や中央 dialog 以外で使わない。

### Two-column form

- 左列を label、右列を control とする。
- label 幅は同一 section 内で固定。
- vector、range、rect の複数値は同一行にまとめる。
- help/preset/more は section header の右端へ置く。
- 長い form は foldout component section で分割する。

## Elevation & Depth

- docked panel: shadow なし、`{colors.chrome-deep}` の境界のみ。
- popup: 1px border と短い shadow。背景全体の scrim は使わない。
- floating Editor window: 周囲に柔らかい shadow を許可。
- modal-style dialog: 中央配置。ただし Unity 2022.3 の観察状態では scrim を使用しない。
- `z-index` の代わりに overlay の anchor と close path を明確にする。

## Shapes

- panel と section: `{rounded.docked}`。
- input と compact button: `{rounded.control}`。
- popup/menu surface: `{rounded.popup}`。
- floating utility window: `{rounded.floating-window}` 前後。
- pill、円形 card、`12px` 以上の大 radius は status chip など明確な意味がある場合だけ。

## Components

### Docked panel

Purpose: ひとつの専門作業領域を Editor layout に常駐させる。

- Anatomy: tab strip / optional local toolbar / scrollable content。
- Surface: `{colors.panel}`。
- Border: `{colors.chrome-deep}`、約 `{dimensions.splitter}`。
- active tab は `{colors.surface-raised}`、inactive は `{colors.tab-idle}`。
- tab change で panel geometry を変えない。
- title 右端の icon action は少数に絞り、tooltip を付ける。

Use when: 頻繁に参照・操作する feature。

Avoid when: 単発確認だけで終わる質問、破壊的 confirmation。

### Local toolbar

- panel 文脈に閉じた command、filter、view toggle を置く。
- control は `20–24px` 高を基準にする。
- related actions は隙間なく group 化し、group 間を `{spacing.md}` 程度空ける。
- active tool は `{colors.tool-active}` の面で示す。
- icon-only action には tooltip と keyboard path を用意する。

### Tree/List row

- Height: `{dimensions.row}`。
- Anatomy: disclosure / type icon / label / metadata or trailing action。
- Default background: transparent over `{colors.panel}`。
- Hover: host の hover surface、または `{colors.control}` より暗い一段明るい面。
- Selected: `{colors.selection}` を行全幅へ適用。
- Disabled: opacity/contrast を下げ、選択可能に見せない。
- hierarchy はインデントで表し、card border を足さない。

Keyboard: 上下移動、左右で collapse/expand、Enter/Space の意味を component ごとに一貫させる。

### Search field

- Height: `{dimensions.field}`。
- Background: `{colors.field}`。
- Left: search icon。Right: clear/filter action。
- Focus: layout を変えず 1px `{colors.focus}` border。
- Placeholder は secondary text。
- clear action は値があるときだけ表示する。
- filter 中の empty state は検索条件と解除 path を明示する。

### Inspector form row

- Height: `{dimensions.row}` を基準にする。
- label と control の 2 column。
- text/numeric field background は `{colors.field}`。
- dropdown、object picker、slider も同じ vertical rhythm に揃える。
- validation message は対象 field の直下。color だけでなく icon と短い recovery text を付ける。
- value change は Undo に統合し、commit timing を一貫させる。

### Component section

- Header height: `{dimensions.component-header}`。
- Left: disclosure + optional enable toggle + icon + title。
- Right: help / preset / more。
- Expanded body は header の直下。card gap を挟まない。
- Collapsed 時も header width と action position を維持する。

### Button

- Default: `{colors.control}` を基準にする。
- Compact action は text button、頻繁な mode は icon toggle、最終確定は明示的 label button。
- Primary action を青で塗りつぶし続けない。青は state と衝突するため、host の button hierarchy を優先する。
- destructive action は文言、icon、confirmation を併用し、赤だけに依存しない。
- disabled は hover/press feedback を出さない。

### Context menu / command menu

- Invoker の直下または pointer に anchor。
- item height は約 `{dimensions.row}`。
- label 左、shortcut 右、submenu chevron は最右。
- separator で command group を分ける。
- `Esc`、outside click、command completion で閉じ、invoker へ focus を戻す。
- EditorWindow 内で独自実装する場合は Dark theme を継承する。OS native menu を模倣した白い surface は使わない。

### Floating settings window

- 左 navigation + 右 content の 2 pane。
- 上部に全設定を対象とする search。
- navigation は tree/list、content は sectioned form。
- 背景 Editor を暗転させない。
- window size を記憶し、狭い場合は content scroll を優先する。
- Project Settings と Preferences の scope を label で混同させない。

### Modal-style dialog

- Centered、短い title bar、単一目的。
- About の観察寸法は `~640 × 296px`。一般 dialog の固定寸法としてコピーせず、内容に合わせる。
- confirmation は consequence、primary action、cancel を明示する。
- background scrim は Unity 2022.3 の観察スタイルでは使わない。
- `Esc` は安全な cancel/close に割り当てる。

## Interaction States

### State priority

同時に複数状態が成立するときは次の優先順にする。

1. Disabled
2. Pressed/dragging
3. Focused
4. Selected/active
5. Hover
6. Default

### Required state checklist

すべての interactive component で確認する。

- Default
- Hover
- Keyboard focus
- Pressed
- Selected/active（該当時）
- Disabled
- Empty/no value
- Error/validation（入力時）

### Focus

- input は 1px `{colors.focus}` border。
- focus border を追加して外寸を増やさない。
- icon-only button も keyboard focus を可視化する。
- overlay close 後は invoker へ focus を戻す。

### Selection

- row は全幅 `{colors.selection}`。
- active tool は `{colors.tool-active}`。
- text selection、object selection、active mode を同じ見た目に統合しない。

## Motion

Unity 2022.3 の調査状態では、menu、selection、focus、settings window は即時に切り替わり、defensible な duration/easing は取得できなかった。

- 基本状態は animation なしで成立させる。
- progress、drag、expand に motion を足す場合も、操作応答を遅らせない。
- reduced motion を尊重できる実装では、decorative transition を無効化する。
- 未測定の duration を「Unity 標準」として固定しない。

## Window and Responsive Behavior

- host Editor の dock/undock、splitter resize、DPI scaling を壊さない。
- 最小幅では label column を無制限に縮めず、content scroll または stacked layout を検討する。
- toolbar は重要 action を左に残し、低頻度 action を overflow へ送る。
- tree/list は horizontal scroll より ellipsis + tooltip を優先する。
- `1920 × 1080 / 100%` 以外は未測定。pixel value を絶対条件にせず、host resolved style を優先する。

## Accessibility

- text と interactive boundary の contrast を検証する。
- color だけで selected、error、disabled を表現しない。
- icon-only control には tooltip、accessible name、keyboard action を付ける。
- `11px` text を下回らない。重要説明は `12px` 以上。
- logical tab order を visual order と一致させる。
- focus ring を消さない。
- dynamic content 更新時は selection と scroll position を不必要に失わない。
- high DPI と CJK font fallback で clipping がないことを別途確認する。

## Do's and Don'ts

### Do

- panel、toolbar、tree、Inspector form の既存文法で feature を表現する。
- `20px` 前後の一貫した vertical rhythm を使う。
- selection/focus/active を異なる面または境界で示す。
- foldout、search、filter、shortcut で高密度情報を扱う。
- host theme と DPI から color/font/scale を解決する。
- menu、popup、dialog に明確な return path を持たせる。

### Don't

- 大きな hero、marketing card、gradient、glassmorphism を持ち込まない。
- すべてを丸い card に分割しない。
- 青を primary brand fill として広範囲に使わない。
- mobile の bottom navigation、floating action button、巨大 touch target を模倣しない。
- 状態変更で panel geometry を跳ねさせない。
- 永続操作を icon だけ、tooltip なしで置かない。
- project content の色を UI token として抽出しない。

## Implementation Guardrails

1. 標準 UI Toolkit/IMGUI control と Editor theme style を最優先する。
2. custom color が必要な場合だけ semantic token を使い、raw hex を component 内へ散らさない。
3. `:hover`, `:focus`, checked/selected, disabled の state を必ず揃える。
4. EditorWindow は dock 可能にし、fixed fullscreen page を作らない。
5. change は Undo、dirty state、save scope と整合させる。
6. user-visible command には menu path または keyboard path を用意する。
7. Windows native menu の白い surface を custom Editor popup にコピーしない。
8. exact pixel は Windows/Dark/100% の観察値。Light theme、macOS、high DPI では host style を優先する。

## Agent Prompt Guide

### Universal direction

「Unity Editor 2022.3 Dark theme の高密度な作業 UI として、ドッキングパネル、20px 行、2 カラム form、低彩度 surface、状態専用の青を使い、marketing/SaaS 的な装飾を避けて実装する。」

### Required visual checklist

- `{colors.panel}` を主面、`{colors.chrome-deep}` を構造境界にしているか。
- tab/local toolbar/content の階層があるか。
- body `12px`、row/field `20px` 前後か。
- selected、focus、active、disabled が区別されるか。
- icon-only action に tooltip/accessible name があるか。
- 狭幅、scroll、ellipsis を考慮したか。
- 大 radius、gradient、hero card、過剰な shadow を避けたか。

### Prompt examples

1. 「アセット依存関係を閲覧する dockable Unity EditorWindow を作る。左に検索可能な tree、中央に依存 graph、右に選択対象の Inspector 形式 detail。各 panel は tab/local toolbar/content を持ち、選択は `#2C5D87`、focus は 1px `#3A79BB` で示す。」
2. 「build validation の結果 panel を Unity 2022.3 Dark theme で設計する。20px の list row、severity icon、短い summary、右端 action を使い、error color だけで意味を伝えない。filter と keyboard navigation を含める。」
3. 「project-scoped settings を二ペインの floating EditorWindow として作る。左に category tree、右に foldout 付き 2-column form、上部に search。背景 scrim は使わず、Undo と validation recovery を明示する。」
4. 「Hierarchy の選択に追従する compact overlay を作る。anchor を明確にし、Dark surface、20px command row、右揃え shortcut、separator、Esc close、focus restore を実装する。」

### Negative guardrails

- Generic SaaS dashboard にしない。
- Mobile app navigation にしない。
- Landing page の visual hierarchy を持ち込まない。
- 色付き card grid を主構造にしない。
- Unity の selection blue を brand decoration に流用しない。

## Validation Notes

- 色と寸法は Unity Editor 2022.3.22f1、Windows、Dark、English、96 DPI、最大化 `1920 × 1080` の実画面測定。
- Inter と Roboto Mono は Unity Editor のインストール済み font resource で確認。
- native menu は OS が描画するため、Editor 内 custom component の token として扱わない。
- hover の exact color、semantic error/warning/success、motion duration は十分に測定できていないため、host style を継承する。
- Light theme、CJK、high DPI、small window、keyboard-only、screen reader は別途検証が必要。
