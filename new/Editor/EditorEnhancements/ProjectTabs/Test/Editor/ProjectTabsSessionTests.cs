using System.Collections.Generic;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
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
            "最後のタブは閉じられない",
            "Project タブ領域が空にならないことを確認します。",
            order: 50)]
        public void Remove_PreservesLastTab()
        {
            var session = CreateSession();
            var tabId = session.State.Tabs[0].Id;

            Assert.That(session.Remove(tabId), Is.False);
            Assert.That(session.State.Tabs.Count, Is.EqualTo(1));
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
