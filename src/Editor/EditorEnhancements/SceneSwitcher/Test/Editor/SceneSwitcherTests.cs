using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.SceneSwitcher.Tests
{
    public sealed class SceneSwitcherTests
    {
        [Test]
        [FeatureTestCase(
            "Scene一覧の同期で利用者の状態を保持する",
            "削除済みSceneを除去し、新規Sceneを追加しても既存の並び、お気に入り、除外状態が維持されることを確認します。",
            order: 0)]
        public void Synchronize_PreservesKnownMetadataAndOrder()
        {
            var current = new[]
            {
                new SceneSwitcherRecord(
                    "Assets/B.unity",
                    isFavorite: true),
                new SceneSwitcherRecord(
                    "Assets/Missing.unity",
                    isIgnored: true),
                new SceneSwitcherRecord(
                    "Assets/A.unity",
                    isIgnored: true)
            };

            var result = SceneSwitcherPolicy.Synchronize(
                current,
                new[]
                {
                    "Assets/C.unity",
                    "Assets/A.unity",
                    "Assets/B.unity",
                    "Assets/NotScene.asset"
                });

            Assert.That(
                result.Select(record => record.Path),
                Is.EqualTo(new[]
                {
                    "Assets/B.unity",
                    "Assets/A.unity",
                    "Assets/C.unity"
                }));
            Assert.That(result[0].IsFavorite, Is.True);
            Assert.That(result[1].IsIgnored, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "開いているSceneとお気に入りを優先表示する",
            "保存順を各グループ内で保ちながら、open、favorite、otherの順で表示することを確認します。",
            order: 10)]
        public void BuildView_PrioritizesOpenThenFavorites()
        {
            var records = new[]
            {
                new SceneSwitcherRecord("Assets/Other.unity"),
                new SceneSwitcherRecord(
                    "Assets/Favorite.unity",
                    isFavorite: true),
                new SceneSwitcherRecord("Assets/Open.unity"),
                new SceneSwitcherRecord(
                    "Assets/Ignored.unity",
                    isIgnored: true,
                    isFavorite: true)
            };

            var state = SceneSwitcherPolicy.BuildView(
                records,
                new[] { "Assets/Open.unity" },
                string.Empty);

            Assert.That(
                state.Items.Select(item => item.Name),
                Is.EqualTo(new[]
                {
                    "Open",
                    "Favorite",
                    "Other"
                }));
            Assert.That(state.Items[0].IsOpen, Is.True);
            Assert.That(state.Items[1].IsFavorite, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "Scene名とフォルダーを検索する",
            "大文字小文字を区別せずScene名と相対フォルダーを検索し、完全一致がない場合だけ作成候補を出すことを確認します。",
            order: 20)]
        public void BuildView_SearchesNameAndFolderAndOffersCreation()
        {
            var records = new[]
            {
                new SceneSwitcherRecord(
                    "Assets/Worlds/Main.unity"),
                new SceneSwitcherRecord(
                    "Assets/Tests/Sandbox.unity")
            };

            var folderMatch = SceneSwitcherPolicy.BuildView(
                records,
                new string[0],
                "worlds");
            var exactMatch = SceneSwitcherPolicy.BuildView(
                records,
                new string[0],
                "MAIN");
            var newScene = SceneSwitcherPolicy.BuildView(
                records,
                new string[0],
                "Lobby");

            Assert.That(
                folderMatch.Items.Single().Name,
                Is.EqualTo("Main"));
            Assert.That(exactMatch.CanCreate, Is.False);
            Assert.That(newScene.CanCreate, Is.True);
            Assert.That(newScene.Items, Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "表示順の変更を永続順へ反映する",
            "並べ替え対象外の除外Sceneを失わず、表示されたSceneの順だけを先頭へ反映することを確認します。",
            order: 30)]
        public void Reorder_PreservesRecordsOutsideVisibleOrder()
        {
            var records = new[]
            {
                new SceneSwitcherRecord("Assets/A.unity"),
                new SceneSwitcherRecord(
                    "Assets/Hidden.unity",
                    isIgnored: true),
                new SceneSwitcherRecord("Assets/B.unity")
            };

            var result = SceneSwitcherPolicy.Reorder(
                records,
                new[]
                {
                    "Assets/B.unity",
                    "Assets/A.unity"
                });

            Assert.That(
                result.Select(record => record.Path),
                Is.EqualTo(new[]
                {
                    "Assets/B.unity",
                    "Assets/A.unity",
                    "Assets/Hidden.unity"
                }));
            Assert.That(result[2].IsIgnored, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "Scene作成先と名前を安全に正規化する",
            "Assets配下だけを受け付け、フォルダー区切りを含むScene名を拒否することを確認します。",
            order: 40)]
        public void CreationPolicy_RestrictsFolderAndSceneName()
        {
            Assert.That(
                SceneSwitcherPolicy.NormalizeAssetFolder(
                    " Assets\\Worlds\\ "),
                Is.EqualTo("Assets/Worlds"));
            Assert.That(
                SceneSwitcherPolicy.NormalizeAssetFolder(
                    "../Outside"),
                Is.Empty);
            Assert.That(
                SceneSwitcherPolicy.NormalizeAssetFolder(
                    "Assets/../Outside"),
                Is.Empty);
            Assert.That(
                SceneSwitcherPolicy.IsValidSceneName("Lobby"),
                Is.True);
            Assert.That(
                SceneSwitcherPolicy.IsValidSceneName("Worlds/Lobby"),
                Is.False);
        }

        [Test]
        [FeatureTestCase(
            "Scene Switcherは既定で有効",
            "Hierarchyの入口と新規Scene保存先の既定値を確認します。",
            order: 50)]
        public void Definitions_HaveExpectedDefaults()
        {
            Assert.That(
                SceneSwitcherDefinitions.Enabled.DefaultValue,
                Is.EqualTo(true));
            Assert.That(
                SceneSwitcherDefinitions.CreateFolder.DefaultValue,
                Is.EqualTo("Assets/Scene"));
        }

        [Test]
        [FeatureTestCase(
            "popupを開いたSceneを切替元として渡す",
            "open状態のSceneを選択した場合も、HierarchyでクリックしたScene handleと切替先をUnity adapterへ渡すことを確認します。",
            order: 60)]
        public void Activate_ForwardsSourceSceneToGateway()
        {
            const string path = "Assets/Open.unity";
            const int sourceSceneHandle = 42;
            var repository = new MemoryRepository(
                new[] { new SceneSwitcherRecord(path) });
            var gateway = new RecordingGateway(
                new[] { path });
            var controller = new SceneSwitcherController(
                repository,
                gateway);

            var activated = controller.Activate(
                path,
                sourceSceneHandle);

            Assert.That(activated, Is.True);
            Assert.That(gateway.LastSwitchedPath, Is.EqualTo(path));
            Assert.That(
                gateway.LastSourceSceneHandle,
                Is.EqualTo(sourceSceneHandle));
        }

        [Test]
        [FeatureTestCase(
            "右クリックではSceneを追加する",
            "切替元Sceneを渡さず、対象Sceneをadditiveで開く操作だけをUnity adapterへ通知することを確認します。",
            order: 65)]
        public void Add_ForwardsTargetToGateway()
        {
            const string path = "Assets/Added.unity";
            var repository = new MemoryRepository(
                new[] { new SceneSwitcherRecord(path) });
            var gateway = new RecordingGateway(
                Array.Empty<string>());
            var controller = new SceneSwitcherController(
                repository,
                gateway);

            var added = controller.Add(path);

            Assert.That(added, Is.True);
            Assert.That(gateway.LastAddedPath, Is.EqualTo(path));
            Assert.That(gateway.LastSwitchedPath, Is.Null);
        }

        [Test]
        [FeatureTestCase(
            "popup配置を旧Scene Switcherと揃える",
            "Hierarchy左端48px、右端46px、行高16pxのanchorを使い、scrollbar分だけ縮むことを確認します。",
            order: 70)]
        public void HierarchyTrigger_UsesLegacyPopupAnchor()
        {
            var withoutScrollbar =
                SceneSwitcherHierarchyTrigger.GetAnchorRect(
                    new Rect(0f, 24f, 320f, 18f),
                    320f);
            var withScrollbar =
                SceneSwitcherHierarchyTrigger.GetAnchorRect(
                    new Rect(0f, 24f, 306f, 18f),
                    320f);

            Assert.That(
                withoutScrollbar,
                Is.EqualTo(new Rect(
                    48f,
                    24f,
                    226f,
                    16f)));
            Assert.That(
                withScrollbar,
                Is.EqualTo(new Rect(
                    48f,
                    24f,
                    212f,
                    16f)));
        }

        private sealed class MemoryRepository
            : ISceneSwitcherRepository
        {
            private IReadOnlyList<SceneSwitcherRecord> _records;

            public MemoryRepository(
                IReadOnlyList<SceneSwitcherRecord> records)
            {
                _records = records ??
                    Array.Empty<SceneSwitcherRecord>();
            }

            public IReadOnlyList<SceneSwitcherRecord> Load()
            {
                return _records;
            }

            public void Save(
                IReadOnlyList<SceneSwitcherRecord> records)
            {
                _records = records ??
                    Array.Empty<SceneSwitcherRecord>();
            }
        }

        private sealed class RecordingGateway
            : ISceneSwitcherGateway
        {
            private readonly IReadOnlyList<string> _openPaths;

            public RecordingGateway(
                IReadOnlyList<string> openPaths)
            {
                _openPaths = openPaths ??
                    Array.Empty<string>();
            }

            public string LastSwitchedPath { get; private set; }

            public string LastAddedPath { get; private set; }

            public int LastSourceSceneHandle { get; private set; }

            public IReadOnlyList<string> FindScenePaths()
            {
                return Array.Empty<string>();
            }

            public IReadOnlyList<string> GetOpenScenePaths()
            {
                return _openPaths;
            }

            public SceneOperationResult SwitchScene(
                string path,
                int sourceSceneHandle)
            {
                LastSwitchedPath = path;
                LastSourceSceneHandle = sourceSceneHandle;
                return new SceneOperationResult(true, path: path);
            }

            public SceneOperationResult AddScene(string path)
            {
                LastAddedPath = path;
                return new SceneOperationResult(true, path: path);
            }

            public SceneOperationResult CreateScene(
                string folder,
                string sceneName)
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.Failed);
            }
        }
    }
}
