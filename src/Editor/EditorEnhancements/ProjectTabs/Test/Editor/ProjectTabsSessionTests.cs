using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

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
        private static readonly ProjectTabLocation Packages =
            new ProjectTabLocation(
                "packages-guid",
                "Packages");
        private static readonly ProjectTabLocation PackageFolder =
            new ProjectTabLocation(
                "package-folder-guid",
                "Packages/dev.4of.ee4v");

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

            Assert.That(session.State.Tabs.Count, Is.EqualTo(1));
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
                session.State.Tabs.Count,
                Is.EqualTo(4));
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

            Assert.That(addedIds.Count, Is.EqualTo(2));
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(session.State.Tabs.Count, Is.EqualTo(4));
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
            "最後の通常タブを閉じても Home を再生成しない",
            "新しいAssets tabを作らず、既存の解除・削除不能なHomeだけがそのまま残ることを確認します。",
            order: 50)]
        public void Remove_LastRegularTab_PreservesHomeTab()
        {
            var nextId = 0;
            var session = new ProjectTabsSession(
                new MemoryStore(),
                Assets,
                () => "tab-" + nextId++);
            var tabId = session.Add(Assets);
            session.RecordNavigation(tabId, Materials);

            Assert.That(session.Remove(tabId), Is.True);
            Assert.That(nextId, Is.EqualTo(1));
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
                session.State.Find(tabId).History.Count,
                Is.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "Home では Assets と Packages の root を開ける",
            "AssetsとPackagesのroot間はHome内で切り替え、どちらかの配下へ" +
            "移動した場合だけ通常tabが必要になることを確認します。",
            order: 54)]
        public void HomeNavigation_KeepsRootsAndOpensChildrenInNewTabs()
        {
            var session = new ProjectTabsSession(
                new MemoryStore(),
                Assets,
                () => "tab");

            Assert.That(
                session.ShouldOpenInNewTab(
                    ProjectTabsSession.HomeTabId,
                    Packages),
                Is.False);
            Assert.That(
                session.RecordNavigation(
                    ProjectTabsSession.HomeTabId,
                    Packages),
                Is.True);
            Assert.That(
                session.State.Tabs[0].CurrentLocation,
                Is.EqualTo(Packages));
            Assert.That(
                session.ShouldOpenInNewTab(
                    ProjectTabsSession.HomeTabId,
                    Assets),
                Is.False);
            Assert.That(
                session.RecordNavigation(
                    ProjectTabsSession.HomeTabId,
                    Assets),
                Is.True);
            Assert.That(
                session.State.Tabs[0].CurrentLocation,
                Is.EqualTo(Assets));
            Assert.That(
                session.ShouldOpenInNewTab(
                    ProjectTabsSession.HomeTabId,
                    Materials),
                Is.True);
            Assert.That(
                session.ShouldOpenInNewTab(
                    ProjectTabsSession.HomeTabId,
                    PackageFolder),
                Is.True);
            Assert.That(
                session.RecordNavigation(
                    ProjectTabsSession.HomeTabId,
                    PackageFolder),
                Is.False);
            Assert.That(
                session.State.Tabs[0].CurrentLocation,
                Is.EqualTo(Assets));
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
                session.State.Find(tabId).History.Count,
                Is.EqualTo(1));
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

            Assert.That(restored.State.Tabs.Count, Is.EqualTo(3));
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
            "起動時にpinとUnity Favoritesを結合する",
            "既存pinをFavoritesへ追加し、既存Favoriteを既存tabのpinへ反映して双方を失わないことを確認します。",
            order: 56)]
        public void FavoriteSync_InitialMergePreservesBothSides()
        {
            var session = CreateSession();
            var assetsId = GetFirstRegularTabId(session);
            session.SetPinned(assetsId, true);
            var materialsId = session.Add(Materials);
            var favorites = new MemoryFavoriteStore(Prefabs, Materials);

            using (new ProjectTabsFavoriteSynchronizer(
                       session,
                       favorites))
            {
                Assert.That(
                    favorites.Locations.Select(
                        location => location.FolderPath),
                    Is.EquivalentTo(new[]
                    {
                        "Assets",
                        "Assets/Materials",
                        "Assets/Prefabs"
                    }));
                Assert.That(
                    session.State.Find(materialsId).IsPinned,
                    Is.True);
                Assert.That(
                    session.State.Tabs.Any(tab =>
                        !tab.IsHome &&
                        tab.IsPinned &&
                        tab.CurrentLocation.Equals(Prefabs)),
                    Is.True);
            }
        }

        [Test]
        [FeatureTestCase(
            "Project tabのpin操作をUnity Favoritesへ反映する",
            "pin追加でfolder Favoriteを作成し、最後の同一folder pin解除でFavoriteを削除することを確認します。",
            order: 56)]
        public void FavoriteSync_TabPinChangesFavorites()
        {
            var session = CreateSession();
            var assetsId = GetFirstRegularTabId(session);
            var duplicateId = session.Add(Assets);
            var favorites = new MemoryFavoriteStore();

            using (new ProjectTabsFavoriteSynchronizer(
                       session,
                       favorites))
            {
                session.SetPinned(assetsId, true);
                session.SetPinned(duplicateId, true);
                Assert.That(
                    favorites.Contains("Assets"),
                    Is.True);

                session.SetPinned(assetsId, false);
                Assert.That(
                    favorites.Contains("Assets"),
                    Is.True);

                session.SetPinned(duplicateId, false);
                Assert.That(
                    favorites.Contains("Assets"),
                    Is.False);
            }
        }

        [Test]
        [FeatureTestCase(
            "Unity Favoritesの変更をProject tabのpinへ反映する",
            "Favorite追加では既存tabを優先してpinし、削除では同一folderのpinをすべて解除することを確認します。",
            order: 56)]
        public void FavoriteSync_ExternalChangesUpdateTabs()
        {
            var session = CreateSession();
            var materialsId = session.Add(Materials);
            var duplicateId = session.Add(Materials);
            var favorites = new MemoryFavoriteStore();

            using (new ProjectTabsFavoriteSynchronizer(
                       session,
                       favorites))
            {
                favorites.SetExternally(Materials);

                Assert.That(
                    session.State.Find(materialsId).IsPinned,
                    Is.True);
                Assert.That(
                    session.State.Find(duplicateId).IsPinned,
                    Is.False);

                session.SetPinned(duplicateId, true);
                favorites.SetExternally();

                Assert.That(
                    session.State.Find(materialsId).IsPinned,
                    Is.False);
                Assert.That(
                    session.State.Find(duplicateId).IsPinned,
                    Is.False);
            }
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

        private sealed class MemoryFavoriteStore
            : IProjectFavoriteFolderStore
        {
            private readonly List<ProjectTabLocation> _locations;

            public MemoryFavoriteStore(
                params ProjectTabLocation[] locations)
            {
                _locations = new List<ProjectTabLocation>(
                    locations ?? System.Array.Empty<ProjectTabLocation>());
            }

            public event System.Action Changed;

            public IReadOnlyList<ProjectTabLocation> Locations
            {
                get { return _locations; }
            }

            public bool TryGetAll(
                out IReadOnlyList<ProjectTabLocation> locations)
            {
                locations = _locations.ToArray();
                return true;
            }

            public bool TryAdd(ProjectTabLocation location)
            {
                if (location == null || Contains(location.FolderPath))
                {
                    return location != null;
                }

                _locations.Add(location);
                Changed?.Invoke();
                return true;
            }

            public bool TryRemove(ProjectTabLocation location)
            {
                if (location == null)
                {
                    return false;
                }

                _locations.RemoveAll(existing =>
                    string.Equals(
                        existing.FolderPath,
                        location.FolderPath,
                        System.StringComparison.Ordinal));
                Changed?.Invoke();
                return true;
            }

            public bool Contains(string folderPath)
            {
                return _locations.Any(location =>
                    string.Equals(
                        location.FolderPath,
                        folderPath,
                        System.StringComparison.Ordinal));
            }

            public void SetExternally(
                params ProjectTabLocation[] locations)
            {
                _locations.Clear();
                _locations.AddRange(
                    locations ??
                    System.Array.Empty<ProjectTabLocation>());
                Changed?.Invoke();
            }
        }
    }
}
