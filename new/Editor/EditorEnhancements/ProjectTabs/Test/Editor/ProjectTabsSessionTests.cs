using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            "タブを任意の位置へ並び替えられる",
            "並び替え後も各タブの ID と履歴を維持し、変更した順序が保存されることを確認します。",
            order: 45)]
        public void Move_ReordersTabsWithoutChangingTheirHistory()
        {
            var session = CreateSession();
            var assetsId = session.State.Tabs[0].Id;
            session.RecordNavigation(assetsId, Materials);
            var prefabsId = session.Add(Prefabs);
            var materialsId = session.Add(Materials);

            Assert.That(session.Move(assetsId, 2), Is.True);

            Assert.That(
                session.State.Tabs,
                Has.Count.EqualTo(3));
            Assert.That(
                session.State.Tabs[0].Id,
                Is.EqualTo(prefabsId));
            Assert.That(
                session.State.Tabs[1].Id,
                Is.EqualTo(materialsId));
            Assert.That(
                session.State.Tabs[2].Id,
                Is.EqualTo(assetsId));
            Assert.That(
                session.State.Tabs[2].CurrentLocation,
                Is.EqualTo(Materials));
        }

        [Test]
        [FeatureTestCase(
            "複数フォルダを一度の変更としてタブへ追加する",
            "ドロップされたフォルダ順を保ち、タブ一覧の更新通知を一度だけ発行することを確認します。",
            order: 47)]
        public void AddRange_AppendsLocationsWithSingleNotification()
        {
            var session = CreateSession();
            var changeCount = 0;
            session.Changed += () => changeCount++;

            var addedIds = session.AddRange(
                new[] { Materials, Prefabs });

            Assert.That(addedIds, Has.Count.EqualTo(2));
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(session.State.Tabs, Has.Count.EqualTo(3));
            Assert.That(
                session.State.Tabs[1].CurrentLocation,
                Is.EqualTo(Materials));
            Assert.That(
                session.State.Tabs[2].CurrentLocation,
                Is.EqualTo(Prefabs));
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
            "横方向のポインター位置からタブ挿入位置を求める",
            "タブの中央を境界として、先頭・中間・末尾の挿入位置を安定して判定することを確認します。",
            order: 55,
            category: FeatureTestCategory.Ui)]
        public void View_FindInsertionIndex_UsesTabCenters()
        {
            var centers = new[] { 20f, 60f, 100f };

            Assert.That(
                ProjectTabsView.FindInsertionIndex(centers, 0f),
                Is.EqualTo(0));
            Assert.That(
                ProjectTabsView.FindInsertionIndex(centers, 59f),
                Is.EqualTo(1));
            Assert.That(
                ProjectTabsView.FindInsertionIndex(centers, 120f),
                Is.EqualTo(3));
        }

        [Test]
        [FeatureTestCase(
            "Project フォルダだけをドロップ対象として解決する",
            "存在しないpathを除外し、同じfolderが複数含まれても1つのtab候補だけを返すことを確認します。",
            order: 57)]
        public void FolderDropResolver_AcceptsValidUniqueFolders()
        {
            var resolver = new UnityProjectTabFolderDropResolver();

            var locations = resolver.Resolve(
                new[]
                {
                    "Assets",
                    "Assets/ee4v-folder-that-does-not-exist",
                    "Assets"
                });

            Assert.That(locations, Has.Count.EqualTo(1));
            Assert.That(locations[0].FolderPath, Is.EqualTo("Assets"));
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
            "ドラッグ可能なタブを通常クリックで切り替える",
            "ポインターをcaptureしても、ドラッグ閾値を超えずに離した場合はクリックしたタブを選択することを確認します。",
            order: 65,
            category: FeatureTestCategory.Ui)]
        public IEnumerator View_ClickWithoutDraggingSelectsTab()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            window.position = new Rect(0f, 0f, 600f, 200f);
            window.Show();

            try
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
                window.rootVisualElement.Add(view);
                yield return null;

                var selectedTabId = string.Empty;
                view.TabSelected += tabId =>
                    selectedTabId = tabId;
                var tabs = view.Query<VisualElement>(
                        className: "ee4v-project-tabs__tab")
                    .ToList();
                var position = tabs[1].worldBound.center;
                using (var pointerDown =
                       PointerDownEvent.GetPooled(new Event
                       {
                           type = EventType.MouseDown,
                           button = 0,
                           mousePosition = position
                       }))
                {
                    tabs[1].SendEvent(pointerDown);
                }

                using (var pointerUp =
                       PointerUpEvent.GetPooled(new Event
                       {
                           type = EventType.MouseUp,
                           button = 0,
                           mousePosition = position
                       }))
                {
                    tabs[1].SendEvent(pointerUp);
                }

                Assert.That(selectedTabId, Is.EqualTo("tab-2"));
            }
            finally
            {
                window.Close();
            }
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
