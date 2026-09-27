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
            { "Reference", 0 },
            { "Inputs", 1 },
            { "Displays", 2 },
            { "Containers", 3 },
            { "Collections", 4 },
            { "Domain", 5 }
        };
        private static readonly Dictionary<string, int> DomainRoleOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Inputs", 0 },
            { "Displays", 1 },
            { "Containers", 2 },
            { "Collections", 3 }
        };
        internal enum ComponentImplementationKind
        {
            UiToolkit,
            Imgui
        }

        private enum InfoCardStoryPreset
        {
            Simple,
            Result
        }

        private static readonly List<StoryRegistration> RegisteredStories = new List<StoryRegistration>();
        private static readonly List<string> RegisteredStyleSheetPaths = new List<string>();
        private static bool _registrationsLoaded;

        private readonly List<StoryDefinition> _stories = new List<StoryDefinition>();

        private VisualElement _navigatorHost;
        private VisualElement _contentHost;
        private StoryDefinition _selectedStory;
        private string _selectedCategoryPath;
        private SearchableTreeView<NavigatorTreeNode> _navigatorTreeView;
        private readonly Dictionary<string, int> _navigatorStoryIds = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _navigatorCategoryIds = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _isSyncingNavigatorSelection;

        [MenuItem("ee4v/Debug/Catalog", false, 1100)]
        private static void ShowWindow()
        {
            var window = GetWindow<CatalogWindow>();
            window.minSize = new Vector2(
                UiSizeTokens.WindowMinWidth,
                UiSizeTokens.WindowMinHeight);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("catalog.window.title"));
            EnsureStories();
        }

        private void CreateGUI()
        {
            RebuildWindow();
        }

        private void RebuildWindow()
        {
            EnsureStories();
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("catalog.window.title"));

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            UiComposition.ApplyTheme(root);
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
            BuildNavigator();
            ShowSelectedPage();
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
                I18N.Get("catalog.window.navigatorEmpty"),
                I18N.Get("ui.search.placeholder"),
                searchTooltip: I18N.Get("ui.search.tooltip"),
                clearTooltip: I18N.Get("ui.clear.tooltip"));
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
            _selectedCategoryPath = null;
            RefreshNavigatorSelection();
            ShowStory(story);
        }

        private void SelectCategory(string categoryPath)
        {
            if (string.IsNullOrWhiteSpace(categoryPath))
            {
                return;
            }

            _selectedStory = null;
            _selectedCategoryPath = categoryPath;
            RefreshNavigatorSelection();
            ShowCategory(categoryPath);
        }

        private void RefreshNavigatorSelection()
        {
            if (_navigatorTreeView == null)
            {
                return;
            }

            var itemId = 0;
            var hasSelection = _selectedStory != null
                ? _navigatorStoryIds.TryGetValue(_selectedStory.Id, out itemId)
                : !string.IsNullOrWhiteSpace(_selectedCategoryPath) &&
                  _navigatorCategoryIds.TryGetValue(_selectedCategoryPath, out itemId);
            if (!hasSelection)
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

        private void ShowSelectedPage()
        {
            if (!string.IsNullOrWhiteSpace(_selectedCategoryPath))
            {
                ShowCategory(_selectedCategoryPath);
                return;
            }

            ShowStory(_selectedStory);
        }

        private void ShowStory(StoryDefinition story)
        {
            if (_contentHost == null || story == null)
            {
                return;
            }

            _contentHost.Clear();
            ScrollView body;
            var page = CreatePage(story.Title, story.Description, out body);

            body.contentContainer.Add(CreateDetailsSection(story));
            story.Build(body.contentContainer);

            _contentHost.Add(page);
        }

        private void ShowCategory(string categoryPath)
        {
            if (_contentHost == null || string.IsNullOrWhiteSpace(categoryPath))
            {
                return;
            }

            var stories = _stories
                .Where(story => IsStoryInCategory(story, categoryPath))
                .OrderBy(story => story, StoryDefinitionGroupComparer.Instance)
                .ToArray();
            var segments = categoryPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var title = segments.Length > 0 ? segments[segments.Length - 1] : categoryPath;

            _contentHost.Clear();
            ScrollView body;
            var page = CreatePage(
                title,
                GetCategoryDescription(categoryPath),
                out body);

            var itemsCard = new InfoCard(new InfoCardState(
                I18N.Get("catalog.category.elements"),
                I18N.Get("catalog.category.elementsDescription", stories.Length)));
            for (var i = 0; i < stories.Length; i++)
            {
                var story = stories[i];
                var item = new NavigationItem(
                    new NavigationItemState(story.Title, story.Description),
                    () => SelectStory(story));
                item.AddToClassList("ee4v-ui-catalog-category-item");
                item.Row.DescriptionText.SetWhiteSpace(WhiteSpace.Normal);
                itemsCard.Body.Add(item);
            }

            body.contentContainer.Add(itemsCard);
            _contentHost.Add(page);
        }

        private static VisualElement CreatePage(
            string title,
            string description,
            out ScrollView body)
        {
            var page = new VisualElement();
            page.AddToClassList("ee4v-ui-catalog-page");

            var header = new VisualElement();
            header.AddToClassList("ee4v-ui-catalog-page__header");
            header.Add(UiTextFactory.Create(title, UiClassNames.CatalogPageTitle));
            header.Add(UiTextFactory.Create(
                description,
                UiClassNames.CatalogPageDescription));

            body = new ScrollView();
            body.AddToClassList("ee4v-ui-catalog-page__body");
            page.Add(header);
            page.Add(body);
            return page;
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

                if (node != null && !string.IsNullOrWhiteSpace(node.CategoryPath))
                {
                    SelectCategory(node.CategoryPath);
                    return;
                }
            }
        }

        private List<SearchableTreeItemData<NavigatorTreeNode>> BuildNavigatorTreeItems()
        {
            _navigatorStoryIds.Clear();
            _navigatorCategoryIds.Clear();

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
                            new NavigatorTreeNode(segments[segmentIndex], string.Empty, null, path));
                        folders.Add(path, folder);
                        currentChildren.Add(folder);
                        _navigatorCategoryIds[path] = folder.Id;
                    }

                    currentChildren = folder.Children;
                }

                var storyNode = new NavigatorTreeNodeBuilder(
                    nextId++,
                    new NavigatorTreeNode(story.Title, GetImplementationShortLabel(story.Implementation), story, null));
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

        private static bool IsStoryInCategory(StoryDefinition story, string categoryPath)
        {
            if (story == null || string.IsNullOrWhiteSpace(categoryPath))
            {
                return false;
            }

            var group = story.Group ?? string.Empty;
            return string.Equals(group, categoryPath, StringComparison.Ordinal) ||
                   group.StartsWith(categoryPath + "/", StringComparison.Ordinal);
        }

        private static string GetCategoryDescription(string categoryPath)
        {
            switch (categoryPath)
            {
                case "Reference":
                    return I18N.Get("catalog.category.referenceDescription");
                case "Inputs":
                    return I18N.Get("catalog.category.inputsDescription");
                case "Displays":
                    return I18N.Get("catalog.category.displaysDescription");
                case "Containers":
                    return I18N.Get("catalog.category.containersDescription");
                case "Collections":
                    return I18N.Get("catalog.category.collectionsDescription");
                case "Domain":
                    return I18N.Get("catalog.category.domainDescription");
            }

            var segments = (categoryPath ?? string.Empty)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var title = segments.Length > 0
                ? segments[segments.Length - 1]
                : categoryPath;
            if (segments.Length > 1 &&
                string.Equals(segments[0], "Domain", StringComparison.Ordinal))
            {
                if (segments.Length > 2)
                {
                    switch (segments[2])
                    {
                        case "Inputs":
                            return I18N.Get("catalog.category.inputsDescription");
                        case "Displays":
                            return I18N.Get("catalog.category.displaysDescription");
                        case "Containers":
                            return I18N.Get("catalog.category.containersDescription");
                        case "Collections":
                            return I18N.Get("catalog.category.collectionsDescription");
                    }
                }

                return I18N.Get(
                    "catalog.category.featureDescription",
                    segments[1]);
            }

            return I18N.Get("catalog.category.defaultDescription", title);
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
            if (story.UsageLocations.Count > 0)
            {
                card.Body.Add(CreateDetailItem(
                    I18N.Get("catalog.common.usageLocations"),
                    string.Join("\n", story.UsageLocations)));
            }
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
                IReadOnlyList<string> usageLocations,
                ComponentImplementationKind implementation,
                Action<VisualElement> build)
            {
                Id = id;
                Group = group;
                Title = title;
                Description = description;
                Details = details;
                Dependencies = dependencies ?? new string[0];
                UsageLocations = usageLocations ?? new string[0];
                Implementation = implementation;
                Build = build;
            }

            public string Id { get; }

            public string Group { get; }

            public string Title { get; }

            public string Description { get; }

            public string Details { get; }

            public IReadOnlyList<string> Dependencies { get; }

            public IReadOnlyList<string> UsageLocations { get; }

            public ComponentImplementationKind Implementation { get; }

            public Action<VisualElement> Build { get; }
        }

        internal sealed class StoryRegistration
        {
            public StoryRegistration(
                string id,
                string group,
                string title,
                string description,
                string details,
                IReadOnlyList<string> dependencies,
                ComponentImplementationKind implementation,
                Action<CatalogWindow, VisualElement> build,
                IReadOnlyList<string> usageLocations = null)
            {
                Id = id;
                Group = group;
                Title = title;
                Description = description;
                Details = details;
                Dependencies = dependencies ?? new string[0];
                UsageLocations = usageLocations ?? new string[0];
                Implementation = implementation;
                Build = build;
            }

            public string Id { get; }

            public string Group { get; }

            public string Title { get; }

            public string Description { get; }

            public string Details { get; }

            public IReadOnlyList<string> Dependencies { get; }

            public IReadOnlyList<string> UsageLocations { get; }

            public ComponentImplementationKind Implementation { get; }

            public Action<CatalogWindow, VisualElement> Build { get; }
        }

        internal sealed class CatalogRegistry
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

        internal interface ICatalogRegistrar
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

            var providerTypes = TypeCache
                .GetTypesDerivedFrom<IUiStoryProvider>()
                .Where(type =>
                    type != null &&
                    !type.IsAbstract &&
                    !type.IsInterface)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            var providers = new List<IUiStoryProvider>();
            for (var i = 0; i < providerTypes.Length; i++)
            {
                var provider = Activator.CreateInstance(
                    providerTypes[i],
                    true) as IUiStoryProvider;
                if (provider != null)
                {
                    providers.Add(provider);
                }
            }

            foreach (var provider in providers.OrderBy(item => item.Order))
            {
                var stories = provider.GetStories() ??
                    Array.Empty<UiStory>();
                for (var i = 0; i < stories.Count; i++)
                {
                    var story = stories[i];
                    if (story == null)
                    {
                        continue;
                    }

                    for (var styleIndex = 0;
                         styleIndex < story.StyleSheetPaths.Count;
                         styleIndex++)
                    {
                        registry.RegisterStyleSheet(
                            story.StyleSheetPaths[styleIndex]);
                    }

                    registry.RegisterStory(new StoryRegistration(
                        story.Id,
                        story.Group,
                        story.Title,
                        story.Description,
                        story.Details,
                        story.Dependencies,
                        story.Implementation ==
                            UiStoryImplementationKind.Imgui
                                ? ComponentImplementationKind.Imgui
                                : ComponentImplementationKind.UiToolkit,
                        (_, parent) => story.Build(parent),
                        story.UsageLocations));
                }
            }
        }

        internal static IReadOnlyList<StoryRegistration>
            GetRegisteredStoriesForTests()
        {
            EnsureCatalogRegistrations();
            return RegisteredStories.ToArray();
        }

        internal static IReadOnlyList<string>
            GetRegisteredStyleSheetPathsForTests()
        {
            EnsureCatalogRegistrations();
            return RegisteredStyleSheetPaths.ToArray();
        }

        private sealed class NavigatorTreeNode
        {
            public NavigatorTreeNode(
                string title,
                string implementationShortLabel,
                StoryDefinition story,
                string categoryPath)
            {
                Title = title ?? string.Empty;
                ImplementationShortLabel = implementationShortLabel ?? string.Empty;
                Story = story;
                CategoryPath = categoryPath ?? string.Empty;
                SearchText = BuildSearchText(story, CategoryPath, Title);
            }

            public string Title { get; }

            public string ImplementationShortLabel { get; }

            public StoryDefinition Story { get; }

            public string CategoryPath { get; }

            public string SearchText { get; }

            private static string BuildSearchText(
                StoryDefinition story,
                string categoryPath,
                string title)
            {
                if (story == null)
                {
                    return string.Join("\n", new[]
                    {
                        title ?? string.Empty,
                        categoryPath ?? string.Empty,
                        GetCategoryDescription(categoryPath)
                    });
                }

                return string.Join("\n", new[]
                {
                    story.Title ?? string.Empty,
                    story.Group ?? string.Empty,
                    story.Description ?? string.Empty,
                    story.Details ?? string.Empty,
                    string.Join(
                        "\n",
                        story.UsageLocations ?? Array.Empty<string>()),
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
                    if (i == 2 &&
                        string.Equals(leftRoot, "Domain", StringComparison.OrdinalIgnoreCase))
                    {
                        var leftRoleOrder = DomainRoleOrder.TryGetValue(
                            leftSegments[i],
                            out var leftRoleValue)
                                ? leftRoleValue
                                : int.MaxValue;
                        var rightRoleOrder = DomainRoleOrder.TryGetValue(
                            rightSegments[i],
                            out var rightRoleValue)
                                ? rightRoleValue
                                : int.MaxValue;
                        var roleCompare = leftRoleOrder.CompareTo(
                            rightRoleOrder);
                        if (roleCompare != 0)
                        {
                            return roleCompare;
                        }
                    }

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
