using System.Collections;
using System.Collections.Generic;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ee4v.ProjectTabs.Tests
{
    public sealed class ProjectTabsSessionTests
    {
        private static readonly ProjectTabLocation Assets =
            new ProjectTabLocation("assets-guid", "Assets");
        private static readonly ProjectTabLocation Materials =
            new ProjectTabLocation(
                "materials-guid",
                "Assets/Materials");
        private static readonly ProjectTabLocation Prefabs =
            new ProjectTabLocation(
                "prefabs-guid",
                "Assets/Prefabs");

        [Test]
        [FeatureTestCase(
            "タブごとに独立した履歴を保持できる",
            "一方のタブを移動しても、別のタブの現在位置が変わらないことを確認します。",
            order: 10)]
        public void RecordNavigation_KeepsHistoryPerTab()
        {
            var session = CreateSession();
            var firstId = session.State.Tabs[0].Id;
            var secondId = session.Add(Prefabs);

            session.RecordNavigation(firstId, Materials);

            Assert.That(
                session.State.Find(firstId).CurrentLocation,
                Is.EqualTo(Materials));
            Assert.That(
                session.State.Find(secondId).CurrentLocation,
                Is.EqualTo(Prefabs));
        }

        [Test]
        [FeatureTestCase(
            "タブの履歴を進む・戻るで移動できる",
            "戻ったあとに進むと、同じ場所へ復帰できることを確認します。",
            order: 20)]
        public void History_CanMoveBackAndForward()
        {
            var session = CreateSession();
            var tabId = session.State.Tabs[0].Id;
            session.RecordNavigation(tabId, Materials);
            session.RecordNavigation(tabId, Prefabs);

            Assert.That(session.GoBack(tabId), Is.EqualTo(Materials));
            Assert.That(session.GoBack(tabId), Is.EqualTo(Assets));
            Assert.That(session.GoForward(tabId), Is.EqualTo(Materials));
            Assert.That(
                session.State.Find(tabId).CanGoForward,
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "履歴一覧から複数ステップ移動できる",
            "戻る・進むボタンの履歴一覧で選んだ場所へ直接移動できることを確認します。",
            order: 25)]
        public void History_CanMoveBySelectedStepCount()
        {
            var session = CreateSession();
            var tabId = session.State.Tabs[0].Id;
            session.RecordNavigation(tabId, Materials);
            session.RecordNavigation(tabId, Prefabs);

            Assert.That(session.GoBack(tabId, 2), Is.EqualTo(Assets));
            Assert.That(session.GoForward(tabId, 2), Is.EqualTo(Prefabs));
        }

        [Test]
        public void HistoryLabel_UsesLastPathSegment()
        {
            Assert.That(
                ProjectTabsHost.FormatHistoryLabel(
                    new ProjectTabLocation(
                        "asset-manager-guid",
                        "Assets/ee4v/Editor/AssetManager")),
                Is.EqualTo("AssetManager"));
            Assert.That(
                ProjectTabsHost.FormatHistoryLabel(
                    new ProjectTabLocation(
                        "editor-guid",
                        "Assets/ee4v/Editor",
                        "toolbar")),
                Is.EqualTo("Editor · toolbar"));
        }

        [Test]
        [FeatureTestCase(
            "戻った後の移動で前方履歴を破棄する",
            "ブラウザーと同様に、戻った地点から別の場所へ移動したとき前方履歴が消えることを確認します。",
            order: 30)]
        public void RecordNavigation_AfterBack_TruncatesForwardHistory()
        {
            var session = CreateSession();
            var tabId = session.State.Tabs[0].Id;
            session.RecordNavigation(tabId, Materials);
            session.RecordNavigation(tabId, Prefabs);
            session.GoBack(tabId);

            var textures = new ProjectTabLocation(
                "textures-guid",
                "Assets/Textures");
            session.RecordNavigation(tabId, textures);

            Assert.That(session.State.Find(tabId).CanGoForward, Is.False);
            Assert.That(session.GoForward(tabId), Is.Null);
        }

        [Test]
        [FeatureTestCase(
            "検索文字列の変更は履歴位置を増やさない",
            "検索入力中に履歴が1文字ごとに増えず、現在位置だけが更新されることを確認します。",
            order: 40)]
        public void RecordNavigation_SearchChange_ReplacesCurrentEntry()
        {
            var session = CreateSession();
            var tabId = session.State.Tabs[0].Id;

            session.RecordNavigation(
                tabId,
                new ProjectTabLocation(
                    Assets.FolderGuid,
                    Assets.FolderPath,
                    "material"));

            var tab = session.State.Find(tabId);
            Assert.That(tab.History.Count, Is.EqualTo(1));
            Assert.That(
                tab.CurrentLocation.SearchText,
                Is.EqualTo("material"));
        }

        [Test]
        [FeatureTestCase(
            "最後のタブを閉じると Assets の新規タブへ置き換わる",
            "Project タブ領域を空にせず、新しい ID と履歴を持つ Assets タブだけが残ることを確認します。",
            order: 50)]
        public void Remove_LastTab_ReplacesItWithFreshAssetsTab()
        {
            var session = CreateSession();
            var tabId = session.State.Tabs[0].Id;
            session.RecordNavigation(tabId, Materials);

            Assert.That(session.Remove(tabId), Is.True);
            Assert.That(session.State.Tabs.Count, Is.EqualTo(1));
            Assert.That(session.State.Tabs[0].Id, Is.Not.EqualTo(tabId));
            Assert.That(session.State.Tabs[0].History.Count, Is.EqualTo(1));
            Assert.That(
                session.State.Tabs[0].CurrentLocation,
                Is.EqualTo(Assets));
        }

        [Test]
        [FeatureTestCase(
            "追加ボタンは右端タブの直後に配置される",
            "タブ一覧を更新しても、追加ボタンがスクロール領域内の末尾に配置されることを確認します。",
            order: 60,
            category: FeatureTestCategory.Ui)]
        public void View_AddButtonFollowsLastTab()
        {
            var view = new ProjectTabsView();
            view.SetState(new ProjectTabsViewState(
                new[]
                {
                    new ProjectTabViewState(
                        "tab-1",
                        "Assets",
                        "Assets",
                        true),
                    new ProjectTabViewState(
                        "tab-2",
                        "Materials",
                        "Assets/Materials",
                        true)
                },
                "tab-1",
                false,
                false));

            var scroll = view.Q<ScrollView>();
            var addButton = scroll.contentContainer[
                scroll.contentContainer.childCount - 1] as Button;

            Assert.That(scroll.contentContainer.childCount, Is.EqualTo(3));
            Assert.That(addButton, Is.Not.Null);
            Assert.That(addButton.text, Is.EqualTo("+"));
        }

        [UnityTest]
        [FeatureTestCase(
            "履歴ボタンの右クリックで履歴一覧を表示する",
            "Project の戻るボタンを右クリックしたときに履歴メニューが開くことを確認します。",
            order: 70,
            category: FeatureTestCategory.Ui)]
        public IEnumerator View_BackButtonContextClickShowsHistoryMenu()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            EditorWindow historyMenu = null;
            window.position = new Rect(0f, 0f, 600f, 200f);
            window.Show();

            try
            {
                var view = new ProjectTabsView();
                view.SetState(new ProjectTabsViewState(
                    null,
                    string.Empty,
                    true,
                    false,
                    new[]
                    {
                        new ProjectHistoryEntryViewState("Assets", 1)
                    }));
                window.rootVisualElement.Add(view);
                yield return null;

                var backButton = view.Q<Button>(
                    className: "ee4v-project-tabs__navigation-button");
                Assert.That(backButton.tooltip, Is.Null.Or.Empty);
                using (var evt = ContextClickEvent.GetPooled())
                {
                    backButton.SendEvent(evt);
                }
                yield return null;

                var editorWindows =
                    Resources.FindObjectsOfTypeAll<EditorWindow>();
                for (var i = 0; i < editorWindows.Length; i++)
                {
                    if (editorWindows[i].GetType().Name ==
                        "ContextMenuWindow")
                    {
                        historyMenu = editorWindows[i];
                    }
                }

                Assert.That(historyMenu, Is.Not.Null);
                var historyRow = historyMenu.rootVisualElement.Q<Button>(
                    className: "ee4v-ui-context-menu__item");
                Assert.That(historyRow, Is.Not.Null);
                Assert.That(historyRow.contentRect.width, Is.GreaterThan(0f));
                Assert.That(
                    historyRow.Q<IMGUIContainer>(),
                    Is.Not.Null);
            }
            finally
            {
                if (historyMenu != null)
                {
                    historyMenu.Close();
                }
                window.Close();
            }
        }

        private static ProjectTabsSession CreateSession()
        {
            var nextId = 0;
            return new ProjectTabsSession(
                new MemoryStore(),
                Assets,
                () => "tab-" + nextId++);
        }

        private sealed class MemoryStore : IProjectTabsStateStore
        {
            public ProjectTabsState State { get; private set; }

            public ProjectTabsState Load()
            {
                return State;
            }

            public void Save(ProjectTabsState state)
            {
                State = state;
            }
        }
    }
}
