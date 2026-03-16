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

        private enum InfoCardStoryPreset
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
                "tab-card",
                "Surface",
                "TabCard",
                "左上のタブ列で内容を切り替える box コンポーネントです。",
                "ブラウザのタブのように、上部タブを切り替えながら下部 panel の内容を差し替える用途を想定しています。content slot には任意の UI 要素を配置できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildTabCardStory));

            _stories.Add(new StoryDefinition(
                "info-card",
                "Surface",
                "InfoCard",
                "タイトル、説明、eyebrow、badge、body を組み合わせて情報面を構成する基本コンポーネントです。",
                "フォームセクション、空状態、密度の高い情報カードの土台として使います。header の各値が欠けても自然に見えるように余白を調整します。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildInfoCardStory));

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

        private void BuildInfoCardStory(VisualElement parent)
        {
            var preset = InfoCardStoryPreset.Default;
            var eyebrow = "Core";
            var title = "Feature Test Manager";
            var description = "密度の高い Editor パネル向けのカードレイアウトです。";
            var badgeText = string.Empty;
            var bodyText = "カードはセクション内に積んだり、単体の表示面として使えます。";
            Action refresh = null;

            Action<InfoCardStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                switch (selectedPreset)
                {
                    case InfoCardStoryPreset.Section:
                        eyebrow = string.Empty;
                        title = "不足キー";
                        description = "解析結果のグループ表示を包むセクションです。";
                        badgeText = "12";
                        bodyText = "本文には結果リスト、カード、任意のコントロールを配置できます。";
                        break;
                    case InfoCardStoryPreset.EmptyState:
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

            var controls = CreateControlsSection(parent, "InfoCard の各プロパティを編集し、値の有無ごとの見た目を確認します。");

            var eyebrowField = AddTextField(controls.Content, "Eyebrow", eyebrow, value =>
            {
                eyebrow = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル（必須）", title, value =>
            {
                title = value;
                refresh();
            });
            var descriptionField = AddTextField(controls.Content, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            var badgeField = AddTextField(controls.Content, "バッジ", badgeText, value =>
            {
                badgeText = value;
                refresh();
            });
            var bodyTextField = AddTextField(controls.Content, "本文テキスト", bodyText, value =>
            {
                bodyText = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var card = new InfoCard();
            preview.Body.Add(card);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(InfoCardStoryPreset.Default.ToString(), "標準"),
                            new TabCardTabState(InfoCardStoryPreset.Section.ToString(), "セクション風"),
                            new TabCardTabState(InfoCardStoryPreset.EmptyState.ToString(), "空状態風")
                        },
                        preset.ToString()),
                    id => applyPreset((InfoCardStoryPreset)Enum.Parse(typeof(InfoCardStoryPreset), id)));

                eyebrowField.SetValueWithoutNotify(eyebrow);
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                badgeField.SetValueWithoutNotify(badgeText);
                bodyTextField.SetValueWithoutNotify(bodyText);

                card.SetState(new InfoCardState(title, description, eyebrow, badgeText));
                card.Body.Clear();

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    var bodyLabel = UiTextFactory.Create(bodyText);
                    bodyLabel.SetWhiteSpace(WhiteSpace.Normal);
                    card.Body.Add(bodyLabel);
                }
            };

            applyPreset(preset);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildTabCardStory(VisualElement parent)
        {
            var firstLabel = "基本";
            var secondLabel = "詳細";
            var thirdLabel = "空状態";
            var selectedTabId = "basic";
            Action refresh = null;

            var controls = CreateControlsSection(parent, "タブ名と、選択中タブで表示する内容を編集します。");
            AddTextField(controls.Content, "タブ1", firstLabel, value =>
            {
                firstLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "タブ2", secondLabel, value =>
            {
                secondLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "タブ3", thirdLabel, value =>
            {
                thirdLabel = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var tabCard = new TabCard();
            preview.Body.Add(tabCard);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState("editor", "編集")
                        },
                        "editor"));

                tabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState("basic", firstLabel),
                            new TabCardTabState("detail", secondLabel),
                            new TabCardTabState("empty", thirdLabel)
                        },
                        selectedTabId),
                    id =>
                    {
                        selectedTabId = id;
                        refresh();
                    });

                tabCard.Content.Clear();
                tabCard.Content.Add(new InfoCard(new InfoCardState(
                    selectedTabId == "basic" ? "基本表示" : selectedTabId == "detail" ? "詳細表示" : "空状態表示",
                    selectedTabId == "basic"
                        ? "タブ切り替え後の content slot に任意の UI を配置できます。"
                        : selectedTabId == "detail"
                            ? "複数のフォーム、説明文、ステータスなどを任意に構成できます。"
                            : "コンポーネント未選択時やデータ空状態の panel としても使えます。",
                    null,
                    selectedTabId.ToUpperInvariant())));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private void BuildAlertsStory(VisualElement parent)
        {
            var tone = UiBannerTone.Info;
            var title = "情報表示";
            var message = "非ブロッキングな案内やエラー通知に使います。";
            Action refresh = null;
            Action<UiBannerTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiBannerTone.Warning:
                        title = "警告表示";
                        message = "確認が必要な状態や注意喚起に使います。";
                        break;
                    case UiBannerTone.Error:
                        title = "エラー表示";
                        message = "処理失敗や設定不備など、強く伝える必要がある状態に使います。";
                        break;
                    default:
                        title = "情報表示";
                        message = "非ブロッキングな案内やエラー通知に使います。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateControlsSection(parent, "タイトル、メッセージ、tone を切り替えて通知の見た目を確認します。");

            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            var messageField = AddTextField(controls.Content, "メッセージ", message, value =>
            {
                message = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var alerts = new Alerts();
            preview.Body.Add(CreatePreviewSurface(alerts, true));

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiBannerTone.Info.ToString(), "Info"),
                            new TabCardTabState(UiBannerTone.Warning.ToString(), "Warning"),
                            new TabCardTabState(UiBannerTone.Error.ToString(), "Error")
                        },
                        tone.ToString()),
                    id => applyPreset((UiBannerTone)Enum.Parse(typeof(UiBannerTone), id)));

                toneField.SetValueWithoutNotify((Enum)(object)tone);
                titleField.SetValueWithoutNotify(title);
                messageField.SetValueWithoutNotify(message);
                alerts.SetState(new AlertsState(tone, title, message));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildStatusBadgeStory(VisualElement parent)
        {
            var text = "実行中";
            var tone = UiStatusTone.Running;
            Action refresh = null;
            Action<UiStatusTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiStatusTone.Passed:
                        text = "成功";
                        break;
                    case UiStatusTone.Failed:
                        text = "失敗";
                        break;
                    case UiStatusTone.Idle:
                        text = "待機中";
                        break;
                    default:
                        text = "実行中";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateControlsSection(parent, "状態テキストと tone を切り替えて badge の見た目を確認します。");

            var textField = AddTextField(controls.Content, "テキスト", text, value =>
            {
                text = value;
                refresh();
            });
            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var badge = new StatusBadge();
            var surface = CreatePreviewSurface(true);
            surface.Add(badge);
            preview.Body.Add(surface);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiStatusTone.Idle.ToString(), "Idle"),
                            new TabCardTabState(UiStatusTone.Running.ToString(), "Running"),
                            new TabCardTabState(UiStatusTone.Passed.ToString(), "Passed"),
                            new TabCardTabState(UiStatusTone.Failed.ToString(), "Failed")
                        },
                        tone.ToString()),
                    id => applyPreset((UiStatusTone)Enum.Parse(typeof(UiStatusTone), id)));

                textField.SetValueWithoutNotify(text);
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                badge.SetState(new StatusBadgeState(text, tone));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }

        private ControlsSectionContext CreateControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            var tabCard = new TabCard();
            card.Body.Add(tabCard);
            parent.Add(card);
            return new ControlsSectionContext(card, tabCard);
        }

        private InfoCard CreatePreviewSection(VisualElement parent)
        {
            var card = new InfoCard(new InfoCardState("プレビュー", "コントロールの変更はすぐにプレビューへ反映されます。"));
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

        private InfoCard CreateDetailsSection(StoryDefinition story)
        {
            var card = new InfoCard(new InfoCardState(
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

        private static void FinalizeControlsSection(VisualElement parent, ControlsSectionContext controls)
        {
            if (controls == null || controls.Content.childCount > 0)
            {
                return;
            }

            parent.Remove(controls.Card);
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

        private sealed class ControlsSectionContext
        {
            public ControlsSectionContext(InfoCard card, TabCard tabCard)
            {
                Card = card;
                TabCard = tabCard;
            }

            public InfoCard Card { get; }

            public TabCard TabCard { get; }

            public VisualElement Content
            {
                get { return TabCard.Content; }
            }
        }
    }
}
