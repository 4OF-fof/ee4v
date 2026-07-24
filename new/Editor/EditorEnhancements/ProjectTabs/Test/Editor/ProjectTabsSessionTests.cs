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
            "Assets の Home タブを常に左端へ構成する",
            "保存tabがないsessionでも、名前を持たない解除・削除不能なHome tabだけが存在することを確認します。",
            order: 5)]
        public void Restore_AlwaysPrependsFixedHomeTab()
        {
            var session = new ProjectTabsSession(
                new MemoryStore(),
                Assets,
                () => "tab");

            Assert.That(session.State.Tabs, Has.Count.EqualTo(1));
            Assert.That(session.State.Tabs[0].IsHome, Is.True);
            Assert.That(session.State.Tabs[0].IsPinned, Is.True);
            Assert.That(
                session.State.Tabs[0].CurrentLocation,
                Is.EqualTo(Assets));
        }

        [Test]
        [FeatureTestCase(
            "タブごとに独立した履歴を保持できる",
            "一方のタブを移動しても、別のタブの現在位置が変わらないことを確認します。",
            order: 10)]
        public void RecordNavigation_KeepsHistoryPerTab()
        {
            var session = CreateSession();
            var firstId = GetFirstRegularTabId(session);
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
            var tabId = GetFirstRegularTabId(session);
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
            var tabId = GetFirstRegularTabId(session);
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
            var tabId = GetFirstRegularTabId(session);
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
            var tabId = GetFirstRegularTabId(session);

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
            var assetsId = GetFirstRegularTabId(session);
            session.RecordNavigation(assetsId, Materials);
            var prefabsId = session.Add(Prefabs);
            var materialsId = session.Add(Materials);

            Assert.That(session.Move(assetsId, 3), Is.True);

            Assert.That(
                session.State.Tabs,
                Has.Count.EqualTo(4));
            Assert.That(session.State.Tabs[0].IsHome, Is.True);
            Assert.That(
                session.State.Tabs[1].Id,
                Is.EqualTo(prefabsId));
            Assert.That(
                session.State.Tabs[2].Id,
                Is.EqualTo(materialsId));
            Assert.That(
                session.State.Tabs[3].Id,
                Is.EqualTo(assetsId));
            Assert.That(
                session.State.Tabs[3].CurrentLocation,
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
            Assert.That(session.State.Tabs, Has.Count.EqualTo(4));
            Assert.That(session.State.Tabs[0].IsHome, Is.True);
            Assert.That(
                session.State.Tabs[2].CurrentLocation,
                Is.EqualTo(Materials));
            Assert.That(
                session.State.Tabs[3].CurrentLocation,
                Is.EqualTo(Prefabs));
        }

        [Test]
        [FeatureTestCase(
            "最後の通常タブを閉じると Home タブだけが残る",
            "Project タブ領域を空にせず、解除・削除不能な Assets の Home タブが残ることを確認します。",
            order: 50)]
        public void Remove_LastRegularTab_PreservesHomeTab()
        {
            var session = CreateSession();
            var tabId = GetFirstRegularTabId(session);
            session.RecordNavigation(tabId, Materials);

            Assert.That(session.Remove(tabId), Is.True);
            Assert.That(session.State.Tabs.Count, Is.EqualTo(1));
            Assert.That(
                session.State.Tabs[0].Id,
                Is.EqualTo(ProjectTabsSession.HomeTabId));
            Assert.That(session.State.Tabs[0].IsHome, Is.True);
            Assert.That(session.State.Tabs[0].IsPinned, Is.True);
            Assert.That(session.State.Tabs[0].History.Count, Is.EqualTo(1));
            Assert.That(
                session.State.Tabs[0].CurrentLocation,
                Is.EqualTo(Assets));
            Assert.That(
                session.Remove(ProjectTabsSession.HomeTabId),
                Is.False);
            Assert.That(
                session.SetPinned(
                    ProjectTabsSession.HomeTabId,
                    false),
                Is.False);
        }

        [Test]
        [FeatureTestCase(
            "ピン止めと解除でタブ位置を変更しない",
            "pin状態だけを切り替え、通常tabとの混在順を維持することを確認します。",
            order: 52)]
        public void SetPinned_PreservesMixedTabOrder()
        {
            var session = CreateSession();
            var assetsId = GetFirstRegularTabId(session);
            var prefabsId = session.Add(Prefabs);

            Assert.That(session.SetPinned(prefabsId, true), Is.True);
            Assert.That(session.State.Tabs[0].IsHome, Is.True);
            Assert.That(session.State.Tabs[1].Id, Is.EqualTo(assetsId));
            Assert.That(session.State.Tabs[1].IsPinned, Is.False);
            Assert.That(session.State.Tabs[2].Id, Is.EqualTo(prefabsId));
            Assert.That(session.State.Tabs[2].IsPinned, Is.True);
            Assert.That(session.SetPinned(assetsId, true), Is.True);
            Assert.That(
                session.State.Tabs.Skip(1).Take(2).Select(tab => tab.Id),
                Is.EqualTo(new[] { assetsId, prefabsId }));

            Assert.That(session.SetPinned(assetsId, false), Is.True);
            Assert.That(session.State.Tabs[0].IsHome, Is.True);
            Assert.That(session.State.Tabs[1].Id, Is.EqualTo(assetsId));
            Assert.That(session.State.Tabs[2].Id, Is.EqualTo(prefabsId));
            Assert.That(session.State.Tabs[1].IsPinned, Is.False);
            Assert.That(session.State.Tabs[2].IsPinned, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "pin tabと通常tabを混在して並び替える",
            "pin境界を設けずに相互の順序を変更でき、Homeだけは左端から移動できないことを確認します。",
            order: 53)]
        public void Move_AllowsMixedTabsButKeepsHomeFixed()
        {
            var session = CreateSession();
            var pinnedId = GetFirstRegularTabId(session);
            session.SetPinned(pinnedId, true);
            var regularId = session.Add(Prefabs);

            Assert.That(session.Move(regularId, 1), Is.True);
            Assert.That(
                session.Move(ProjectTabsSession.HomeTabId, 2),
                Is.False);
            Assert.That(session.State.Tabs[0].IsHome, Is.True);
            Assert.That(session.State.Tabs[1].Id, Is.EqualTo(regularId));
            Assert.That(session.State.Tabs[1].IsPinned, Is.False);
            Assert.That(session.State.Tabs[2].Id, Is.EqualTo(pinnedId));
            Assert.That(session.State.Tabs[2].IsPinned, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "ピン止めタブの場所を固定する",
            "異なるlocationへの移動は新規tabが必要と判定し、pin元の履歴と現在位置を変更しないことを確認します。",
            order: 54)]
        public void PinnedTab_DifferentNavigationRequiresNewTab()
        {
            var session = CreateSession();
            var tabId = GetFirstRegularTabId(session);
            session.RecordNavigation(tabId, Materials);
            session.RecordNavigation(tabId, Prefabs);
            Assert.That(session.SetPinned(tabId, true), Is.True);

            Assert.That(
                session.ShouldOpenInNewTab(tabId, Materials),
                Is.True);
            Assert.That(
                session.RecordNavigation(tabId, Materials),
                Is.False);
            Assert.That(
                session.State.Find(tabId).CurrentLocation,
                Is.EqualTo(Prefabs));
            Assert.That(
                session.State.Find(tabId).History,
                Has.Count.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "ピン止めタブでは同じフォルダの検索状態を更新できる",
            "検索文字列だけの変更では新規tabを作らず、固定folder pathを保ったまま現在状態を更新することを確認します。",
            order: 55)]
        public void PinnedTab_SearchChangeKeepsPinnedFolder()
        {
            var session = CreateSession();
            var tabId = GetFirstRegularTabId(session);
            session.RecordNavigation(tabId, Prefabs);
            Assert.That(session.SetPinned(tabId, true), Is.True);
            var searchedPrefabs = new ProjectTabLocation(
                "prefabs-guid",
                "Assets/Prefabs",
                "t:prefab");

            Assert.That(
                session.ShouldOpenInNewTab(tabId, searchedPrefabs),
                Is.False);
            Assert.That(
                session.RecordNavigation(tabId, searchedPrefabs),
                Is.True);
            Assert.That(
                session.State.Find(tabId).CurrentLocation,
                Is.EqualTo(searchedPrefabs));
            Assert.That(
                session.State.Find(tabId).History,
                Has.Count.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "ピン止め状態を復元してHomeを重複させない",
            "保存snapshotの通常・pin混在順を維持し、固定Home tabを左端へ1つだけ再構成することを確認します。",
            order: 56)]
        public void Restore_PreservesPinnedTabsAndRecreatesSingleHome()
        {
            var store = new MemoryStore();
            var nextId = 0;
            var first = new ProjectTabsSession(
                store,
                Assets,
                () => "tab-" + nextId++);
            var regularId = first.Add(Materials);
            var pinnedId = first.Add(Prefabs);
            first.SetPinned(pinnedId, true);

            var restored = new ProjectTabsSession(
                store,
                Assets,
                () => "restored-" + nextId++);

            Assert.That(restored.State.Tabs, Has.Count.EqualTo(3));
            Assert.That(restored.State.Tabs[0].IsHome, Is.True);
            Assert.That(restored.State.Tabs[1].Id, Is.EqualTo(regularId));
            Assert.That(restored.State.Tabs[1].IsPinned, Is.False);
            Assert.That(restored.State.Tabs[2].Id, Is.EqualTo(pinnedId));
            Assert.That(restored.State.Tabs[2].IsPinned, Is.True);
            Assert.That(
                restored.State.Tabs.Count(tab => tab.IsHome),
                Is.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "横方向のポインター位置からタブ挿入位置を求める",
            "タブの中央を境界として、先頭・中間・末尾の挿入位置を安定して判定することを確認します。",
            order: 57,
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
                        ProjectTabsSession.HomeTabId,
                        string.Empty,
                        "Assets",
                        false,
                        true,
                        true),
                    new ProjectTabViewState(
                        "tab-1",
                        "Materials",
                        "Assets/Materials",
                        true),
                    new ProjectTabViewState(
                        "tab-2",
                        "Prefabs",
                        "Assets/Prefabs",
                        true,
                        true)
                },
                "tab-1",
                false,
                false));

            var scroll = view.Q<ScrollView>();
            var addButton = scroll.contentContainer[
                scroll.contentContainer.childCount - 1] as Button;

            Assert.That(scroll.contentContainer.childCount, Is.EqualTo(4));
            Assert.That(addButton, Is.Not.Null);
            Assert.That(addButton.text, Is.EqualTo("+"));
            Assert.That(
                scroll.contentContainer[0]
                    .ClassListContains(
                        "ee4v-project-tabs__tab--home"),
                Is.True);
            var pinnedTab = scroll.contentContainer[2];
            var pinIcon = pinnedTab.Q<VisualElement>(
                className: "ee4v-project-tabs__pin-icon");
            var title = pinnedTab.Q<VisualElement>(
                className: "ee4v-project-tabs__tab-title");
            Assert.That(pinIcon, Is.Not.Null);
            Assert.That(title, Is.Not.Null);
            Assert.That(
                pinnedTab.IndexOf(pinIcon),
                Is.LessThan(pinnedTab.IndexOf(title)));
        }

        [Test]
        [FeatureTestCase(
            "タブの右クリックでピン止めを切り替える",
            "通常tabはpin切替を通知し、固定Home tabの右クリックでは通知しないことを確認します。",
            order: 62,
            category: FeatureTestCategory.Ui)]
        public void View_ContextClickTogglesPinExceptForHome()
        {
            var view = new ProjectTabsView();
            view.SetState(new ProjectTabsViewState(
                new[]
                {
                    new ProjectTabViewState(
                        ProjectTabsSession.HomeTabId,
                        string.Empty,
                        "Assets",
                        false,
                        true,
                        true),
                    new ProjectTabViewState(
                        "tab-1",
                        "Assets",
                        "Assets",
                        true)
                },
                "tab-1",
                false,
                false));
            var toggledIds = new List<string>();
            view.TabPinToggleRequested += toggledIds.Add;
            var tabs = view.Query<VisualElement>(
                    className: "ee4v-project-tabs__tab")
                .ToList();

            using (var contextClick =
                   ContextClickEvent.GetPooled())
            {
                tabs[1].SendEvent(contextClick);
            }

            using (var contextClick =
                   ContextClickEvent.GetPooled())
            {
                tabs[0].SendEvent(contextClick);
            }

            Assert.That(toggledIds, Is.EqualTo(new[] { "tab-1" }));
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
                            ProjectTabsSession.HomeTabId,
                            string.Empty,
                            "Assets",
                            false,
                            true,
                            true),
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
                var position = tabs[2].worldBound.center;
                using (var pointerDown =
                       PointerDownEvent.GetPooled(new Event
                       {
                           type = EventType.MouseDown,
                           button = 0,
                           mousePosition = position
                       }))
                {
                    tabs[2].SendEvent(pointerDown);
                }

                using (var pointerUp =
                       PointerUpEvent.GetPooled(new Event
                       {
                           type = EventType.MouseUp,
                           button = 0,
                           mousePosition = position
                       }))
                {
                    tabs[2].SendEvent(pointerUp);
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
            var session = new ProjectTabsSession(
                new MemoryStore(),
                Assets,
                () => "tab-" + nextId++);
            session.Add(Assets);
            return session;
        }

        private static string GetFirstRegularTabId(
            ProjectTabsSession session)
        {
            return session.State.Tabs
                .First(tab => !tab.IsHome)
                .Id;
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
