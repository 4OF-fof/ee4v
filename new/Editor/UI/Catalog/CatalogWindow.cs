using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow : EditorWindow
    {
        private const string RootClassName = "ee4v-ui";
        private static readonly Dictionary<string, int> RootGroupOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Display", 0 },
            { "DataView", 1 },
            { "Interactive", 2 },
            { "Overlay", 3 },
            { "Domain", 4 }
        };
        private enum ComponentImplementationKind
        {
            UiToolkit,
            Imgui
        }

        private enum InfoCardStoryPreset
        {
            Simple,
            Result
        }

        private enum WindowToastStoryPreset
        {
            Info,
            Success,
            Warning,
            Error
        }

        private enum SingleSelectButtonGroupStoryIconOption
        {
            None,
            Search,
            Close,
            DisclosureClosed,
            DisclosureOpen
        }

        private static readonly List<StoryRegistration> RegisteredStories = new List<StoryRegistration>();
        private static readonly List<string> RegisteredStyleSheetPaths = new List<string>();
        private static bool _registrationsLoaded;

        private readonly List<StoryDefinition> _stories = new List<StoryDefinition>();

        private VisualElement _navigatorHost;
        private VisualElement _contentHost;
        private StoryDefinition _selectedStory;
        private SearchableTreeView<NavigatorTreeNode> _navigatorTreeView;
        private readonly Dictionary<string, int> _navigatorStoryIds = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _isSyncingNavigatorSelection;

        [MenuItem("Debug/Catalog")]
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

        private void RebuildWindow()
        {
            EnsureStories();
            titleContent = new GUIContent(I18N.Get("catalog.window.title"));

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            AddCatalogStyleSheets(root);

            var shell = new VisualElement();
            shell.AddToClassList("ee4v-ui-catalog-shell");

            _navigatorHost = new VisualElement();
            _navigatorHost.AddToClassList("ee4v-ui-catalog-shell__navigator");

            _contentHost = new VisualElement();
            _contentHost.AddToClassList("ee4v-ui-catalog-shell__content");

            shell.Add(_navigatorHost);
            shell.Add(_contentHost);
            root.Add(shell);
            WindowToastApi.EnsureHost(this);

            BuildNavigator();
            ShowStory(_selectedStory);
        }

        private void BuildNavigator()
        {
            _navigatorHost.Clear();

            var title = UiTextFactory.Create(I18N.Get("catalog.window.title"), UiClassNames.CatalogNavigatorTitle);
            _navigatorHost.Add(title);

            _navigatorTreeView = new SearchableTreeView<NavigatorTreeNode>(
                CreateNavigatorTreeItem,
                BindNavigatorTreeItem,
                OnNavigatorSelectionChanged,
                I18N.Get("catalog.window.navigatorEmpty"));
            _navigatorTreeView.SetViewDataKey("ee4v-ui-catalog-navigator-tree");
            _navigatorTreeView.SetItems(BuildNavigatorTreeItems());
            _navigatorHost.Add(_navigatorTreeView);

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
            if (_navigatorTreeView == null)
            {
                return;
            }

            if (_selectedStory == null)
            {
                _isSyncingNavigatorSelection = true;
                try
                {
                    _navigatorTreeView.ClearSelection();
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
                    _navigatorTreeView.SetSelectionById(new[] { itemId });
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

        private VisualElement CreateNavigatorTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-ui-catalog-tree-item");
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeTitle));
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeImplementation));
            return row;
        }

        private void BindNavigatorTreeItem(VisualElement element, NavigatorTreeNode node)
        {
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

        private void OnNavigatorSelectionChanged(IReadOnlyList<NavigatorTreeNode> items)
        {
            if (_isSyncingNavigatorSelection || items == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var node = items[i];
                if (node != null && node.Story != null)
                {
                    SelectStory(node.Story);
                    return;
                }
            }
        }

        private List<SearchableTreeItemData<NavigatorTreeNode>> BuildNavigatorTreeItems()
        {
            _navigatorStoryIds.Clear();

            var roots = new List<NavigatorTreeNodeBuilder>();
            var folders = new Dictionary<string, NavigatorTreeNodeBuilder>(StringComparer.Ordinal);
            var nextId = 1;
            var orderedStories = _stories
                .OrderBy(story => story, StoryDefinitionGroupComparer.Instance)
                .ToArray();

            for (var i = 0; i < orderedStories.Length; i++)
            {
                var story = orderedStories[i];
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

        private static List<SearchableTreeItemData<NavigatorTreeNode>> ConvertNavigatorTreeItems(IReadOnlyList<NavigatorTreeNodeBuilder> builders)
        {
            var items = new List<SearchableTreeItemData<NavigatorTreeNode>>(builders.Count);
            for (var i = 0; i < builders.Count; i++)
            {
                items.Add(new SearchableTreeItemData<NavigatorTreeNode>(
                    builders[i].Id,
                    builders[i].Node,
                    builders[i].Node.SearchText,
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

        private sealed class StoryRegistration
        {
            public StoryRegistration(
                string id,
                string group,
                string title,
                string description,
                string details,
                IReadOnlyList<string> dependencies,
                ComponentImplementationKind implementation,
                Action<CatalogWindow, VisualElement> build)
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

            public Action<CatalogWindow, VisualElement> Build { get; }
        }

        private sealed class CatalogRegistry
        {
            public void RegisterStory(StoryRegistration story)
            {
                CatalogWindow.RegisterStory(story);
            }

            public void RegisterStyleSheet(string packageRelativePath)
            {
                CatalogWindow.RegisterStyleSheet(packageRelativePath);
            }
        }

        private interface ICatalogRegistrar
        {
            int Order { get; }

            void Register(CatalogRegistry registry);
        }

        private static void RegisterStory(StoryRegistration story)
        {
            if (story == null || string.IsNullOrWhiteSpace(story.Id))
            {
                return;
            }

            for (var i = 0; i < RegisteredStories.Count; i++)
            {
                if (string.Equals(RegisteredStories[i].Id, story.Id, StringComparison.Ordinal))
                {
                    RegisteredStories[i] = story;
                    return;
                }
            }

            RegisteredStories.Add(story);
        }

        private static void RegisterStyleSheet(string packageRelativePath)
        {
            if (string.IsNullOrWhiteSpace(packageRelativePath))
            {
                return;
            }

            if (RegisteredStyleSheetPaths.Contains(packageRelativePath))
            {
                return;
            }

            RegisteredStyleSheetPaths.Add(packageRelativePath);
        }

        private static void EnsureCatalogRegistrations()
        {
            if (_registrationsLoaded)
            {
                return;
            }

            _registrationsLoaded = true;
            var registrarTypes = TypeCache.GetTypesDerivedFrom<ICatalogRegistrar>()
                .Where(type => type != null && !type.IsAbstract && !type.IsInterface)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            var registrars = new List<ICatalogRegistrar>();

            for (var i = 0; i < registrarTypes.Length; i++)
            {
                var registrar = Activator.CreateInstance(registrarTypes[i], true) as ICatalogRegistrar;
                if (registrar != null)
                {
                    registrars.Add(registrar);
                }
            }

            var registry = new CatalogRegistry();
            foreach (var registrar in registrars.OrderBy(registrar => registrar.Order))
            {
                registrar.Register(registry);
            }
        }

        private sealed class NavigatorTreeNode
        {
            public NavigatorTreeNode(string title, string implementationShortLabel, StoryDefinition story)
            {
                Title = title ?? string.Empty;
                ImplementationShortLabel = implementationShortLabel ?? string.Empty;
                Story = story;
                SearchText = BuildSearchText(story, Title);
            }

            public string Title { get; }

            public string ImplementationShortLabel { get; }

            public StoryDefinition Story { get; }

            public string SearchText { get; }

            private static string BuildSearchText(StoryDefinition story, string title)
            {
                if (story == null)
                {
                    return title ?? string.Empty;
                }

                return string.Join("\n", new[]
                {
                    story.Title ?? string.Empty,
                    story.Group ?? string.Empty,
                    story.Description ?? string.Empty,
                    story.Details ?? string.Empty,
                });
            }
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

        private sealed class StoryDefinitionGroupComparer : IComparer<StoryDefinition>
        {
            public static readonly StoryDefinitionGroupComparer Instance = new StoryDefinitionGroupComparer();

            public int Compare(StoryDefinition left, StoryDefinition right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                var groupCompare = CompareGroup(left.Group, right.Group);
                if (groupCompare != 0)
                {
                    return groupCompare;
                }

                return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            }

            private static int CompareGroup(string leftGroup, string rightGroup)
            {
                var leftSegments = (leftGroup ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var rightSegments = (rightGroup ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                var leftRoot = leftSegments.Length > 0 ? leftSegments[0] : string.Empty;
                var rightRoot = rightSegments.Length > 0 ? rightSegments[0] : string.Empty;
                var leftOrder = RootGroupOrder.TryGetValue(leftRoot, out var leftValue) ? leftValue : int.MaxValue;
                var rightOrder = RootGroupOrder.TryGetValue(rightRoot, out var rightValue) ? rightValue : int.MaxValue;
                var rootCompare = leftOrder.CompareTo(rightOrder);
                if (rootCompare != 0)
                {
                    return rootCompare;
                }

                var maxLength = Math.Max(leftSegments.Length, rightSegments.Length);
                for (var i = 0; i < maxLength; i++)
                {
                    if (i >= leftSegments.Length)
                    {
                        return -1;
                    }

                    if (i >= rightSegments.Length)
                    {
                        return 1;
                    }

                    var compare = string.Compare(leftSegments[i], rightSegments[i], StringComparison.OrdinalIgnoreCase);
                    if (compare != 0)
                    {
                        return compare;
                    }
                }

                return 0;
            }
        }

    }
}
