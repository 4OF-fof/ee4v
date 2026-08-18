using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Ee4v.AssetManager.Contracts;
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
            Assert.That(_manager.GetTags().Single().Path, Is.EqualTo("foo/bar"));

            _manager.SetItemTags(
                new[] { tagged.Id },
                Array.Empty<string>());

            Assert.That(_manager.GetTags(), Is.Empty);
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
        public void EagleSync_ReusesIdsAndMarksMissingFileUnavailable()
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

            var first = _manager.SyncEagle(
                new EagleSyncRequest(library));
            var before = _manager.SearchItems().Items.Single();
            var beforeFile = before.Files.Single();

            Assert.That(first.State, Is.EqualTo(AssetSyncState.Success));
            Assert.That(before.SourceId, Is.EqualTo("avatar-folder"));
            Assert.That(beforeFile.SourceId, Is.EqualTo("file-entry"));
            Assert.That(beforeFile.IsAvailable, Is.True);
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

            WriteFolderMetadata(folderMetadata, "After");
            _manager.SyncEagle(new EagleSyncRequest(library));
            var renamed = _manager.SearchItems().Items.Single();

            Assert.That(renamed.Id, Is.EqualTo(before.Id));
            Assert.That(renamed.Name, Is.EqualTo("After"));

            Directory.Delete(entry, true);
            _manager.SyncEagle(new EagleSyncRequest(library));
            var missing = _manager.GetFile(beforeFile.Id);

            Assert.That(missing.Id, Is.EqualTo(beforeFile.Id));
            Assert.That(missing.IsAvailable, Is.False);
            Assert.That(
                _manager.SearchItems(new AssetItemQuery
                {
                    Filter = AssetFilterNode.Condition(
                        AssetFilterConditionType.HasFileExtension,
                        "zip")
                }).Items,
                Is.Empty);

            var unassigned = _manager.SetFileItem(
                    new[] { beforeFile.Id },
                    null)
                .Single();

            Assert.That(unassigned.ItemId, Is.Null);
            Assert.That(
                _manager.GetUnassignedFiles().Single().Id,
                Is.EqualTo(beforeFile.Id));
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
            Assert.That(restored.Files.Single().IsAvailable, Is.True);
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
        public void FileDependencies_AllowCrossItemAndRejectCycles()
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
                first.Id,
                new[] { second.Id, second.Id });
            _manager.SetFileDependencies(
                second.Id,
                new[] { third.Id });

            Assert.That(
                _manager.GetFileDependencies(first.Id)
                    .Single().DependencyFileId,
                Is.EqualTo(second.Id));

            var exception = Assert.Throws<AssetManagerException>(() =>
                _manager.SetFileDependencies(
                    third.Id,
                    new[] { first.Id }));

            Assert.That(
                exception.Code,
                Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(
                _manager.GetFileDependencies(third.Id),
                Is.Empty);
            _manager.SetFileDependencies(
                second.Id,
                Array.Empty<string>());
            Assert.That(
                _manager.GetFileDependencies(second.Id),
                Is.Empty);

            _manager = AssetManagerFactory.Open(_databasePath);
            Assert.That(
                _manager.GetFileDependencies(first.Id)
                    .Single().DependencyFileId,
                Is.EqualTo(second.Id));

            _manager.DeleteFile(new[] { second.Id });

            Assert.That(
                _manager.GetFileDependencies(first.Id),
                Is.Empty);
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
                dependent.Id,
                new[] { dependency.Id });

            var assetPath = "Assets/" + itemName;
            try
            {
                _manager.ImportFileTargets(dependent.Id);

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
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
            }
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
                _manager.ImportFileTargets(file.Id));
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
                SourcePath = sourcePath,
                IsAvailable = true
            };

            importer.Import(
                item,
                file,
                new[] { string.Empty },
                _ => { });
            importer.Import(
                item,
                file,
                new[] { "Packages/avatar.prefab" },
                _ => { });

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
        public void TargetImporter_KeepsPackageUntilImportCompletes()
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
            var importCompleted = false;
            var importer = new AssetTargetImporter(
                Path.Combine(_root, "Assets"),
                () => { },
                (path, completed) =>
                {
                    packagePath = path;
                    complete = completed;
                });

            importer.Import(
                new AssetItem { Id = "item", Name = "Avatar" },
                new AssetFile
                {
                    Id = "file",
                    ItemId = "item",
                    FileName = "avatar.zip",
                    SourcePath = sourcePath,
                    IsAvailable = true
                },
                new[] { "Packages/avatar.unitypackage" },
                succeeded => importCompleted = succeeded);

            Assert.That(File.Exists(packagePath), Is.True);
            Assert.That(importCompleted, Is.False);

            complete(true);

            Assert.That(importCompleted, Is.True);
            Assert.That(
                Directory.Exists(Path.GetDirectoryName(packagePath)),
                Is.False);
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
                _manager.GetItem(imported.Id).Files.Single().IsAvailable,
                Is.True);
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
    }
}
