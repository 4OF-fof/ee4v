using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class CatalogWindow : EditorWindow
    {
        private enum ComponentImplementationKind
        {
            UiToolkit,
            Imgui
        }

        private enum UiCardStoryPreset
        {
            Default,
            Section,
            EmptyState
        }

        private readonly List<StoryDefinition> _stories = new List<StoryDefinition>();

        private VisualElement _navigatorHost;
        private VisualElement _contentHost;
        private StoryDefinition _selectedStory;
        private TreeView _navigatorTree;
        private readonly Dictionary<string, int> _navigatorStoryIds = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _isSyncingNavigatorSelection;

        [MenuItem("Debug/UI Catalog")]
        private static void ShowWindow()
        {
            var window = GetWindow<CatalogWindow>();
            window.minSize = new Vector2(UiTokens.WindowMinWidth, UiTokens.WindowMinHeight);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(I18N.Get("catalog.window.title"));
            EnsureStories();
        }

        private void CreateGUI()
        {
            RebuildWindow();
        }

        private void EnsureStories()
        {
            if (_stories.Count > 0)
            {
                return;
            }

            _stories.Add(new StoryDefinition(
                "card",
                "Surface",
                "UiCard",
                "タイトル、説明、eyebrow、badge、body を組み合わせて情報面を構成する基本コンポーネントです。",
                "フォームセクション、空状態、密度の高い情報カードの土台として使います。header の各値が欠けても自然に見えるように余白を調整します。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildCardStory));

            _stories.Add(new StoryDefinition(
                "alerts",
                "Feedback",
                "Alerts",
                "情報、警告、エラーの tone を切り替えてメッセージを表示する通知コンポーネントです。",
                "非ブロッキングな案内からエラー通知までを同じ構造で扱います。タイトルとメッセージの両方を持てるので、短い要約と補足説明を分けて表示できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildAlertsStory));

            _stories.Add(new StoryDefinition(
                "status-badge",
                "Status",
                "StatusBadge",
                "短い状態テキストを pill 形で表示するステータス表示コンポーネントです。",
                "カード header や一覧の補助情報に載せる小さな状態表示です。長めのテキストでも楕円に潰れず、pill 形を維持する前提で調整しています。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildStatusBadgeStory));

            if (_selectedStory == null && _stories.Count > 0)
            {
                _selectedStory = _stories[0];
            }
        }

        private void RebuildWindow()
        {
            EnsureStories();
            titleContent = new GUIContent(I18N.Get("catalog.window.title"));

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(UiClassNames.Root);
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Styles/ee4v-ui.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Styles/catalog-window.uss");

            var shell = new VisualElement();
            shell.AddToClassList("ee4v-ui-catalog-shell");

            _navigatorHost = new VisualElement();
            _navigatorHost.AddToClassList("ee4v-ui-catalog-shell__navigator");

            _contentHost = new VisualElement();
            _contentHost.AddToClassList("ee4v-ui-catalog-shell__content");

            shell.Add(_navigatorHost);
            shell.Add(_contentHost);
            root.Add(shell);

            BuildNavigator();
            ShowStory(_selectedStory);
        }

        private void BuildNavigator()
        {
            _navigatorHost.Clear();

            var title = UiTextFactory.Create(I18N.Get("catalog.window.title"), UiClassNames.CatalogNavigatorTitle);
            _navigatorHost.Add(title);

            var subtitle = UiTextFactory.Create(I18N.Get("catalog.window.navigatorSubtitle"), UiClassNames.CatalogNavigatorSubtitle);
            _navigatorHost.Add(subtitle);

            _navigatorTree = new TreeView();
            _navigatorTree.AddToClassList("ee4v-ui-catalog-shell__navigator-tree");
            _navigatorTree.viewDataKey = "ee4v-ui-catalog-navigator-tree";
            _navigatorTree.selectionType = SelectionType.Single;
            _navigatorTree.fixedItemHeight = 20;
            _navigatorTree.makeItem = CreateNavigatorTreeItem;
            _navigatorTree.bindItem = BindNavigatorTreeItem;
            _navigatorTree.selectionChanged += OnNavigatorSelectionChanged;
            _navigatorTree.SetRootItems(BuildNavigatorTreeItems());
            _navigatorTree.Rebuild();
            _navigatorTree.ExpandAll();
            _navigatorTree.style.flexGrow = 1f;
            _navigatorHost.Add(_navigatorTree);

            RefreshNavigatorSelection();
        }

        private void SelectStory(StoryDefinition story)
        {
            if (story == null)
            {
                return;
            }

            _selectedStory = story;
            RefreshNavigatorSelection();
            ShowStory(story);
        }

        private void RefreshNavigatorSelection()
        {
            if (_navigatorTree == null)
            {
                return;
            }

            if (_selectedStory == null)
            {
                _isSyncingNavigatorSelection = true;
                try
                {
                    _navigatorTree.ClearSelection();
                }
                finally
                {
                    _isSyncingNavigatorSelection = false;
                }

                return;
            }

            int itemId;
            if (_navigatorStoryIds.TryGetValue(_selectedStory.Id, out itemId))
            {
                _isSyncingNavigatorSelection = true;
                try
                {
                    _navigatorTree.SetSelectionById(new[] { itemId });
                }
                finally
                {
                    _isSyncingNavigatorSelection = false;
                }
            }
        }

        private void ShowStory(StoryDefinition story)
        {
            if (_contentHost == null || story == null)
            {
                return;
            }

            _contentHost.Clear();

            var page = new VisualElement();
            page.AddToClassList("ee4v-ui-catalog-page");

            var header = new VisualElement();
            header.AddToClassList("ee4v-ui-catalog-page__header");
            header.Add(UiTextFactory.Create(story.Title, UiClassNames.CatalogPageTitle));
            header.Add(UiTextFactory.Create(story.Description, UiClassNames.CatalogPageDescription));

            var body = new ScrollView();
            body.AddToClassList("ee4v-ui-catalog-page__body");

            page.Add(header);
            page.Add(body);

            body.contentContainer.Add(CreateDetailsSection(story));
            story.Build(body.contentContainer);

            _contentHost.Add(page);
        }

        private void BuildCardStory(VisualElement parent)
        {
            var preset = UiCardStoryPreset.Default;
            var eyebrow = "Core";
            var title = "Feature Test Manager";
            var description = "密度の高い Editor パネル向けのカードレイアウトです。";
            var badgeText = string.Empty;
            var bodyText = "カードはセクション内に積んだり、単体の表示面として使えます。";
            Action refresh = null;

            Action<UiCardStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                switch (selectedPreset)
                {
                    case UiCardStoryPreset.Section:
                        eyebrow = string.Empty;
                        title = "不足キー";
                        description = "解析結果のグループ表示を包むセクションです。";
                        badgeText = "12";
                        bodyText = "本文には結果リスト、カード、任意のコントロールを配置できます。";
                        break;
                    case UiCardStoryPreset.EmptyState:
                        eyebrow = string.Empty;
                        title = "結果なし";
                        description = "現在の条件で表示対象がないときに使う状態です。";
                        badgeText = string.Empty;
                        bodyText = string.Empty;
                        break;
                    default:
                        eyebrow = "Core";
                        title = "Feature Test Manager";
                        description = "密度の高い Editor パネル向けのカードレイアウトです。";
                        badgeText = string.Empty;
                        bodyText = "カードはセクション内に積んだり、単体の表示面として使えます。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateControlsSection(parent, "Card の各プロパティを編集し、値の有無ごとの見た目を確認します。");
            var presetRow = new VisualElement();
            presetRow.AddToClassList(UiClassNames.CatalogButtonRow);
            presetRow.Add(UiTextFactory.Create("プリセット"));
            presetRow.Add(new Button(() => applyPreset(UiCardStoryPreset.Default)) { text = "標準" });
            presetRow.Add(new Button(() => applyPreset(UiCardStoryPreset.Section)) { text = "UiSection" });
            presetRow.Add(new Button(() => applyPreset(UiCardStoryPreset.EmptyState)) { text = "UiEmptyState" });
            controls.Body.Add(presetRow);

            var eyebrowField = AddTextField(controls.Body, "Eyebrow", eyebrow, value =>
            {
                eyebrow = value;
                refresh();
            });
            var titleField = AddTextField(controls.Body, "タイトル（必須）", title, value =>
            {
                title = value;
                refresh();
            });
            var descriptionField = AddTextField(controls.Body, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            var badgeField = AddTextField(controls.Body, "バッジ", badgeText, value =>
            {
                badgeText = value;
                refresh();
            });
            var bodyTextField = AddTextField(controls.Body, "本文テキスト", bodyText, value =>
            {
                bodyText = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var card = new UiCard();
            preview.Body.Add(card);

            refresh = () =>
            {
                eyebrowField.SetValueWithoutNotify(eyebrow);
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                badgeField.SetValueWithoutNotify(badgeText);
                bodyTextField.SetValueWithoutNotify(bodyText);

                card.SetState(new UiCardState(title, description, eyebrow, badgeText));
                card.Body.Clear();

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    var bodyLabel = UiTextFactory.Create(bodyText);
                    bodyLabel.SetWhiteSpace(WhiteSpace.Normal);
                    card.Body.Add(bodyLabel);
                }
            };

            applyPreset(preset);
        }

        private void BuildAlertsStory(VisualElement parent)
        {
            var tone = UiBannerTone.Info;
            var title = "情報表示";
            var message = "非ブロッキングな案内やエラー通知に使います。";
            Action refresh = null;

            var controls = CreateControlsSection(parent, "タイトル、メッセージ、tone を切り替えて通知の見た目を確認します。");
            AddEnumField(controls.Body, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });
            AddTextField(controls.Body, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "メッセージ", message, value =>
            {
                message = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var alerts = new Alerts();
            preview.Body.Add(CreatePreviewSurface(alerts, true));

            refresh = () => { alerts.SetState(new AlertsState(tone, title, message)); };
            refresh();
        }

        private void BuildStatusBadgeStory(VisualElement parent)
        {
            var text = "実行中";
            var tone = UiStatusTone.Running;
            Action refresh = null;

            var controls = CreateControlsSection(parent, "状態テキストと tone を切り替えて badge の見た目を確認します。");
            AddTextField(controls.Body, "テキスト", text, value =>
            {
                text = value;
                refresh();
            });
            AddEnumField(controls.Body, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var badge = new StatusBadge();
            var surface = CreatePreviewSurface(true);
            surface.Add(badge);
            preview.Body.Add(surface);

            refresh = () => { badge.SetState(new StatusBadgeState(text, tone)); };
            refresh();
        }

        private UiCard CreateControlsSection(VisualElement parent, string description)
        {
            var card = new UiCard(new UiCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            parent.Add(card);
            return card;
        }

        private UiCard CreatePreviewSection(VisualElement parent)
        {
            var card = new UiCard(new UiCardState("プレビュー", "コントロールの変更はすぐにプレビューへ反映されます。"));
            var inserted = false;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.ElementAt(i);
                if (!Equals(child.userData, "catalog-controls-section"))
                {
                    continue;
                }

                parent.Insert(i, card);
                inserted = true;
                break;
            }

            if (!inserted)
            {
                parent.Add(card);
            }

            return card;
        }

        private VisualElement CreatePreviewSurface(bool compact = false)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-ui-catalog-preview-surface");
            if (compact)
            {
                surface.AddToClassList("ee4v-ui-catalog-preview-surface--compact");
            }

            return surface;
        }

        private VisualElement CreatePreviewSurface(VisualElement content, bool compact = false)
        {
            var surface = CreatePreviewSurface(compact);
            surface.Add(content);
            return surface;
        }

        private static TextField AddTextField(VisualElement parent, string label, string value, Action<string> onChanged, bool multiline = false)
        {
            var field = new TextField(label);
            field.multiline = multiline;
            if (multiline)
            {
                field.style.minHeight = 72f;
            }

            field.value = value;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }

        private static Toggle AddToggle(VisualElement parent, string label, bool value, Action<bool> onChanged)
        {
            var field = new Toggle(label);
            field.value = value;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }

        private static IntegerField AddIntegerField(VisualElement parent, string label, int value, Action<int> onChanged)
        {
            var field = new IntegerField(label);
            field.value = value;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }

        private static EnumField AddEnumField<TEnum>(VisualElement parent, string label, TEnum value, Action<TEnum> onChanged)
            where TEnum : struct, Enum
        {
            var field = new EnumField(label, (Enum)(object)value);
            field.RegisterValueChangedCallback(evt => onChanged((TEnum)(object)evt.newValue));
            parent.Add(field);
            return field;
        }

        private VisualElement CreateNavigatorTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-ui-catalog-tree-item");
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeTitle));
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeImplementation));
            return row;
        }

        private void BindNavigatorTreeItem(VisualElement element, int index)
        {
            var node = _navigatorTree.GetItemDataForIndex<NavigatorTreeNode>(index);
            var title = element.ElementAt(0) as UiTextElement;
            var implementation = element.ElementAt(1) as UiTextElement;

            if (title != null)
            {
                title.SetText(node.Title);
            }

            if (implementation != null)
            {
                implementation.SetText(node.ImplementationShortLabel);
                implementation.EnableInClassList("ee4v-ui-catalog-tree-item__implementation--hidden", string.IsNullOrEmpty(node.ImplementationShortLabel));
            }
        }

        private void OnNavigatorSelectionChanged(IEnumerable<object> items)
        {
            if (_isSyncingNavigatorSelection || items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                var node = item as NavigatorTreeNode;
                if (node != null && node.Story != null)
                {
                    SelectStory(node.Story);
                    return;
                }
            }
        }

        private List<TreeViewItemData<NavigatorTreeNode>> BuildNavigatorTreeItems()
        {
            _navigatorStoryIds.Clear();

            var roots = new List<NavigatorTreeNodeBuilder>();
            var folders = new Dictionary<string, NavigatorTreeNodeBuilder>(StringComparer.Ordinal);
            var nextId = 1;

            for (var i = 0; i < _stories.Count; i++)
            {
                var story = _stories[i];
                var currentChildren = roots;
                var segments = story.Group.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var path = string.Empty;

                for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    path = string.IsNullOrEmpty(path)
                        ? segments[segmentIndex]
                        : path + "/" + segments[segmentIndex];

                    NavigatorTreeNodeBuilder folder;
                    if (!folders.TryGetValue(path, out folder))
                    {
                        folder = new NavigatorTreeNodeBuilder(
                            nextId++,
                            new NavigatorTreeNode(segments[segmentIndex], string.Empty, null));
                        folders.Add(path, folder);
                        currentChildren.Add(folder);
                    }

                    currentChildren = folder.Children;
                }

                var storyNode = new NavigatorTreeNodeBuilder(
                    nextId++,
                    new NavigatorTreeNode(story.Title, GetImplementationShortLabel(story.Implementation), story));
                currentChildren.Add(storyNode);
                _navigatorStoryIds[story.Id] = storyNode.Id;
            }

            return ConvertNavigatorTreeItems(roots);
        }

        private static List<TreeViewItemData<NavigatorTreeNode>> ConvertNavigatorTreeItems(IReadOnlyList<NavigatorTreeNodeBuilder> builders)
        {
            var items = new List<TreeViewItemData<NavigatorTreeNode>>(builders.Count);
            for (var i = 0; i < builders.Count; i++)
            {
                items.Add(new TreeViewItemData<NavigatorTreeNode>(
                    builders[i].Id,
                    builders[i].Node,
                    ConvertNavigatorTreeItems(builders[i].Children)));
            }

            return items;
        }

        private UiCard CreateDetailsSection(StoryDefinition story)
        {
            var card = new UiCard(new UiCardState(
                I18N.Get("catalog.common.details"),
                story.Details));

            card.Body.Add(CreateDetailItem(I18N.Get("catalog.common.implementation"), GetImplementationLabel(story.Implementation)));
            card.Body.Add(CreateDetailItem(
                I18N.Get("catalog.common.dependencies"),
                story.Dependencies.Count == 0 ? I18N.Get("catalog.common.none") : string.Join("\n", story.Dependencies)));
            return card;
        }

        private VisualElement CreateDetailItem(string label, string value)
        {
            var item = new VisualElement();
            item.AddToClassList("ee4v-ui-catalog-detail-item");

            var labelElement = UiTextFactory.Create(label, UiClassNames.CatalogDetailLabel);
            var valueElement = UiTextFactory.Create(value, UiClassNames.CatalogDetailValue);
            valueElement.SetWhiteSpace(WhiteSpace.Normal);

            item.Add(labelElement);
            item.Add(valueElement);
            return item;
        }

        private static string GetImplementationShortLabel(ComponentImplementationKind implementation)
        {
            switch (implementation)
            {
                case ComponentImplementationKind.Imgui:
                    return "IMGUI";
                default:
                    return string.Empty;
            }
        }

        private static string GetImplementationLabel(ComponentImplementationKind implementation)
        {
            switch (implementation)
            {
                case ComponentImplementationKind.Imgui:
                    return I18N.Get("catalog.common.imguiVisual");
                default:
                    return I18N.Get("catalog.common.uiToolkitVisual");
            }
        }

        private sealed class StoryDefinition
        {
            public StoryDefinition(
                string id,
                string group,
                string title,
                string description,
                string details,
                IReadOnlyList<string> dependencies,
                ComponentImplementationKind implementation,
                Action<VisualElement> build)
            {
                Id = id;
                Group = group;
                Title = title;
                Description = description;
                Details = details;
                Dependencies = dependencies ?? new string[0];
                Implementation = implementation;
                Build = build;
            }

            public string Id { get; }

            public string Group { get; }

            public string Title { get; }

            public string Description { get; }

            public string Details { get; }

            public IReadOnlyList<string> Dependencies { get; }

            public ComponentImplementationKind Implementation { get; }

            public Action<VisualElement> Build { get; }
        }

        private sealed class NavigatorTreeNode
        {
            public NavigatorTreeNode(string title, string implementationShortLabel, StoryDefinition story)
            {
                Title = title ?? string.Empty;
                ImplementationShortLabel = implementationShortLabel ?? string.Empty;
                Story = story;
            }

            public string Title { get; }

            public string ImplementationShortLabel { get; }

            public StoryDefinition Story { get; }
        }

        private sealed class NavigatorTreeNodeBuilder
        {
            public NavigatorTreeNodeBuilder(int id, NavigatorTreeNode node)
            {
                Id = id;
                Node = node;
                Children = new List<NavigatorTreeNodeBuilder>();
            }

            public int Id { get; }

            public NavigatorTreeNode Node { get; }

            public List<NavigatorTreeNodeBuilder> Children { get; }
        }
    }
}
