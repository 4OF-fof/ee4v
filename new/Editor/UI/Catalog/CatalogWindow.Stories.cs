using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private void EnsureStories()
        {
            if (_stories.Count > 0)
            {
                return;
            }

            _stories.Add(new StoryDefinition(
                "search-field",
                "Interactive",
                "SearchField",
                "検索入力と clear 操作をまとめた単体利用向けの検索コンポーネントです。",
                "一覧やカード列の絞り込みに使う軽量な検索入力です。placeholder と clear button を持ち、SearchableTreeView の検索 UI と同じ見た目・挙動を単体でも使えます。",
                new[]
                {
                    "Icon"
                },
                ComponentImplementationKind.UiToolkit,
                BuildSearchFieldStory));

            _stories.Add(new StoryDefinition(
                "single-select-button-group",
                "Interactive",
                "SingleSelectButtonGroup",
                "縦並びの button 群から 1 件だけを選ぶ、単一選択向けコンポーネントです。",
                "old AssetManager navigation のように、カテゴリやモードをリストから 1 つ選ぶ用途を想定しています。選択中 item は面色で強調し、他 item と同じ button 操作で切り替えます。",
                new[]
                {
                    "Icon"
                },
                ComponentImplementationKind.UiToolkit,
                BuildSingleSelectButtonGroupStory));

            _stories.Add(new StoryDefinition(
                "context-menu-window",
                "Interactive",
                "ContextMenuWindow",
                "old AssetManager の GenericDropdownMenu に近い見た目を UI Toolkit と USS で再現したコンテキストメニューWindowです。",
                "target VisualElement と panel/world position を渡して開きます。項目、区切り、disabled、icon、shortcut、選択 callback を扱い、幅は項目テキストを測定して決めます。",
                new[]
                {
                    "Icon"
                },
                ComponentImplementationKind.UiToolkit,
                BuildContextMenuWindowStory));

            _stories.Add(new StoryDefinition(
                "searchable-tree-view",
                "DataView",
                "SearchableTreeView",
                "検索窓と tree view をまとめて提供する、絞り込み可能なツリーコンポーネントです。",
                "呼び出し側は階層データと row 描画だけを渡し、検索文字列の状態管理や tree の絞り込みは component 側に任せます。検索欄は SearchField を内部利用し、tree 本体と同じ面の中で扱います。各 row 右側の短い文字列は component が自動生成するものではなく、bindItem で描画する row data 側の meta 表示です。",
                new[]
                {
                    "SearchField"
                },
                ComponentImplementationKind.UiToolkit,
                BuildSearchableTreeViewStory));

            _stories.Add(new StoryDefinition(
                "copyable-text-area",
                "Display",
                "CopyableTextArea",
                "長文の確認結果を選択・コピーできる、読み取り専用のテキスト領域コンポーネントです。",
                "右上に copy button を持つ readonly multiline text field です。テスト詳細や監査ログのような長文を表示し、そのまま clipboard へ渡す用途を想定しています。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildCopyableTextAreaStory));

            _stories.Add(new StoryDefinition(
                "window-toast",
                "Overlay",
                "WindowToast",
                "ee4v 自前 EditorWindow に後付けできる、右上スタック型の toast 通知基盤です。",
                "window root に absolute overlay host を追加し、info/success/warning/error の通知を縦に積みます。action button を持つ toast も同じ面の中で扱えます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildWindowToastStory));

            _stories.Add(new StoryDefinition(
                "test-result-group",
                "Domain/Testing",
                "TestResultGroup",
                "feature test の状態、件数 alert、実行導線、登録テスト一覧をまとめて表示する testing 向けコンポーネントです。",
                "Test List の結果表示用に作った domain-specific component です。InfoCard を土台にしつつ、header 右側に status badge と run button、body に件数 alert、copy 可能な詳細結果、登録済みテスト一覧の開閉を持たせています。",
                new[]
                {
                    "InfoCard",
                    "StatusBadge",
                    "Alerts",
                    "CopyableTextArea"
                },
                ComponentImplementationKind.UiToolkit,
                BuildTestResultGroupStory));

            _stories.Add(new StoryDefinition(
                "asset-manager-navigation-panel",
                "Domain/AssetManager",
                "NavigationPanel",
                "AssetManager 左ペイン用の navigation コンポーネントです。",
                "カテゴリ、ソース、保存済みビューのような左側導線を単体で再利用できるようにした panel component です。AssetManagerWindowLayout の左ペインにも、単体 window にも同じものを載せます。",
                new[]
                {
                    "InfoCard"
                },
                ComponentImplementationKind.UiToolkit,
                BuildAssetManagerNavigationPanelStory));

            _stories.Add(new StoryDefinition(
                "asset-manager-main-view",
                "Domain/AssetManager",
                "MainView",
                "AssetManager 中央領域の toolbar 以下だけを表す main view コンポーネントです。",
                "layout 内では上部 toolbar の下に配置し、単体 window では toolbar と呼び出し側で合成する前提です。一覧、空状態、進行中タスク表示などを置くベース領域として扱います。",
                new[]
                {
                    "InfoCard"
                },
                ComponentImplementationKind.UiToolkit,
                BuildAssetManagerMainViewStory));

            _stories.Add(new StoryDefinition(
                "asset-manager-infomation-panel",
                "Domain/AssetManager",
                "InfomationPanel",
                "AssetManager 右ペイン用の情報パネルコンポーネントです。",
                "選択中アセットの詳細、プレビュー、検証結果の文脈を単体でも layout 内でも同じ構成で再利用する右ペイン component です。",
                new[]
                {
                    "InfoCard"
                },
                ComponentImplementationKind.UiToolkit,
                BuildAssetManagerInfomationPanelStory));

            _stories.Add(new StoryDefinition(
                "asset-manager-window-layout",
                "Domain/AssetManager",
                "AssetManagerWindowLayout",
                "AssetManager 向けの 3 カラム window shell です。左右ペインは drag で幅変更でき、完全に折りたためます。",
                "左に navigation、中央に一覧、右に inspector を置く前提の domain-specific layout です。左右の split bar は drag で幅変更し、bar 上の button で完全に折りたたみできます。ヘッダー表示は持たず、各ペイン内部の UI が自身の見出しを持つ前提です。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildAssetManagerWindowLayoutStory));

            _stories.Add(new StoryDefinition(
                "asset-manager-toolbar",
                "Domain/AssetManager",
                "AssetManagerToolbar",
                "AssetManager main view 上部に置く、横並びの toolbar コンテナです。",
                "現時点では中身を持たない container-only component です。呼び出し側が search、filter、action button などを Content slot に追加して使う前提です。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildAssetManagerToolbarStory));

            _stories.Add(new StoryDefinition(
                "asset-item-grid",
                "Domain/AssetManager",
                "AssetItemGrid",
                "AssetManager item list を受け取り、汎用 ItemGrid に表示状態として流し込む domain component です。",
                "AssetManagerItemList から Texture2D 付き ItemGridState への変換と cache 利用を内包し、MainView 側が ItemGridCache を直接意識しないための adapter として扱います。",
                new[]
                {
                    "ItemGrid",
                    "ItemCard"
                },
                ComponentImplementationKind.UiToolkit,
                BuildAssetItemGridStory));

            _stories.Add(new StoryDefinition(
                "item-card",
                "Display",
                "ItemCard",
                "サムネイルと item 名だけの汎用カードコンポーネントです。",
                "データ取得やキャッシュは外側の loader/service が担当し、ItemCard は Texture2D と item 名を受け取って表示するだけの薄い UI component として扱います。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildAssetManagerItemCardStory));

            _stories.Add(new StoryDefinition(
                "item-grid",
                "DataView",
                "ItemGrid",
                "ItemCard を仮想スクロールで並べる汎用グリッドコンポーネントです。",
                "UI Toolkit の ListView を行単位で使い、表示領域に必要な行だけを生成します。列数は available width から再計算し、各セルには ItemCard を配置します。",
                new[]
                {
                    "ItemCard"
                },
                ComponentImplementationKind.UiToolkit,
                BuildAssetManagerItemGridStory));

            _stories.Add(new StoryDefinition(
                "tab-card",
                "Interactive",
                "TabCard",
                "左上のタブ列で内容を切り替える box コンポーネントです。",
                "ブラウザのタブのように、上部タブを切り替えながら下部 panel の内容を差し替える用途を想定しています。content slot には任意の UI 要素を配置できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildTabCardStory));

            _stories.Add(new StoryDefinition(
                "info-card",
                "Display",
                "InfoCard",
                "タイトル、説明、eyebrow、badge、body を組み合わせて情報面を構成する基本コンポーネントです。",
                "シンプルな情報表示から、結果一覧の見出し付きカードまで幅広く使う土台です。header の各値が欠けても自然に見えるように余白を調整し、内蔵の badge と本文を組み合わせて情報密度を調整できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildInfoCardStory));

            _stories.Add(new StoryDefinition(
                "alerts",
                "Display",
                "Alerts",
                "情報、警告、エラーの tone を切り替えてメッセージを表示する通知コンポーネントです。",
                "非ブロッキングな案内からエラー通知までを同じ構造で扱います。タイトルとメッセージの両方を持てるので、短い要約と補足説明を分けて表示できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildAlertsStory));

            _stories.Add(new StoryDefinition(
                "status-badge",
                "Display",
                "StatusBadge",
                "短い状態テキストを pill 形で表示するステータス表示コンポーネントです。",
                "カード header や一覧の補助情報に載せる小さな状態表示です。長めのテキストでも楕円に潰れず、pill 形を維持する前提で調整しています。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildStatusBadgeStory));

            _stories.Add(new StoryDefinition(
                "icon",
                "Display",
                "Icon",
                "任意の texture または enum 管理された Unity 内蔵アイコンを表示するアイコンコンポーネントです。",
                "Unity 内蔵アイコンは version 差分の影響を抑えるため enum で許可したものだけを解決します。初期状態では検索アイコンをサポートし、custom texture に切り替えれば任意 texture を表示できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildIconStory));

            if (_selectedStory == null && _stories.Count > 0)
            {
                _selectedStory = _stories[0];
            }
        }
    }
}
