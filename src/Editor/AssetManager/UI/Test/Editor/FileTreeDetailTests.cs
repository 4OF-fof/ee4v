using System.Collections;
using System.Linq;
using Ee4v.Testing.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
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
        }

        [Test]
        public void AssetFile_CreatesFileDetailState()
        {
            var state = FileTreeDetailState.FromAssetFile("file-1", "avatar.zip");

            Assert.That(state.Id, Is.EqualTo("asset-file|file-1"));
            Assert.That(state.Name, Is.EqualTo("avatar.zip"));
            Assert.That(state.ParentName, Is.Empty);
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
                detailParentName: "archive.zip");

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
        public void HistoryNavigation_UsesProjectTabArrowGlyphs()
        {
            var navigation = new HistoryNavigation();
            var buttons = navigation.Query<UiButton>(
                    className:
                    "ee4v-ui-history-navigation__icon-button")
                .ToList();

            Assert.That(buttons.Count, Is.EqualTo(2));
            Assert.That(
                buttons.Select(button =>
                    button.LabelElement.Text),
                Is.EqualTo(new[] { "\u2190", "\u2192" }));
            Assert.That(
                buttons.All(button =>
                    button.LabelElement.GetType().Name ==
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

        [UnityTest]
        [FeatureTestCase(
            "履歴ボタンの右クリックで履歴一覧を表示する",
            "AssetManager の戻るボタンを右クリックしたときに履歴メニューが開くことを確認します。",
            order: 440,
            category: FeatureTestCategory.Ui)]
        public IEnumerator HistoryNavigation_ContextClickShowsHistoryMenu()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            ContextMenuState shownState = null;
            var navigation = new HistoryNavigation(
                showHistoryMenu: (_, rows, maximumRows) =>
                    shownState = HistoryNavigationMenu.CreateState(
                        rows,
                        maximumRows));
            navigation.SetState(new AssetItemGridHistoryState(
                new AssetItemGridHistoryEntry(
                    AssetItemGridHistoryEntryKind.View,
                    "current",
                    "Current"),
                true,
                false,
                new[]
                {
                    new AssetItemGridHistoryEntry(
                        AssetItemGridHistoryEntryKind.FileList,
                        "previous",
                        "Previous",
                        "item-1",
                        "Avatar")
                }));
            window.rootVisualElement.Add(navigation);
            window.Show();
            yield return null;

            try
            {
                var backButton = navigation.Q<Button>(
                    className: "ee4v-ui-history-navigation__icon-button");
                Assert.That(backButton.tooltip, Is.Null.Or.Empty);
                using (var evt = ContextClickEvent.GetPooled(
                           backButton.worldBound.center,
                           (int)MouseButton.RightMouse,
                           1,
                           Vector2.zero,
                           EventModifiers.None))
                {
                    evt.target = backButton;
                    backButton.SendEvent(evt);
                }

                Assert.That(shownState, Is.Not.Null);
                Assert.That(shownState.Items.Count, Is.EqualTo(1));
                Assert.That(
                    shownState.Items[0].Label,
                    Is.EqualTo("Avatar"));
                Assert.That(shownState.Items[0].Enabled, Is.True);
            }
            finally
            {
                window.Close();
            }
        }

    }
}
