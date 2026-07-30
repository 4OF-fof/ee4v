using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.Application.Tests
{
    public sealed class ImportFileUseCaseTests
    {
        [Test]
        [FeatureTestCase(
            "File import plan を外部 adapter へ渡す",
            "Application が item と file の所属、path、target を検証して正規化済み plan だけを gateway へ渡すことを確認します。",
            order: 4)]
        public void ImportEntry_ValidInput_CreatesNormalizedPlan()
        {
            var ports = new RecordingImportPorts
            {
                Item = new AssetItem { Id = "item-1", Name = "Avatar" },
                Files = new[]
                {
                    new AssetFile
                    {
                        Id = "file-1",
                        ItemId = "item-1",
                        FileName = "avatar.zip",
                        Lifecycle = AssetFileLifecycle.Active
                    }
                },
                Resolution = new AssetFilePathResolution
                {
                    Found = true,
                    Path = "C:/library/avatar.zip"
                },
                GatewayAssetGuids = new[]
                {
                    "ABCDEF0123456789ABCDEF0123456789",
                    "abcdef0123456789abcdef0123456789",
                    "not-a-guid"
                }
            };
            var useCase = new ImportFileUseCase(
                ports,
                ports,
                ports,
                ports,
                ports,
                ports,
                change => ports.PublishedChange = change);

            useCase.ImportEntry("item-1", "file-1", "\\Textures\\body.png/");

            Assert.That(ports.Plan, Is.Not.Null);
            Assert.That(ports.Plan.FileId, Is.EqualTo("file-1"));
            Assert.That(ports.Plan.AssetName, Is.EqualTo("Avatar"));
            Assert.That(ports.Plan.AssetFileName, Is.EqualTo("avatar.zip"));
            Assert.That(ports.Plan.SourcePath, Is.EqualTo("C:/library/avatar.zip"));
            Assert.That(ports.Plan.RelativePaths, Is.EqualTo(new[] { "Textures/body.png" }));
            Assert.That(
                ports.StoredAssetGuids,
                Is.EqualTo(new[]
                {
                    "abcdef0123456789abcdef0123456789"
                }));
            Assert.That(
                ports.PublishedChange.Kind,
                Is.EqualTo(
                    AssetManagerChangeKind.ImportedAssetGuids));
            Assert.That(
                ports.PublishedChange.RelatedId,
                Is.EqualTo("file-1"));
        }

        [Test]
        [FeatureTestCase(
            "存在しない Item の import を拒否する",
            "Item が見つからない場合は file path 解決や gateway 実行へ進まず NotFound を返すことを確認します。",
            order: 5)]
        public void ImportEntry_MissingItem_DoesNotCallGateway()
        {
            var ports = new RecordingImportPorts();
            var useCase = new ImportFileUseCase(
                ports,
                ports,
                ports,
                ports,
                ports,
                ports,
                change => ports.PublishedChange = change);

            var exception = Assert.Throws<AssetManagerException>(
                () => useCase.ImportEntry("missing", "file-1", "avatar.unitypackage"));

            Assert.That(exception.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(ports.GetFilesCallCount, Is.Zero);
            Assert.That(ports.Plan, Is.Null);
        }

        [Test]
        [FeatureTestCase(
            "失敗した import では GUID 関連付けを維持する",
            "UnityPackage のキャンセルまたは失敗時に既存 GUID を空の結果で置換せず、変更通知もしないことを確認します。",
            order: 6)]
        public void ImportEntry_FailedGateway_DoesNotReplaceGuids()
        {
            var ports = new RecordingImportPorts
            {
                Item = new AssetItem
                {
                    Id = "item-1",
                    Name = "Avatar"
                },
                Files = new[]
                {
                    new AssetFile
                    {
                        Id = "file-1",
                        ItemId = "item-1",
                        FileName = "avatar.unitypackage",
                        Lifecycle =
                            AssetFileLifecycle.Active
                    }
                },
                Resolution = new AssetFilePathResolution
                {
                    Found = true,
                    Path =
                        "C:/library/avatar.unitypackage"
                },
                GatewaySucceeded = false
            };
            var useCase = new ImportFileUseCase(
                ports,
                ports,
                ports,
                ports,
                ports,
                ports,
                change => ports.PublishedChange = change);

            useCase.ImportEntry(
                "item-1",
                "file-1",
                "avatar.unitypackage");

            Assert.That(
                ports.ReplaceGuidsCallCount,
                Is.Zero);
            Assert.That(
                ports.PublishedChange,
                Is.Null);
        }

        [Test]
        [FeatureTestCase(
            "依存 File を再帰的に先行 import する",
            "共有する間接 dependency を一度だけ import し、各 gateway 完了後に dependent file へ進むことを確認します。",
            order: 7)]
        public void ImportEntry_RecursiveDependencies_ImportsDependenciesFirst()
        {
            var ports = new RecordingImportPorts
            {
                Item = new AssetItem
                {
                    Id = "item-1",
                    Name = "Avatar"
                },
                Files = new[]
                {
                    File("file-a"),
                    File("file-b"),
                    File("file-c")
                },
                Dependencies =
                    new Dictionary<string, IReadOnlyList<string>>
                    {
                        {
                            "file-a",
                            new[] { "file-b", "file-c" }
                        },
                        {
                            "file-b",
                            new[] { "file-c" }
                        }
                    },
                ImportTargets =
                    new Dictionary<string, IReadOnlyList<AssetFileImportTarget>>
                    {
                        {
                            "file-b",
                            new[]
                            {
                                Target("file-b")
                            }
                        },
                        {
                            "file-c",
                            new[]
                            {
                                Target("file-c")
                            }
                        }
                    },
                Resolution = new AssetFilePathResolution
                {
                    Found = true,
                    Path = "C:/library/file.zip"
                }
            };
            var useCase = new ImportFileUseCase(
                ports,
                ports,
                ports,
                ports,
                ports,
                ports,
                change => ports.PublishedChange = change);

            useCase.ImportEntry(
                "item-1",
                "file-a",
                "content/file-a.prefab");

            Assert.That(
                ports.ImportedFileIds,
                Is.EqualTo(
                    new[]
                    {
                        "file-c",
                        "file-b",
                        "file-a"
                    }));
        }

        private static AssetFile File(string id)
        {
            return new AssetFile
            {
                Id = id,
                ItemId = "item-1",
                FileName = id + ".zip",
                Lifecycle = AssetFileLifecycle.Active
            };
        }

        private static AssetFileImportTarget Target(
            string fileId)
        {
            return new AssetFileImportTarget
            {
                FileId = fileId,
                RelativePath =
                    "content/" + fileId + ".prefab"
            };
        }

        private sealed class RecordingImportPorts :
            IAssetCatalogReadStore,
            IAssetFileReadStore,
            IAssetDependencyReadStore,
            IAssetImportTargetReadStore,
            IImportedAssetGuidCommandStore,
            IAssetImportGateway
        {
            internal AssetItem Item { get; set; }
            internal IReadOnlyList<AssetFile> Files { get; set; } = Array.Empty<AssetFile>();
            internal AssetFilePathResolution Resolution { get; set; }
            internal int GetFilesCallCount { get; private set; }
            internal AssetImportPlan Plan { get; private set; }
            internal IReadOnlyList<string> GatewayAssetGuids { get; set; } =
                Array.Empty<string>();
            internal bool GatewaySucceeded { get; set; } = true;
            internal IReadOnlyList<string> StoredAssetGuids { get; private set; } =
                Array.Empty<string>();
            internal int ReplaceGuidsCallCount { get; private set; }
            internal AssetManagerChange PublishedChange { get; set; }
            internal IReadOnlyDictionary<
                string,
                IReadOnlyList<string>> Dependencies
            {
                get;
                set;
            } = new Dictionary<
                string,
                IReadOnlyList<string>>();
            internal IReadOnlyDictionary<
                string,
                IReadOnlyList<AssetFileImportTarget>>
                ImportTargets
            {
                get;
                set;
            } = new Dictionary<
                string,
                IReadOnlyList<AssetFileImportTarget>>();
            internal List<string> ImportedFileIds
            {
                get;
            } = new List<string>();

            public AssetItem GetItem(string itemId) => Item;

            public AssetCatalogSnapshot LoadCatalogSnapshot() =>
                throw new NotSupportedException();

            public IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query)
            {
                GetFilesCallCount++;
                return Files;
            }

            public AssetFilePathResolution ResolveFilePath(string fileId) => Resolution;

            public AssetFile GetFile(string fileId) =>
                Files == null
                    ? null
                    : System.Linq.Enumerable.FirstOrDefault(
                        Files,
                        file => string.Equals(
                            file.Id,
                            fileId,
                            StringComparison.Ordinal));

            public string GetFileOwnerItemId(string fileId) =>
                GetFile(fileId)?.ItemId;

            public IReadOnlyList<AssetFileDependency>
                GetFileDependencies(string fileId)
            {
                if (!Dependencies.TryGetValue(
                        fileId,
                        out var dependencies))
                {
                    return Array.Empty<
                        AssetFileDependency>();
                }

                return System.Linq.Enumerable
                    .Select(
                        dependencies,
                        dependencyFileId =>
                            new AssetFileDependency
                            {
                                DependentFileId = fileId,
                                DependencyFileId =
                                    dependencyFileId
                            })
                    .ToArray();
            }

            public IReadOnlyList<AssetDependency>
                GetDependencies(
                    DependencyEndpointRequest source) =>
                Array.Empty<AssetDependency>();

            public void Import(
                AssetImportPlan plan,
                Action<AssetImportResult> completed)
            {
                Plan = plan;
                ImportedFileIds.Add(plan.FileId);
                completed?.Invoke(
                    new AssetImportResult(
                        GatewaySucceeded,
                        GatewayAssetGuids));
            }

            public void ReplaceFileImportedAssetGuids(
                string fileId,
                IReadOnlyList<string> assetGuids)
            {
                ReplaceGuidsCallCount++;
                StoredAssetGuids = assetGuids;
            }

            public void SetImportedAssetProtection(
                string assetGuid,
                bool isProtected)
            {
                throw new NotSupportedException();
            }

            public IReadOnlyList<AssetFileImportTarget>
                GetFileImportTargets(string fileId) =>
                ImportTargets.TryGetValue(
                    fileId,
                    out var targets)
                    ? targets
                    : Array.Empty<AssetFileImportTarget>();

            public AssetSearchResult SearchItems(AssetItemQuery query) =>
                throw new NotSupportedException();

            public AssetSearchResult SearchItemSummaries(AssetItemQuery query) =>
                throw new NotSupportedException();

            public AssetThumbnail GetThumbnail(string itemId) =>
                throw new NotSupportedException();

            public IReadOnlyDictionary<string, AssetThumbnail> GetThumbnails(IReadOnlyList<string> itemIds) =>
                throw new NotSupportedException();

            public IReadOnlyList<AssetTag> GetTags(string keyword) =>
                throw new NotSupportedException();

            public IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId) =>
                throw new NotSupportedException();

            public IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId) =>
                throw new NotSupportedException();
        }
    }
}
