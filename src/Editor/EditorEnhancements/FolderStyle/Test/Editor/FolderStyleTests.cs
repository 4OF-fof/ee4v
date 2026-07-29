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
            "FolderStyle の設定は既定で有効",
            "初回起動時からAlt操作でフォルダー装飾を編集できることを確認します。",
            order: 10)]
        public void Definition_IsEnabledByDefault()
        {
            Assert.That(
                FolderStyleDefinitions.Enabled.DefaultValue,
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "FolderStyle のgrid配置を計算できる",
            "Project Window のgrid表示で元のフォルダーアイコンと同じ領域へ装飾を描画できることを確認します。",
            order: 20)]
        public void Layout_UsesProjectFolderIconRect()
        {
            var itemRect = new Rect(
                10f,
                20f,
                64f,
                80f);

            var iconRect = ProjectItemLayout.GetIconRect(
                itemRect,
                ProjectItemViewMode.TwoColumns,
                ProjectItemOrientation.Vertical);

            Assert.That(iconRect.x, Is.EqualTo(9f));
            Assert.That(iconRect.y, Is.EqualTo(19f));
            Assert.That(iconRect.width, Is.EqualTo(66f));
            Assert.That(
                iconRect.height,
                Is.EqualTo(62.7f).Within(0.001f));
        }

        [Test]
        [FeatureTestCase(
            "FolderStyle のプリセットは旧版同様に陰影を残す",
            "プリセットが半透明で、フォルダーアイコン本来の陰影を潰さずに着色できることを確認します。",
            order: 25)]
        public void ColorPresets_PreserveFolderIconShading()
        {
            var presets = FolderStyleColorPresets.GetAll();

            Assert.That(presets.Count, Is.EqualTo(12));
            for (var i = 0; i < presets.Count; i++)
            {
                Assert.That(
                    presets[i].a,
                    Is.EqualTo(0.7f).Within(0.001f));
            }
        }

        [Test]
        [FeatureTestCase(
            "選択中も装飾アイコンの背景色を変えない",
            "元アイコンの消去には選択色ではなくProject表示方式ごとの通常背景色を使い、大きな選択色の矩形を作らないことを確認します。",
            order: 27)]
        public void RendererBackground_UsesStableProjectBackground()
        {
            Assert.That(
                FolderStyleRenderer.ResolveBackgroundColor(
                    ProjectItemViewMode.TwoColumns,
                    ProjectItemOrientation.Vertical,
                    true),
                Is.EqualTo((Color)
                    new Color32(51, 51, 51, 255)));
            Assert.That(
                FolderStyleRenderer.ResolveBackgroundColor(
                    ProjectItemViewMode.TwoColumns,
                    ProjectItemOrientation.Horizontal,
                    true),
                Is.EqualTo((Color)
                    new Color32(56, 56, 56, 255)));
        }

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

        [Test]
        [FeatureTestCase(
            "FolderStyle popupをdesktop内へ収める",
            "Project itemが画面端にある場合も編集popup全体がdesktop bounds内へ配置されることを確認します。",
            order: 80)]
        public void WindowLayout_ClampsToDesktopBounds()
        {
            var desktop =
                new Rect(100f, 50f, 800f, 600f);

            var result =
                PopupWindowLayout.ClampToDesktop(
                        new Vector2(850f, 620f),
                        new Vector2(360f, 268f),
                        desktop);

            Assert.That(
                result.xMax,
                Is.EqualTo(desktop.xMax));
            Assert.That(
                result.yMax,
                Is.EqualTo(desktop.yMax));
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
