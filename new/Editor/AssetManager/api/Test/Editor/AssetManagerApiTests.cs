using System;
using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
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
        public void AssetFile_DoesNotExposePrimarySourceType()
        {
            Assert.That(typeof(AssetFile).GetProperty("PrimarySourceType"), Is.Null);
        }

        [Test]
        public void Schema_CreatesVersion1Constraints()
        {
            var databasePath = GetDatabasePath();

            AssetManagerApi.GetTags();

            using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.PrivateCache))
            {
                Assert.That(connection.ExecuteScalar<int>("SELECT version FROM schema_version LIMIT 1"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('file_info') WHERE name = 'primary_source_type'"), Is.EqualTo(0));
                Assert.That(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'file_info'"), Does.Contain("CHECK"));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = 'prevent_collection_collection_cycle_insert'"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MoveCollection_RejectsCycles()
        {
            var parent = AssetManagerApi.CreateCollection(new CreateCollectionRequest { Name = "Parent" });
            var child = AssetManagerApi.CreateCollection(new CreateCollectionRequest { Name = "Child", ParentCollectionId = parent.Id });

            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.MoveCollection(parent.Id, child.Id));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.CollectionCycle));
        }

        [Test]
        public void UpdateItem_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.UpdateItem("missing-item", new UpdateAssetItemRequest { Name = "Updated" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        public void GetFiles_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.GetFiles("missing-item"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        public void ArchiveFile_MissingFile_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.ArchiveFile("missing-file"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        public void SetItemTags_MissingTag_ThrowsNotFound()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });

            var ex = Assert.Throws<AssetManagerException>(() => AssetManagerApi.SetItemTags(item.Id, new[] { "missing-tag" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        public void CreateItem_MissingCollection_ThrowsNotFoundWithoutCreatingItem()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item", CollectionIds = new[] { "missing-collection" } }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(AssetManagerApi.SearchItems(new AssetItemQuery()).TotalCount, Is.EqualTo(0));
        }

        [Test]
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
        public void CreateCollection_MissingParent_ThrowsNotFoundWithoutCreatingCollection()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                AssetManagerApi.CreateCollection(new CreateCollectionRequest { Name = "Child", ParentCollectionId = "missing-parent" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(AssetManagerApi.GetCollections().Count, Is.EqualTo(0));
        }

        [Test]
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

        private string GetDatabasePath()
        {
            return Path.Combine(_tempRoot, "asset-manager.db");
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
