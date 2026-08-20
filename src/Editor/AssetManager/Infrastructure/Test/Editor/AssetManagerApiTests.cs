using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure.Ee4v;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.AssetManager.Infrastructure.Tests
{
    public sealed class AssetManagerApiTests
    {
        private string _root;
        private string _databasePath;
        private IAssetManager _manager;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "ee4v-asset-manager-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _databasePath = Path.Combine(_root, "asset-manager.db");
            _manager = AssetManagerFactory.Open(_databasePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void CollectionSearch_EvaluatesNestedLogicAndHierarchicalTags()
        {
            var tagged = _manager.CreateItem(
                new CreateAssetItemRequest
                {
                    Name = "Avatar Alpha",
                    Description = "hidden"
                });
            _manager.SetItemTags(
                new[] { tagged.Id },
                new[] { "#Foo/Bar" });
            var visible = _manager.CreateItem(
                new CreateAssetItemRequest
                {
                    Name = "Other Visible",
                    Description = "visible"
                });
            var collection = _manager.CreateCollection(
                new CreateAssetCollectionRequest
                {
                    Name = "Selection",
                    Root = AssetFilterNode.Or(
                        AssetFilterNode.And(
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.NameContains,
                                "avatar"),
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.HasTag,
                                "foo")),
                        AssetFilterNode.Not(
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.DescriptionContains,
                                "hidden")))
                });

            var result = _manager.SearchCollection(collection.Id);

            Assert.That(
                result.Items.Select(item => item.Id),
                Is.EquivalentTo(new[] { tagged.Id, visible.Id }));
        }

        [Test]
        public void MatchesCollection_ReevaluatesOnlyTheRequestedItem()
        {
            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Before" });
            var collection = _manager.CreateCollection(
                new CreateAssetCollectionRequest
                {
                    Name = "After items",
                    Root = AssetFilterNode.Condition(
                        AssetFilterConditionType.NameContains,
                        "after")
                });

            Assert.That(
                _manager.MatchesCollection(collection.Id, item.Id),
                Is.False);

            _manager.UpdateItem(
                item.Id,
                new UpdateAssetItemRequest { Name = "After" });

            Assert.That(
                _manager.MatchesCollection(collection.Id, item.Id),
                Is.True);
        }

        [Test]
        public void CollectionName_MustBeUnique()
        {
            var root = AssetFilterNode.Condition(
                AssetFilterConditionType.NameContains,
                "avatar");
            _manager.CreateCollection(
                new CreateAssetCollectionRequest
                {
                    Name = "Unique",
                    Root = root
                });

            var exception = Assert.Throws<AssetManagerException>(() =>
                _manager.CreateCollection(
                    new CreateAssetCollectionRequest
                    {
                        Name = "Unique",
                        Root = root
                    }));

            Assert.That(
                exception.Code,
                Is.EqualTo(AssetManagerErrorCode.Duplicate));
        }

        [Test]
        public void ChangeSubscriberFailure_DoesNotStopOtherSubscribers()
        {
            var observed = 0;
            _manager.Changed += _ =>
                throw new InvalidOperationException("broken subscriber");
            _manager.Changed += _ => observed++;

            Assert.DoesNotThrow(() =>
                _manager.CreateItem(
                    new CreateAssetItemRequest { Name = "Item" }));

            Assert.That(observed, Is.EqualTo(1));
        }

        [Test]
        public void ItemAndCollectionChanges_IdentifyMutationAndSubjects()
        {
            var changes = new List<AssetManagerChange>();
            _manager.Changed += changes.Add;

            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Before" });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.ItemCreated,
                new[] { item.Id });

            changes.Clear();
            _manager.UpdateItem(
                item.Id,
                new UpdateAssetItemRequest { Name = "After" });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.ItemUpdated,
                new[] { item.Id });

            changes.Clear();
            _manager.SetItemTags(new[] { item.Id }, new[] { "avatar" });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.ItemTagsChanged,
                new[] { item.Id });

            changes.Clear();
            _manager.SetItemArchived(new[] { item.Id }, true);
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.ItemArchiveChanged,
                new[] { item.Id });

            changes.Clear();
            _manager.DeleteItem(new[] { item.Id });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.ItemDeleted,
                new[] { item.Id });

            changes.Clear();
            var collection = _manager.CreateCollection(
                new CreateAssetCollectionRequest
                {
                    Name = "Before",
                    Root = AssetFilterNode.Condition(
                        AssetFilterConditionType.NameContains,
                        "avatar")
                });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.CollectionCreated,
                new[] { collection.Id });

            changes.Clear();
            _manager.UpdateCollection(
                collection.Id,
                new UpdateAssetCollectionRequest
                {
                    Name = "After",
                    Root = AssetFilterNode.Condition(
                        AssetFilterConditionType.HasTag,
                        "avatar")
                });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.CollectionUpdated,
                new[] { collection.Id });

            changes.Clear();
            _manager.DeleteCollection(collection.Id);
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.CollectionDeleted,
                new[] { collection.Id });
        }

        [Test]
        public void FileChanges_IdentifyFilesAndAffectedItems()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var first = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "First" });
            var second = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Second" });
            var changes = new List<AssetManagerChange>();
            _manager.Changed += changes.Add;

            var file = _manager.RegisterFile(
                first.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.FileCreated,
                new[] { file.Id },
                new[] { first.Id });

            changes.Clear();
            _manager.SetFileItem(new[] { file.Id }, second.Id);
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.FilePlacementChanged,
                new[] { file.Id },
                new[] { first.Id, second.Id });

            changes.Clear();
            _manager.SetFileArchived(new[] { file.Id }, true);
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.FileArchiveChanged,
                new[] { file.Id },
                new[] { second.Id });

            changes.Clear();
            _manager.SetItemTargets(
                second.Id,
                new[]
                {
                    new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = "Assets/Sample.prefab"
                    }
                });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.ItemTargetsChanged,
                new[] { second.Id },
                new[] { file.Id });

            changes.Clear();
            _manager.SetFileDependencies(
                new[] { file.Id },
                Array.Empty<AssetFileTarget>());
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.FileDependenciesChanged,
                new[] { file.Id });

            changes.Clear();
            _manager.DeleteFile(new[] { file.Id });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.FileDeleted,
                new[] { file.Id },
                new[] { second.Id });
        }

        [Test]
        public void EagleSync_ReusesIdsAndDeletesMissingFile()
        {
            var library = Path.Combine(_root, "test.library");
            var images = Path.Combine(library, "images");
            var entry = Path.Combine(images, "file-entry.info");
            Directory.CreateDirectory(entry);
            var folderMetadata = Path.Combine(library, "metadata.json");
            WriteFolderMetadata(folderMetadata, "Before");
            File.WriteAllText(
                Path.Combine(entry, "metadata.json"),
                "{\"id\":\"file-entry\",\"name\":\"avatar\",\"ext\":\"zip\",\"folders\":[\"avatar-folder\"],\"isDeleted\":false}");
            File.WriteAllText(Path.Combine(entry, "avatar.zip"), "payload");

            _manager.SyncEagle(new EagleSyncRequest(library));
            var before = _manager.SearchItems().Items.Single();
            var beforeFile = before.Files.Single();

            WriteFolderMetadata(folderMetadata, "After");
            _manager.SyncEagle(new EagleSyncRequest(library));
            var renamed = _manager.SearchItems().Items.Single();

            Assert.That(renamed.Id, Is.EqualTo(before.Id));
            Assert.That(renamed.Name, Is.EqualTo("After"));

            Directory.Delete(entry, true);
            var removedFile = _manager.SyncEagle(
                new EagleSyncRequest(library));

            Assert.That(
                removedFile.DeletedFileIds,
                Is.EqualTo(new[] { beforeFile.Id }));
            Assert.That(_manager.GetItem(before.Id).Files, Is.Empty);
        }

        [Test]
        public void EagleSync_PreservesManagedTagsAndTargets()
        {
            var library = Path.Combine(_root, "normalized.library");
            var entry = Path.Combine(
                library,
                "images",
                "file-entry.info");
            Directory.CreateDirectory(entry);
            WriteFolderMetadata(
                Path.Combine(library, "metadata.json"),
                "Ｌｅｍｏｎ\u2B52\u260E🍋");
            File.WriteAllText(
                Path.Combine(entry, "metadata.json"),
                "{\"id\":\"file-entry\",\"name\":\"𝑵𝒐𝒊𝒓\u2B52\u260E🍋\",\"ext\":\"ｚｉｐ🍋\",\"folders\":[\"avatar-folder\"],\"tags\":[\"Ｆｏｏ\u2B52\u260E🍋\",\"boothmeta\",\"VRCMeta\"],\"isDeleted\":false}");
            using (var stream = File.Create(
                       Path.Combine(entry, "payload.zip")))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Create))
            using (var writer = new StreamWriter(
                       archive.CreateEntry("Assets/Sample.prefab").Open()))
            {
                writer.Write("prefab");
            }
            var metadataEntry = Path.Combine(
                library,
                "images",
                "booth-entry.info");
            Directory.CreateDirectory(metadataEntry);
            File.WriteAllText(
                Path.Combine(metadataEntry, "metadata.json"),
                "{\"id\":\"booth-entry\",\"name\":\"booth\",\"ext\":\"json\",\"folders\":[\"avatar-folder\"],\"tags\":[\"BoothMeta\",\"internal-only\"],\"isDeleted\":false}");
            File.WriteAllText(
                Path.Combine(metadataEntry, "booth.json"),
                "{\"boothItemId\":1,\"itemUrl\":\"https://booth.pm/ja/items/1\",\"name\":\"Ｌｅｍｏｎ\u2B52\u260E🍋\",\"description\":\"\",\"thumbnailUrl\":\"\",\"shopName\":\"Ｌｅｍｏｎ Ｓｔｏｒｅ🍋\",\"shopUrl\":\"https://lemon.booth.pm\"}");

            _manager.SyncEagle(new EagleSyncRequest(library));
            var item = _manager.SearchItems().Items.Single();

            Assert.That(item.Name, Is.EqualTo("Lemon*"));
            Assert.That(item.Files.Single().FileName, Is.EqualTo("Noir*.zip"));
            Assert.That(item.Files.Single().Extension, Is.EqualTo("zip"));
            Assert.That(item.Tags, Is.Empty);
            Assert.That(
                item.Booth.ItemUrl,
                Is.EqualTo("https://booth.pm/ja/items/1"));
            Assert.That(item.Booth.ShopName, Is.EqualTo("Lemon Store"));
            Assert.That(
                item.Booth.ShopUrl,
                Is.EqualTo("https://lemon.booth.pm"));

            _manager.SetItemTags(
                new[] { item.Id },
                new[] { "Managed" });
            var file = item.Files.Single();
            _manager.SetItemTargets(
                item.Id,
                new[] { Target(file, "Assets/Sample.prefab") });
            _manager.SyncEagle(new EagleSyncRequest(library));

            var synchronized = _manager.GetItem(item.Id);
            Assert.That(
                synchronized.Tags.Single().Path,
                Is.EqualTo("managed"));
            Assert.That(
                _manager.GetItemTargets(item.Id).Single().TargetPath,
                Is.EqualTo("Assets/Sample.prefab"));
        }

        [Test]
        public void EagleSync_MissingTargetPreservesData()
        {
            var library = Path.Combine(_root, "test.library");
            var entry = Path.Combine(
                library,
                "images",
                "file-entry.info");
            Directory.CreateDirectory(entry);
            var folderMetadata = Path.Combine(library, "metadata.json");
            WriteFolderMetadata(folderMetadata, "Avatar");
            File.WriteAllText(
                Path.Combine(entry, "metadata.json"),
                "{\"id\":\"file-entry\",\"name\":\"avatar\",\"ext\":\"zip\",\"folders\":[\"avatar-folder\"],\"isDeleted\":false}");
            File.WriteAllText(Path.Combine(entry, "avatar.zip"), "payload");
            _manager.SyncEagle(new EagleSyncRequest(library));
            var item = _manager.SearchItems().Items.Single();
            var file = item.Files.Single();

            var missingTarget = _manager.SyncEagle(
                new EagleSyncRequest(library, "Missing"));

            Assert.That(
                missingTarget.State,
                Is.EqualTo(AssetSyncState.Failed));
            Assert.That(missingTarget.ErrorMessages, Is.Not.Empty);
            Assert.That(_manager.GetItem(item.Id).Id, Is.EqualTo(item.Id));
            Assert.That(_manager.GetFile(file.Id).Id, Is.EqualTo(file.Id));

        }

        [Test]
        public void ThumbnailApis_ReturnMissingWithoutAThumbnailSource()
        {
            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "No thumbnail" });

            var thumbnail = _manager.GetThumbnail(item.Id)
                .GetAwaiter().GetResult();
            var thumbnails = _manager.GetThumbnails(new[] { item.Id })
                .GetAwaiter().GetResult();

            Assert.That(thumbnail.Found, Is.False);
            Assert.That(thumbnail.Data, Is.Empty);
            Assert.That(thumbnails[item.Id].Found, Is.False);
        }

        [Test]
        public void FileRegistration_RejectsDirectoriesFromEverySource()
        {
            var eagleLibrary = Path.Combine(_root, "test.library");
            var images = Path.Combine(eagleLibrary, "images");
            var entry = Path.Combine(images, "directory-entry.info");
            var payload = Path.Combine(entry, "avatar");
            Directory.CreateDirectory(payload);
            WriteFolderMetadata(
                Path.Combine(eagleLibrary, "metadata.json"),
                "Avatar");
            File.WriteAllText(
                Path.Combine(entry, "metadata.json"),
                "{\"id\":\"directory-entry\",\"name\":\"avatar\",\"folders\":[\"avatar-folder\"],\"isDeleted\":false}");

            _manager.SyncEagle(new EagleSyncRequest(eagleLibrary));

            Assert.That(_manager.SearchItems().Items.Single().Files, Is.Empty);
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.RegisterFile(
                        null,
                        new RegisterFileRequest
                        {
                            LibraryPath = Path.Combine(_root, "ee4v"),
                            FilePath = payload
                        })).Code,
                Is.EqualTo(AssetManagerErrorCode.DatasourceError));
        }

        [Test]
        public void Ee4vImport_PairRestoresCatalogAfterDatabaseRebuild()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var imported = _manager.ImportEe4vFile(
                new ImportEe4vFileRequest(
                    library,
                    sourcePath,
                    "Before",
                    "description",
                    new[] { "#Foo/Bar" }));
            _manager.UpdateItem(
                imported.Id,
                new UpdateAssetItemRequest
                {
                    Name = "After",
                    Description = "updated"
                });
            _manager.SetItemTags(
                new[] { imported.Id },
                new[] { "#Updated/Tag" });

            File.Delete(_databasePath);
            _manager = AssetManagerFactory.Open(_databasePath);
            var sync = _manager.SyncEe4v(new Ee4vSyncRequest(library));
            var restored = _manager.SearchItems().Items.Single();

            Assert.That(sync.State, Is.EqualTo(AssetSyncState.Success));
            Assert.That(restored.SourceId, Is.EqualTo(imported.SourceId));
            Assert.That(restored.Name, Is.EqualTo("After"));
            Assert.That(restored.Description, Is.EqualTo("updated"));
            Assert.That(restored.Tags.Single().Path, Is.EqualTo("updated/tag"));
            Assert.That(File.Exists(restored.Files.Single().SourcePath), Is.True);
        }

        [Test]
        public void RegisterFile_RestoresUnassignedPairAfterDatabaseRebuild()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var unassigned = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });

            File.Delete(_databasePath);
            _manager = AssetManagerFactory.Open(_databasePath);
            var sync = _manager.SyncEe4v(new Ee4vSyncRequest(library));

            Assert.That(sync.State, Is.EqualTo(AssetSyncState.Success));
            Assert.That(
                _manager.GetUnassignedFiles().Single().FileName,
                Is.EqualTo(unassigned.FileName));
        }

        [Test]
        public void Ee4vDeleteStaging_RestoresSourceUntilCommitted()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var file = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });
            var source = new Ee4vAssetSource();

            using (source.BeginDelete(new[] { file }))
            {
                Assert.That(File.Exists(file.SourcePath), Is.False);
            }

            Assert.That(File.Exists(file.SourcePath), Is.True);
            _manager.DeleteFile(new[] { file.Id });
            Assert.That(File.Exists(file.SourcePath), Is.False);
        }

        [Test]
        public void ArchiveOperations_UpdateMultipleTargetsAndCanRestore()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var first = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "First" });
            var second = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Second" });
            var firstFile = _manager.RegisterFile(
                first.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "first.zip"
                });
            var secondFile = _manager.RegisterFile(
                second.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "second.zip"
                });

            var archivedItems = _manager.SetItemArchived(
                new[] { first.Id, second.Id },
                true);
            var archivedFiles = _manager.SetFileArchived(
                new[] { firstFile.Id, secondFile.Id },
                true);

            Assert.That(archivedItems.All(item => item.IsArchived), Is.True);
            Assert.That(archivedFiles.All(file => file.IsArchived), Is.True);
            Assert.That(_manager.SearchItems().Items, Is.Empty);
            Assert.That(
                _manager.SearchItems(new AssetItemQuery
                {
                    IncludeArchived = true
                }).Items.Count,
                Is.EqualTo(2));
            Assert.That(_manager.GetFiles(first.Id), Is.Empty);
            Assert.That(_manager.GetFiles(first.Id, true).Single().IsArchived, Is.True);

            _manager.SetItemArchived(
                new[] { first.Id, second.Id },
                false);
            _manager.SetFileArchived(
                new[] { firstFile.Id, secondFile.Id },
                false);

            Assert.That(_manager.SearchItems().Items.Count, Is.EqualTo(2));
            Assert.That(_manager.GetFiles(first.Id).Count, Is.EqualTo(1));
        }

        [Test]
        public void DeleteItem_DeletesMultipleItemsAndTheirEe4vFiles()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var first = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "First" });
            var second = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Second" });
            var firstFile = _manager.RegisterFile(
                first.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "first.zip"
                });
            var secondFile = _manager.RegisterFile(
                second.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "second.zip"
                });
            var firstEntry = Path.GetDirectoryName(firstFile.SourcePath);
            var secondEntry = Path.GetDirectoryName(secondFile.SourcePath);

            _manager.DeleteItem(new[] { first.Id, second.Id });

            Assert.That(Directory.Exists(firstEntry), Is.False);
            Assert.That(Directory.Exists(secondEntry), Is.False);
            Assert.Throws<AssetManagerException>(() =>
                _manager.GetItem(first.Id));
            Assert.Throws<AssetManagerException>(() =>
                _manager.GetFile(secondFile.Id));
        }

        [Test]
        public void DeleteFile_DeletesMultipleEe4vSourcePairs()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var first = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "first.zip"
                });
            var second = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "second.zip"
                });
            var firstEntry = Path.GetDirectoryName(first.SourcePath);
            var secondEntry = Path.GetDirectoryName(second.SourcePath);

            _manager.DeleteFile(new[] { first.Id, second.Id });

            Assert.That(Directory.Exists(firstEntry), Is.False);
            Assert.That(Directory.Exists(secondEntry), Is.False);
            Assert.That(_manager.GetUnassignedFiles(), Is.Empty);
            Assert.Throws<AssetManagerException>(() =>
                _manager.GetFile(first.Id));
        }

        [Test]
        public void FileDependencies_RejectCyclesWithoutChangingData()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "source.txt");
            File.WriteAllText(sourcePath, "payload");
            var firstItem = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "First" });
            var secondItem = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Second" });
            var first = _manager.RegisterFile(
                firstItem.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "first.txt"
                });
            var third = _manager.RegisterFile(
                secondItem.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "third.txt"
                });

            _manager.SetFileDependencies(
                new[] { first.Id },
                new[] { Target(third) });

            Assert.That(
                _manager.GetFileDependencies(first.Id)
                    .Single().DependencyFileId,
                Is.EqualTo(third.Id));
            var exception = Assert.Throws<AssetManagerException>(() =>
                _manager.SetFileDependencies(
                    new[] { third.Id },
                    new[] { Target(first) }));

            Assert.That(
                exception.Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(
                _manager.GetFileDependencies(third.Id),
                Is.Empty);
            Assert.That(
                _manager.GetFileDependencies(first.Id)
                    .Single().DependencyFileId,
                Is.EqualTo(third.Id));
        }

        [Test]
        public void ImportItemTargets_ImportsDependencyTargetsBeforeDependent()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var dependencySource = Path.Combine(_root, "dependency.zip");
            using (var stream = File.Create(dependencySource))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Create))
            {
                WriteArchiveEntry(archive, "shared.txt", "dependency");
                WriteArchiveEntry(archive, "ignored.txt", "ignored");
            }
            var dependentSource = Path.Combine(_root, "dependent.txt");
            File.WriteAllText(dependentSource, "dependent");
            var itemName = "DependencyOrder" +
                           Guid.NewGuid().ToString("N");
            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = itemName });
            var dependency = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = dependencySource,
                    FileName = "shared.zip"
                });
            var dependent = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = dependentSource,
                    FileName = "shared.txt"
                });
            _manager.SetItemTargets(
                item.Id,
                new[] { Target(dependent) });
            _manager.SetFileDependencies(
                new[] { dependent.Id },
                new[] { Target(dependency, "shared.txt") });

            var assetPath = "Assets/" + itemName;
            try
            {
                var result = _manager.ImportItemTargets(
                        item.Id,
                        Array.Empty<AssetFileTarget>())
                    .GetAwaiter().GetResult();

                Assert.That(result.Succeeded, Is.True);
                Assert.That(
                    result.FileIds,
                    Is.EqualTo(new[] { dependency.Id, dependent.Id }));

                var destination = Path.Combine(
                    UnityEngine.Application.dataPath,
                    itemName,
                    "shared");
                Assert.That(
                    File.ReadAllText(Path.Combine(
                        destination,
                        "shared.txt")),
                    Is.EqualTo("dependent"));
                Assert.That(
                    File.Exists(Path.Combine(destination, "ignored.txt")),
                    Is.False);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ImportFileEntries_ImportsTemporaryZipSelectionWithoutSavingTarget()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "temporary-entry.zip");
            using (var archiveStream = File.Create(sourcePath))
            using (var archive = new ZipArchive(
                       archiveStream,
                       ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(
                           archive.CreateEntry(
                                   "temporary-entry/configured.txt")
                               .Open()))
                {
                    writer.Write("configured");
                }

                using (var writer = new StreamWriter(
                           archive.CreateEntry(
                                   "temporary-entry/temporary.txt")
                               .Open()))
                {
                    writer.Write("temporary");
                }
            }

            var itemName = "TemporaryEntry" +
                           Guid.NewGuid().ToString("N");
            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = itemName });
            var file = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });
            _manager.SetItemTargets(
                item.Id,
                new[] { Target(file, "configured.txt") });

            var assetPath = "Assets/" + itemName;
            try
            {
                var entryResult = _manager.ImportFileEntries(
                        file.Id,
                        new[] { "temporary.txt" })
                    .GetAwaiter().GetResult();
                var destination = Path.Combine(
                    UnityEngine.Application.dataPath,
                    itemName,
                    "temporary-entry");

                Assert.That(entryResult.Succeeded, Is.True);
                Assert.That(entryResult.FileIds, Is.EqualTo(new[] { file.Id }));
                Assert.That(
                    File.ReadAllText(Path.Combine(
                        destination,
                        "temporary.txt")),
                    Is.EqualTo("temporary"));
                Assert.That(
                    File.Exists(Path.Combine(
                        destination,
                        "configured.txt")),
                    Is.False);
                Assert.That(
                    _manager.GetItemTargets(item.Id)
                        .Select(target => target.TargetPath),
                    Is.EqualTo(new[] { "configured.txt" }));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ImportItemTargets_ImportsOneChoicePerGroupAndEveryUngroupedTarget()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "choices.zip");
            using (var archiveStream = File.Create(sourcePath))
            using (var archive = new ZipArchive(
                       archiveStream,
                       ZipArchiveMode.Create))
            {
                WriteArchiveEntry(archive, "a.pdf", "a");
                WriteArchiveEntry(
                    archive,
                    "a_variation.pdf",
                    "variation");
                WriteArchiveEntry(
                    archive,
                    "c.png",
                    Convert.FromBase64String(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwC" +
                        "AAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            }

            var itemName = "TargetChoices" +
                           Guid.NewGuid().ToString("N");
            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = itemName });
            var file = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });
            _manager.SetItemTargets(
                item.Id,
                new[]
                {
                    new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = "a.pdf"
                    },
                    new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = "a_variation.pdf"
                    },
                    new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = "c.png"
                    }
                });
            _manager.SetItemTargetGroup(
                item.Id,
                file.Id,
                "a.pdf",
                "a");
            _manager.SetItemTargetGroup(
                item.Id,
                file.Id,
                "a_variation.pdf",
                "a");
            Assert.That(
                _manager.GetItemTargets(item.Id)
                    .Where(target => target.GroupName == "a")
                    .Select(target => target.TargetPath),
                Is.EquivalentTo(new[] { "a.pdf", "a_variation.pdf" }));
            var assetPath = "Assets/" + itemName;
            try
            {
                var result = _manager.ImportItemTargets(
                        item.Id,
                        new[]
                        {
                            new AssetFileTarget
                            {
                                FileId = file.Id,
                                TargetPath = "a_variation.pdf"
                            }
                        })
                    .GetAwaiter().GetResult();
                var destination = Path.Combine(
                    UnityEngine.Application.dataPath,
                    itemName,
                    "choices");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.FileIds, Is.EqualTo(new[] { file.Id }));
                Assert.That(
                    File.Exists(Path.Combine(destination, "a.pdf")),
                    Is.False);
                Assert.That(
                    File.ReadAllText(Path.Combine(
                        destination,
                        "a_variation.pdf")),
                    Is.EqualTo("variation"));
                Assert.That(
                    File.Exists(Path.Combine(destination, "c.png")),
                    Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
            }
        }

        private static void WriteArchiveEntry(
            ZipArchive archive,
            string path,
            string contents)
        {
            using (var writer = new StreamWriter(
                       archive.CreateEntry(path).Open()))
            {
                writer.Write(contents);
            }
        }

        private static void WriteArchiveEntry(
            ZipArchive archive,
            string path,
            byte[] contents)
        {
            using (var stream = archive.CreateEntry(path).Open())
            {
                stream.Write(contents, 0, contents.Length);
            }
        }

        private static void AssertChange(
            AssetManagerChange change,
            AssetManagerChangeKind kind,
            IReadOnlyList<string> subjectIds = null,
            IReadOnlyList<string> relatedIds = null,
            AssetSourceType? sourceType = null)
        {
            Assert.That(change, Is.Not.Null);
            Assert.That(change.Kind, Is.EqualTo(kind));
            Assert.That(
                change.SubjectIds,
                Is.EqualTo(subjectIds ?? Array.Empty<string>()));
            Assert.That(
                change.RelatedIds,
                Is.EqualTo(relatedIds ?? Array.Empty<string>()));
            Assert.That(change.SourceType, Is.EqualTo(sourceType));
        }

        private static AssetFileTarget Target(
            AssetFile file,
            string targetPath = "")
        {
            return new AssetFileTarget
            {
                FileId = file.Id,
                TargetPath = targetPath
            };
        }

        [Test]
        public void FileDependencies_RejectInvalidTargetsWithoutChangingData()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Avatar" });
            var dependency = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });
            var dependent = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "dependent.txt"
                });

            _manager.SetFileDependencies(
                new[] { dependent.Id },
                new[] { Target(dependency, "Packages/avatar.prefab") });

            var exception = Assert.Throws<AssetManagerException>(() =>
                _manager.SetFileDependencies(
                    new[] { dependent.Id },
                    new[] { Target(dependency, "../outside.prefab") }));

            Assert.That(
                exception.Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.SetFileDependencies(
                        new[] { dependent.Id },
                        new[] { Target(dependency) })).Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.SetFileDependencies(
                        new[] { dependent.Id },
                        new[]
                        {
                            Target(dependency, "Packages/nested.ZIP")
                        })).Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(
                _manager.GetFileDependencies(dependent.Id)
                    .Single().TargetPath,
                Is.EqualTo("Packages/avatar.prefab"));
        }

        [Test]
        public void FileDependencies_AllowMultipleEntriesInsideZip()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            using (var stream = File.Create(sourcePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                archive.CreateEntry("Packages/");
                using (var writer = new StreamWriter(
                           archive.CreateEntry(
                                   "Packages/avatar.unitypackage")
                               .Open()))
                {
                    writer.Write("package");
                }
                using (var writer = new StreamWriter(
                           archive.CreateEntry("Packages/avatar.prefab")
                               .Open()))
                {
                    writer.Write("prefab");
                }
            }

            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Avatar" });
            var file = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });
            var dependent = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "dependent.txt"
                });
            _manager.SetFileDependencies(
                new[] { dependent.Id },
                new[]
                {
                    Target(file, "Packages/avatar.unitypackage"),
                    Target(file, "Packages/avatar.prefab")
                });

            Assert.That(
                _manager.GetFileDependencies(dependent.Id)
                    .Select(dependency => dependency.TargetPath),
                Is.EquivalentTo(new[]
                {
                    "Packages/avatar.unitypackage",
                    "Packages/avatar.prefab"
                }));
        }

        [Test]
        public void TargetImporter_CopiesFileAndZipEntry()
        {
            var sourcePath = Path.Combine(_root, "avatar.zip");
            using (var archiveStream = File.Create(sourcePath))
            using (var archive = new ZipArchive(
                       archiveStream,
                       ZipArchiveMode.Create))
            using (var writer = new StreamWriter(
                       archive.CreateEntry(
                               "avatar/Packages/avatar.prefab")
                           .Open()))
            {
                writer.Write("prefab");
            }

            var assets = Path.Combine(_root, "Assets");
            var refreshCount = 0;
            var importer = new AssetTargetImporter(
                assets,
                () => refreshCount++);
            var item = new AssetItem
            {
                Id = "item",
                Name = "Avatar"
            };
            var file = new AssetFile
            {
                Id = "file",
                ItemId = item.Id,
                FileName = "avatar.zip",
                SourcePath = sourcePath
            };

            importer.Import(
                item,
                file,
                new[] { string.Empty },
                CancellationToken.None).GetAwaiter().GetResult();
            importer.Import(
                item,
                file,
                new[] { "Packages/avatar.prefab" },
                CancellationToken.None).GetAwaiter().GetResult();

            var destination = Path.Combine(
                assets,
                "Avatar",
                "avatar");
            Assert.That(
                File.Exists(Path.Combine(destination, "avatar.zip")),
                Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(
                    destination,
                    "Packages",
                    "avatar.prefab")),
                Is.EqualTo("prefab"));
            Assert.That(refreshCount, Is.EqualTo(2));
        }

        [Test]
        public void TargetImporter_ReportsPackageFailureAfterCleanup()
        {
            var sourcePath = Path.Combine(_root, "avatar.zip");
            using (var archiveStream = File.Create(sourcePath))
            using (var archive = new ZipArchive(
                       archiveStream,
                       ZipArchiveMode.Create))
            using (var writer = new StreamWriter(
                       archive.CreateEntry(
                               "Packages/avatar.unitypackage")
                           .Open()))
            {
                writer.Write("package");
            }

            string packagePath = null;
            Action<bool> complete = null;
            var importer = new AssetTargetImporter(
                Path.Combine(_root, "Assets"),
                () => { },
                (path, completed) =>
                {
                    packagePath = path;
                    complete = completed;
                });

            var importTask = importer.Import(
                new AssetItem { Id = "item", Name = "Avatar" },
                new AssetFile
                {
                    Id = "file",
                    ItemId = "item",
                    FileName = "avatar.zip",
                    SourcePath = sourcePath
                },
                new[] { "Packages/avatar.unitypackage" },
                CancellationToken.None);

            Assert.That(File.Exists(packagePath), Is.True);
            Assert.That(importTask.IsCompleted, Is.False);

            complete(false);

            Assert.That(
                importTask.GetAwaiter().GetResult().State,
                Is.EqualTo(AssetImportState.Failed));
            Assert.That(
                Directory.Exists(Path.GetDirectoryName(packagePath)),
                Is.False);
        }

        [Test]
        public void AnalyzeFile_ReadsZipAndUnityPackageContents()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var zipPath = Path.Combine(_root, "avatar.zip");
            using (var stream = File.Create(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(
                       archive.CreateEntry("avatar/Prefabs/avatar.prefab").Open()))
            {
                writer.Write("prefab");
            }

            const string guid = "0123456789abcdef0123456789abcdef";
            var packagePath = Path.Combine(_root, "avatar.unitypackage");
            WriteUnityPackage(
                packagePath,
                guid,
                "Assets/Avatar/avatar.prefab",
                Encoding.UTF8.GetBytes("prefab"));
            var zip = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = zipPath
                });
            var package = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = packagePath
                });

            var zipAnalysis = _manager.AnalyzeFile(zip.Id);
            var asyncZipAnalysis = _manager
                .AnalyzeFileAsync(zip.Id)
                .GetAwaiter()
                .GetResult();
            var packageAnalysis = _manager.AnalyzeFile(package.Id);

            Assert.That(zipAnalysis.Kind, Is.EqualTo(AssetFileAnalysisKind.Zip));
            Assert.That(
                zipAnalysis.Entries.Single().Path,
                Is.EqualTo("Prefabs/avatar.prefab"));
            Assert.That(
                asyncZipAnalysis.Entries.Single().Path,
                Is.EqualTo("Prefabs/avatar.prefab"));
            Assert.That(
                packageAnalysis.Kind,
                Is.EqualTo(AssetFileAnalysisKind.UnityPackage));
            Assert.That(
                packageAnalysis.Entries.Single().Path,
                Is.EqualTo("Assets/Avatar/avatar.prefab"));
            Assert.That(
                packageAnalysis.Entries.Single().AssetGuid,
                Is.EqualTo(guid));
        }

        [Test]
        public void Ee4vSync_InvalidMetadataDoesNotChangeCatalog()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var imported = _manager.ImportEe4vFile(
                new ImportEe4vFileRequest(library, sourcePath));
            File.WriteAllText(
                Path.Combine(
                    library,
                    "Assets",
                    imported.SourceId,
                    "metadata.json"),
                "{ invalid json }");

            var result = _manager.SyncEe4v(
                new Ee4vSyncRequest(library));

            Assert.That(result.State, Is.EqualTo(AssetSyncState.Failed));
            Assert.That(
                _manager.GetItem(imported.Id).Files.Single().Id,
                Is.EqualTo(imported.Files.Single().Id));
        }

        private static void WriteFolderMetadata(
            string path,
            string folderName)
        {
            File.WriteAllText(
                path,
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"" +
                folderName +
                "\",\"children\":[]}]}]}");
        }

        private static void WriteUnityPackage(
            string path,
            string guid,
            string assetPath,
            byte[] assetBytes)
        {
            using (var file = File.Create(path))
            using (var gzip = new GZipStream(
                       file,
                       CompressionMode.Compress))
            {
                WriteTarEntry(
                    gzip,
                    guid + "/pathname",
                    Encoding.UTF8.GetBytes(assetPath));
                WriteTarEntry(gzip, guid + "/asset", assetBytes);
                gzip.Write(new byte[1024], 0, 1024);
            }
        }

        private static void WriteTarEntry(
            Stream stream,
            string name,
            byte[] content)
        {
            var header = new byte[512];
            var nameBytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(nameBytes, header, nameBytes.Length);
            var size = Convert.ToString(content.Length, 8)
                .PadLeft(11, '0');
            var sizeBytes = Encoding.ASCII.GetBytes(size);
            Array.Copy(sizeBytes, 0, header, 124, sizeBytes.Length);
            stream.Write(header, 0, header.Length);
            stream.Write(content, 0, content.Length);
            var padding = (512 - content.Length % 512) % 512;
            if (padding > 0)
            {
                stream.Write(new byte[padding], 0, padding);
            }
        }
    }
}
