using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class AssetManagerWindow : EditorWindow
    {
        private const string WindowTitle = "Asset Manager";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string LayoutClassName = "ee4v-asset-manager-window__layout";
        private const string PaneScrollClassName = "ee4v-asset-manager-window__pane-scroll";
        private const string PaneListClassName = "ee4v-asset-manager-window__pane-list";
        private const string PaneCardClassName = "ee4v-asset-manager-window__pane-card";
        private const string PaneRowClassName = "ee4v-asset-manager-window__pane-row";
        private const float DefaultNavigationWidth = 240f;
        private const float DefaultInspectorWidth = 300f;
        private const float NavigationMinWidth = 180f;
        private const float NavigationMaxWidth = 360f;
        private const float ContentMinWidth = 420f;
        private const float InspectorMinWidth = 240f;
        private const float InspectorMaxWidth = 420f;

        [SerializeField]
        private float _navigationWidth = DefaultNavigationWidth;

        [SerializeField]
        private float _inspectorWidth = DefaultInspectorWidth;

        [SerializeField]
        private bool _navigationCollapsed;

        [SerializeField]
        private bool _inspectorCollapsed;

        [MenuItem("ee4v/Asset Manager", false, 0)]
        private static void ShowWindow()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(UiTokens.WindowMinWidth, UiTokens.WindowMinHeight);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(UiTokens.WindowMinWidth, UiTokens.WindowMinHeight);
        }

        private void CreateGUI()
        {
            RebuildWindow();
        }

        private void RebuildWindow()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/asset-manager-window-layout.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/info-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/asset-manager-window.uss");

            var layout = new AssetManagerWindowLayout(CreateLayoutState());
            layout.AddToClassList(LayoutClassName);
            layout.NavigationPaneWidthChanged += value => _navigationWidth = value;
            layout.InspectorPaneWidthChanged += value => _inspectorWidth = value;
            layout.NavigationCollapsedChanged += value => _navigationCollapsed = value;
            layout.InspectorCollapsedChanged += value => _inspectorCollapsed = value;

            PopulatePane(
                layout.NavigationPaneContent,
                CreateCard(
                    "AssetManager",
                    "Navigation",
                    "カテゴリ、ソース、保存済みコレクションを切り替える領域です。",
                    "All Assets",
                    "Favorites",
                    "Booth Library",
                    "Packages"),
                CreateCard(
                    "Collections",
                    "Saved Views",
                    "よく使う絞り込みや作業セットをここに配置します。",
                    "Recent Imports",
                    "Needs Review",
                    "Ready To Publish"));

            PopulatePane(
                layout.ContentPaneContent,
                CreateCard(
                    "AssetManager",
                    "Assets",
                    "検索、一覧、選択状態の主要導線を置く主領域です。",
                    "検索バーとフィルタ",
                    "グリッド / リスト切り替え",
                    "一括操作と import/export 導線"),
                CreateCard(
                    "Workflow",
                    "Main View",
                    "ここに Asset 一覧や空状態、読み込み中表示を組み込みます。",
                    "検索結果一覧",
                    "ドラッグ＆ドロップ導線",
                    "進行中タスクの状態表示"));

            PopulatePane(
                layout.InspectorPaneContent,
                CreateCard(
                    "Selection",
                    "Inspector",
                    "選択中アセットの詳細と編集操作を置く領域です。",
                    "メタデータ",
                    "タグ / ラベル",
                    "依存関係と参照先"),
                CreateCard(
                    "Preview",
                    "Details",
                    "未選択時の空状態と、プレビュー・検証結果をここに出します。",
                    "Thumbnail / Preview",
                    "Validation",
                    "Version Notes"));

            root.Add(layout);
            WindowToastApi.EnsureHost(this);
        }

        private AssetManagerWindowLayoutState CreateLayoutState()
        {
            return new AssetManagerWindowLayoutState(
                navigationWidth: Mathf.Max(0f, _navigationWidth),
                inspectorWidth: Mathf.Max(0f, _inspectorWidth),
                navigationMinWidth: NavigationMinWidth,
                navigationMaxWidth: NavigationMaxWidth,
                contentMinWidth: ContentMinWidth,
                inspectorMinWidth: InspectorMinWidth,
                inspectorMaxWidth: InspectorMaxWidth,
                navigationCollapsed: _navigationCollapsed,
                inspectorCollapsed: _inspectorCollapsed);
        }

        private static void PopulatePane(VisualElement paneContent, params InfoCard[] cards)
        {
            CreatePaneScroll(paneContent, out var list);

            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                {
                    continue;
                }

                cards[i].AddToClassList(PaneCardClassName);
                list.Add(cards[i]);
            }
        }

        private static InfoCard CreateCard(string eyebrow, string title, string description, params string[] rows)
        {
            var card = new InfoCard(new InfoCardState(title, description, eyebrow));
            for (var i = 0; i < rows.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(rows[i]))
                {
                    continue;
                }

                var row = UiTextFactory.Create(rows[i]);
                row.AddToClassList(PaneRowClassName);
                row.SetWhiteSpace(WhiteSpace.Normal);
                card.Body.Add(row);
            }

            return card;
        }

        private static ScrollView CreatePaneScroll(VisualElement paneContent, out VisualElement list)
        {
            paneContent.Clear();

            var scroll = new ScrollView();
            scroll.AddToClassList(PaneScrollClassName);

            list = new VisualElement();
            list.AddToClassList(PaneListClassName);

            scroll.Add(list);
            paneContent.Add(scroll);
            return scroll;
        }
    }
}
