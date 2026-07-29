using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class NavigationTests
    {
        [Test]
        public void Catalog_ContainsRequestedNavigationItemsInOrder()
        {
            Assert.That(
                AssetManagerNavigationCatalog.Items.Select(item => item.Id).ToArray(),
                Is.EqualTo(new[]
                {
                    "all-assets",
                    "booth-items",
                    "uncategorized",
                    "tags"
                }));
        }

        [Test]
        public void Catalog_UsesFluentPngIcons()
        {
            var items = AssetManagerNavigationCatalog.Items;

            Assert.That(
                items.Select(item =>
                        item.IconState.SourceKind)
                    .ToArray(),
                Is.All.EqualTo(
                    UiIconSourceKind.Fluent));
            Assert.That(
                items.Select(item =>
                        item.IconState.FluentIcon)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    UiFluentIcon.Library,
                    UiFluentIcon.BoxMultiple,
                    UiFluentIcon.Folder,
                    UiFluentIcon.Tag
                }));
        }

        [Test]
        public void CreateQuery_UsesBoothInformationInsteadOfSourceType()
        {
            var query = MainViewController.CreateQuery(
                new MainViewRequest("booth-items"));

            Assert.That(query.HasBoothInformation, Is.True);
            Assert.That(query.SourceTypes, Is.Null);
        }

        [Test]
        public void CreateQuery_RequestsOnlyUncategorizedItems()
        {
            var query = MainViewController.CreateQuery(
                new MainViewRequest("uncategorized"));

            Assert.That(query.UncategorizedOnly, Is.True);
        }

        [Test]
        public void CreateQuery_UsesCollectionIdForCollectionView()
        {
            var query = MainViewController.CreateQuery(
                new MainViewRequest(
                    AssetManagerCollectionViewId.Encode("collection-1")));

            Assert.That(query.CollectionId, Is.EqualTo("collection-1"));
        }

        [Test]
        public void PendingCollectionNavigation_IsPreservedUntilCollectionsLoad()
        {
            var viewId =
                AssetManagerCollectionViewId.Encode(
                    "new-smart-collection");
            var pendingItem =
                MainViewController
                    .CreateCollectionNavigationItem(
                        viewId,
                        "new-smart-collection");

            Assert.That(
                MainViewController.ResolveNavigationItemId(
                    viewId,
                    collectionExists: false),
                Is.EqualTo(viewId));
            Assert.That(pendingItem.Id, Is.EqualTo(viewId));
            Assert.That(
                MainViewController.CreateQuery(
                        new MainViewRequest(pendingItem.Id))
                    .CollectionId,
                Is.EqualTo("new-smart-collection"));
            Assert.That(
                MainViewController.ResolveNavigationItemId(
                    "missing-fixed-item",
                    collectionExists: false),
                Is.EqualTo(
                    AssetManagerNavigationCatalog.DefaultItemId));
        }

        [Test]
        public void MainViewController_CachesOnlyFileAndGroupChildren()
        {
            var itemListCaches = typeof(MainViewController)
                .GetFields(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .Where(field =>
                    field.FieldType ==
                    typeof(Dictionary<
                        string,
                        AssetItemGridList>))
                .Select(field => field.Name)
                .ToArray();

            Assert.That(
                itemListCaches,
                Is.EqualTo(new[] { "_childItemCache" }));
        }

        [Test]
        public void SmartCollectionRuleChange_InvalidatesAffectedViews()
        {
            var change = new AssetManagerChange(
                AssetManagerChangeKind.SmartCollectionRule,
                "smart");

            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "smart")),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        "uncategorized"),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "other")),
                Is.False);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        "all-assets"),
                Is.False);
        }

        [Test]
        public void ItemCollectionChange_InvalidatesOnlyAffectedViews()
        {
            var change = new AssetManagerChange(
                AssetManagerChangeKind.ItemCollections,
                relatedId: "target");

            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "target")),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        "uncategorized"),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        "all-assets"),
                Is.False);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "source")),
                Is.False);
        }

        [Test]
        public void GetChildCollections_ReturnsDirectChildrenInDisplayOrder()
        {
            var children =
                MainViewController.GetChildCollections(
                    new[]
                    {
                        new AssetCollection
                        {
                            Id = "second",
                            Name = "Second",
                            ParentCollectionId = "parent",
                            SortOrder = 1
                        },
                        new AssetCollection
                        {
                            Id = "grandchild",
                            Name = "Grandchild",
                            ParentCollectionId = "second",
                            SortOrder = 0
                        },
                        new AssetCollection
                        {
                            Id = "first",
                            Name = "First",
                            ParentCollectionId = "parent",
                            SortOrder = 0
                        },
                        new AssetCollection
                        {
                            Id = "other",
                            Name = "Other",
                            ParentCollectionId = "other-parent",
                            SortOrder = 0
                        }
                    },
                    "parent",
                    "s");

            Assert.That(
                children.Select(collection => collection.Id),
                Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void CreateHistoryViewPath_ReturnsCollectionAncestors()
        {
            var path =
                MainViewController.CreateHistoryViewPath(
                    new[]
                    {
                        new AssetCollection
                        {
                            Id = "hoge",
                            Name = "hoge"
                        },
                        new AssetCollection
                        {
                            Id = "fuga",
                            Name = "fuga",
                            ParentCollectionId = "hoge"
                        },
                        new AssetCollection
                        {
                            Id = "child",
                            Name = "child",
                            ParentCollectionId = "fuga"
                        }
                    },
                    AssetManagerCollectionViewId.Encode("child"),
                    "child");

            Assert.That(
                path.Select(item => item.Id),
                Is.EqualTo(new[]
                {
                    AssetManagerCollectionViewId.Encode("hoge"),
                    AssetManagerCollectionViewId.Encode("fuga"),
                    AssetManagerCollectionViewId.Encode("child")
                }));
            Assert.That(
                path.Select(item => item.Label),
                Is.EqualTo(new[] { "hoge", "fuga", "child" }));
        }

        [Test]
        public void CollectionArtwork_UsesImageStackOrTypeIconByContentCount()
        {
            var firstPreview = new ItemCardState(
                "item-1",
                "First",
                new ItemImageState(new byte[] { 1, 2, 3 }));
            var secondPreview = new ItemCardState(
                "item-2",
                "Second",
                new ItemImageState(new byte[] { 4, 5, 6 }));
            var typeIcon =
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Folder);

            var emptyArtwork =
                MainViewController.CreateCollectionArtworkState(
                    null,
                    typeIcon);
            var singleArtwork =
                MainViewController.CreateCollectionArtworkState(
                    new[] { firstPreview },
                    typeIcon);
            var stackedArtwork =
                MainViewController.CreateCollectionArtworkState(
                    new[]
                    {
                        firstPreview,
                        secondPreview
                    },
                    typeIcon);

            Assert.That(
                emptyArtwork.IconState,
                Is.SameAs(typeIcon));
            Assert.That(
                emptyArtwork.StackStates,
                Is.Empty);
            Assert.That(
                singleArtwork.IconState,
                Is.Null);
            Assert.That(
                singleArtwork.StackStates,
                Is.EqualTo(new[]
                {
                    firstPreview
                }));
            Assert.That(
                stackedArtwork.StackStates,
                Is.EqualTo(new[]
                {
                    firstPreview,
                    secondPreview
                }));
            Assert.That(stackedArtwork.IconState, Is.Null);
        }

        [Test]
        public void FileArtwork_AlwaysUsesExtensionIcon()
        {
            var archive =
                MainViewController.CreateFileArtworkState(
                    "zip");
            var image =
                MainViewController.CreateFileArtworkState(
                    "png");

            Assert.That(
                archive.IconState.SourceKind,
                Is.EqualTo(UiIconSourceKind.Fluent));
            Assert.That(
                archive.IconState.FluentIcon,
                Is.EqualTo(UiFluentIcon.FolderZip));
            Assert.That(
                archive.IconState.Size,
                Is.EqualTo(88f));
            Assert.That(
                archive.StackStates,
                Is.Empty);
            Assert.That(
                image.IconState.SourceKind,
                Is.EqualTo(UiIconSourceKind.Fluent));
            Assert.That(
                image.IconState.FluentIcon,
                Is.EqualTo(UiFluentIcon.Image));
            Assert.That(
                image.StackStates,
                Is.Empty);
        }

        [Test]
        public void GroupArtwork_AlwaysUsesTypeIconWithoutImageStack()
        {
            var variant =
                MainViewController.CreateGroupArtworkState(
                    AssetItemGridNodeKind.VariantGroup);
            var version =
                MainViewController.CreateGroupArtworkState(
                    AssetItemGridNodeKind.VersionGroup);

            Assert.That(
                variant.IconState.FluentIcon,
                Is.EqualTo(
                    UiFluentIcon.FolderBranchFork));
            Assert.That(
                variant.StackStates,
                Is.Empty);
            Assert.That(
                version.IconState.FluentIcon,
                Is.EqualTo(
                    UiFluentIcon.FolderLayer));
            Assert.That(
                version.StackStates,
                Is.Empty);
        }

        [Test]
        public void GroupTypeIcons_UseDistinctFluentIcons()
        {
            var variant =
                MainViewController.CreateGroupTypeIcon(
                    AssetItemGridNodeKind.VariantGroup);
            var version =
                MainViewController.CreateGroupTypeIcon(
                    AssetItemGridNodeKind.VersionGroup);

            Assert.That(
                variant.SourceKind,
                Is.EqualTo(UiIconSourceKind.Fluent));
            Assert.That(
                variant.FluentIcon,
                Is.EqualTo(
                    UiFluentIcon.FolderBranchFork));
            Assert.That(
                variant.Size,
                Is.EqualTo(88f));
            Assert.That(
                version.SourceKind,
                Is.EqualTo(UiIconSourceKind.Fluent));
            Assert.That(
                version.FluentIcon,
                Is.EqualTo(
                    UiFluentIcon.FolderLayer));
            Assert.That(
                version.Size,
                Is.EqualTo(88f));
        }

        [Test]
        public void CollectionNavigationList_RendersRegularAndSmartInOneTree()
        {
            var list = new CollectionNavigationList(null);

            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "regular",
                    Name = "Regular",
                    Icon = AssetCollectionIcon.Folder
                },
                new AssetCollection
                {
                    Id = "smart",
                    Name = "Smart",
                    Icon = AssetCollectionIcon.Folder,
                    IsSmartCollection = true
                }
            }, string.Empty);

            var buttons = list.Query<Button>(
                    className:
                    "ee4v-asset-manager-collection-list__button")
                .ToList();
            Assert.That(buttons.Count, Is.EqualTo(2));
            Assert.That(
                buttons.All(button =>
                    button.GetClasses().Contains(
                        "ee4v-asset-manager-collection-list__button")),
                Is.True);
        }

        [Test]
        public void CollectionNavigationList_UsesLargerRowsAndDepthLines()
        {
            var list = new CollectionNavigationList(null);

            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "parent",
                    Name = "Parent"
                },
                new AssetCollection
                {
                    Id = "child",
                    Name = "Child",
                    ParentCollectionId = "parent"
                },
                new AssetCollection
                {
                    Id = "grandchild",
                    Name = "Grandchild",
                    ParentCollectionId = "child"
                }
            }, string.Empty);

            var buttons = list.Query<UiButton>(
                    className:
                    "ee4v-asset-manager-collection-list__button")
                .ToList();
            Assert.That(
                buttons.All(button =>
                    button.GetClasses().Contains(
                        "ee4v-ui-button--compact")),
                Is.True);
            Assert.That(
                buttons.All(button =>
                    button.LabelElement.GetClasses().Contains(
                        UiClassNames.CollectionNavigationLabel)),
                Is.True);
            Assert.That(
                buttons.All(button =>
                    button.LabelElement.GetType().Name ==
                    "ImguiUiTextElement"),
                Is.True);
            Assert.That(
                list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__depth-line--current")
                    .ToList().Count,
                Is.EqualTo(2));
            Assert.That(
                list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__depth-line--children")
                    .ToList(),
                Is.Empty);
            Assert.That(
                list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__depth-branch")
                    .ToList().Count,
                Is.EqualTo(1));
            Assert.That(
                list.Query<Foldout>(
                        className:
                        "ee4v-asset-manager-collection-list__disclosure--toggle")
                    .ToList().Count,
                Is.EqualTo(2));
            Assert.That(
                CollectionNavigationList.GetDepthLineLeft(1),
                Is.EqualTo(UiSizeTokens.Size16 * 0.5f));
            Assert.That(
                CollectionNavigationList.GetDepthLineLeft(2),
                Is.EqualTo(
                    UiSizeTokens.Size16 * 0.5f +
                    UiSpacingTokens.Xl));
            var childRow = list.Query<VisualElement>(
                    className:
                    "ee4v-asset-manager-collection-list__row")
                .ToList()[1];
            var childElements = childRow.Children().ToList();
            var depthLine = childRow.Q<VisualElement>(
                className:
                "ee4v-asset-manager-collection-list__depth-line--current");
            var disclosure = childRow.Q<VisualElement>(
                className:
                "ee4v-asset-manager-collection-list__disclosure");
            Assert.That(
                childElements.IndexOf(depthLine),
                Is.LessThan(childElements.IndexOf(disclosure)));
        }

        [Test]
        public void CollectionNavigationList_TogglesIndividualFolders()
        {
            var list = new CollectionNavigationList(null);
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "parent",
                    Name = "Parent"
                },
                new AssetCollection
                {
                    Id = "child",
                    Name = "Child",
                    ParentCollectionId = "parent"
                }
            }, string.Empty);

            Assert.That(
                list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__row")
                    .ToList().Count,
                Is.EqualTo(2));
            var disclosure = list.Query<Foldout>(
                    className:
                    "ee4v-asset-manager-collection-list__disclosure")
                .ToList()
                .Single();

            disclosure.value = false;

            Assert.That(
                list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__row")
                    .ToList().Count,
                Is.EqualTo(1));

            list.Query<Foldout>(
                    className:
                    "ee4v-asset-manager-collection-list__disclosure")
                .ToList()
                .Single()
                .value = true;

            Assert.That(
                list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__row")
                    .ToList().Count,
                Is.EqualTo(2));
        }

        [Test]
        public void CollectionNavigationList_UsesPersistedSiblingOrder()
        {
            var list = new CollectionNavigationList(null);
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "second",
                    Name = "A",
                    SortOrder = 1
                },
                new AssetCollection
                {
                    Id = "first",
                    Name = "Z",
                    SortOrder = 0
                }
            }, string.Empty);

            Assert.That(
                list.Query<UiButton>(
                        className:
                        "ee4v-asset-manager-collection-list__button")
                    .ToList()
                    .Select(button => button.LabelElement.Text)
                    .ToArray(),
                Is.EqualTo(new[] { "Z", "A" }));
        }

        [Test]
        public void NavigationPanel_UnifiesRegularAndSmartCollections()
        {
            var panel = new NavigationPanel();
            panel.SetCollections(new[]
            {
                new AssetCollection
                {
                    Id = "regular",
                    Name = "Regular"
                },
                new AssetCollection
                {
                    Id = "smart",
                    Name = "Smart",
                    IsSmartCollection = true
                }
            }, string.Empty);

            var lists = panel.Query<CollectionNavigationList>()
                .ToList();
            Assert.That(lists.Count, Is.EqualTo(1));
            Assert.That(
                lists[0].Query<Button>(
                        className:
                        "ee4v-asset-manager-collection-list__button")
                    .ToList().Count,
                Is.EqualTo(2));
        }

        [Test]
        public void NavigationPanel_RemovesCollectionFoldout()
        {
            var panel = new NavigationPanel();

            Assert.That(
                panel.Query<Foldout>(
                        className:
                        "ee4v-asset-manager-panel__collection-section")
                    .ToList(),
                Is.Empty);
            Assert.That(
                panel.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-panel__collection-section")
                    .ToList().Count,
                Is.EqualTo(1));
        }

        [Test]
        public void NavigationPanel_CollectionHeaderUsesSingleAddButton()
        {
            var panel = new NavigationPanel();
            var section = panel.Query<VisualElement>(
                    className:
                    "ee4v-asset-manager-panel__collection-section")
                .ToList()
                .Single();

            Assert.That(
                section.Query<UiButton>(
                        className:
                        "ee4v-asset-manager-panel__header-action")
                    .ToList().Count,
                Is.EqualTo(1));
        }

        [Test]
        public void NavigationPanel_HeaderActionsUseFluentPngIcons()
        {
            var panel = new NavigationPanel();
            var buttons = panel.Query<UiButton>(
                    className:
                    "ee4v-asset-manager-panel__header-action")
                .ToList();
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.ArrowClockwise,
                out var refreshTexture);
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Add,
                out var addTexture);

            Assert.That(buttons.Count, Is.EqualTo(2));
            Assert.That(
                buttons.Select(button =>
                        button.IconElement.Q<Image>().image)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    refreshTexture,
                    addTexture
                }));
        }

        [Test]
        public void MainToolbar_ActionsUseFluentPngIcons()
        {
            var toolbar = new MainToolbar();
            var buttons = toolbar.Query<UiButton>(
                    className:
                    "ee4v-ui-main-toolbar__icon-button")
                .ToList();
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Filter,
                out var filterTexture);
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Options,
                out var sortTexture);

            Assert.That(buttons.Count, Is.EqualTo(2));
            Assert.That(
                buttons.Select(button =>
                        button.IconElement.Q<Image>().image)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    filterTexture,
                    sortTexture
                }));
        }

        [Test]
        public void NavigationPanel_ReloadButtonDoesNotRetainFocus()
        {
            var panel = new NavigationPanel();
            var picker = panel.Query<VisualElement>(
                    className:
                    "ee4v-asset-manager-panel__navigation-picker")
                .ToList()
                .Single();
            var reloadButton = picker.Query<UiButton>(
                    className:
                    "ee4v-asset-manager-panel__header-action")
                .ToList()
                .Single();

            Assert.That(reloadButton.focusable, Is.False);
        }

        [Test]
        public void CollectionCreationMenu_ChoosesRegularOrSmart()
        {
            var anchor = new VisualElement();
            var regularRequested = false;
            var smartRequested = false;
            var state =
                NavigationPanel.CreateCollectionCreationMenuState(
                    anchor,
                    _ => regularRequested = true,
                    _ => smartRequested = true);

            Assert.That(
                state.Items.Select(item => item.Id).ToArray(),
                Is.EqualTo(new[]
                {
                    "create-collection",
                    "create-smart-collection"
                }));

            state.Items[0].Action();
            Assert.That(regularRequested, Is.True);
            Assert.That(smartRequested, Is.False);

            state.Items[1].Action();
            Assert.That(smartRequested, Is.True);
        }

        [Test]
        public void CollectionContextMenu_OffersConditionsOnlyForSmartCollection()
        {
            var anchor = new VisualElement();
            AssetCollection renamed = null;
            AssetCollection edited = null;
            AssetCollection deleted = null;
            var pointerPosition = new Vector2(80f, 96f);
            var renamedPosition = Vector2.zero;
            var editedPosition = Vector2.zero;
            var regular = new AssetCollection
            {
                Id = "regular",
                Name = "Regular"
            };
            var smart = new AssetCollection
            {
                Id = "smart",
                Name = "Smart",
                IsSmartCollection = true
            };

            var regularState =
                NavigationPanel.CreateCollectionContextMenuState(
                    regular,
                    new[] { regular },
                    anchor,
                    pointerPosition,
                    (collection, _, position) =>
                    {
                        renamed = collection;
                        renamedPosition = position;
                    },
                    (collection, _, position) =>
                    {
                        edited = collection;
                        editedPosition = position;
                    },
                    collections =>
                        deleted = collections.Single());
            Assert.That(
                regularState.Items
                    .Where(item =>
                        item.Kind == ContextMenuItemKind.Action)
                    .Select(item => item.Id)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    "rename-collection",
                    "delete-collection"
                }));

            var smartState =
                NavigationPanel.CreateCollectionContextMenuState(
                    smart,
                    new[] { smart },
                    anchor,
                    pointerPosition,
                    (collection, _, position) =>
                    {
                        renamed = collection;
                        renamedPosition = position;
                    },
                    (collection, _, position) =>
                    {
                        edited = collection;
                        editedPosition = position;
                    },
                    collections =>
                        deleted = collections.Single());
            Assert.That(
                smartState.Items
                    .Where(item =>
                        item.Kind == ContextMenuItemKind.Action)
                    .Select(item => item.Id)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    "rename-collection",
                    "edit-smart-collection",
                    "delete-collection"
                }));

            smartState.Items[0].Action();
            smartState.Items[1].Action();
            smartState.Items[3].Action();
            Assert.That(renamed, Is.SameAs(smart));
            Assert.That(edited, Is.SameAs(smart));
            Assert.That(deleted, Is.SameAs(smart));
            Assert.That(renamedPosition, Is.EqualTo(pointerPosition));
            Assert.That(editedPosition, Is.EqualTo(pointerPosition));
        }

        [Test]
        public void CollectionContextMenu_MultipleSelectionOffersOnlyDelete()
        {
            var first = new AssetCollection
            {
                Id = "first",
                Name = "First"
            };
            var second = new AssetCollection
            {
                Id = "second",
                Name = "Second",
                IsSmartCollection = true
            };
            IReadOnlyList<AssetCollection> deleted = null;

            var state =
                NavigationPanel.CreateCollectionContextMenuState(
                    first,
                    new[] { first, second },
                    new VisualElement(),
                    Vector2.zero,
                    null,
                    null,
                    collections => deleted = collections);

            Assert.That(
                state.Items
                    .Where(item =>
                        item.Kind == ContextMenuItemKind.Action)
                    .Select(item => item.Id)
                    .ToArray(),
                Is.EqualTo(new[] { "delete-collections" }));

            state.Items.Single().Action();
            Assert.That(
                deleted.Select(collection => collection.Id)
                    .ToArray(),
                Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void CollectionEditPopup_UsesContextMenuPointerPosition()
        {
            Assert.That(
                CollectionCreationWindow.CalculatePointerScreenPosition(
                    new Vector2(100f, 200f),
                    new Vector2(10f, 20f),
                    new Vector2(40f, 60f)),
                Is.EqualTo(new Vector2(130f, 240f)));
        }

        [UnityTest]
        public IEnumerator CollectionNavigationList_RightClickPreservesSelectionAndRequestsMenu()
        {
            var window =
                ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                string selectedViewId = null;
                AssetCollection requestedCollection = null;
                var list = new CollectionNavigationList(
                    viewId => selectedViewId = viewId,
                    null,
                    (collection, _, __, ___) =>
                        requestedCollection = collection);
                list.SetState(new[]
                {
                    new AssetCollection
                    {
                        Id = "selected",
                        Name = "Selected",
                        SortOrder = 0
                    },
                    new AssetCollection
                    {
                        Id = "context-menu",
                        Name = "Context Menu",
                        SortOrder = 1
                    }
                }, AssetManagerCollectionViewId.Encode("selected"));
                window.rootVisualElement.Add(list);
                window.Show();
                yield return null;

                var rows = list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__row")
                    .ToList();
                using (var pointerDown =
                       PointerDownEvent.GetPooled(new Event
                       {
                           type = EventType.MouseDown,
                           button =
                               (int)MouseButton.RightMouse,
                           mousePosition =
                               rows[1].worldBound.center
                       }))
                {
                    pointerDown.target = rows[1];
                    rows[1].SendEvent(pointerDown);
                }

                yield return null;
                Assert.That(
                    list.SelectedCollectionIds,
                    Is.EqualTo(new[] { "selected" }));
                Assert.That(selectedViewId, Is.Null);
                Assert.That(
                    requestedCollection.Id,
                    Is.EqualTo("context-menu"));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void CollectionNavigationList_DragMoveRejectsCyclesAndSmartParents()
        {
            string movedId = null;
            string parentId = null;
            var list = new CollectionNavigationList(
                null,
                (collectionIds, nextParentId, _) =>
                {
                    movedId = collectionIds.Single();
                    parentId = nextParentId;
                });
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "regular",
                    Name = "Regular"
                },
                new AssetCollection
                {
                    Id = "child",
                    Name = "Child",
                    ParentCollectionId = "regular"
                },
                new AssetCollection
                {
                    Id = "smart",
                    Name = "Smart",
                    IsSmartCollection = true
                }
            }, string.Empty);

            Assert.That(
                list.CanMoveCollection("regular", "child"),
                Is.False);
            Assert.That(
                list.TryRequestMove("smart", "regular"),
                Is.True);
            Assert.That(movedId, Is.EqualTo("smart"));
            Assert.That(parentId, Is.EqualTo("regular"));
            Assert.That(
                list.TryRequestMove("regular", "smart"),
                Is.False);
        }

        [Test]
        public void CollectionNavigationList_ItemDropAcceptsOnlyRegularCollection()
        {
            IReadOnlyList<string> droppedItemIds = null;
            string droppedCollectionId = null;
            var list = new CollectionNavigationList(
                null,
                itemsDropped: (itemIds, collectionId) =>
                {
                    droppedItemIds = itemIds;
                    droppedCollectionId = collectionId;
                });
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "regular",
                    Name = "Regular"
                },
                new AssetCollection
                {
                    Id = "smart",
                    Name = "Smart",
                    IsSmartCollection = true
                }
            }, string.Empty);

            Assert.That(
                list.TryRequestItemDrop(
                    new[] { "item-1", "item-2", "item-1" },
                    "regular"),
                Is.True);
            Assert.That(
                droppedItemIds,
                Is.EqualTo(new[] { "item-1", "item-2" }));
            Assert.That(
                droppedCollectionId,
                Is.EqualTo("regular"));
            Assert.That(
                list.TryRequestItemDrop(
                    new[] { "item-1" },
                    "smart"),
                Is.False);
            Assert.That(
                list.TryRequestItemDrop(
                    new[] { "item-1" },
                    "missing"),
                Is.False);
        }

        [Test]
        public void AssetItemDragAndDrop_UsesParentItemForFileAndGroupCards()
        {
            var itemIds =
                AssetItemDragAndDrop.GetAssetItemIds(
                    new[]
                    {
                        new ItemCardState(
                            "item",
                            "Item",
                            new ItemImageState()),
                        new ItemCardState(
                            "file:1",
                            "File",
                            new ItemImageState(),
                            null,
                            "parent"),
                        new ItemCardState(
                            "group:1",
                            "Group",
                            new ItemImageState(),
                            null,
                            "parent"),
                        new ItemCardState(
                            AssetItemGridNodeKey.Encode(
                                AssetItemGridNodeKind.Collection,
                                "collection"),
                            "Collection",
                            new ItemImageState())
                    });

            Assert.That(
                itemIds,
                Is.EqualTo(new[] { "item", "parent" }));
        }

        [Test]
        public void CollectionNavigationList_CanMoveChildBackToRoot()
        {
            string parentId = "not-called";
            var list = new CollectionNavigationList(
                null,
                (_, nextParentId, __) =>
                    parentId = nextParentId);
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "parent",
                    Name = "Parent"
                },
                new AssetCollection
                {
                    Id = "child",
                    Name = "Child",
                    ParentCollectionId = "parent"
                }
            }, string.Empty);

            Assert.That(
                list.TryRequestMove("child", null),
                Is.True);
            Assert.That(parentId, Is.Null);
        }

        [Test]
        public void CollectionNavigationList_CanReorderWithinParent()
        {
            string movedId = null;
            string parentId = "not-called";
            var siblingIndex = -1;
            var list = new CollectionNavigationList(
                null,
                (nextMovedIds, nextParentId, nextSiblingIndex) =>
                {
                    movedId = nextMovedIds.Single();
                    parentId = nextParentId;
                    siblingIndex = nextSiblingIndex;
                });
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "first",
                    Name = "First",
                    SortOrder = 0
                },
                new AssetCollection
                {
                    Id = "second",
                    Name = "Second",
                    SortOrder = 1
                }
            }, string.Empty);

            Assert.That(
                list.TryRequestMove("second", null, 0),
                Is.True);
            Assert.That(movedId, Is.EqualTo("second"));
            Assert.That(parentId, Is.Null);
            Assert.That(siblingIndex, Is.Zero);
            Assert.That(
                list.TryRequestMove("first", null, 0),
                Is.False);
        }

        [Test]
        public void CollectionNavigationList_CtrlTogglesAndShiftSelectsVisibleRange()
        {
            var list = new CollectionNavigationList(null);
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "first",
                    Name = "First",
                    SortOrder = 0
                },
                new AssetCollection
                {
                    Id = "second",
                    Name = "Second",
                    SortOrder = 1
                },
                new AssetCollection
                {
                    Id = "third",
                    Name = "Third",
                    SortOrder = 2
                }
            }, string.Empty);

            list.SelectCollection(
                "first",
                toggle: false,
                range: false);
            list.SelectCollection(
                "third",
                toggle: true,
                range: false);

            Assert.That(
                list.SelectedCollectionIds,
                Is.EquivalentTo(new[] { "first", "third" }));

            list.SelectCollection(
                "third",
                toggle: true,
                range: false);

            Assert.That(
                list.SelectedCollectionIds,
                Is.EquivalentTo(new[] { "first" }));

            list.SelectCollection(
                "first",
                toggle: true,
                range: false);

            Assert.That(
                list.SelectedCollectionIds,
                Is.Empty);

            list.SelectCollection(
                "first",
                toggle: false,
                range: false);
            list.SelectCollection(
                "third",
                toggle: false,
                range: true);

            Assert.That(
                list.SelectedCollectionIds,
                Is.EquivalentTo(
                    new[] { "first", "second", "third" }));
        }

        [Test]
        public void CollectionNavigationList_ClearSelectionSurvivesRefresh()
        {
            var collections = new[]
            {
                new AssetCollection
                {
                    Id = "first",
                    Name = "First"
                }
            };
            var list = new CollectionNavigationList(null);
            var selectedViewId =
                AssetManagerCollectionViewId.Encode("first");
            list.SetState(collections, selectedViewId);

            list.ClearSelection();
            list.SetState(collections, selectedViewId);

            Assert.That(
                list.SelectedCollectionIds,
                Is.Empty);
        }

        [UnityTest]
        public IEnumerator CollectionNavigationList_BackgroundAndEscapeClearSelection()
        {
            var window =
                ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                var list = new CollectionNavigationList(null);
                list.style.height = 100f;
                list.SetState(new[]
                {
                    new AssetCollection
                    {
                        Id = "first",
                        Name = "First"
                    },
                    new AssetCollection
                    {
                        Id = "second",
                        Name = "Second"
                    }
                }, string.Empty);
                window.rootVisualElement.Add(list);
                window.Show();
                yield return null;

                list.SelectCollection(
                    "first",
                    toggle: false,
                    range: false);
                using (var pointerDown =
                       PointerDownEvent.GetPooled(new Event
                       {
                           type = EventType.MouseDown,
                           button =
                               (int)MouseButton.LeftMouse,
                           mousePosition = new Vector2(
                               list.worldBound.center.x,
                               list.worldBound.yMax - 1f)
                       }))
                {
                    pointerDown.target = list;
                    list.SendEvent(pointerDown);
                }

                Assert.That(
                    list.SelectedCollectionIds,
                    Is.Empty);

                list.SelectCollection(
                    "second",
                    toggle: false,
                    range: false);
                using (var keyDown =
                       KeyDownEvent.GetPooled(new Event
                       {
                           type = EventType.KeyDown,
                           keyCode = KeyCode.Escape
                       }))
                {
                    keyDown.target = list;
                    list.SendEvent(keyDown);
                }

                Assert.That(
                    list.SelectedCollectionIds,
                    Is.Empty);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void CollectionNavigationList_MovesSelectionAsOrderedBlock()
        {
            IReadOnlyList<string> movedIds = null;
            string parentId = "not-called";
            var siblingIndex = -1;
            var list = new CollectionNavigationList(
                null,
                (nextMovedIds, nextParentId, nextSiblingIndex) =>
                {
                    movedIds = nextMovedIds;
                    parentId = nextParentId;
                    siblingIndex = nextSiblingIndex;
                });
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "first",
                    Name = "First",
                    SortOrder = 0
                },
                new AssetCollection
                {
                    Id = "second",
                    Name = "Second",
                    SortOrder = 1
                },
                new AssetCollection
                {
                    Id = "third",
                    Name = "Third",
                    SortOrder = 2
                }
            }, string.Empty);

            Assert.That(
                list.TryRequestMoves(
                    new[] { "first", "second" },
                    null,
                    1),
                Is.True);
            Assert.That(
                movedIds,
                Is.EqualTo(new[] { "first", "second" }));
            Assert.That(parentId, Is.Null);
            Assert.That(siblingIndex, Is.EqualTo(1));
        }

        [Test]
        public void CollectionNavigationList_MultiMoveOmitsSelectedDescendants()
        {
            IReadOnlyList<string> movedIds = null;
            var list = new CollectionNavigationList(
                null,
                (nextMovedIds, _, __) =>
                    movedIds = nextMovedIds);
            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "parent",
                    Name = "Parent"
                },
                new AssetCollection
                {
                    Id = "child",
                    Name = "Child",
                    ParentCollectionId = "parent"
                },
                new AssetCollection
                {
                    Id = "target",
                    Name = "Target"
                }
            }, string.Empty);

            Assert.That(
                list.TryRequestMoves(
                    new[] { "parent", "child" },
                    "target"),
                Is.True);
            Assert.That(
                movedIds,
                Is.EqualTo(new[] { "parent" }));
        }

        [UnityTest]
        public IEnumerator CollectionNavigationList_PointerDragMovesAndReorders()
        {
            var window =
                ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                var root = window.rootVisualElement;
                root.AddToClassList("ee4v-ui");
                UiStyleUtility.AddPackageStyleSheet(
                    root,
                    "Editor/UI/Components/common.uss");
                UiStyleUtility.AddPackageStyleSheet(
                    root,
                    "Editor/UI/Components/Inputs/Button/ui-button.uss");
                UiStyleUtility.AddPackageStyleSheet(
                    root,
                    "Editor/UI/Components/Content/Icon/icon.uss");
                UiStyleUtility.AddPackageStyleSheet(
                    root,
                    "Editor/AssetManager/UI/Panels/NavigationPanel/navigation-panel.uss");

                string movedId = null;
                string parentId = null;
                var siblingIndex = -1;
                var list = new CollectionNavigationList(
                    null,
                    (collectionIds, nextParentId, nextSiblingIndex) =>
                    {
                        movedId = collectionIds.Single();
                        parentId = nextParentId;
                        siblingIndex = nextSiblingIndex;
                    });
                list.SetState(new[]
                {
                    new AssetCollection
                    {
                        Id = "parent",
                        Name = "Parent"
                    },
                    new AssetCollection
                    {
                        Id = "source",
                        Name = "Source"
                    }
                }, string.Empty);
                root.Add(list);
                window.Show();
                yield return null;

                var rows = list.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-collection-list__row")
                    .ToList();
                var source = rows[1];
                var sourcePosition = source.worldBound.center;
                var targetPosition = rows[0].worldBound.center;

                SendPointerDrag(
                    source,
                    sourcePosition,
                    targetPosition);

                Assert.That(movedId, Is.EqualTo("source"));
                Assert.That(parentId, Is.EqualTo("parent"));
                Assert.That(siblingIndex, Is.Zero);

                movedId = null;
                parentId = "not-called";
                siblingIndex = -1;
                SendPointerDrag(
                    source,
                    sourcePosition,
                    new Vector2(
                        rows[0].worldBound.center.x,
                        rows[0].worldBound.yMin + 1f));

                Assert.That(movedId, Is.EqualTo("source"));
                Assert.That(parentId, Is.Null);
                Assert.That(siblingIndex, Is.Zero);
            }
            finally
            {
                window.Close();
            }
        }

        private static void SendPointerDrag(
            VisualElement source,
            Vector2 sourcePosition,
            Vector2 targetPosition)
        {
            using (var pointerDown =
                   PointerDownEvent.GetPooled(new Event
                   {
                       type = EventType.MouseDown,
                       button = (int)MouseButton.LeftMouse,
                       mousePosition = sourcePosition
                   }))
            {
                pointerDown.target = source;
                source.SendEvent(pointerDown);
            }

            using (var pointerMove =
                   PointerMoveEvent.GetPooled(new Event
                   {
                       type = EventType.MouseDrag,
                       button = (int)MouseButton.LeftMouse,
                       mousePosition = targetPosition
                   }))
            {
                pointerMove.target = source;
                source.SendEvent(pointerMove);
            }

            using (var pointerUp =
                   PointerUpEvent.GetPooled(new Event
                   {
                       type = EventType.MouseUp,
                       button = (int)MouseButton.LeftMouse,
                       mousePosition = targetPosition
                   }))
            {
                pointerUp.target = source;
                source.SendEvent(pointerUp);
            }
        }

        [Test]
        public void RegularCollectionIcon_IsAlwaysFolder()
        {
            var state = AssetCollectionIconPresenter.CreateState(
                new AssetCollection
                {
                    Id = "regular",
                    Icon = AssetCollectionIcon.Star,
                    IconAssetGuid = "legacy-custom-icon"
                });

            Assert.That(
                state.SourceKind,
                Is.EqualTo(UiIconSourceKind.Fluent));
            Assert.That(
                state.FluentIcon,
                Is.EqualTo(UiFluentIcon.Folder));
        }

        [Test]
        public void SmartCollectionPresets_MapToFluentPngIcons()
        {
            var icons =
                System.Enum.GetValues(
                        typeof(AssetCollectionIcon))
                    .Cast<AssetCollectionIcon>()
                    .ToArray();
            var expected = new[]
            {
                UiFluentIcon.Folder,
                UiFluentIcon.Star,
                UiFluentIcon.Box,
                UiFluentIcon.Tag,
                UiFluentIcon.Search,
                UiFluentIcon.Image,
                UiFluentIcon.MusicNote2,
                UiFluentIcon.DocumentCode,
                UiFluentIcon.Cube,
                UiFluentIcon.Database,
                UiFluentIcon.Heart,
                UiFluentIcon.Library,
                UiFluentIcon.Collections,
                UiFluentIcon.Group,
                UiFluentIcon.Grid,
                UiFluentIcon.List,
                UiFluentIcon.Table,
                UiFluentIcon.Camera,
                UiFluentIcon.Video,
                UiFluentIcon.Document,
                UiFluentIcon.Archive,
                UiFluentIcon.Cloud,
                UiFluentIcon.Color,
                UiFluentIcon.Lightbulb,
                UiFluentIcon.Wrench,
                UiFluentIcon.Settings,
                UiFluentIcon.Pin,
                UiFluentIcon.Home,
                UiFluentIcon.Apps,
                UiFluentIcon.Key
            };

            Assert.That(icons.Length, Is.EqualTo(30));
            for (var i = 0; i < icons.Length; i++)
            {
                var state =
                    AssetCollectionIconPresenter.CreateState(
                        new AssetCollection
                        {
                            IsSmartCollection = true,
                            Icon = icons[i]
                        });
                Assert.That(
                    state.SourceKind,
                    Is.EqualTo(
                        UiIconSourceKind.Fluent),
                    icons[i].ToString());
                Assert.That(
                    state.FluentIcon,
                    Is.EqualTo(expected[i]),
                    icons[i].ToString());
            }
        }

        [Test]
        public void NavigationPanel_CollectionContentUsesNavigationInset()
        {
            var panel = new NavigationPanel();

            Assert.That(
                panel.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-panel__collections-content")
                    .ToList().Count,
                Is.EqualTo(1));
        }

        [Test]
        public void NavigationTypography_UsesImguiFontCacheWorkaround()
        {
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.SectionTitle).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.NavigationItemLabel).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.CollectionNavigationLabel)
                    .Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.CollectionNavigationLabel)
                    .Style.FontSize,
                Is.EqualTo(
                    UiTypographyTokens.LargeBodyFontSize));
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.ButtonLabel).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.ButtonMeta).Style.RequiresImgui,
                Is.True);

            var panel = new NavigationPanel();
            panel.SetCollections(new[]
            {
                new AssetCollection
                {
                    Id = "collection",
                    Name = "Collection"
                }
            }, string.Empty);
            var textElements = panel.Query<UiTextElement>().ToList();

            Assert.That(textElements, Is.Not.Empty);
            Assert.That(
                textElements.All(element =>
                    element.GetType().Name == "ImguiUiTextElement"),
                Is.True);
            Assert.That(
                panel.Query<Button>().ToList()
                    .All(button => button is UiButton),
                Is.True);
        }

        [Test]
        public void SmartCollectionPopup_BuildsBeforeNativePositionExists()
        {
            var window =
                ScriptableObject.CreateInstance<
                    CollectionCreationWindow>();
            try
            {
                var smartField = typeof(CollectionCreationWindow)
                    .GetField(
                        "_smart",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                Assert.That(smartField, Is.Not.Null);
                smartField.SetValue(window, true);
                var createGui = typeof(CollectionCreationWindow)
                    .GetMethod(
                        "CreateGUI",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                Assert.That(createGui, Is.Not.Null);
                Assert.DoesNotThrow(() =>
                    createGui.Invoke(window, null));

                var textElements = window.rootVisualElement
                    .Query<UiTextElement>()
                    .ToList();
                Assert.That(textElements, Is.Not.Empty);
                Assert.That(
                    textElements.All(element =>
                        element.GetType().Name ==
                        "ImguiUiTextElement"),
                    Is.True);
                var scrollView = window.rootVisualElement
                    .Query<ScrollView>()
                    .ToList()
                    .Single();
                Assert.That(
                    scrollView.verticalScrollerVisibility,
                    Is.EqualTo(ScrollerVisibility.Auto));
                Assert.That(
                    scrollView.horizontalScrollerVisibility,
                    Is.EqualTo(ScrollerVisibility.Hidden));
                var addConditionButton = window.rootVisualElement
                    .Query<UiButton>(
                        className:
                        "ee4v-collection-creation-window__add-condition")
                    .ToList()
                    .Single();
                UiFluentIconResolver.TryResolve(
                    UiFluentIcon.Add,
                    out var expectedAddIcon);
                Assert.That(
                    addConditionButton.IconElement.Q<Image>().image,
                    Is.SameAs(expectedAddIcon));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RegularCollectionPopup_DoesNotOfferIconSelection()
        {
            var window =
                ScriptableObject.CreateInstance<
                    CollectionCreationWindow>();
            try
            {
                var createGui = typeof(CollectionCreationWindow)
                    .GetMethod(
                        "CreateGUI",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                Assert.That(createGui, Is.Not.Null);
                createGui.Invoke(window, null);

                Assert.That(
                    window.rootVisualElement
                        .Query<AssetCollectionIconSelector>()
                        .ToList(),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator CollectionNamePopups_FocusNameInput()
        {
            CollectionCreationWindow popup = null;
            try
            {
                var smartField = typeof(CollectionCreationWindow)
                    .GetField(
                        "_smart",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                var modeField = typeof(CollectionCreationWindow)
                    .GetField(
                        "_mode",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                var initialCollectionField =
                    typeof(CollectionCreationWindow)
                        .GetField(
                            "_initialCollection",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic);
                Assert.That(smartField, Is.Not.Null);
                Assert.That(modeField, Is.Not.Null);
                Assert.That(initialCollectionField, Is.Not.Null);

                foreach (var rename in new[] { false, true })
                {
                    foreach (var smart in new[] { false, true })
                    {
                        popup =
                            ScriptableObject.CreateInstance<
                                CollectionCreationWindow>();
                        smartField.SetValue(popup, smart);
                        modeField.SetValue(
                            popup,
                            System.Enum.Parse(
                                modeField.FieldType,
                                rename ? "Rename" : "Create"));
                        if (rename)
                        {
                            initialCollectionField.SetValue(
                                popup,
                                new AssetCollection
                                {
                                    Id = "collection",
                                    Name = "Before",
                                    IsSmartCollection = smart
                                });
                        }

                        popup.Show();
                        popup.Focus();
                        yield return null;

                        var nameField = popup.rootVisualElement
                            .Query<InputField>()
                            .ToList()
                            .First();
                        var focusedElement =
                            popup.rootVisualElement.panel
                                .focusController.focusedElement
                            as VisualElement;
                        var popupDescription =
                            (smart ? "Smart collection " : "Collection ") +
                            (rename ? "rename" : "creation") +
                            " name input";

                        Assert.That(
                            focusedElement,
                            Is.Not.Null,
                            popupDescription);
                        Assert.That(
                            nameField == focusedElement ||
                            nameField.Contains(focusedElement),
                            Is.True,
                            popupDescription);

                        popup.Close();
                        popup = null;
                        yield return null;
                    }
                }
            }
            finally
            {
                popup?.Close();
            }
        }

        [UnityTest]
        public IEnumerator CollectionRenamePopup_OpensAndSubmitsUpdate()
        {
            var owner =
                ScriptableObject.CreateInstance<EditorWindow>();
            CollectionCreationWindow popup = null;
            try
            {
                var anchor = new VisualElement();
                anchor.style.width = 120f;
                anchor.style.height = 22f;
                owner.rootVisualElement.Add(anchor);
                owner.Show();
                yield return null;

                string renamedId = null;
                UpdateCollectionRequest renamedRequest = null;
                popup = CollectionCreationWindow.ShowRename(
                    anchor,
                    new Vector2(30f, 40f),
                    new AssetCollection
                    {
                        Id = "collection",
                        Name = "Before"
                    },
                    (collectionId, request) =>
                    {
                        renamedId = collectionId;
                        renamedRequest = request;
                    });
                yield return null;

                var nameField = popup.rootVisualElement
                    .Query<InputField>()
                    .ToList()
                    .Single();
                Assert.That(nameField.Value, Is.EqualTo("Before"));
                nameField.Value = "After";
                InvokeCollectionPopupSubmit(popup);

                Assert.That(renamedId, Is.EqualTo("collection"));
                Assert.That(renamedRequest, Is.Not.Null);
                Assert.That(renamedRequest.Name, Is.EqualTo("After"));
                Assert.That(
                    renamedRequest.Icon,
                    Is.EqualTo(AssetCollectionIcon.Folder));
                Assert.That(renamedRequest.IconAssetGuid, Is.Null);
            }
            finally
            {
                if (popup != null)
                {
                    popup.Close();
                }

                owner.Close();
            }
        }

        [UnityTest]
        public IEnumerator SmartCollectionRenamePopup_ChangesIcon()
        {
            var owner =
                ScriptableObject.CreateInstance<EditorWindow>();
            CollectionCreationWindow popup = null;
            try
            {
                var anchor = new VisualElement();
                anchor.style.width = 120f;
                anchor.style.height = 22f;
                owner.rootVisualElement.Add(anchor);
                owner.Show();
                yield return null;

                UpdateCollectionRequest updatedRequest = null;
                popup = CollectionCreationWindow.ShowRename(
                    anchor,
                    new Vector2(30f, 40f),
                    new AssetCollection
                    {
                        Id = "smart",
                        Name = "Before",
                        IsSmartCollection = true,
                        Icon = AssetCollectionIcon.Search
                    },
                    (_, request) => updatedRequest = request);
                yield return null;

                var selector = popup.rootVisualElement
                    .Query<AssetCollectionIconSelector>()
                    .ToList()
                    .Single();
                Assert.That(
                    selector.Value,
                    Is.EqualTo(AssetCollectionIcon.Search));
                Assert.That(
                    popup.rootVisualElement
                        .Query<PopupField<SmartCollectionMatchMode>>()
                        .ToList(),
                    Is.Empty);

                selector.Value = AssetCollectionIcon.Star;
                InvokeCollectionPopupSubmit(popup);

                Assert.That(updatedRequest, Is.Not.Null);
                Assert.That(updatedRequest.Name, Is.EqualTo("Before"));
                Assert.That(
                    updatedRequest.Icon,
                    Is.EqualTo(AssetCollectionIcon.Star));
                Assert.That(
                    updatedRequest.IconAssetGuid,
                    Is.EqualTo(string.Empty));
            }
            finally
            {
                if (popup != null)
                {
                    popup.Close();
                }

                owner.Close();
            }
        }

        [UnityTest]
        public IEnumerator SmartConditionsPopup_RestoresAndSubmitsRule()
        {
            var owner =
                ScriptableObject.CreateInstance<EditorWindow>();
            CollectionCreationWindow popup = null;
            try
            {
                var anchor = new VisualElement();
                anchor.style.width = 120f;
                anchor.style.height = 22f;
                owner.rootVisualElement.Add(anchor);
                owner.Show();
                yield return null;

                string updatedId = null;
                UpdateSmartCollectionRequest updatedRequest = null;
                popup =
                    CollectionCreationWindow.ShowSmartConditions(
                        anchor,
                        new Vector2(30f, 40f),
                        new AssetCollection
                        {
                            Id = "smart",
                            Name = "Smart",
                            IsSmartCollection = true,
                            SmartRule = new SmartCollectionRule
                            {
                                MatchMode =
                                    SmartCollectionMatchMode.Any,
                                Conditions = new[]
                                {
                                    new SmartCollectionCondition
                                    {
                                        Field =
                                            SmartCollectionConditionField.Tag,
                                        Operator =
                                            SmartCollectionConditionOperator.Equals,
                                        QueryText = "avatar"
                                    }
                                }
                            }
                        },
                        (collectionId, request) =>
                        {
                            updatedId = collectionId;
                            updatedRequest = request;
                        });
                yield return null;

                Assert.That(
                    popup.rootVisualElement
                        .Query<PopupField<SmartCollectionMatchMode>>()
                        .ToList()
                        .Single()
                        .value,
                    Is.EqualTo(SmartCollectionMatchMode.Any));
                Assert.That(
                    popup.rootVisualElement
                        .Query<InputField>()
                        .ToList()
                        .Single()
                        .Value,
                    Is.EqualTo("avatar"));
                InvokeCollectionPopupSubmit(popup);

                Assert.That(updatedId, Is.EqualTo("smart"));
                Assert.That(updatedRequest, Is.Not.Null);
                Assert.That(
                    updatedRequest.MatchMode,
                    Is.EqualTo(SmartCollectionMatchMode.Any));
                Assert.That(
                    updatedRequest.Conditions.Single().Field,
                    Is.EqualTo(SmartCollectionConditionField.Tag));
                Assert.That(
                    updatedRequest.Conditions.Single().QueryText,
                    Is.EqualTo("avatar"));
            }
            finally
            {
                if (popup != null)
                {
                    popup.Close();
                }

                owner.Close();
            }
        }

        [Test]
        public void CollectionPopupHeight_UsesNaturalHeightUntilMaximum()
        {
            Assert.That(
                CollectionCreationWindow.CalculatePopupHeight(
                    200f,
                    26f),
                Is.EqualTo(230f));
            Assert.That(
                CollectionCreationWindow.CalculatePopupHeight(
                    800f,
                    26f),
                Is.EqualTo(676f));
        }

        [Test]
        public void CollectionPopupResize_PreservesAnchoredOrigin()
        {
            var current = new Rect(
                480f,
                320f,
                400f,
                520f);

            var resized =
                CollectionCreationWindow
                    .CalculateResizedPopupRect(
                        current,
                        new Vector2(400f, 260f));

            Assert.That(resized.position, Is.EqualTo(current.position));
            Assert.That(
                resized.size,
                Is.EqualTo(new Vector2(400f, 260f)));
        }

        [Test]
        public void FormTypography_UsesImguiFontCacheWorkaround()
        {
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.InputPlaceholder).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.FormLabel).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.FormError).Style.RequiresImgui,
                Is.True);
        }

        [Test]
        public void CollectionIconSelector_UsesInlineIconCandidates()
        {
            var selector = new AssetCollectionIconSelector(
                AssetCollectionIcon.Search);

            Assert.That(
                selector.Query<Button>(
                        className:
                        "ee4v-collection-icon-selector__candidate")
                    .ToList().Count,
                Is.EqualTo(30));
            var rows = selector.Query<VisualElement>(
                    className:
                    "ee4v-collection-icon-selector__preset-row")
                .ToList();
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(
                rows.Select(row =>
                        row.Query<Button>(
                                className:
                                "ee4v-collection-icon-selector__candidate")
                            .ToList().Count)
                    .ToArray(),
                Is.EqualTo(new[] { 15, 15 }));
            Assert.That(
                selector.Query<Button>(
                        className:
                        "ee4v-collection-icon-selector__candidate--selected")
                    .ToList().Count,
                Is.EqualTo(1));
            Assert.That(
                selector.Value,
                Is.EqualTo(AssetCollectionIcon.Search));
            Assert.That(
                selector.Query<ObjectField>().ToList().Count,
                Is.EqualTo(1));
        }

        [Test]
        public void SmartCollectionConditions_ExcludeSourceAndLifecycle()
        {
            Assert.That(
                System.Enum.GetNames(
                    typeof(SmartCollectionConditionField)),
                Is.EqualTo(new[]
                {
                    "Name",
                    "Description",
                    "Tag",
                    "FileName",
                    "Extension"
                }));
        }

        [Test]
        public void StandaloneSession_SharesNavigationSelection()
        {
            var session = new StandaloneAssetManagerViewSession();
            string changedItemId = null;
            session.NavigationChanged +=
                itemId => changedItemId = itemId;

            session.SetNavigation("tags");

            Assert.That(
                session.SelectedNavigationItemId,
                Is.EqualTo("tags"));
            Assert.That(changedItemId, Is.EqualTo("tags"));
        }

        [Test]
        public void TagListPage_RendersTagsWithUiTextElements()
        {
            var page = new TagListPage();

            page.SetTags(new[]
            {
                new AssetTag { Id = "tag-1", Name = "Avatar" },
                new AssetTag { Id = "tag-2", Name = "Shader" }
            });

            Assert.That(page.Tags.Select(tag => tag.Name).ToArray(), Is.EqualTo(new[] { "Avatar", "Shader" }));
            Assert.That(
                page.Query<UiTextElement>().ToList().Any(text => text.Text == "Avatar"),
                Is.True);
            Assert.That(
                page.Query<UiTextElement>().ToList().Any(text => text.Text == "Shader"),
                Is.True);
        }

        private static void InvokeCollectionPopupSubmit(
            CollectionCreationWindow popup)
        {
            var submit = typeof(CollectionCreationWindow)
                .GetMethod(
                    "Submit",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(submit, Is.Not.Null);
            submit.Invoke(popup, null);
        }
    }
}
