using System;
using System.Collections.Generic;
using Ee4v.Testing.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.HierarchyStyle.Tests
{
    public sealed class HierarchyStyleTests
    {
        [Test]
        [FeatureTestCase(
            "最も近い背景色を子へ継承する",
            "自身からrootへ並べた設定のうち最初の明示背景色を採用し、子の個別設定を優先することを確認します。",
            order: 20)]
        public void Inheritance_PrefersNearestExplicitColor()
        {
            var childColor = new Color(
                0.2f,
                0.4f,
                0.8f,
                0.3f);
            var parentColor = new Color(
                0.8f,
                0.3f,
                0.2f,
                0.3f);

            var resolved =
                HierarchyStyleInheritance
                    .TryResolveBackgroundColor(
                        new[]
                        {
                            HierarchyStyleValue.Empty(
                                "grand-child"),
                            new HierarchyStyleValue(
                                "child",
                                true,
                                childColor,
                                string.Empty),
                            new HierarchyStyleValue(
                                "parent",
                                true,
                                parentColor,
                                string.Empty)
                        },
                        out var color);

            Assert.That(resolved, Is.True);
            Assert.That(color, Is.EqualTo(childColor));
        }

        [Test]
        [FeatureTestCase(
            "アイコン設定だけでは背景継承を止めない",
            "子に個別アイコンがあっても背景色が未設定なら親の背景色を継承することを確認します。",
            order: 30)]
        public void Inheritance_ContinuesPastIconOnlyStyle()
        {
            var parentColor = new Color(
                0.1f,
                0.6f,
                0.4f,
                0.3f);

            var resolved =
                HierarchyStyleInheritance
                    .TryResolveBackgroundColor(
                        new[]
                        {
                            new HierarchyStyleValue(
                                "child",
                                false,
                                Color.clear,
                                "icon-guid"),
                            new HierarchyStyleValue(
                                "parent",
                                true,
                                parentColor,
                                string.Empty)
                        },
                        out var color);

            Assert.That(resolved, Is.True);
            Assert.That(color, Is.EqualTo(parentColor));
        }

        [Test]
        [FeatureTestCase(
            "背景色とアイコンを独立して更新できる",
            "背景色の変更でアイコンを失わず、アイコン解除後も背景色が残ることを確認します。",
            order: 40)]
        public void Service_PreservesColorAndIconIndependently()
        {
            var repository =
                new MemoryHierarchyStyleRepository();
            var service =
                new HierarchyStyleService(repository);
            var targets = new[] { "object-id" };
            var color = new Color(
                0.2f,
                0.4f,
                0.8f,
                0.3f);

            service.SetBackgroundColor(targets, color);
            service.SetIcon(targets, "icon-guid");
            service.SetIcon(targets, string.Empty);

            var style = service.Get("object-id");
            Assert.That(
                style.HasBackgroundColor,
                Is.True);
            Assert.That(
                style.BackgroundColor,
                Is.EqualTo(color));
            Assert.That(style.HasIcon, Is.False);
            Assert.That(repository.SaveCount, Is.EqualTo(3));
        }

        [Test]
        [FeatureTestCase(
            "複数オブジェクトを一括保存できる",
            "重複したobject IDを除外し、複数対象の変更を1回の永続化として扱うことを確認します。",
            order: 50)]
        public void Service_SavesBatchOnce()
        {
            var repository =
                new MemoryHierarchyStyleRepository();
            var service =
                new HierarchyStyleService(repository);

            service.SetBackgroundColor(
                new[] { "a", "a", "b" },
                Color.cyan);

            Assert.That(repository.Count, Is.EqualTo(2));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "Alt押下中の多重起動を防止する",
            "同じHierarchy項目上でAltを押し続けても編集画面の起動要求が1回だけになることを確認します。",
            order: 60)]
        public void AltTrigger_ActivatesOnceUntilReleased()
        {
            var trigger =
                new HierarchyStyleAltTrigger();

            Assert.That(
                trigger.TryActivate(42, true, true),
                Is.True);
            Assert.That(
                trigger.TryActivate(42, true, true),
                Is.False);
            Assert.That(
                trigger.TryActivate(42, false, true),
                Is.False);
            Assert.That(
                trigger.TryActivate(42, true, true),
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "選択外オブジェクトへ一括適用しない",
            "カーソル下の項目が複数選択に含まれない場合、その項目だけを編集対象にすることを確認します。",
            order: 70)]
        public void Selection_RequiresHoveredObjectInBatch()
        {
            Assert.That(
                HierarchyStyleSelection.ResolveTargetIds(
                    9,
                    new[] { 1, 2 }),
                Is.EqualTo(new[] { 9 }));
            Assert.That(
                HierarchyStyleSelection.ResolveTargetIds(
                    1,
                    new[] { 1, 2 }),
                Is.EqualTo(new[] { 1, 2 }));
        }

        private sealed class MemoryHierarchyStyleRepository
            : IHierarchyStyleRepository
        {
            private readonly Dictionary<
                string,
                HierarchyStyleValue> _styles =
                    new Dictionary<
                        string,
                        HierarchyStyleValue>(
                        StringComparer.Ordinal);
            private readonly List<string> _recentIconGuids =
                new List<string>();

            public int SaveCount { get; private set; }

            public int Count
            {
                get { return _styles.Count; }
            }

            public HierarchyStyleValue Get(string objectId)
            {
                return _styles.TryGetValue(
                    objectId,
                    out var style)
                    ? style
                    : null;
            }

            public void Put(HierarchyStyleValue style)
            {
                if (style.IsEmpty)
                {
                    _styles.Remove(style.ObjectId);
                    return;
                }

                _styles[style.ObjectId] = style;
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

            public void Save()
            {
                SaveCount++;
            }
        }
    }
}
