using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.UI;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class AssetManagerScreensCatalogStory
    {
        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 110;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                RegisterStyles(registry);
                RegisterComponent(
                    registry,
                    "asset-collection-icon-selector",
                    "AssetCollectionIconSelector",
                    BuildIconSelector);
                RegisterComponent(
                    registry,
                    "collection-navigation-list",
                    "CollectionNavigationList",
                    BuildCollectionNavigationList);
                RegisterComponent(
                    registry,
                    "searchable-file-tree",
                    "SearchableFileTree",
                    BuildSearchableFileTree);
                RegisterComponent(
                    registry,
                    "file-icon-catalog",
                    "FileIconCatalog",
                    BuildFileIconCatalog);
                RegisterComponent(
                    registry,
                    "tag-list-page",
                    "TagListPage",
                    BuildTagListPage);
                RegisterComponent(
                    registry,
                    "source-priority-setting",
                    "SourcePrioritySetting",
                    BuildSourcePrioritySetting);
                RegisterScreen(
                    registry,
                    "asset-manager-window",
                    "AssetManager Window",
                    BuildAssetManagerWindow);
                RegisterScreen(
                    registry,
                    "asset-manager-navigation-window",
                    "Navigation Window",
                    BuildNavigationWindow);
                RegisterScreen(
                    registry,
                    "asset-manager-main-view-window",
                    "Main View Window",
                    BuildMainViewWindow);
                RegisterScreen(
                    registry,
                    "asset-manager-infomation-window",
                    "Infomation Window",
                    BuildInfomationWindow);
                RegisterScreen(
                    registry,
                    "asset-manager-collection-creation-window",
                    "Collection Creation Window",
                    BuildCollectionCreationWindow);
            }

            private static void RegisterComponent(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string title,
                System.Action<CatalogWindow, VisualElement> build)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        "Domain/AssetManager/Components",
                        title,
                        CatalogCoveragePreview.ComponentDescription(title),
                        CatalogCoveragePreview.ComponentDetails(title),
                        null,
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        build));
            }

            private static void RegisterScreen(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string title,
                System.Action<CatalogWindow, VisualElement> build)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        "Domain/AssetManager/Screens",
                        title,
                        CatalogCoveragePreview.ScreenDescription(title),
                        CatalogCoveragePreview.ScreenDetails(title),
                        null,
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        build));
            }

            private static void RegisterStyles(
                CatalogWindow.CatalogRegistry registry)
            {
                var paths = new[]
                {
                    "Editor/UI/Components/Inputs/Button/ui-button.uss",
                    "Editor/UI/Components/Inputs/InputField/input-field.uss",
                    "Editor/UI/Components/Inputs/SearchField/search-field.uss",
                    "Editor/UI/Components/Inputs/NumericSlider/numeric-slider.uss",
                    "Editor/UI/Components/Inputs/ReorderableListField/reorderable-list-field.uss",
                    "Editor/UI/Components/Inputs/Selection/SingleSelectButtonGroup/single-select-button-group.uss",
                    "Editor/UI/Components/Inputs/Selection/SelectableItemGrid/selectable-item-grid.uss",
                    "Editor/UI/Components/Collections/ItemGrid/item-grid.uss",
                    "Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss",
                    "Editor/UI/Components/Content/Icon/icon.uss",
                    "Editor/UI/Components/Content/ErrorScreen/error-screen.uss",
                    "Editor/UI/Components/Content/ItemCard/item-card.uss",
                    "Editor/UI/Components/Content/ItemImage/item-image.uss",
                    "Editor/UI/Components/Content/ImageStack/image-stack.uss",
                    "Editor/UI/Components/Content/Interactive/ViewToggleTabs/view-toggle-tabs.uss",
                    "Editor/UI/Components/Layout/ThreePaneLayout/three-pane-layout.uss",
                    "Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss",
                    "Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss",
                    "Editor/AssetManager/UI/Panels/MainView/main-view.uss",
                    "Editor/AssetManager/UI/Panels/NavigationPanel/navigation-panel.uss",
                    "Editor/AssetManager/UI/Toolbar/MainToolbar/main-toolbar.uss",
                    "Editor/AssetManager/UI/Window/AssetManagerWindow/asset-manager-window.uss",
                    "Editor/AssetManager/UI/Window/collection-creation-window.uss",
                    "Editor/AssetManager/UI/Window/InfomationWindow/infomation-window.uss",
                    "Editor/AssetManager/UI/Window/MainViewWindow/main-view-window.uss",
                    "Editor/AssetManager/UI/Window/NavigationWindow/navigation-window.uss"
                };
                for (var i = 0; i < paths.Length; i++)
                {
                    registry.RegisterStyleSheet(paths[i]);
                }
            }
        }

        private static void BuildIconSelector(
            CatalogWindow window,
            VisualElement parent)
        {
            var surface = CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                180f);
            surface.Add(new AssetCollectionIconSelector(
                AssetCollectionIcon.Star));
        }

        private static void BuildCollectionNavigationList(
            CatalogWindow window,
            VisualElement parent)
        {
            var list = new CollectionNavigationList(_ => { });
            list.SetState(CreateCollections(), "collection|favorites");
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                280f).Add(list);
        }

        private static void BuildSearchableFileTree(
            CatalogWindow window,
            VisualElement parent)
        {
            var tree = new SearchableFileTree();
            tree.ClearTree();
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                300f).Add(tree);
        }

        private static void BuildFileIconCatalog(
            CatalogWindow window,
            VisualElement parent)
        {
            var definitions =
                FileIconCatalog.Definitions;
            if (definitions.Count == 0)
            {
                return;
            }

            var choices = new List<string>(
                definitions.Count);
            for (var i = 0; i < definitions.Count; i++)
            {
                choices.Add(
                    CreateFileIconChoiceLabel(
                        definitions[i]));
            }

            var selectedIndex = 0;
            var controls = window.CreatePlainControlsSection(
                parent,
                "拡張子・directory・group の定義を選択し、対応する icon を preview します。");
            var selector = new PopupField<string>(
                choices,
                selectedIndex);
            selector.label = string.Empty;
            controls.Content.Add(selector);

            var surface = CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                220f,
                true);
            var previewContent = new VisualElement();
            previewContent.style.flexGrow = 1f;
            previewContent.style.alignItems = Align.Center;
            previewContent.style.justifyContent =
                Justify.Center;
            var icon = new Icon();
            var idText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.CatalogDetailLabel);
            var targetText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.CatalogDetailValue);
            var sizeText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.CatalogDetailValue);
            previewContent.Add(icon);
            previewContent.Add(idText);
            previewContent.Add(targetText);
            previewContent.Add(sizeText);
            surface.Add(previewContent);

            System.Action refresh = () =>
            {
                var definition =
                    definitions[selectedIndex];
                icon.SetState(
                    definition.CreateArtworkIconState());
                idText.SetText(definition.Id);
                targetText.SetText(
                    CreateFileIconTargetText(
                        definition));
                sizeText.SetText(
                    definition.ArtworkIconSize + " px");
            };
            selector.RegisterValueChangedCallback(evt =>
            {
                var nextIndex =
                    choices.IndexOf(evt.newValue);
                if (nextIndex < 0)
                {
                    return;
                }

                selectedIndex = nextIndex;
                refresh();
            });

            refresh();
            CatalogWindow.FinalizeControlsSection(
                parent,
                controls);
        }

        private static string
            CreateFileIconChoiceLabel(
                FileIconDefinition definition)
        {
            return definition.Id + " · " +
                   CreateFileIconTargetText(
                       definition);
        }

        private static string
            CreateFileIconTargetText(
                FileIconDefinition definition)
        {
            return definition.Extensions.Count > 0
                ? string.Join(
                    ", ",
                    definition.Extensions)
                : definition.Kind.ToString();
        }

        private static void BuildTagListPage(
            CatalogWindow window,
            VisualElement parent)
        {
            var view = new TagListPage();
            view.SetTags(new[]
            {
                new AssetTag
                {
                    Id = "avatar",
                    Name = CatalogCoveragePreview.SampleTagOne
                },
                new AssetTag
                {
                    Id = "environment",
                    Name = CatalogCoveragePreview.SampleTagTwo
                }
            });
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                240f).Add(view);
        }

        private static void BuildSourcePrioritySetting(
            CatalogWindow window,
            VisualElement parent)
        {
            var field = SourcePrioritySettingDrawer.CreateField(
                new SettingDrawerContext<string>(
                    CatalogCoveragePreview.SampleDescription,
                    "ee4v,eagle,blm",
                    string.Empty,
                    _ => { }));
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                190f).Add(field);
        }

        private static void BuildAssetManagerWindow(
            CatalogWindow window,
            VisualElement parent)
        {
            var host = new MainViewHost();
            var layout = new ThreePaneLayout(
                new ThreePaneLayoutState(
                    210f,
                    260f,
                    160f,
                    300f,
                    320f,
                    220f,
                    360f));
            var info = new InfomationPanel();
            host.NavigationPanel.SetCollections(
                CreateCollections(),
                "collection|favorites");
            layout.LeftPaneContent.Add(host.NavigationPanel);
            layout.MainToolbarContent.Add(host.Toolbar);
            layout.MainContent.Add(host.MainView);
            layout.RightPaneContent.Add(info);
            host.MainView.SetExternalFileDropSurface(
                layout,
                layout.MainOverlayContent);
            layout.RegisterCallback<DetachFromPanelEvent>(_ =>
                host.Dispose());
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                480f).Add(layout);
        }

        private static void BuildNavigationWindow(
            CatalogWindow window,
            VisualElement parent)
        {
            var panel = new NavigationPanel();
            panel.SetCollections(
                CreateCollections(),
                "collection|favorites");
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                380f).Add(panel);
        }

        private static void BuildMainViewWindow(
            CatalogWindow window,
            VisualElement parent)
        {
            var host = new MainViewHost();
            var body = new VisualElement();
            body.AddToClassList(
                "ee4v-asset-manager-window__main-view-window-body");
            host.MainView.AddToClassList(
                "ee4v-asset-manager-window__main-view-window-content");
            body.Add(host.Toolbar);
            body.Add(host.MainView);
            host.MainView.SetExternalFileDropSurface(body);
            body.RegisterCallback<DetachFromPanelEvent>(_ =>
                host.Dispose());
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                420f).Add(body);
        }

        private static void BuildInfomationWindow(
            CatalogWindow window,
            VisualElement parent)
        {
            var body = new VisualElement();
            body.AddToClassList(
                "ee4v-asset-manager-window__standalone-panel-body");
            body.Add(new InfomationPanel());
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                420f).Add(body);
        }

        private static void BuildCollectionCreationWindow(
            CatalogWindow window,
            VisualElement parent)
        {
            var popup = new VisualElement();
            popup.AddToClassList(
                "ee4v-collection-creation-window");
            popup.AddToClassList(UiClassNames.PopupSurface);

            var form = new ScrollView(ScrollViewMode.Vertical);
            form.AddToClassList(
                "ee4v-collection-creation-window__form");
            form.Add(UiTextFactory.Create(
                CatalogCoveragePreview.SampleCollection,
                UiClassNames.SectionTitle,
                "ee4v-collection-creation-window__title"));

            var nameField = new VisualElement();
            nameField.AddToClassList(
                "ee4v-collection-creation-window__field");
            nameField.Add(UiTextFactory.Create(
                CatalogCoveragePreview.SampleTitle,
                UiClassNames.FormLabel,
                "ee4v-collection-creation-window__label"));
            nameField.Add(new InputField(new InputFieldState(
                placeholder:
                    CatalogCoveragePreview.SampleDescription)));
            form.Add(nameField);

            var iconField = new VisualElement();
            iconField.AddToClassList(
                "ee4v-collection-creation-window__field");
            iconField.Add(new AssetCollectionIconSelector(
                AssetCollectionIcon.Folder));
            form.Add(iconField);
            popup.Add(new PopupLayout(
                form,
                new PopupActionState(
                    CatalogCoveragePreview.SampleClearSelection,
                    null),
                new PopupActionState(
                    CatalogCoveragePreview.SampleCreateFormat
                        .Replace(
                            "{0}",
                            CatalogCoveragePreview.SampleCollection),
                    null)));
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                430f).Add(popup);
        }

        private static AssetCollection[] CreateCollections()
        {
            return new[]
            {
                new AssetCollection
                {
                    Id = "favorites",
                    Name = CatalogCoveragePreview.SampleCollection,
                    Icon = AssetCollectionIcon.Star,
                    SortOrder = 0
                },
                new AssetCollection
                {
                    Id = "smart",
                    Name = CatalogCoveragePreview.SampleSmartCollection,
                    Icon = AssetCollectionIcon.Search,
                    IsSmartCollection = true,
                    SortOrder = 1
                }
            };
        }
    }
}
