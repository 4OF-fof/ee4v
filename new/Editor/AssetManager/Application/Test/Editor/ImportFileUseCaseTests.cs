using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Testing;
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
                }
            };
            var useCase = new ImportFileUseCase(ports, ports, ports, ports);

            useCase.ImportEntry("item-1", "file-1", "\\Textures\\body.png/");

            Assert.That(ports.Plan, Is.Not.Null);
            Assert.That(ports.Plan.AssetName, Is.EqualTo("Avatar"));
            Assert.That(ports.Plan.AssetFileName, Is.EqualTo("avatar.zip"));
            Assert.That(ports.Plan.SourcePath, Is.EqualTo("C:/library/avatar.zip"));
            Assert.That(ports.Plan.RelativePaths, Is.EqualTo(new[] { "Textures/body.png" }));
        }

        [Test]
        [FeatureTestCase(
            "存在しない Item の import を拒否する",
            "Item が見つからない場合は file path 解決や gateway 実行へ進まず NotFound を返すことを確認します。",
            order: 5)]
        public void ImportEntry_MissingItem_DoesNotCallGateway()
        {
            var ports = new RecordingImportPorts();
            var useCase = new ImportFileUseCase(ports, ports, ports, ports);

            var exception = Assert.Throws<AssetManagerException>(
                () => useCase.ImportEntry("missing", "file-1", "avatar.unitypackage"));

            Assert.That(exception.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(ports.GetFilesCallCount, Is.Zero);
            Assert.That(ports.Plan, Is.Null);
        }

        private sealed class RecordingImportPorts :
            IAssetCatalogReadStore,
            IAssetFileReadStore,
            IAssetImportTargetReadStore,
            IAssetImportGateway
        {
            internal AssetItem Item { get; set; }
            internal IReadOnlyList<AssetFile> Files { get; set; } = Array.Empty<AssetFile>();
            internal AssetFilePathResolution Resolution { get; set; }
            internal int GetFilesCallCount { get; private set; }
            internal AssetImportPlan Plan { get; private set; }

            public AssetItem GetItem(string itemId) => Item;

            public IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query)
            {
                GetFilesCallCount++;
                return Files;
            }

            public AssetFilePathResolution ResolveFilePath(string fileId) => Resolution;

            public void Import(AssetImportPlan plan)
            {
                Plan = plan;
            }

            public IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId) =>
                Array.Empty<AssetFileImportTarget>();

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
