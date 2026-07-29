using System;
using System.Collections.Generic;
using Ee4v.Core.Injector;
using Ee4v.Testing.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.FolderStyle.Tests
{
    public sealed class FolderStyleTests
    {
        [Test]
        [FeatureTestCase(
            "色とアイコンを独立して更新できる",
            "色の変更でアイコンを失わず、アイコン解除後に設定済みの色へ戻れることを確認します。",
            order: 30)]
        public void Service_PreservesColorAndIconIndependently()
        {
            var repository =
                new MemoryFolderStyleRepository();
            var service =
                new FolderStyleService(repository);
            var targets = new[] { "folder-guid" };
            var color = new Color(
                0.2f,
                0.4f,
                0.8f,
                1f);

            service.SetColor(targets, color);
            service.SetIcon(targets, "icon-guid");
            service.SetIcon(targets, string.Empty);

            var style = service.Get("folder-guid");
            Assert.That(style.HasColor, Is.True);
            Assert.That(style.Color, Is.EqualTo(color));
            Assert.That(style.HasIcon, Is.False);
            Assert.That(repository.SaveCount, Is.EqualTo(3));
        }

        [Test]
        [FeatureTestCase(
            "複数フォルダーを一括保存できる",
            "重複GUIDを除外し、複数フォルダーの編集を1回の永続化として扱うことを確認します。",
            order: 40)]
        public void Service_SavesBatchOnce()
        {
            var repository =
                new MemoryFolderStyleRepository();
            var service =
                new FolderStyleService(repository);

            service.SetColor(
                new[] { "a", "a", "b" },
                Color.cyan);

            Assert.That(repository.Count, Is.EqualTo(2));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "Alt押下中の多重起動を防止する",
            "同じフォルダー上でAltを押し続けても編集画面の起動要求が1回だけになることを確認します。",
            order: 50)]
        public void AltTrigger_ActivatesOnceUntilReleased()
        {
            var trigger = new FolderStyleAltTrigger();

            Assert.That(
                trigger.TryActivate("folder", true, true),
                Is.True);
            Assert.That(
                trigger.TryActivate("folder", true, true),
                Is.False);
            Assert.That(
                trigger.TryActivate("folder", false, true),
                Is.False);
            Assert.That(
                trigger.TryActivate("folder", true, true),
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "選択外フォルダーへ一括適用しない",
            "カーソル下のフォルダーが複数選択に含まれない場合、そのフォルダーだけを編集対象にすることを確認します。",
            order: 60)]
        public void Selection_RequiresHoveredFolderInBatch()
        {
            Assert.That(
                FolderStyleSelection.ResolveTargets(
                    "hovered",
                    new[] { "first", "second" }),
                Is.EqualTo(new[] { "hovered" }));
            Assert.That(
                FolderStyleSelection.ResolveTargets(
                    "first",
                    new[] { "first", "second" }),
                Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        [FeatureTestCase(
            "最近使ったアイコンを新しい順で保持する",
            "同じアイコンの重複を除き、候補数を上限以内に保ちながら最新の選択を先頭へ移動することを確認します。",
            order: 70)]
        public void Service_RecordsBoundedRecentIcons()
        {
            var repository =
                new MemoryFolderStyleRepository();
            var service =
                new FolderStyleService(repository);
            var targets = new[] { "folder" };

            for (var i = 0; i < 10; i++)
            {
                service.SetIcon(
                    targets,
                    "icon-" + i);
            }

            service.SetIcon(targets, "icon-5");

            Assert.That(
                service.GetRecentIconGuids().Count,
                Is.EqualTo(
                    FolderStyleService.RecentIconLimit));
            Assert.That(
                service.GetRecentIconGuids()[0],
                Is.EqualTo("icon-5"));
            Assert.That(
                service.GetRecentIconGuids(),
                Is.Unique);
        }

        [Test]
        [FeatureTestCase(
            "アイコン候補を履歴だけから削除できる",
            "右クリックしたアイコンGUIDを履歴から即時削除し、変更があった場合だけ保存することを確認します。",
            order: 75)]
        public void Service_RemovesRecentIconImmediately()
        {
            var repository =
                new MemoryFolderStyleRepository();
            var service =
                new FolderStyleService(repository);
            var targets = new[] { "folder" };

            service.SetIcon(targets, "first");
            service.SetIcon(targets, "second");
            var saveCountBeforeRemoval =
                repository.SaveCount;

            service.RemoveRecentIcon("first");
            service.RemoveRecentIcon("missing");

            Assert.That(
                service.GetRecentIconGuids(),
                Is.EqualTo(new[] { "second" }));
            Assert.That(
                repository.SaveCount,
                Is.EqualTo(saveCountBeforeRemoval + 1));
            Assert.That(
                service.Get("folder").IconGuid,
                Is.EqualTo("second"));
        }

        [Test]
        [FeatureTestCase(
            "popup表示中はアイコン履歴の順番を固定する",
            "アイコン選択で永続履歴が更新されても表示中snapshotは並び替えず、開き直したsnapshotだけに最新順を反映することを確認します。",
            order: 77)]
        public void RecentIconSession_KeepsOrderUntilReopened()
        {
            var repository =
                new MemoryFolderStyleRepository();
            var service =
                new FolderStyleService(repository);
            var targets = new[] { "folder" };
            service.SetIcon(targets, "first");
            service.SetIcon(targets, "second");
            var openSession =
                new DecorationRecentIconSession(
                    service.GetRecentIconGuids());

            service.SetIcon(targets, "first");

            Assert.That(
                openSession.IconGuids,
                Is.EqualTo(
                    new[] { "second", "first" }));
            var reopenedSession =
                new DecorationRecentIconSession(
                    service.GetRecentIconGuids());
            Assert.That(
                reopenedSession.IconGuids,
                Is.EqualTo(
                    new[] { "first", "second" }));
        }

        private sealed class MemoryFolderStyleRepository
            : IFolderStyleRepository
        {
            private readonly Dictionary<string, FolderStyleValue>
                _styles =
                    new Dictionary<string, FolderStyleValue>(
                        StringComparer.Ordinal);
            private readonly List<string> _recentIconGuids =
                new List<string>();

            public int SaveCount { get; private set; }

            public int Count
            {
                get { return _styles.Count; }
            }

            public FolderStyleValue Get(string folderGuid)
            {
                return _styles.TryGetValue(
                    folderGuid,
                    out var style)
                    ? style
                    : null;
            }

            public void Put(FolderStyleValue style)
            {
                if (style.IsEmpty)
                {
                    _styles.Remove(style.FolderGuid);
                    return;
                }

                _styles[style.FolderGuid] = style;
            }

            public void Save()
            {
                SaveCount++;
            }

            public IReadOnlyList<string>
                GetRecentIconGuids()
            {
                return _recentIconGuids.ToArray();
            }

            public void RecordRecentIcon(
                string iconGuid,
                int maximumCount)
            {
                _recentIconGuids.RemoveAll(
                    guid => string.Equals(
                        guid,
                        iconGuid,
                        StringComparison.Ordinal));
                _recentIconGuids.Insert(0, iconGuid);
                if (_recentIconGuids.Count >
                    maximumCount)
                {
                    _recentIconGuids.RemoveRange(
                        maximumCount,
                        _recentIconGuids.Count -
                        maximumCount);
                }
            }

            public bool RemoveRecentIcon(
                string iconGuid)
            {
                return _recentIconGuids.RemoveAll(
                    guid => string.Equals(
                        guid,
                        iconGuid,
                        StringComparison.Ordinal)) > 0;
            }
        }
    }
}
