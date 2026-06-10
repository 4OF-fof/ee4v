using System;
using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
using Ee4v.Core.Testing;
using NUnit.Framework;
using SQLite;

namespace Ee4v.AssetManager.Api.Tests
{
    public sealed class AssetManagerApiTests
    {
        private string _tempRoot;
        private string _oldGlobalPath;
        private string _oldSourcePriority;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ee4v-asset-manager-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            AssetManagerDefinitions.RegisterAll();
            _oldGlobalPath = SettingApi.Get(AssetManagerDefinitions.Ee4vGlobalPath);
            _oldSourcePriority = SettingApi.Get(AssetManagerDefinitions.SourcePriority);
            SettingApi.Set(AssetManagerDefinitions.Ee4vGlobalPath, _tempRoot, saveImmediately: false);
            SettingApi.Set(AssetManagerDefinitions.SourcePriority, "ee4v,eagle,blm", saveImmediately: false);
        }

        [TearDown]
        public void TearDown()
        {
            SettingApi.Set(AssetManagerDefinitions.Ee4vGlobalPath, _oldGlobalPath, saveImmediately: false);
            SettingApi.Set(AssetManagerDefinitions.SourcePriority, _oldSourcePriority, saveImmediately: false);
            if (!string.IsNullOrWhiteSpace(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        [Test]
        [FeatureTestCase(
            "AssetFile は PrimarySourceType を公開しない",
            "AssetFile の public API が origin 優先度を直接公開しないことを確認します。",
            order: 300)]
        public void AssetFile_DoesNotExposePrimarySourceType()
        {
            Assert.That(typeof(AssetFile).GetProperty("PrimarySourceType"), Is.Null);
        }

        [Test]
        [FeatureTestCase(
            "schema version 1 の DB 制約を作成する",
            "AssetManager DB が schema_version、file_info 制約、collection cycle trigger を作成することを確認します。",
            order: 301)]
        public void Schema_CreatesVersion1Constraints()
        {
            var databasePath = GetDatabasePath();

            AssetManagerApi.GetTags();

            using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.PrivateCache))
            {
                Assert.That(connection.ExecuteScalar<int>("SELECT version FROM schema_version LIMIT 1"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('file_info') WHERE name = 'primary_source_type'"), Is.EqualTo(0));
                Assert.That(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'file_info'"), Does.Contain("CHECK"));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'file_import_target'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'unique_file_import_target_file_path'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = 'prevent_collection_collection_cycle_insert'"), Is.EqualTo(1));
            }
        }

        [Test]
        [FeatureTestCase(
            "collection cycle を拒否する",
            "親 collection を子 collection 配下へ移動しようとした場合に CollectionCycle として拒否することを確認します。",
            order: 302)]
        public void MoveCollection_RejectsCycles()
        {
            var parent = AssetManagerApi.CreateCollection(new CreateCollectionRequest { Name = "Parent" });
            var child = AssetManagerApi.CreateCollection(new CreateCollectionRequest { Name = "Child", ParentCollectionId = parent.Id });

            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.MoveCollection(parent.Id, child.Id));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.CollectionCycle));
        }

        [Test]
        [FeatureTestCase(
            "存在しない item の更新は NotFound",
            "UpdateItem が missing item に対して NotFound を返すことを確認します。",
            order: 303)]
        public void UpdateItem_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.UpdateItem("missing-item", new UpdateAssetItemRequest { Name = "Updated" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "存在しない item の file 取得は NotFound",
            "GetFiles が missing item に対して NotFound を返すことを確認します。",
            order: 304)]
        public void GetFiles_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.GetFiles("missing-item"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "存在しない item の thumbnail 取得は NotFound",
            "GetThumbnail が missing item に対して NotFound を返すことを確認します。",
            order: 305)]
        public void GetThumbnail_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.GetThumbnail("missing-item"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "thumbnail URL がない item は missing thumbnail",
            "thumbnail_url を持たない item の GetThumbnail が missing 結果を返すことを確認します。",
            order: 306)]
        public void GetThumbnail_ItemWithoutThumbnailUrl_ReturnsMissing()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });

            var thumbnail = AssetManagerApi.GetThumbnail(item.Id);

            Assert.That(thumbnail.Found, Is.False);
            Assert.That(thumbnail.Data, Is.Empty);
            Assert.That(thumbnail.MissingReason, Is.Not.Empty);
        }

        [Test]
        [FeatureTestCase(
            "存在しない file の archive は NotFound",
            "ArchiveFile が missing file に対して NotFound を返すことを確認します。",
            order: 307)]
        public void ArchiveFile_MissingFile_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.ArchiveFile("missing-file"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "存在しない tag の設定は NotFound",
            "SetItemTags が missing tag に対して NotFound を返すことを確認します。",
            order: 308)]
        public void SetItemTags_MissingTag_ThrowsNotFound()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });

            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.SetItemTags(item.Id, new[] { "missing-tag" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "存在しない collection 指定では item を作成しない",
            "CreateItem が missing collection を検出した場合に item_info を残さないことを確認します。",
            order: 309)]
        public void CreateItem_MissingCollection_ThrowsNotFoundWithoutCreatingItem()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item", CollectionIds = new[] { "missing-collection" } }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(AssetManagerApi.SearchItems(new AssetItemQuery()).TotalCount, Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "collection 設定失敗時に既存所属を保持する",
            "SetItemCollections が missing collection で失敗しても既存 item_collection を削除しないことを確認します。",
            order: 310)]
        public void SetItemCollections_MissingCollection_DoesNotClearExistingCollections()
        {
            var collection = AssetManagerApi.CreateCollection(new CreateCollectionRequest { Name = "Collection" });
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item", CollectionIds = new[] { collection.Id } });

            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.SetItemCollections(item.Id, new[] { "missing-collection" }));
            var result = AssetManagerApi.SearchItems(new AssetItemQuery { CollectionId = collection.Id });

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(result.Items.Select(collectionItem => collectionItem.Id).ToArray(), Is.EqualTo(new[] { item.Id }));
        }

        [Test]
        [FeatureTestCase(
            "file dependency 設定失敗時に既存依存を保持する",
            "SetFileDependencies が self dependency で失敗しても既存 dependency を削除しないことを確認します。",
            order: 311)]
        public void SetFileDependencies_InvalidRequest_DoesNotClearExistingDependencies()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var dependentPath = Path.Combine(_tempRoot, "dependent.txt");
            var dependencyPath = Path.Combine(_tempRoot, "dependency.txt");
            File.WriteAllText(dependentPath, "dependent");
            File.WriteAllText(dependencyPath, "dependency");
            var dependent = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = dependentPath, FileName = "dependent.txt" });
            var dependency = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = dependencyPath, FileName = "dependency.txt" });
            AssetManagerApi.SetFileDependencies(dependent.Id, new[] { dependency.Id });

            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.SetFileDependencies(dependent.Id, new[] { dependent.Id }));
            var dependencies = AssetManagerApi.GetFileDependencies(dependent.Id);

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(dependencies.Select(itemDependency => itemDependency.DependencyFileId).ToArray(), Is.EqualTo(new[] { dependency.Id }));
        }

        [Test]
        [FeatureTestCase(
            "file import target を複数保持する",
            "SetFileImportTargets が zip / directory 配下の複数 target を file 単位で保存することを確認します。",
            order: 312)]
        public void SetFileImportTargets_StoresMultipleTargetsForFile()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip" });

            AssetManagerApi.SetFileImportTargets(
                file.Id,
                new[]
                {
                    new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" },
                    new AssetFileImportTargetRequest { RelativePath = "\\Textures\\", IsDirectory = true },
                    new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" }
                });

            var targets = AssetManagerApi.GetFileImportTargets(file.Id);

            Assert.That(targets.Select(target => target.RelativePath).ToArray(), Is.EqualTo(new[] { "Packages/avatar.unitypackage", "Textures" }));
            Assert.That(targets.Single(target => target.RelativePath == "Textures").IsDirectory, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "不正な import target path では既存 target を保持する",
            "SetFileImportTargets が parent traversal を拒否し、既存 file_import_target を削除しないことを確認します。",
            order: 313)]
        public void SetFileImportTargets_InvalidPath_DoesNotClearExistingTargets()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip" });
            AssetManagerApi.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" } });

            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = "../outside.unitypackage" } }));
            var targets = AssetManagerApi.GetFileImportTargets(file.Id);

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(targets.Select(target => target.RelativePath).ToArray(), Is.EqualTo(new[] { "Packages/avatar.unitypackage" }));
        }

        [Test]
        [FeatureTestCase(
            "file import target 設定は asset 一覧更新を通知しない",
            "SetFileImportTargets が AssetManagerApi.Changed を発火せず、asset grid reload を誘発しないことを確認します。",
            order: 314)]
        public void SetFileImportTargets_DoesNotRaiseAssetManagerChanged()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip" });
            var changed = false;
            Action handler = () => changed = true;

            AssetManagerApi.Changed += handler;
            try
            {
                AssetManagerApi.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" } });
            }
            finally
            {
                AssetManagerApi.Changed -= handler;
            }

            Assert.That(changed, Is.False);
        }

        [Test]
        [FeatureTestCase(
            "存在しない親 collection 指定では collection を作成しない",
            "CreateCollection が missing parent を検出した場合に collection_info を残さないことを確認します。",
            order: 315)]
        public void CreateCollection_MissingParent_ThrowsNotFoundWithoutCreatingCollection()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.CreateCollection(new CreateCollectionRequest { Name = "Child", ParentCollectionId = "missing-parent" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(AssetManagerApi.GetCollections().Count, Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "不正な smart collection 条件では collection を作成しない",
            "CreateSmartCollection が query text のない条件を拒否し、collection_info を残さないことを確認します。",
            order: 316)]
        public void CreateSmartCollection_InvalidCondition_ThrowsWithoutCreatingCollection()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.CreateSmartCollection(new CreateSmartCollectionRequest
                {
                    Name = "Smart",
                    Conditions = new[] { new SmartCollectionCondition { Field = SmartCollectionConditionField.Name, Operator = SmartCollectionConditionOperator.Contains } }
                }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.InvalidSmartCollectionCondition));
            Assert.That(AssetManagerApi.GetCollections().Count, Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "source priority に従って file path を解決する",
            "ResolveFilePath が assetManager.sourcePriority の順序で origin path を選ぶことを確認します。",
            order: 317)]
        public void ResolveFilePath_UsesConfiguredSourcePriority()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var ee4vPath = Path.Combine(_tempRoot, "manual.txt");
            var eaglePath = Path.Combine(_tempRoot, "eagle-folder");
            File.WriteAllText(ee4vPath, "ee4v");
            Directory.CreateDirectory(eaglePath);
            var file = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = ee4vPath, FileName = "manual.txt" });

            using (var connection = new SQLiteConnection(GetDatabasePath()))
            {
                connection.Execute(
                    "INSERT INTO eagle_file_origin(file_info_id, eagle_item_id, file_path_cache, is_deleted, imported_at) VALUES (?, ?, ?, 0, ?)",
                    file.Id,
                    "eagle-file",
                    eaglePath,
                    DateTime.UtcNow.ToString("O"));
            }

            SettingApi.Set(AssetManagerDefinitions.SourcePriority, "eagle,ee4v,blm", saveImmediately: false);

            var resolved = AssetManagerApi.ResolveFilePath(file.Id);

            Assert.That(resolved.Found, Is.True);
            Assert.That(resolved.SourceType, Is.EqualTo(AssetSourceType.Eagle));
            Assert.That(resolved.Path, Is.EqualTo(eaglePath));
        }

        [Test]
        [FeatureTestCase(
            "最初の手動登録 file は primary になる",
            "RegisterFile が primary 未設定 item の最初の file を自動的に primary にすることを確認します。",
            order: 318)]
        public void RegisterFile_FirstFileBecomesPrimary()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var firstPath = Path.Combine(_tempRoot, "first.txt");
            var secondPath = Path.Combine(_tempRoot, "second.txt");
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(secondPath, "second");

            var first = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = firstPath, FileName = "first.txt" });
            var second = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = secondPath, FileName = "second.txt" });

            Assert.That(first.IsPrimary, Is.True);
            Assert.That(second.IsPrimary, Is.False);
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は VRCAsset folder から item を作成する",
            "SyncEagle が Booth metadata を item 情報として扱い、metadata file を通常 file から除外することを確認します。",
            order: 319)]
        public void SyncEagle_CreatesItemsFromVrcAssetFolders_AndSkipsMetadataFiles()
        {
            var libraryPath = Path.Combine(_tempRoot, "library.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(imagesPath, "boothmeta-entry", "avatar-folder", "_boothmeta", "json", "{\"boothItemId\":123,\"name\":\"Booth Avatar\",\"description\":\"Booth Desc\",\"thumbnailUrl\":\"thumb\",\"shopName\":\"Shop\",\"shopUrl\":\"https://shop.booth.pm\",\"shopThumbnailUrl\":\"shopthumb\",\"downloads\":[]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "unitypackage", null);

            var result = AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var items = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items;

            Assert.That(result.CreatedCount, Is.EqualTo(2));
            Assert.That(result.UpdatedCount, Is.EqualTo(0));
            Assert.That(result.UnchangedCount, Is.EqualTo(0));
            Assert.That(result.ErrorCount, Is.EqualTo(0));
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Name, Is.EqualTo("Booth Avatar"));
            Assert.That(items[0].Booth.BoothItemId, Is.EqualTo(123));
            Assert.That(items[0].Files.Select(file => file.FileName).ToArray(), Is.EqualTo(new[] { "avatar.unitypackage" }));

            var secondResult = AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));

            Assert.That(secondResult.CreatedCount, Is.EqualTo(0));
            Assert.That(secondResult.UpdatedCount, Is.EqualTo(0));
            Assert.That(secondResult.UnchangedCount, Is.EqualTo(2));
            Assert.That(secondResult.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は Booth metadata の downloadId を保存する",
            "importedItemIds が一致する Booth download の downloadId、filename、extension を file_info に保存することを確認します。",
            order: 320)]
        public void SyncEagle_StoresDownloadIdFromBoothMetadata()
        {
            var libraryPath = Path.Combine(_tempRoot, "download-id.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(
                imagesPath,
                "boothmeta-entry",
                "avatar-folder",
                "_boothmeta",
                "json",
                "{\"boothItemId\":123,\"name\":\"Booth Avatar\",\"description\":\"Booth Desc\",\"thumbnailUrl\":\"thumb\",\"shopName\":\"Shop\",\"shopUrl\":\"https://shop.booth.pm\",\"shopThumbnailUrl\":\"shopthumb\",\"downloads\":[{\"downloadId\":456,\"filename\":\"avatar-from-booth.zip\",\"importedItemIds\":[\"file-entry\"]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "unitypackage", null);

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = AssetManagerApi.GetFiles(item.Id).Single();

            Assert.That(file.DownloadId, Is.EqualTo(456));
            Assert.That(file.FileName, Is.EqualTo("avatar-from-booth.zip"));
            Assert.That(file.Extension, Is.EqualTo("zip"));
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は filename 一致で downloadId を補完する",
            "importedItemIds が空でも Booth download filename が一意に一致する場合に downloadId を保存することを確認します。",
            order: 321)]
        public void SyncEagle_StoresDownloadIdWhenImportedItemIdsAreMissingButFilenameMatches()
        {
            var libraryPath = Path.Combine(_tempRoot, "download-filename.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(
                imagesPath,
                "boothmeta-entry",
                "avatar-folder",
                "_boothmeta",
                "json",
                "{\"boothItemId\":123,\"name\":\"Booth Avatar\",\"description\":\"Booth Desc\",\"thumbnailUrl\":\"thumb\",\"shopName\":\"Shop\",\"shopUrl\":\"https://shop.booth.pm\",\"shopThumbnailUrl\":\"shopthumb\",\"downloads\":[{\"downloadId\":456,\"filename\":\"avatar.zip\",\"importedItemIds\":[]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "zip", null);

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = AssetManagerApi.GetFiles(item.Id).Single();

            Assert.That(file.DownloadId, Is.EqualTo(456));
            Assert.That(file.Origins.Single().SourceId, Is.EqualTo("file-entry"));
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は未対応 download も file_info に残す",
            "Eagle item に対応しない Booth download も download_id 付きの file_info として作成することを確認します。",
            order: 322)]
        public void SyncEagle_CreatesDownloadOnlyFilesForUnmatchedBoothDownloads()
        {
            var libraryPath = Path.Combine(_tempRoot, "download-only.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(
                imagesPath,
                "boothmeta-entry",
                "avatar-folder",
                "_boothmeta",
                "json",
                "{\"boothItemId\":123,\"name\":\"Booth Avatar\",\"description\":\"Booth Desc\",\"thumbnailUrl\":\"thumb\",\"shopName\":\"Shop\",\"shopUrl\":\"https://shop.booth.pm\",\"shopThumbnailUrl\":\"shopthumb\",\"downloads\":[{\"downloadId\":456,\"filename\":\"avatar.zip\",\"importedItemIds\":[\"file-entry\"]},{\"downloadId\":789,\"filename\":\"texture.zip\",\"importedItemIds\":[]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "zip", null);

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var files = AssetManagerApi.GetFiles(item.Id).OrderBy(file => file.DownloadId).ToArray();

            Assert.That(files.Select(file => file.DownloadId).ToArray(), Is.EqualTo(new long?[] { 456, 789 }));
            Assert.That(files.Single(file => file.DownloadId == 789).Origins, Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync 失敗は sync_info に failed として残る",
            "存在しない Eagle library を同期した場合に AssetSyncResult と sync_info が Failed になることを確認します。",
            order: 323)]
        public void SyncEagle_MissingLibrary_PersistsFailedSyncInfo()
        {
            var missingLibraryPath = Path.Combine(_tempRoot, "missing.library");

            var result = AssetManagerApi.SyncEagle(new EagleSyncRequest(missingLibraryPath));
            var syncInfo = AssetManagerApi.GetSyncInfo().Single(info => info.SourceType == AssetSourceType.Eagle);

            Assert.That(result.State, Is.EqualTo(AssetSyncState.Failed));
            Assert.That(syncInfo.LastSyncState, Is.EqualTo(AssetSyncState.Failed));
            Assert.That(syncInfo.LastSyncAt, Is.Not.Null);
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync の一部失敗は sync_info に partial として残る",
            "file upsert の一部が失敗した場合に AssetSyncResult と sync_info が Partial になることを確認します。",
            order: 324)]
        public void SyncEagle_FileError_PersistsPartialSyncInfo()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Manual" });
            var manualPath = Path.Combine(_tempRoot, "manual.txt");
            File.WriteAllText(manualPath, "manual");
            var file = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = manualPath, FileName = "manual.txt" });
            using (var connection = new SQLiteConnection(GetDatabasePath()))
            {
                var now = DateTime.UtcNow.ToString("O");
                connection.Execute(
                    "INSERT INTO eagle_file_origin(file_info_id, eagle_item_id, file_path_cache, is_deleted, imported_at) VALUES (?, ?, ?, 0, ?)",
                    file.Id,
                    "file-entry",
                    manualPath,
                    now);
                connection.Execute(
                    @"INSERT INTO file_info(id, item_info_id, file_name, extension, size_bytes, download_id, is_primary, lifecycle, created_at, updated_at)
                      VALUES (?, ?, 'conflict.zip', 'zip', NULL, 456, 0, 'active', ?, ?)",
                    Guid.NewGuid().ToString("N"),
                    item.Id,
                    now,
                    now);
            }

            var libraryPath = Path.Combine(_tempRoot, "partial.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(
                imagesPath,
                "boothmeta-entry",
                "avatar-folder",
                "_boothmeta",
                "json",
                "{\"boothItemId\":123,\"name\":\"Booth Avatar\",\"description\":\"Booth Desc\",\"thumbnailUrl\":\"thumb\",\"shopName\":\"Shop\",\"shopUrl\":\"https://shop.booth.pm\",\"shopThumbnailUrl\":\"shopthumb\",\"downloads\":[{\"downloadId\":456,\"filename\":\"avatar-from-booth.zip\",\"importedItemIds\":[\"file-entry\"]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "unitypackage", null);

            var result = AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var syncInfo = AssetManagerApi.GetSyncInfo().Single(info => info.SourceType == AssetSourceType.Eagle);

            Assert.That(result.State, Is.EqualTo(AssetSyncState.Partial));
            Assert.That(result.ErrorCount, Is.EqualTo(1));
            Assert.That(syncInfo.LastSyncState, Is.EqualTo(AssetSyncState.Partial));
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は datasource text を正規化して保存する",
            "数学英数字や制御文字を含む datasource text が正規化されて item/file 名に保存されることを確認します。",
            order: 325)]
        public void SyncEagle_NormalizesDatasourceTextBeforeSaving()
        {
            var libraryPath = Path.Combine(_tempRoot, "normalized.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"𝒙ero\",\"children\":[]}]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "\\uD835\\uDC99ero", "unitypackage", null);

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

            Assert.That(item.Name, Is.EqualTo("xero"));
            Assert.That(item.Files.Single().FileName, Is.EqualTo("xero.unitypackage"));
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は数学英字を surrogate drop 前に ASCII 化する",
            "数学英字の surrogate pair を落とす前に ASCII へ変換して item/file 名に保存することを確認します。",
            order: 326)]
        public void SyncEagle_MapsMathematicalLettersBeforeDroppingUnsupportedSurrogates()
        {
            var libraryPath = Path.Combine(_tempRoot, "mathematical.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"𝑵𝒐𝒊𝒓_𝑳𝒖𝒙𝒆\",\"children\":[]}]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "𝑵𝒐𝒊𝒓_𝑳𝒖𝒙𝒆", "unitypackage", null);

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

            Assert.That(item.Name, Is.EqualTo("Noir_Luxe"));
            Assert.That(item.Files.Single().FileName, Is.EqualTo("Noir_Luxe.unitypackage"));
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は .info ではなく payload path を保存する",
            "Eagle item の file_path_cache が .info directory ではなく内部 payload file を指すことを確認します。",
            order: 327)]
        public void SyncEagle_StoresPayloadFilePathInsteadOfInfoDirectory()
        {
            var libraryPath = Path.Combine(_tempRoot, "payload-path.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(
                imagesPath,
                "boothmeta-entry",
                "avatar-folder",
                "_boothmeta",
                "json",
                "{\"boothItemId\":123,\"name\":\"Booth Avatar\",\"description\":\"Booth Desc\",\"thumbnailUrl\":\"thumb\",\"shopName\":\"Shop\",\"shopUrl\":\"https://shop.booth.pm\",\"shopThumbnailUrl\":\"shopthumb\",\"downloads\":[{\"downloadId\":456,\"filename\":\"avatar.zip\",\"importedItemIds\":[\"file-entry\"]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "zip", null);
            var payloadPath = Path.Combine(imagesPath, "file-entry.info", "avatar.zip");
            File.WriteAllText(payloadPath, "payload");

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = AssetManagerApi.GetFiles(item.Id).Single();
            var resolved = AssetManagerApi.ResolveFilePath(file.Id);

            Assert.That(file.Origins.Single().FilePathCache, Is.EqualTo(payloadPath));
            Assert.That(resolved.Path, Is.EqualTo(payloadPath));
        }

        [Test]
        [FeatureTestCase(
            "BLM sync は同名 wrapper の内側 path を保存する",
            "BLM item directory の top-level entry が同名 directory を 1 つだけ包む場合に file_path_cache が inner directory を指すことを確認します。",
            order: 328)]
        public void SyncBlm_StoresInnerDirectoryPathForSameNameWrapper()
        {
            var databasePath = Path.Combine(_tempRoot, "blm-data.db");
            var itemDirectoryPath = Path.Combine(_tempRoot, "blm-items");
            var registeredItemId = "registered-item";
            var outerPath = Path.Combine(itemDirectoryPath, registeredItemId, "Avatar");
            var innerPath = Path.Combine(outerPath, "Avatar");
            Directory.CreateDirectory(innerPath);
            File.WriteAllText(Path.Combine(innerPath, "avatar.unitypackage"), "payload");
            CreateBlmDatabase(databasePath, registeredItemId);

            AssetManagerApi.SyncBlm(new BlmSyncRequest(databasePath, itemDirectoryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = AssetManagerApi.GetFiles(item.Id).Single();
            var resolved = AssetManagerApi.ResolveFilePath(file.Id);

            Assert.That(file.FileName, Is.EqualTo("Avatar"));
            Assert.That(file.Origins.Single().FilePathCache, Is.EqualTo(innerPath));
            Assert.That(resolved.Path, Is.EqualTo(innerPath));
        }

        private string GetDatabasePath()
        {
            return Path.Combine(_tempRoot, "asset-manager.db");
        }

        private static void CreateBlmDatabase(string databasePath, string registeredItemId)
        {
            using (var connection = new SQLiteConnection(databasePath))
            {
                connection.Execute("CREATE TABLE registered_items(id TEXT PRIMARY KEY, booth_item_id INTEGER)");
                connection.Execute("CREATE TABLE booth_items(id INTEGER PRIMARY KEY, name TEXT, shop_subdomain TEXT, thumbnail_url TEXT, description TEXT)");
                connection.Execute("CREATE TABLE shops(subdomain TEXT PRIMARY KEY, name TEXT, thumbnail_url TEXT)");
                connection.Execute("CREATE TABLE overwritten_booth_items(booth_item_id INTEGER, name TEXT, description TEXT)");
                connection.Execute("CREATE TABLE booth_item_update_history(booth_item_id INTEGER, last_updated_at TEXT)");
                connection.Execute("INSERT INTO shops(subdomain, name, thumbnail_url) VALUES ('shop', 'Shop', 'shopthumb')");
                connection.Execute("INSERT INTO booth_items(id, name, shop_subdomain, thumbnail_url, description) VALUES (123, 'Booth Avatar', 'shop', 'thumb', 'desc')");
                connection.Execute("INSERT INTO registered_items(id, booth_item_id) VALUES (?, 123)", registeredItemId);
            }
        }

        private static void CreateEagleEntry(string imagesPath, string id, string folderId, string name, string ext, string boothJson)
        {
            var entryPath = Path.Combine(imagesPath, id + ".info");
            Directory.CreateDirectory(entryPath);
            File.WriteAllText(
                Path.Combine(entryPath, "metadata.json"),
                "{\"id\":\"" + id + "\",\"name\":\"" + name + "\",\"ext\":\"" + ext + "\",\"size\":42,\"folders\":[\"" + folderId + "\"],\"isDeleted\":false}");
            if (boothJson != null)
            {
                File.WriteAllText(Path.Combine(entryPath, "_boothmeta.json"), boothJson);
            }
        }
    }
}
