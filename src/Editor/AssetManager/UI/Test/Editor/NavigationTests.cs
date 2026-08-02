using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class NavigationTests
    {
        [Test]
        public void InformationDetailTab_DependsOnSelectionKind()
        {
            Assert.That(
                InfomationPanel.ResolveDetailTabId(
                    AssetSelectionContentKind.AssetItem,
                    "asset-info"),
                Is.EqualTo("asset-info"));
            Assert.That(
                InfomationPanel.ResolveDetailTabId(
                    AssetSelectionContentKind.AssetItem,
                    "file-tree"),
                Is.EqualTo("file-tree"));
            Assert.That(
                InfomationPanel.ResolveDetailTabId(
                    AssetSelectionContentKind.AssetFile,
                    "asset-info"),
                Is.EqualTo("file-tree"));
            Assert.That(
                InfomationPanel.ResolveDetailTabId(
                    AssetSelectionContentKind.AssetVersionGroup,
                    "asset-info"),
                Is.EqualTo("asset-info"));
        }

        [Test]
        public void AssetInfo_UpdatesOnlyWhenEditedValuesDiffer()
        {
            var state = new AssetInfoState(
                "item-1",
                "Avatar",
                "Description",
                new[] { "Sample" },
                1,
                "1 MB",
                "ZIP",
                "ee4v",
                string.Empty,
                string.Empty);

            Assert.That(
                AssetInfoView.HasChanges(
                    new AssetInfoEditRequest(
                        "Avatar",
                        "Description",
                        new[] { "Sample" }),
                    state),
                Is.False);
            Assert.That(
                AssetInfoView.HasChanges(
                    new AssetInfoEditRequest(
                        "Edited",
                        "Description",
                        new[] { "Sample" }),
                    state),
                Is.True);
        }

        [Test]
        public void AssetInfo_GroupStateUsesGroupFieldsAndHidesTags()
        {
            var state = AssetInfoController.CreateState(
                "variant-1",
                "PC",
                "PC向けファイル",
                Array.Empty<string>(),
                new[]
                {
                    new AssetFile
                    {
                        Id = "file-1",
                        FileName = "avatar.unitypackage",
                        SizeBytes = 2L * 1024L * 1024L,
                        Origins = new[]
                        {
                            new AssetFileOrigin
                            {
                                SourceType = AssetSourceType.Ee4v
                            }
                        }
                    }
                },
                DateTime.MinValue,
                DateTime.MinValue,
                showTags: false);

            Assert.That(state.ItemId, Is.EqualTo("variant-1"));
            Assert.That(state.Name, Is.EqualTo("PC"));
            Assert.That(state.Description, Is.EqualTo("PC向けファイル"));
            Assert.That(state.FileCount, Is.EqualTo(1));
            Assert.That(state.TotalFileSize, Is.EqualTo("2 MB"));
            Assert.That(state.FileTypes, Is.EqualTo("UNITYPACKAGE"));
            Assert.That(state.ShowTags, Is.False);
        }

        [Test]
        public void AssetTagSelection_FiltersAndTogglesNormalizedNames()
        {
            var selection = new AssetTagSelection(
                new[] { "Avatar", "Sample", "sample" },
                new[] { "Avatar" });

            Assert.That(
                selection.AvailableOptions(),
                Is.EqualTo(new[] { "Sample" }));

            selection.Toggle(" sample ");
            selection.Toggle("avatar");
            selection.Toggle(" Custom ");

            Assert.That(
                selection.Selected,
                Is.EqualTo(new[] { "Sample", "Custom" }));
            Assert.That(
                selection.SelectedOptions("cus"),
                Is.EqualTo(new[] { "Custom" }));
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
        public void CreateQuery_UsesCollectionIdForCollectionView()
        {
            var query = MainViewController.CreateQuery(
                new MainViewRequest(
                    AssetManagerCollectionViewId.Encode("collection-1")));

            Assert.That(query.CollectionId, Is.EqualTo("collection-1"));
        }

        [Test]
        public void ProjectHighlightTarget_DistinguishesItemAndFileCards()
        {
            AssetSelectionContentKind kind;
            string id;

            Assert.That(
                MainView.TryResolveHighlightTarget(
                    new ItemCardState(
                        "item-1",
                        "Item",
                        new ItemImageState()),
                    out kind,
                    out id),
                Is.True);
            Assert.That(
                kind,
                Is.EqualTo(
                    AssetSelectionContentKind.AssetItem));
            Assert.That(id, Is.EqualTo("item-1"));

            Assert.That(
                MainView.TryResolveHighlightTarget(
                    new ItemCardState(
                        AssetItemGridNodeKey.Encode(
                            AssetItemGridNodeKind.File,
                            "file-1"),
                        "File",
                        new ItemImageState()),
                    out kind,
                    out id),
                Is.True);
            Assert.That(
                kind,
                Is.EqualTo(
                    AssetSelectionContentKind.AssetFile));
            Assert.That(id, Is.EqualTo("file-1"));

            Assert.That(
                MainView.TryResolveHighlightTarget(
                    new ItemCardState(
                        AssetItemGridNodeKey.Encode(
                            AssetItemGridNodeKind.VersionGroup,
                            "version-1"),
                        "Version",
                        new ItemImageState()),
                    out kind,
                    out id),
                Is.False);
        }

        [Test]
        public void MainContextMenu_KeepsApplicableDisabledActions()
        {
            var items = MainView.CreateContextMenuItems(
                new[]
                {
                    new AssetItemContextAction(
                        "create-variant",
                        "Create Variant",
                        () => { },
                        enabled: false)
                },
                import: null,
                canImport: false,
                highlight: new ContextMenuItemState(
                    "highlight",
                    "Highlight",
                    () => { },
                    enabled: false));

            Assert.That(
                items
                    .Where(item =>
                        item.Kind == ContextMenuItemKind.Action)
                    .Select(item => item.Id),
                Is.EqualTo(
                    new[]
                    {
                        "import",
                        "create-variant",
                        "highlight"
                    }));
            Assert.That(
                items.Select(item => item.Kind),
                Is.EqualTo(
                    new[]
                    {
                        ContextMenuItemKind.Action,
                        ContextMenuItemKind.Action,
                        ContextMenuItemKind.Separator,
                        ContextMenuItemKind.Action
                    }));
            Assert.That(
                items
                    .Where(item =>
                        item.Kind == ContextMenuItemKind.Action)
                    .All(item => !item.Enabled),
                Is.True);
        }

        [Test]
        public void CollectionDrag_SkipsUnassignedFiles()
        {
            var itemIds = AssetItemDragAndDrop.GetAssetItemIds(
                new[]
                {
                    new ItemCardState(
                        AssetItemGridNodeKey.Encode(
                            AssetItemGridNodeKind.File,
                            "unassigned-file"),
                        "Unassigned",
                        new ItemImageState()),
                    new ItemCardState(
                        AssetItemGridNodeKey.Encode(
                            AssetItemGridNodeKind.File,
                            "assigned-file"),
                        "Assigned",
                        new ItemImageState(),
                        null,
                        "item-1"),
                    new ItemCardState(
                        "item-2",
                        "Item",
                        new ItemImageState())
                });

            Assert.That(
                itemIds,
                Is.EqualTo(new[] { "item-1", "item-2" }));
        }

        [Test]
        public void ProjectAssociationIndex_AggregatesFilesAndUsesLatestIconOwner()
        {
            var earlier = new DateTime(2026, 1, 1);
            var later = earlier.AddMinutes(1);
            var index = AssetManagerProjectAssociationIndex.Create(
                new[]
                {
                    new AssetImportedAssetAssociation
                    {
                        ItemId = "item-a",
                        FileId = "file-a1",
                        AssetGuid = "guid-a",
                        ImportedAt = earlier
                    },
                    new AssetImportedAssetAssociation
                    {
                        ItemId = "item-a",
                        FileId = "file-a2",
                        AssetGuid = "shared-guid",
                        ImportedAt = earlier
                    },
                    new AssetImportedAssetAssociation
                    {
                        ItemId = "item-b",
                        FileId = "file-b",
                        AssetGuid = "shared-guid",
                        ImportedAt = later
                    }
                });

            Assert.That(
                index.GuidsByItem["item-a"],
                Is.EqualTo(new[] { "guid-a", "shared-guid" }));
            Assert.That(
                index.GuidsByFile["file-a1"],
                Is.EqualTo(new[] { "guid-a" }));
            Assert.That(
                index.ItemIdByAssetGuid["shared-guid"],
                Is.EqualTo("item-b"));
        }

        [Test]
        public void ProjectHighlightState_MatchesOnlyTheActiveTarget()
        {
            var selection =
                new AssetManagerProjectHighlightSelection();
            selection.Select(
                AssetManagerProjectHighlightTargetKind.Item,
                "item-a");

            Assert.That(
                selection.IsSelected(
                    AssetManagerProjectHighlightTargetKind.Item,
                    "item-a"),
                Is.True);
            Assert.That(
                selection.IsSelected(
                    AssetManagerProjectHighlightTargetKind.Item,
                    "item-b"),
                Is.False);
            Assert.That(
                selection.IsSelected(
                    AssetManagerProjectHighlightTargetKind.File,
                    "item-a"),
                Is.False,
                "the same id in another target kind is not selected");

            selection.Select(
                AssetManagerProjectHighlightTargetKind.Item,
                "item-b");

            Assert.That(
                selection.IsSelected(
                    AssetManagerProjectHighlightTargetKind.Item,
                    "item-a"),
                Is.False);
            Assert.That(
                selection.IsSelected(
                    AssetManagerProjectHighlightTargetKind.Item,
                    "item-b"),
                Is.True);

            Assert.That(selection.Clear(), Is.True);

            Assert.That(
                selection.IsSelected(
                    AssetManagerProjectHighlightTargetKind.Item,
                    "item-b"),
                Is.False);
        }

        [Test]
        public void GridImportSelection_UsesAllConfiguredFilesOrOneRequestedFile()
        {
            var files = new[]
            {
                new AssetFile { Id = "file-a" },
                new AssetFile { Id = "file-b" },
                new AssetFile { Id = "file-c" },
                new AssetFile { Id = "file-a" }
            };
            var configured =
                new HashSet<string>(
                    new[] { "file-a", "file-c" },
                    StringComparer.Ordinal);

            Assert.That(
                MainViewController.SelectImportableFileIds(
                    files,
                    configured,
                    requestedFileId: null),
                Is.EqualTo(
                    new[] { "file-a", "file-c" }));
            Assert.That(
                MainViewController.SelectImportableFileIds(
                    files,
                    configured,
                    "file-c"),
                Is.EqualTo(new[] { "file-c" }));
            Assert.That(
                MainViewController.SelectImportableFileIds(
                    files,
                    configured,
                    "file-b"),
                Is.Empty);
        }

        [Test]
        public void ProjectFolderIconSelection_KeepsOnlyTopmostAppliedFolders()
        {
            var selected =
                AssetManagerProjectFolderIconSelection
                    .SelectTopmost(
                        new[]
                        {
                            new AssetManagerProjectFolderIconCandidate(
                                "animation-guid",
                                "Assets/Amatousagi/Chocolat/Animation",
                                "item-a"),
                            new AssetManagerProjectFolderIconCandidate(
                                "sibling-guid",
                                "Assets/Other",
                                "item-b"),
                            new AssetManagerProjectFolderIconCandidate(
                                "chocolat-guid",
                                "Assets/Amatousagi/Chocolat",
                                "item-a"),
                            new AssetManagerProjectFolderIconCandidate(
                                "fbx-guid",
                                "Assets/Amatousagi/Chocolat/FBX",
                                "item-a"),
                            new AssetManagerProjectFolderIconCandidate(
                                "similar-prefix-guid",
                                "Assets/Amatousagi/ChocolatExtra",
                                "item-c")
                        });

            Assert.That(
                selected.Keys,
                Is.EquivalentTo(new[]
                {
                    "chocolat-guid",
                    "sibling-guid",
                    "similar-prefix-guid"
                }));
            Assert.That(
                selected["chocolat-guid"],
                Is.EqualTo("item-a"));
        }

        [Test]
        public void ProjectDecoration_InitialCacheFailureDoesNotEscapeInitialization()
        {
            var source =
                new FailingProjectCacheSource();
            var presenter =
                new AssetManagerProjectDecorationPresenter(
                    source,
                    new ImmediateUiScheduler());
            Assert.That(
                source.AssociationReadCount,
                Is.Zero,
                "constructor must not read the DB before UI dependencies are configured");
            LogAssert.Expect(
                LogType.Exception,
                "AssetManagerException: incompatible schema");

            Assert.DoesNotThrow(presenter.Initialize);

            Assert.That(source.AssociationReadCount, Is.EqualTo(1));
            presenter.Dispose();
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
        public void CollectionReload_RetriesCatalogChangeAfterLoadFailure()
        {
            Assert.That(
                CollectionNavigationController
                    .ShouldReloadCollections(
                        new AssetManagerChange(
                            AssetManagerChangeKind.Catalog),
                        collectionLoadFailed: true),
                Is.True);
            Assert.That(
                CollectionNavigationController
                    .ShouldReloadCollections(
                        new AssetManagerChange(
                            AssetManagerChangeKind.Catalog),
                        collectionLoadFailed: false),
                Is.False);
            Assert.That(
                CollectionNavigationController
                    .ShouldReloadCollections(
                        new AssetManagerChange(
                            AssetManagerChangeKind.Collections),
                        collectionLoadFailed: false),
                Is.True);
        }

        private sealed class FailingProjectCacheSource :
            IAssetManagerProjectCacheSource
        {
            public event Action<AssetManagerChange> Changed
            {
                add { }
                remove { }
            }

            internal int AssociationReadCount { get; private set; }

            public IReadOnlyList<AssetImportedAssetAssociation>
                GetImportedAssetAssociations()
            {
                AssociationReadCount++;
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatabaseError,
                    "incompatible schema");
            }

            public IReadOnlyDictionary<string, AssetThumbnail>
                GetThumbnails(IReadOnlyList<string> itemIds) =>
                new Dictionary<string, AssetThumbnail>();
        }

        private sealed class ImmediateUiScheduler :
            IAssetManagerUiScheduler
        {
            public void RunOnMainThread(Action operation)
            {
                operation?.Invoke();
            }

            public void RunInBackground<T>(
                Func<CancellationToken, T> operation,
                CancellationToken cancellationToken,
                Action<AssetManagerBackgroundResult<T>> completed)
            {
                throw new NotSupportedException();
            }
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
                Is.False);
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
                Is.False);
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
        public void ErrorScreenMessageTypography_UsesImguiFontCacheWorkaround()
        {
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.ErrorScreenMessage)
                    .Style.RequiresImgui,
                Is.True);

            var message = UiTextFactory.Create(
                "Error",
                UiClassNames.ErrorScreenMessage);

            Assert.That(
                message.GetType().Name,
                Is.EqualTo("ImguiUiTextElement"));
        }

        [Test]
        public void AssetManagerErrorMessage_MapsStableErrorCodesToLocalization()
        {
            Assert.That(
                AssetManagerUiErrorMessage.ResolveKind(
                    new AssetManagerException(
                        AssetManagerErrorCode
                            .DatabaseSchemaIncompatible,
                        "infrastructure detail")),
                Is.EqualTo(
                    AssetManagerUiErrorKind
                        .DatabaseSchemaIncompatible));
            Assert.That(
                AssetManagerUiErrorMessage.ResolveKind(
                    new InvalidOperationException(
                        "unlocalized detail")),
                Is.EqualTo(AssetManagerUiErrorKind.Unknown));
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

            Assert.That(
                page.Tags.Select(tag => tag.Name).ToArray(),
                Is.EqualTo(new[] { "Avatar", "Shader" }));
            Assert.That(
                page.Query<UiTextElement>()
                    .ToList()
                    .Any(text => text.Text == "Avatar"),
                Is.True);
            Assert.That(
                page.Query<UiTextElement>()
                    .ToList()
                    .Any(text => text.Text == "Shader"),
                Is.True);
        }
    }
}
