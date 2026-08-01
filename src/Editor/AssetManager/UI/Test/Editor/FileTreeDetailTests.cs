using Ee4v.AssetManager.Contracts;
using Ee4v.Testing.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class FileTreeDetailTests
    {
        [Test]
        [FeatureTestCase(
            "ZIP 内要素を詳細表示 state に変換する",
            "File Tree の ZIP 内 entry でも名前を持つ詳細表示 state を生成できることを確認します。",
            order: 430,
            category: FeatureTestCategory.Ui)]
        public void ZipEntry_CreatesFileDetailState()
        {
            var node = new FileTreeNode(
                "preview.png",
                string.Empty,
                "archive/content/preview.png",
                relativePath: "content/preview.png",
                detailParentName: "archive.zip");

            var state = node.CreateDetailState("item-1");

            Assert.That(state.Name, Is.EqualTo("preview.png"));
            Assert.That(state.ParentName, Is.EqualTo("archive.zip"));
            Assert.That(state.Id, Does.Contain("archive/content/preview.png"));
            Assert.That(state.Extension, Is.EqualTo("png"));
        }

        [Test]
        public void AssetFile_CreatesFileDetailState()
        {
            var state = FileTreeDetailState.FromAssetFile("file-1", "avatar.zip");

            Assert.That(state.Id, Is.EqualTo("asset-file|file-1"));
            Assert.That(state.Name, Is.EqualTo("avatar.zip"));
            Assert.That(state.ParentName, Is.Empty);
            Assert.That(state.Extension, Is.EqualTo("zip"));
        }

        [Test]
        public void AssetFileRoot_PreservesFileIdForArchiveDetail()
        {
            var node = new FileTreeNode(
                "avatar.unitypackage",
                string.Empty,
                "C:/Library/avatar.unitypackage",
                isAssetFileRoot: true,
                assetFileId: "file-1");

            var state =
                node.CreateDetailState("item-1");

            Assert.That(
                state.AssetFileId,
                Is.EqualTo("file-1"));
            Assert.That(
                state.Extension,
                Is.EqualTo("unitypackage"));
        }

        [Test]
        [FeatureTestCase(
            "ZIP 内 UnityPackage の読み込み元を詳細表示へ引き継ぐ",
            "表示用に省略した path ではなく元 ZIP と実 entry path を詳細表示 state が保持することを確認します。",
            order: 431,
            category: FeatureTestCategory.Ui)]
        public void ZipUnityPackageEntry_PreservesArchiveSource()
        {
            var node = new FileTreeNode(
                "avatar.unitypackage",
                string.Empty,
                "Packages/avatar.unitypackage",
                detailParentName: "avatar.zip",
                detailArchivePath:
                    "C:/Library/avatar.zip",
                detailArchiveEntryPath:
                    "Avatar/Packages/avatar.unitypackage");

            var state =
                node.CreateDetailState("item-1");

            Assert.That(
                state.HasArchiveEntrySource,
                Is.True);
            Assert.That(
                state.SourceArchivePath,
                Is.EqualTo("C:/Library/avatar.zip"));
            Assert.That(
                state.SourceArchiveEntryPath,
                Is.EqualTo(
                    "Avatar/Packages/avatar.unitypackage"));
        }

        [TestCase(".PNG", "image", "Image")]
        [TestCase("Assets/model.VRM", "model", "Cube")]
        [TestCase("unitypackage", "package-archive", "FolderZip")]
        [TestCase("unknown", "file", "Document")]
        public void IconCatalog_ResolvesFileDefinition(
            string extension,
            string expectedId,
            string expectedIcon)
        {
            var definition =
                FileIconCatalog.Resolve(
                    FileEntryKind.File,
                    extension);

            Assert.That(definition.Id, Is.EqualTo(expectedId));
            Assert.That(
                definition.Icon.ToString(),
                Is.EqualTo(expectedIcon));
        }

        [Test]
        public void IconCatalog_OwnsUniqueExtensions()
        {
            var duplicateExtensions =
                FileIconCatalog.Definitions
                    .Where(definition =>
                        definition.Kind ==
                        FileEntryKind.File)
                    .SelectMany(definition =>
                        definition.Extensions)
                    .GroupBy(
                        extension => extension,
                        StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();

            Assert.That(duplicateExtensions, Is.Empty);
        }

        [Test]
        public void IconCatalog_UsesStandard88PixelSize()
        {
            Assert.That(
                FileIconCatalog.Definitions
                    .Select(definition =>
                        definition.ArtworkIconSize)
                    .Distinct()
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    FileIconDefinition.StandardIconSize
                }));
            Assert.That(
                FileIconDefinition.StandardIconSize,
                Is.EqualTo(88f));
        }

        [Test]
        public void DetailContentCatalog_UsesNameOnlyFallback()
        {
            Assert.That(
                FileTreeDetailContentCatalog.Definitions
                    .Select(definition =>
                        definition.Id),
                Is.EqualTo(new[]
                {
                    "zip",
                    "unitypackage"
                }));

            var definition =
                FileTreeDetailContentCatalog.Resolve("png");
            var content = definition.CreateContent(
                FileTreeDetailState.FromAssetFile(
                    "file-1",
                    "preview.png"));
            var textElements =
                content.Query<UiTextElement>().ToList();

            Assert.That(definition.Id, Is.EqualTo("fallback"));
            Assert.That(textElements, Has.Count.EqualTo(1));
            Assert.That(
                textElements[0].Text,
                Is.EqualTo("preview.png"));
        }

        [TestCase("zip", "zip")]
        [TestCase(".unitypackage", "unitypackage")]
        public void DetailContentCatalog_ResolvesArchiveDetails(
            string extension,
            string expectedId)
        {
            Assert.That(
                FileTreeDetailContentCatalog
                    .Resolve(extension)
                    .Id,
                Is.EqualTo(expectedId));
        }

        [Test]
        public void ArchiveDetail_UsesPreloadedContent()
        {
            var content = new AssetArchiveContent(
                AssetArchiveContentKind.Zip,
                2048L,
                new[]
                {
                    new AssetArchiveContentEntry(
                        "Avatar/Body.prefab",
                        AssetArchiveContentEntryKind.File,
                        128L)
                });
            var definition =
                FileTreeDetailContentCatalog.Resolve("zip");

            var view = definition.CreateContent(
                new FileTreeDetailState(
                    "archive",
                    "Avatar.zip",
                    extension: "zip",
                    archiveContent: content));

            Assert.That(
                view,
                Is.TypeOf<ArchiveFileDetailView>());
            Assert.That(
                view.Query<SearchableTreeView<
                    ArchiveFileDetailNode>>()
                    .First(),
                Is.Not.Null);
            Assert.That(
                view.Query<UiTextElement>()
                    .ToList()
                    .Any(element =>
                        element.Text == "Avatar.zip"),
                Is.True);
            Assert.That(
                view.Q<VisualElement>(
                    className:
                    "ee4v-asset-manager-archive-detail__tree-pane"),
                Is.Not.Null);
            Assert.That(
                view.Q<VisualElement>(
                    className:
                    "ee4v-asset-manager-archive-detail__preview"),
                Is.Not.Null);
            Assert.That(
                view.Q<VisualElement>(
                    className:
                    "ee4v-asset-manager-archive-detail__summary"),
                Is.Null);
            Assert.That(
                view.Q<VisualElement>(
                    className:
                    "ee4v-asset-manager-archive-detail__format"),
                Is.Null);
        }

        [Test]
        public void ArchiveTree_PreservesPreviewSourcePath()
        {
            var tree =
                ArchiveFileDetailTreeBuilder.Build(
                    new[]
                    {
                        new AssetArchiveContentEntry(
                            "Avatar/Body.png",
                            AssetArchiveContentEntryKind.File,
                            sourcePath:
                            "Root/Avatar/Body.png")
                    });

            Assert.That(
                tree[0].Children[0]
                    .Data.SourcePath,
                Is.EqualTo(
                    "Root/Avatar/Body.png"));
        }

        [Test]
        public void ArchiveTreeRow_UsesInformationFileTreeAppearance()
        {
            var row =
                ArchiveFileDetailView.CreateTreeRow();

            Assert.That(
                row.ClassListContains(
                    SearchableFileTree.RowClassName),
                Is.True);
            Assert.That(
                row.Q<UiTextElement>(
                    className:
                    SearchableFileTree.RowTitleClassName),
                Is.Not.Null);
            Assert.That(
                row.Q<UiTextElement>(
                        className:
                        SearchableFileTree.RowMetaClassName)
                    .ClassListContains(
                        SearchableFileTree
                            .RowEmptyMetaClassName),
                Is.True);
        }

        [Test]
        public void ArchivePreviewLoading_ShowsNothing()
        {
            var view = new ArchiveFileDetailView();

            view.SetPreviewLoading();

            Assert.That(
                view.Q<Icon>(
                        className:
                        "ee4v-asset-manager-archive-detail__preview-icon")
                    .style.display.value,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(
                view.Q<Image>(
                        className:
                        "ee4v-asset-manager-archive-detail__preview-image")
                    .style.display.value,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(
                view.Q<UiTextElement>(
                        className:
                        "ee4v-asset-manager-archive-detail__preview-name")
                    .Text,
                Is.Empty);
            Assert.That(
                view.Q<UiTextElement>(
                        className:
                        "ee4v-asset-manager-archive-detail__preview-status")
                    .style.display.value,
                Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void DetailContentDefinition_NormalizesExtensions()
        {
            var definition =
                new FileTreeDetailContentDefinition(
                    "image",
                    new[] { ".PNG", "jpg" },
                    _ => new VisualElement());

            Assert.That(
                definition.Matches(
                    "Assets/Preview.png"),
                Is.True);
            Assert.That(
                definition.Matches("jpeg"),
                Is.False);
        }

        [Test]
        public void NonFileNode_DoesNotSelectExtensionContent()
        {
            var node = new FileTreeNode(
                "Group.png",
                string.Empty,
                "Group.png",
                isGroup: true,
                groupKind:
                    FileTreeGroupKind.Variant);

            var state =
                node.CreateDetailState("item-1");

            Assert.That(state.Extension, Is.Empty);
            Assert.That(
                FileTreeDetailContentCatalog
                    .Resolve(state.Extension)
                    .Id,
                Is.EqualTo("fallback"));
        }

        [TestCase(
            "VariantGroup",
            "variant-group")]
        [TestCase(
            "VersionGroup",
            "version-group")]
        public void IconCatalog_ResolvesGroupDefinition(
            string expectedKindName,
            string expectedDefinitionId)
        {
            var kind =
                (FileEntryKind)Enum.Parse(
                    typeof(FileEntryKind),
                    expectedKindName);
            var definition =
                FileIconCatalog.Resolve(
                    kind,
                    string.Empty);

            Assert.That(
                definition.Kind.ToString(),
                Is.EqualTo(expectedKindName));
            Assert.That(
                definition.Id,
                Is.EqualTo(expectedDefinitionId));
        }

        [Test]
        public void DetailTypography_UsesImguiFontCacheWorkaround()
        {
            var view = new FileTreeDetailView();
            view.SetState(
                FileTreeDetailState.FromAssetFile(
                    "file-1",
                    "preview.png"));

            var textElements =
                view.Query<UiTextElement>().ToList();

            Assert.That(
                textElements.Any(element =>
                    element.Text == "preview.png"),
                Is.True);
            Assert.That(
                textElements
                    .Single(element =>
                        element.Text == "preview.png")
                    .GetType()
                    .Name,
                Is.EqualTo(
                    "ImguiUiTextElement"));
        }

        [Test]
        public void FileDetail_ContainsDependencySettingsComponent()
        {
            var view = new FileTreeDetailView();

            Assert.That(
                view.Query<FileDependencySettingsView>()
                    .First(),
                Is.Not.Null);
        }

        [Test]
        public void FileDetail_ContainsDependencySelectionOverlay()
        {
            var view = new FileTreeDetailView();

            Assert.That(
                view.Q<VisualElement>(
                        className:
                        "ee4v-asset-manager-file-detail__dependency-overlay")
                    .style.display.value,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(
                view.Q<UiButton>(
                    className:
                    "ee4v-asset-manager-file-detail__dependency-button"),
                Is.Not.Null);
        }

        [Test]
        public void FileDetail_CanPresentDependencySelection()
        {
            var view = new FileTreeDetailView();
            view.ShowDependencySelection(
                new FileDependencySettingsState(
                    "item-1",
                    Array.Empty<
                        FileDependencyOption>(),
                    Array.Empty<string>(),
                    _ => { }));

            Assert.That(
                view.Q<VisualElement>(
                        className:
                        "ee4v-asset-manager-file-detail__dependency-overlay")
                    .style.display.value,
                Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void DependencySettings_UsesSearchableTreeInsteadOfSelector()
        {
            var view =
                new FileDependencySettingsView();
            view.SetState(
                new FileDependencySettingsState(
                    "item-1",
                    new[]
                    {
                        new FileDependencyOption(
                            "item-1",
                            "Item",
                            "file-b",
                            "B.prefab")
                    },
                    Array.Empty<string>(),
                    _ => { }));

            Assert.That(
                view.Query<SearchableTreeView<
                    FileDependencyTreeNode>>()
                    .First()
                    .GetType(),
                Is.EqualTo(typeof(
                    SearchableTreeView<
                        FileDependencyTreeNode>)));
            Assert.That(
                view.Query<DropdownField>()
                    .ToList(),
                Is.Empty);
        }

        [Test]
        public void DependencySettings_HasFixedHeaderElements()
        {
            var view =
                new FileDependencySettingsView();

            var title =
                view.Q<UiTextElement>(
                    className:
                    "ee4v-file-dependency-settings__title");
            var instruction =
                view.Q<UiTextElement>(
                    className:
                    "ee4v-file-dependency-settings__instruction");

            Assert.That(title, Is.Not.Null);
            Assert.That(instruction, Is.Not.Null);
            Assert.That(
                title.style.flexShrink.value,
                Is.EqualTo(0f));
            Assert.That(
                instruction.style.flexShrink.value,
                Is.EqualTo(0f));
        }

        [Test]
        public void DependencySettings_PinsSameItemBeforeOtherItems()
        {
            var view = new FileDependencySettingsView();
            view.SetState(
                new FileDependencySettingsState(
                    "item-1",
                    new[]
                    {
                        new FileDependencyOption(
                            "item-2",
                            "Other",
                            "file-c",
                            "C.prefab"),
                        new FileDependencyOption(
                            "item-1",
                            "Current",
                            "file-b",
                            "B.prefab")
                    },
                    Array.Empty<string>(),
                    _ => { }));

            var current = view.Q<VisualElement>(
                className:
                "ee4v-file-dependency-settings__section--current");
            var other = view.Q<VisualElement>(
                className:
                "ee4v-file-dependency-settings__section--other");

            Assert.That(current, Is.Not.Null);
            Assert.That(other, Is.Not.Null);
            Assert.That(
                view.IndexOf(current),
                Is.LessThan(view.IndexOf(other)));
            Assert.That(
                current.Query<FileDependencyTreeRow>()
                    .ToList(),
                Has.Count.EqualTo(1));
            Assert.That(
                view.Query<UiTextElement>()
                    .ToList()
                    .All(element =>
                        element.GetType().Name ==
                        "ImguiUiTextElement"),
                Is.True);
        }

        [Test]
        public void DependencyTreeRow_UsesEmptyToggleLabel()
        {
            bool? changed = null;
            var row = new FileDependencyTreeRow(
                (_, selected) =>
                    changed = selected);
            row.Bind(
                new FileDependencyTreeNode(
                    "B.prefab",
                    "file-b",
                    "prefab"),
                selected: true);

            Assert.That(row.Toggle.label, Is.Empty);
            Assert.That(row.Toggle.value, Is.True);

            row.Toggle.value = false;

            Assert.That(changed, Is.False);
            Assert.That(
                row.Query<UiTextElement>()
                    .ToList()
                    .All(element =>
                        element.GetType().Name ==
                        "ImguiUiTextElement"),
                Is.True);
        }

        [Test]
        public void History_CanMoveToSelectedOverlayEntry()
        {
            var history = new AssetItemGridHistory();
            var root = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.View,
                "all-assets",
                "All Assets");
            var list = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.FileList,
                "all-assets",
                "All Assets",
                "item-1",
                "Avatar");
            var tags = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.View,
                "tags",
                "Tags");

            history.SetCurrent(root);
            history.SetCurrent(list);
            history.SetCurrent(tags);

            Assert.That(history.TryGoBack(2, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(root));
            Assert.That(history.State.ForwardEntries, Is.EqualTo(new[] { list, tags }));
        }

        [Test]
        public void HistoryMenu_DefaultsToFiveVisibleEntriesAndCanBeConfigured()
        {
            var rows = new[]
            {
                new HistoryNavigationOverlayRowState("1", () => { }),
                new HistoryNavigationOverlayRowState("2", () => { }),
                new HistoryNavigationOverlayRowState("3", () => { }),
                new HistoryNavigationOverlayRowState("4", () => { }),
                new HistoryNavigationOverlayRowState("5", () => { }),
                new HistoryNavigationOverlayRowState("6", () => { })
            };
            var defaultState = HistoryNavigationMenu.CreateState(rows);
            Assert.That(defaultState.Items.Count, Is.EqualTo(5));
            Assert.That(defaultState.Items[0].Label, Is.EqualTo("1"));

            var configuredState =
                HistoryNavigationMenu.CreateState(rows, 2);
            Assert.That(configuredState.Items.Count, Is.EqualTo(2));
            Assert.That(configuredState.Items[1].Label, Is.EqualTo("2"));
        }

    }
}
