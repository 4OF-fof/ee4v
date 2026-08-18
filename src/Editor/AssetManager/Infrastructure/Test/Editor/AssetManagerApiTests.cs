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
        public void CollectionSearch_UsesNestedLogicAndHierarchicalTags()
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
            _manager.CreateItem(new CreateAssetItemRequest
            {
                Name = "Other Hidden",
                Description = "hidden"
            });
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
            var page = _manager.SearchItems(new AssetItemQuery
            {
                Offset = 1,
                Limit = 1
            });
            Assert.That(page.TotalCount, Is.EqualTo(3));
            Assert.That(page.Items.Single().Name, Is.EqualTo("Other Hidden"));
            Assert.That(_manager.GetTags().Single().Path, Is.EqualTo("foo/bar"));

            _manager.SetItemTags(
                new[] { tagged.Id },
                Array.Empty<string>());

            Assert.That(_manager.GetTags(), Is.Empty);
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
            _manager.SetFileTargets(file.Id, new[] { string.Empty });
            AssertChange(
                changes.Single(),
                AssetManagerChangeKind.FileTargetsChanged,
                new[] { file.Id });

            changes.Clear();
            _manager.SetFileDependencies(
                new[] { file.Id },
                Array.Empty<string>());
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
            AssetManagerChange synchronized = null;
            _manager.Changed += change => synchronized = change;
            var library = Path.Combine(_root, "test.library");
            var images = Path.Combine(library, "images");
            var entry = Path.Combine(images, "file-entry.info");
            var boothEntry = Path.Combine(images, "booth-entry.info");
            Directory.CreateDirectory(entry);
            Directory.CreateDirectory(boothEntry);
            var folderMetadata = Path.Combine(library, "metadata.json");
            WriteFolderMetadata(folderMetadata, "Before");
            File.WriteAllText(
                Path.Combine(entry, "metadata.json"),
                "{\"id\":\"file-entry\",\"name\":\"avatar\",\"ext\":\"zip\",\"folders\":[\"avatar-folder\"],\"tags\":[\"Avatar/PC\"],\"isDeleted\":false}");
            File.WriteAllText(
                Path.Combine(boothEntry, "metadata.json"),
                "{\"id\":\"booth-entry\",\"name\":\"booth\",\"ext\":\"json\",\"folders\":[\"avatar-folder\"],\"isDeleted\":false}");
            File.WriteAllText(
                Path.Combine(boothEntry, "booth.json"),
                "{\"boothItemId\":1,\"thumbnailUrl\":\"https://example.invalid/avatar.png\"}");
            File.WriteAllText(Path.Combine(entry, "avatar.zip"), "payload");

            var first = _manager.SyncEagle(
                new EagleSyncRequest(library));
            var before = _manager.SearchItems().Items.Single();
            var beforeFile = before.Files.Single();

            Assert.That(first.State, Is.EqualTo(AssetSyncState.Success));
            Assert.That(first.CreatedItemIds, Is.EqualTo(new[] { before.Id }));
            Assert.That(
                first.CreatedFileIds,
                Is.EqualTo(new[] { beforeFile.Id }));
            AssertChange(
                synchronized,
                AssetManagerChangeKind.SourceSynchronized,
                new[] { before.Id },
                new[] { beforeFile.Id },
                sourceType: AssetSourceType.Eagle);
            Assert.That(before.SourceId, Is.EqualTo("avatar-folder"));
            Assert.That(
                before.ThumbnailUrl,
                Is.EqualTo("https://example.invalid/avatar.png"));
            Assert.That(beforeFile.SourceId, Is.EqualTo("file-entry"));
            Assert.That(before.Tags.Single().Path, Is.EqualTo("avatar/pc"));
            Assert.That(
                _manager.SearchItems(new AssetItemQuery
                {
                    Filter = AssetFilterNode.Condition(
                        AssetFilterConditionType.HasFileExtension,
                        ".ZIP")
                }).Items.Single().Id,
                Is.EqualTo(before.Id));

            var deleteError = Assert.Throws<AssetManagerException>(() =>
                _manager.DeleteFile(new[] { beforeFile.Id }));

            Assert.That(
                deleteError.Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(File.Exists(beforeFile.SourcePath), Is.True);
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.SetFileItem(
                        new[] { beforeFile.Id },
                        null))
                    .Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));

            File.WriteAllText(
                Path.Combine(entry, "metadata.json"),
                "{\"id\":\"file-entry\",\"name\":\"avatar\",\"ext\":\"zip\",\"folders\":[\"avatar-folder\"],\"tags\":[\"Avatar/Quest\"],\"isDeleted\":false}");
            var tagChanged = _manager.SyncEagle(
                new EagleSyncRequest(library));
            Assert.That(
                tagChanged.UpdatedItemIds,
                Is.EqualTo(new[] { before.Id }));
            Assert.That(tagChanged.UpdatedFileIds, Is.Empty);

            WriteFolderMetadata(folderMetadata, "After");
            var renamedResult = _manager.SyncEagle(
                new EagleSyncRequest(library));
            var renamed = _manager.SearchItems().Items.Single();

            Assert.That(renamed.Id, Is.EqualTo(before.Id));
            Assert.That(renamed.Name, Is.EqualTo("After"));
            Assert.That(
                renamedResult.UpdatedItemIds,
                Is.EqualTo(new[] { before.Id }));
            AssertChange(
                synchronized,
                AssetManagerChangeKind.SourceSynchronized,
                new[] { before.Id },
                sourceType: AssetSourceType.Eagle);

            Directory.Delete(entry, true);
            var removedFile = _manager.SyncEagle(
                new EagleSyncRequest(library));

            Assert.That(
                removedFile.DeletedFileIds,
                Is.EqualTo(new[] { beforeFile.Id }));
            AssertChange(
                synchronized,
                AssetManagerChangeKind.SourceSynchronized,
                new[] { before.Id },
                new[] { beforeFile.Id },
                sourceType: AssetSourceType.Eagle);
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.GetFile(beforeFile.Id)).Code,
                Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(_manager.GetItem(before.Id).Files, Is.Empty);
            Assert.That(
                _manager.SearchItems(new AssetItemQuery
                {
                    Filter = AssetFilterNode.Condition(
                        AssetFilterConditionType.HasFileExtension,
                        "zip")
                }).Items,
                Is.Empty);

            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.UpdateItem(
                        before.Id,
                        new UpdateAssetItemRequest { Name = "Local" }))
                    .Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.SetItemTags(
                        new[] { before.Id },
                        new[] { "local" }))
                    .Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
        }

        [Test]
        public void EagleSync_MissingTargetPreservesDataAndDeletesRemovedItem()
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

            File.WriteAllText(
                folderMetadata,
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[]}]}" );
            var removed = _manager.SyncEagle(
                new EagleSyncRequest(library));

            Assert.That(removed.State, Is.EqualTo(AssetSyncState.Success));
            Assert.That(removed.DeletedItemIds, Is.EqualTo(new[] { item.Id }));
            Assert.That(removed.DeletedFileIds, Is.EqualTo(new[] { file.Id }));
            Assert.That(_manager.SearchItems().Items, Is.Empty);
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.GetItem(item.Id)).Code,
                Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(
                Assert.Throws<AssetManagerException>(() =>
                    _manager.GetFile(file.Id)).Code,
                Is.EqualTo(AssetManagerErrorCode.NotFound));
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
            Assert.That(
                _manager.GetThumbnails(Array.Empty<string>())
                    .GetAwaiter().GetResult(),
                Is.Empty);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() =>
                    _manager.GetThumbnail(item.Id, cancellation.Token)
                        .GetAwaiter().GetResult());
            }
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
            var changes = new List<AssetManagerChange>();
            _manager.Changed += changes.Add;

            var imported = _manager.ImportEe4vFile(
                new ImportEe4vFileRequest(
                    library,
                    sourcePath,
                    "Before",
                    "description",
                    new[] { "#Foo/Bar" }));
            var importedFile = imported.Files.Single();
            var entryPath = Path.Combine(
                library,
                "Assets",
                imported.SourceId);

            Assert.That(imported.SourceType, Is.EqualTo(AssetSourceType.Ee4v));
            Assert.That(importedFile.SourceType, Is.EqualTo(AssetSourceType.Ee4v));
            Assert.That(importedFile.SourceId, Is.EqualTo(imported.SourceId));
            Assert.That(
                Directory.GetFiles(entryPath).Select(Path.GetFileName),
                Is.EquivalentTo(new[] { "avatar.zip", "metadata.json" }));
            Assert.That(imported.Tags.Single().Path, Is.EqualTo("foo/bar"));
            AssertChange(
                changes[0],
                AssetManagerChangeKind.ItemCreated,
                new[] { imported.Id });
            AssertChange(
                changes[1],
                AssetManagerChangeKind.FileCreated,
                new[] { importedFile.Id },
                new[] { imported.Id });

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
        public void RegisterFile_StoresEe4vPairAndRestoresAsFile()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var item = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Avatar" });

            var assigned = _manager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "registered.zip"
                });
            var unassigned = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath
                });

            Assert.That(assigned.ItemId, Is.EqualTo(item.Id));
            Assert.That(assigned.SourceType, Is.EqualTo(AssetSourceType.Ee4v));
            Assert.That(assigned.FileName, Is.EqualTo("registered.zip"));
            Assert.That(File.ReadAllText(assigned.SourcePath), Is.EqualTo("payload"));
            Assert.That(
                Directory.GetFiles(Path.GetDirectoryName(assigned.SourcePath))
                    .Select(Path.GetFileName),
                Is.EquivalentTo(new[] { "registered.zip", "metadata.json" }));
            Assert.That(
                _manager.GetUnassignedFiles().Single().Id,
                Is.EqualTo(unassigned.Id));

            File.Delete(_databasePath);
            _manager = AssetManagerFactory.Open(_databasePath);
            var sync = _manager.SyncEe4v(new Ee4vSyncRequest(library));

            Assert.That(sync.State, Is.EqualTo(AssetSyncState.Success));
            Assert.That(_manager.SearchItems().Items, Is.Empty);
            Assert.That(_manager.GetUnassignedFiles().Count, Is.EqualTo(2));
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
        public void MultiTargetMutations_UpdateEveryRequestedTarget()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var sourcePath = Path.Combine(_root, "avatar.zip");
            File.WriteAllText(sourcePath, "payload");
            var first = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "First" });
            var second = _manager.CreateItem(
                new CreateAssetItemRequest { Name = "Second" });
            var firstFile = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "first.zip"
                });
            var secondFile = _manager.RegisterFile(
                null,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "second.zip"
                });

            var files = _manager.SetFileItem(
                new[] { firstFile.Id, secondFile.Id },
                first.Id);
            var items = _manager.SetItemTags(
                new[] { first.Id, second.Id },
                new[] { "#Shared/Tag" });

            Assert.That(
                files.All(file => file.ItemId == first.Id),
                Is.True);
            Assert.That(
                items.All(item =>
                    item.Tags.Single().Path == "shared/tag"),
                Is.True);
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
        public void FileDependencies_UpdateMultipleFilesAndRejectCycles()
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
            var second = _manager.RegisterFile(
                secondItem.Id,
                new RegisterFileRequest
                {
                    LibraryPath = library,
                    FilePath = sourcePath,
                    FileName = "second.txt"
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
                new[] { first.Id, second.Id },
                new[] { third.Id, third.Id });

            Assert.That(
                _manager.GetFileDependencies(first.Id)
                    .Single().DependencyFileId,
                Is.EqualTo(third.Id));
            Assert.That(
                _manager.GetFileDependencies(second.Id)
                    .Single().DependencyFileId,
                Is.EqualTo(third.Id));

            var exception = Assert.Throws<AssetManagerException>(() =>
                _manager.SetFileDependencies(
                    new[] { third.Id },
                    new[] { first.Id }));

            Assert.That(
                exception.Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(
                _manager.GetFileDependencies(third.Id),
                Is.Empty);
            _manager.SetFileDependencies(
                new[] { first.Id, second.Id },
                Array.Empty<string>());
            Assert.That(
                _manager.GetFileDependencies(second.Id),
                Is.Empty);

            _manager.SetFileDependencies(
                new[] { first.Id, second.Id },
                new[] { third.Id });

            _manager = AssetManagerFactory.Open(_databasePath);
            Assert.That(
                _manager.GetFileDependencies(first.Id)
                    .Single().DependencyFileId,
                Is.EqualTo(third.Id));

            var changes = new List<AssetManagerChange>();
            _manager.Changed += changes.Add;
            _manager.DeleteFile(new[] { third.Id });

            Assert.That(
                _manager.GetFileDependencies(first.Id),
                Is.Empty);
            Assert.That(
                _manager.GetFileDependencies(second.Id),
                Is.Empty);
            AssertChange(
                changes.Single(change =>
                    change.Kind ==
                    AssetManagerChangeKind.FileDependenciesChanged),
                AssetManagerChangeKind.FileDependenciesChanged,
                new[] { first.Id, second.Id }
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray());
        }

        [Test]
        public void ImportFileTargets_ImportsDependenciesBeforeDependent()
        {
            var library = Path.Combine(_root, "ee4v-library");
            var dependencySource = Path.Combine(_root, "dependency.zip");
            using (var archiveStream = File.Create(dependencySource))
            using (var archive = new ZipArchive(
                       archiveStream,
                       ZipArchiveMode.Create))
            using (var writer = new StreamWriter(
                       archive.CreateEntry("marker.txt").Open()))
            {
                writer.Write("dependency");
            }

            var dependentSource = Path.Combine(_root, "dependent.zip");
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
                    FileName = "shared.zip"
                });
            _manager.SetFileTargets(
                dependency.Id,
                new[] { string.Empty, "marker.txt" });
            _manager.SetFileTargets(
                dependent.Id,
                new[] { string.Empty });
            _manager.SetFileDependencies(
                new[] { dependent.Id },
                new[] { dependency.Id });
            var changes = new List<AssetManagerChange>();
            _manager.Changed += changes.Add;

            var assetPath = "Assets/" + itemName;
            try
            {
                var result = _manager.ImportFileTargets(dependent.Id)
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
                        "marker.txt")),
                    Is.EqualTo("dependency"));
                Assert.That(
                    File.ReadAllText(Path.Combine(
                        destination,
                        "shared.zip")),
                    Is.EqualTo("dependent"));
                Assert.That(
                    _manager.GetFileImportedAssetGuids(dependency.Id),
                    Is.Not.Empty);
                Assert.That(
                    _manager.GetFileImportedAssetGuids(dependent.Id),
                    Is.Not.Empty);
                Assert.That(
                    changes.Select(change => change.Kind),
                    Is.All.EqualTo(
                        AssetManagerChangeKind
                            .FileImportedAssetGuidsChanged));
                Assert.That(
                    changes.SelectMany(change => change.SubjectIds),
                    Is.EqualTo(new[] { dependency.Id, dependent.Id }));
                Assert.That(
                    changes.SelectMany(change => change.RelatedIds),
                    Is.EqualTo(new[] { item.Id, item.Id }));
                var itemGuids = _manager.GetItemImportedAssetGuids(item.Id);
                var associations =
                    _manager.GetImportedAssetAssociations(itemGuids);
                Assert.That(itemGuids, Is.Not.Empty);
                Assert.That(
                    associations.Select(value => value.FileId).Distinct(),
                    Is.EquivalentTo(new[]
                    {
                        dependency.Id,
                        dependent.Id
                    }));
                Assert.That(
                    associations.Select(value => value.ItemId).Distinct(),
                    Is.EqualTo(new[] { item.Id }));
                Assert.That(
                    _manager.GetImportedAssetAssociations()
                        .Select(value => value.AssetGuid)
                        .Distinct(),
                    Is.EquivalentTo(itemGuids));

                _manager = AssetManagerFactory.Open(_databasePath);
                Assert.That(
                    _manager.GetFileImportedAssetGuids(dependent.Id),
                    Is.Not.Empty);
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
            _manager.SetFileTargets(
                file.Id,
                new[] { "configured.txt" });

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
                    _manager.GetFileTargets(file.Id)
                        .Select(target => target.TargetPath),
                    Is.EqualTo(new[] { "configured.txt" }));

                var targetResult = _manager.ImportFileTargets(file.Id)
                    .GetAwaiter().GetResult();

                Assert.That(targetResult.Succeeded, Is.True);
                Assert.That(
                    File.ReadAllText(Path.Combine(
                        destination,
                        "configured.txt")),
                    Is.EqualTo("configured"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
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

        [Test]
        public void FileTargets_SupportSelfChildrenAndUnset()
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

            var targets = _manager.SetFileTargets(
                file.Id,
                new[]
                {
                    string.Empty,
                    "Packages\\Avatar.prefab",
                    "packages/avatar.prefab"
                });

            Assert.That(
                targets.Select(target => target.TargetPath),
                Is.EqualTo(new[]
                {
                    string.Empty,
                    "Packages/Avatar.prefab"
                }));

            _manager = AssetManagerFactory.Open(_databasePath);
            Assert.That(
                _manager.GetFileTargets(file.Id)
                    .Select(target => target.TargetPath),
                Is.EqualTo(new[]
                {
                    string.Empty,
                    "Packages/Avatar.prefab"
                }));

            var exception = Assert.Throws<AssetManagerException>(() =>
                _manager.SetFileTargets(
                    file.Id,
                    new[] { "../outside.prefab" }));

            Assert.That(
                exception.Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(_manager.GetFileTargets(file.Id).Count, Is.EqualTo(2));

            _manager.SetFileTargets(file.Id, Array.Empty<string>());

            Assert.That(_manager.GetFileTargets(file.Id), Is.Empty);
            Assert.DoesNotThrow(() =>
                _manager.ImportFileTargets(file.Id)
                    .GetAwaiter().GetResult());
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
            var packageAnalysis = _manager.AnalyzeFile(package.Id);

            Assert.That(zipAnalysis.Kind, Is.EqualTo(AssetFileAnalysisKind.Zip));
            Assert.That(
                zipAnalysis.Entries.Single().Path,
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
