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
                FileTreeDetailContentCatalog.Definitions,
                Is.Empty);

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
        public void FileDetailHistory_SupportsBackAndForward()
        {
            var history = new AssetItemGridHistory();
            var list = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.FileList,
                "all-assets",
                "All Assets",
                "item-1",
                "Avatar");
            var detail = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.FileDetail,
                "all-assets",
                "All Assets",
                "item-1",
                "Avatar",
                detailId: "item-1|preview.png",
                detailName: "preview.png",
                detailParentName: "archive.zip",
                detailExtension: "png");

            history.SetCurrent(list);
            history.SetCurrent(detail);

            Assert.That(history.State.BackEntries, Is.EqualTo(new[] { list }));
            Assert.That(history.State.ForwardEntries, Is.Empty);
            Assert.That(history.State.Current.Breadcrumbs, Is.EqualTo(new[]
            {
                "All Assets",
                "Avatar",
                "archive.zip",
                "preview.png"
            }));
            Assert.That(history.TryGoBack(out var previous), Is.True);
            Assert.That(previous.Kind, Is.EqualTo(AssetItemGridHistoryEntryKind.FileList));
            Assert.That(history.State.ForwardEntries, Is.EqualTo(new[] { detail }));
            Assert.That(history.TryGoForward(out var next), Is.True);
            Assert.That(next.Kind, Is.EqualTo(AssetItemGridHistoryEntryKind.FileDetail));
            Assert.That(next.DetailName, Is.EqualTo("preview.png"));
            Assert.That(next.DetailParentName, Is.EqualTo("archive.zip"));
            Assert.That(next.DetailExtension, Is.EqualTo("png"));
        }

        [Test]
        public void CollectionFileHistory_IncludesCollectionPath()
        {
            var entry = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.FileDetail,
                AssetManagerCollectionViewId.Encode("fuga"),
                "fuga",
                "item-1",
                "item",
                detailId: "item-1|preview.png",
                detailName: "preview.png",
                detailParentName: "archive.zip",
                viewPath: new[]
                {
                    new AssetItemGridHistoryView(
                        AssetManagerCollectionViewId.Encode("hoge"),
                        "hoge"),
                    new AssetItemGridHistoryView(
                        AssetManagerCollectionViewId.Encode("fuga"),
                        "fuga")
                });

            Assert.That(entry.Breadcrumbs, Is.EqualTo(new[]
            {
                "hoge",
                "fuga",
                "item",
                "archive.zip",
                "preview.png"
            }));
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
            var detail = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.FileDetail,
                "all-assets",
                "All Assets",
                "item-1",
                "Avatar",
                detailId: "file-1",
                detailName: "preview.png");

            history.SetCurrent(root);
            history.SetCurrent(list);
            history.SetCurrent(detail);

            Assert.That(history.TryGoBack(2, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(root));
            Assert.That(history.State.ForwardEntries, Is.EqualTo(new[] { list, detail }));
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
