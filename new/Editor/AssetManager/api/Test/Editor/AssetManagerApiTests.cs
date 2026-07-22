using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
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
        private string _oldAvatarNames;
        private string _oldVersionGroupRegex;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ee4v-asset-manager-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            AssetManagerDefinitions.RegisterAll();
            _oldGlobalPath = SettingApi.Get(AssetManagerDefinitions.Ee4vGlobalPath);
            _oldSourcePriority = SettingApi.Get(AssetManagerDefinitions.SourcePriority);
            _oldAvatarNames = SettingApi.Get(AssetManagerDefinitions.AvatarNames);
            _oldVersionGroupRegex = SettingApi.Get(AssetManagerDefinitions.VersionGroupRegex);
            SettingApi.Set(AssetManagerDefinitions.Ee4vGlobalPath, _tempRoot, saveImmediately: false);
            SettingApi.Set(AssetManagerDefinitions.SourcePriority, "ee4v,eagle,blm", saveImmediately: false);
        }

        [TearDown]
        public void TearDown()
        {
            SettingApi.Set(AssetManagerDefinitions.Ee4vGlobalPath, _oldGlobalPath, saveImmediately: false);
            SettingApi.Set(AssetManagerDefinitions.SourcePriority, _oldSourcePriority, saveImmediately: false);
            SettingApi.Set(AssetManagerDefinitions.AvatarNames, _oldAvatarNames, saveImmediately: false);
            SettingApi.Set(AssetManagerDefinitions.VersionGroupRegex, _oldVersionGroupRegex, saveImmediately: false);
            if (!string.IsNullOrWhiteSpace(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        [Test]
        [FeatureTestCase(
            "schema version 2 の DB 制約を作成する",
            "AssetManager DB が source origin、availability、collection cycle trigger を作成することを確認します。",
            order: 301)]
        public void Schema_CreatesVersion2Constraints()
        {
            var databasePath = GetDatabasePath();

            AssetManagerApi.GetTags();

            using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.PrivateCache))
            {
                Assert.That(connection.ExecuteScalar<int>("SELECT version FROM schema_version LIMIT 1"), Is.EqualTo(2));
                Assert.That(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'file_info'"), Does.Contain("CHECK"));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'item_source_origin'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'datasource_tag'"), Is.EqualTo(1));
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
            "file dependency 置換で file-to-version を保持する",
            "SetFileDependencies が同じ source file の version dependency を削除しないことを確認します。",
            order: 312)]
        public void SetFileDependencies_PreservesVersionDependency()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var sourcePath = Path.Combine(_tempRoot, "source.zip");
            var targetPath = Path.Combine(_tempRoot, "target.zip");
            File.WriteAllText(sourcePath, "source");
            File.WriteAllText(targetPath, "target");
            var source = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = sourcePath, FileName = "source.zip" });
            var target = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = targetPath, FileName = "target.zip" });
            var version = AssetManagerApi.CreateVersionGroup(item.Id, new CreateVersionGroupRequest { Name = "1.0" });
            var sourceEndpoint = new DependencyEndpointRequest { Type = AssetDependencyEndpointType.File, Id = source.Id };
            AssetManagerApi.SetDependencies(
                sourceEndpoint,
                new[] { new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VersionGroup, Id = version.Id } });

            AssetManagerApi.SetFileDependencies(source.Id, new[] { target.Id });
            var dependencies = AssetManagerApi.GetDependencies(sourceEndpoint);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Any(dependency => dependency.Target.Type == AssetDependencyEndpointType.File && dependency.Target.Id == target.Id), Is.True);
            Assert.That(dependencies.Any(dependency => dependency.Target.Type == AssetDependencyEndpointType.VersionGroup && dependency.Target.Id == version.Id), Is.True);
        }

        [Test]
        [FeatureTestCase(
            "version / variant group と依存を保存する",
            "file が version / variant group を親にでき、variant から version への dependency を保存できることを確認します。",
            order: 312)]
        public void GroupsAndDependencies_StoreVersionVariantRelations()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var variant = AssetManagerApi.CreateVariantGroup(item.Id, new CreateVariantGroupRequest { Name = "Quest" });
            var version = AssetManagerApi.CreateVersionGroup(item.Id, new CreateVersionGroupRequest { Name = "1.0", VariantGroupId = variant.Id });
            var versionFilePath = Path.Combine(_tempRoot, "version.zip");
            var variantFilePath = Path.Combine(_tempRoot, "variant.zip");
            File.WriteAllText(versionFilePath, "version");
            File.WriteAllText(variantFilePath, "variant");

            var versionFile = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = versionFilePath, FileName = "version.zip", VersionGroupId = version.Id });
            var variantFile = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = variantFilePath, FileName = "variant.zip", VariantGroupId = variant.Id });
            AssetManagerApi.SetVersionGroupPrimaryFile(version.Id, versionFile.Id);
            AssetManagerApi.SetDependencies(
                new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VariantGroup, Id = variant.Id },
                new[] { new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VersionGroup, Id = version.Id } });

            var files = AssetManagerApi.GetFiles(item.Id).OrderBy(file => file.FileName).ToArray();
            var dependencies = AssetManagerApi.GetDependencies(new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VariantGroup, Id = variant.Id });
            var updatedVersion = AssetManagerApi.GetVersionGroups(item.Id).Single(group => group.Id == version.Id);

            Assert.That(files.Select(file => file.Id).ToArray(), Is.EqualTo(new[] { variantFile.Id, versionFile.Id }));
            Assert.That(versionFile.VersionGroupId, Is.EqualTo(version.Id));
            Assert.That(variantFile.VariantGroupId, Is.EqualTo(variant.Id));
            Assert.That(updatedVersion.PrimaryFileId, Is.EqualTo(versionFile.Id));
            Assert.That(dependencies.Single().Target.Type, Is.EqualTo(AssetDependencyEndpointType.VersionGroup));
            Assert.That(dependencies.Single().Target.Id, Is.EqualTo(version.Id));
        }

        [Test]
        [FeatureTestCase(
            "avatar names と version regex で group を自動作成する",
            "設定されたアバター名と version 正規表現で file 名から variant と version を抽出し、file を version group 配下へ登録することを確認します。",
            order: 313)]
        public void RegisterFile_AutoGroupsByConfiguredAvatarNamesAndVersionRegex()
        {
            SettingApi.Set(AssetManagerDefinitions.AvatarNames, "Chiffon,Lime,Manuka", saveImmediately: false);
            SettingApi.Set(AssetManagerDefinitions.VersionGroupRegex, @"(?i)v(?<name>\d+(?:\.\d+)*)", saveImmediately: false);
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var chiffonLimeFilePath = Path.Combine(_tempRoot, "Avatar_Chiffon＿Lime_v2.zip");
            var manukaFilePath = Path.Combine(_tempRoot, "Avatar_Manuka_v2.zip");
            var nextVersionFilePath = Path.Combine(_tempRoot, "Avatar_Chiffon＿Lime_v3.zip");
            File.WriteAllText(chiffonLimeFilePath, "zip");
            File.WriteAllText(manukaFilePath, "zip");
            File.WriteAllText(nextVersionFilePath, "zip");

            var chiffonLimeFile = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = chiffonLimeFilePath, FileName = "Avatar_Chiffon＿Lime_v2.zip" });
            var singleFile = AssetManagerApi.GetFiles(item.Id).Single();

            Assert.That(AssetManagerApi.GetVariantGroups(item.Id), Is.Empty);
            Assert.That(AssetManagerApi.GetVersionGroups(item.Id), Is.Empty);
            Assert.That(singleFile.ItemId, Is.EqualTo(item.Id));
            Assert.That(singleFile.VersionGroupId, Is.Null);
            Assert.That(singleFile.VariantGroupId, Is.Null);

            var manukaFile = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = manukaFilePath, FileName = "Avatar_Manuka_v2.zip" });
            var variants = AssetManagerApi.GetVariantGroups(item.Id).OrderBy(group => group.Name).ToArray();
            var files = AssetManagerApi.GetFiles(item.Id).OrderBy(file => file.FileName).ToArray();
            var variant = variants.Single(group => group.Name == "Avatar");
            var updatedChiffonLimeFile = files.Single(file => file.Id == chiffonLimeFile.Id);
            var updatedManukaFile = files.Single(file => file.Id == manukaFile.Id);

            Assert.That(variants.Select(group => group.Name).ToArray(), Is.EqualTo(new[] { "Avatar" }));
            Assert.That(AssetManagerApi.GetVersionGroups(item.Id), Is.Empty);
            Assert.That(updatedChiffonLimeFile.VariantGroupId, Is.EqualTo(variant.Id));
            Assert.That(updatedManukaFile.VariantGroupId, Is.EqualTo(variant.Id));

            var nextVersionFile = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = nextVersionFilePath, FileName = "Avatar_Chiffon＿Lime_v3.zip" });
            var versions = AssetManagerApi.GetVersionGroups(item.Id).OrderBy(group => group.VariantGroupId).ToArray();
            files = AssetManagerApi.GetFiles(item.Id).OrderBy(file => file.FileName).ToArray();
            var version = versions.Single(group => group.VariantGroupId == variant.Id);
            updatedChiffonLimeFile = files.Single(file => file.Id == chiffonLimeFile.Id);
            updatedManukaFile = files.Single(file => file.Id == manukaFile.Id);
            var updatedNextVersionFile = files.Single(file => file.Id == nextVersionFile.Id);

            Assert.That(version.Name, Is.EqualTo("Avatar"));
            Assert.That(version.PrimaryFileId, Is.EqualTo(chiffonLimeFile.Id));
            Assert.That(updatedChiffonLimeFile.VersionGroupId, Is.EqualTo(version.Id));
            Assert.That(updatedManukaFile.VersionGroupId, Is.EqualTo(version.Id));
            Assert.That(updatedNextVersionFile.VersionGroupId, Is.EqualTo(version.Id));
        }

        [Test]
        [FeatureTestCase(
            "file import target を複数保持する",
            "SetFileImportTargets が zip / directory 配下の複数 target を file 単位で保存することを確認します。",
            order: 314)]
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
                    new AssetFileImportTargetRequest { RelativePath = "\\Textures\\albedo.png" },
                    new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" }
                });

            var targets = AssetManagerApi.GetFileImportTargets(file.Id);

            Assert.That(targets.Select(target => target.RelativePath).ToArray(), Is.EqualTo(new[] { "Packages/avatar.unitypackage", "Textures/albedo.png" }));
        }

        [Test]
        [FeatureTestCase(
            "不正な import target path では既存 target を保持する",
            "SetFileImportTargets が parent traversal を拒否し、既存 file_import_target を削除しないことを確認します。",
            order: 315)]
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
            order: 316)]
        public void SetFileImportTargets_DoesNotRaiseAssetManagerChanged()
        {
            var item = AssetManagerApi.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = AssetManagerApi.RegisterFile(item.Id, new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip" });
            var changed = false;
            var fileTreeChanged = false;
            Action handler = () => changed = true;
            Action fileTreeHandler = () => fileTreeChanged = true;

            AssetManagerApi.Changed += handler;
            AssetManagerApi.FileTreeChanged += fileTreeHandler;
            try
            {
                AssetManagerApi.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" } });
            }
            finally
            {
                AssetManagerApi.Changed -= handler;
                AssetManagerApi.FileTreeChanged -= fileTreeHandler;
            }

            Assert.That(changed, Is.False);
            Assert.That(fileTreeChanged, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "存在しない親 collection 指定では collection を作成しない",
            "CreateCollection が missing parent を検出した場合に collection_info を残さないことを確認します。",
            order: 317)]
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
            order: 318)]
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
            order: 319)]
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
            "Eagle sync は VRCAsset folder から item を作成する",
            "SyncEagle が Booth metadata を item 情報として扱い、metadata file を通常 file から除外することを確認します。",
            order: 320)]
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
            "Eagle 起動時同期は fingerprint が一致すれば再同期しない",
            "成功時の fingerprint を library cache に保存し、同じ datasource snapshot の事前確認が無変更になることを確認します。",
            order: 321)]
        public void PrepareEagleSync_UnchangedFingerprintSkipsSynchronization()
        {
            var libraryPath = CreateEagleLibrary("fingerprint.library", "Avatar");
            var first = AssetManagerDatabase.PrepareEagleSync(new EagleSyncRequest(libraryPath));

            Assert.That(first.Preview.HasChanges, Is.True);
            Assert.That(AssetManagerDatabase.ApplyPreparedEagleSync(first, true).State, Is.EqualTo(AssetSyncState.Success));

            var second = AssetManagerDatabase.PrepareEagleSync(new EagleSyncRequest(libraryPath));

            Assert.That(second.Preview.HasChanges, Is.False);
            Assert.That(second.Preview.Conflicts, Is.Empty);
            Assert.That(AssetSyncFingerprintCache.GetPath(AssetSourceType.Eagle), Does.StartWith(Path.Combine(_tempRoot, "cache", "sync")));
            Assert.That(File.Exists(AssetSyncFingerprintCache.GetPath(AssetSourceType.Eagle)), Is.True);
        }

        [Test]
        [FeatureTestCase(
            "Eagle 同期は新しい Unity item を競合として検出する",
            "前回取り込み後に Unity item が更新され、Eagle の名前も変わった場合に差分を返し、承認後は同期元の値で上書きすることを確認します。",
            order: 322)]
        public void PrepareEagleSync_NewerUnityItemRequiresOverwriteConfirmation()
        {
            var libraryPath = CreateEagleLibrary("conflict.library", "Before");
            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"From Eagle\",\"children\":[]}]}]}");
            using (var connection = new SQLiteConnection(GetDatabasePath()))
            {
                connection.Execute(
                    "UPDATE item_info SET name = ?, updated_at = ? WHERE id = ?",
                    "From Unity",
                    DateTime.UtcNow.AddDays(1).ToString("O"),
                    item.Id);
            }

            var prepared = AssetManagerDatabase.PrepareEagleSync(new EagleSyncRequest(libraryPath));

            Assert.That(prepared.Preview.HasChanges, Is.True);
            Assert.That(prepared.Preview.Conflicts.Count, Is.EqualTo(1));
            Assert.That(prepared.Preview.Conflicts[0].Fields.Single().UnityValue, Is.EqualTo("From Unity"));
            Assert.That(prepared.Preview.Conflicts[0].Fields.Single().DatasourceValue, Is.EqualTo("From Eagle"));

            AssetManagerDatabase.ApplyPreparedEagleSync(prepared, true);

            Assert.That(AssetManagerApi.GetItem(item.Id).Name, Is.EqualTo("From Eagle"));
        }

        [Test]
        [FeatureTestCase(
            "BLM 同期は新しい Unity item を競合として検出する",
            "Unity item の更新時刻が BLM の更新時刻より新しい場合に差分を返し、承認後は BLM の値で上書きすることを確認します。",
            order: 323)]
        public void PrepareBlmSync_NewerUnityItemRequiresOverwriteConfirmation()
        {
            var databasePath = Path.Combine(_tempRoot, "blm-conflict.db");
            CreateBlmDatabase(databasePath, "registered-item");
            AssetManagerApi.SyncBlm(new BlmSyncRequest(databasePath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            using (var source = new SQLiteConnection(databasePath))
            {
                source.Execute("INSERT INTO overwritten_booth_items(booth_item_id, name, description) VALUES (123, 'From BLM', 'desc')");
                source.Execute("INSERT INTO booth_item_update_history(booth_item_id, last_updated_at) VALUES (123, ?)", DateTime.UtcNow.AddDays(-1).ToString("O"));
            }

            using (var connection = new SQLiteConnection(GetDatabasePath()))
            {
                connection.Execute(
                    "UPDATE item_info SET name = ?, updated_at = ? WHERE id = ?",
                    "From Unity",
                    DateTime.UtcNow.AddDays(1).ToString("O"),
                    item.Id);
            }

            var prepared = AssetManagerDatabase.PrepareBlmSync(new BlmSyncRequest(databasePath));

            Assert.That(prepared.Preview.HasChanges, Is.True);
            Assert.That(prepared.Preview.Conflicts.Count, Is.EqualTo(1));
            Assert.That(prepared.Preview.Conflicts[0].Fields.Single().DatasourceValue, Is.EqualTo("From BLM"));

            AssetManagerDatabase.ApplyPreparedBlmSync(prepared, true);

            Assert.That(AssetManagerApi.GetItem(item.Id).Name, Is.EqualTo("From BLM"));
        }

        [Test]
        [FeatureTestCase(
            "Eagle folder rename は同じ item を更新する",
            "folder id を datasource identity として使い、folder 名変更で item が重複しないことを確認します。",
            order: 330)]
        public void SyncEagle_RenamedFolder_ReusesItemIdentity()
        {
            var libraryPath = Path.Combine(_tempRoot, "renamed.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            var metadataPath = Path.Combine(libraryPath, "metadata.json");
            File.WriteAllText(metadataPath, "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Before\",\"children\":[]}]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "zip", null);

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var before = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            File.WriteAllText(metadataPath, "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"After\",\"children\":[]}]}]}");

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var after = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.Name, Is.EqualTo("After"));
        }

        [Test]
        [FeatureTestCase(
            "Eagle snapshot から消えた file を unavailable にする",
            "再同期で消えた Eagle origin を保持しつつ通常検索と path 解決から除外することを確認します。",
            order: 331)]
        public void SyncEagle_MissingFile_BecomesUnavailable()
        {
            var libraryPath = Path.Combine(_tempRoot, "missing-file.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(Path.Combine(libraryPath, "metadata.json"), "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "zip", null);
            var payloadPath = Path.Combine(imagesPath, "file-entry.info", "avatar.zip");
            File.WriteAllText(payloadPath, "payload");

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = AssetManagerApi.GetFiles(item.Id).Single();
            Directory.Delete(Path.Combine(imagesPath, "file-entry.info"), true);

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var unavailable = AssetManagerApi.GetFiles(item.Id, new AssetFileQuery { IncludeUnavailable = true }).Single();

            Assert.That(AssetManagerApi.GetFiles(item.Id), Is.Empty);
            Assert.That(unavailable.Id, Is.EqualTo(file.Id));
            Assert.That(unavailable.IsAvailable, Is.False);
            Assert.That(unavailable.Origins.Single().IsAvailable, Is.False);
            Assert.That(AssetManagerApi.ResolveFilePath(file.Id).Found, Is.False);
        }

        [Test]
        [FeatureTestCase(
            "Eagle datasource tag を snapshot として保存する",
            "Booth metadata の tags が user tag と分離された datasource tags として返ることを確認します。",
            order: 332)]
        public void SyncEagle_StoresDatasourceTags()
        {
            var libraryPath = Path.Combine(_tempRoot, "tags.library");
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(Path.Combine(libraryPath, "metadata.json"), "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"Avatar\",\"children\":[]}]}]}");
            CreateEagleEntry(imagesPath, "boothmeta-entry", "avatar-folder", "_boothmeta", "json", "{\"boothItemId\":123,\"name\":\"Avatar\",\"description\":\"\",\"tags\":[\"avatar\",\"quest\"],\"downloads\":[]}");

            AssetManagerApi.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = AssetManagerApi.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

            Assert.That(item.Booth.DatasourceTags, Is.EqualTo(new[] { "avatar", "quest" }));
            Assert.That(item.Tags, Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "Eagle sync は Booth metadata の downloadId を保存する",
            "importedItemIds が一致する Booth download の downloadId、filename、extension を file_info に保存することを確認します。",
            order: 321)]
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
            order: 322)]
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
            order: 323)]
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
            order: 324)]
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
            order: 325)]
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
                    @"INSERT INTO file_info(id, item_info_id, file_name, extension, size_bytes, download_id, lifecycle, created_at, updated_at)
                      VALUES (?, ?, 'conflict.zip', 'zip', NULL, 456, 'active', ?, ?)",
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
            order: 326)]
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
            order: 327)]
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
            order: 328)]
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
            order: 329)]
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

        [Test]
        [FeatureTestCase(
            "file tree archive cache を library の cache に保存する",
            "ZIP metadata cache を永続化し、source 更新時に非同期読み込み用snapshotを再生成できることを確認します。",
            order: 330)]
        public void FileTreeCache_PersistsAndInvalidatesArchiveEntries()
        {
            var zipPath = Path.Combine(_tempRoot, "archive.zip");
            CreateZip(zipPath, "Assets/first.txt");
            var originalWriteTime = File.GetLastWriteTimeUtc(zipPath);
            var cacheDirectory = AssetFileTreeCache.ResolveCacheDirectory();

            var first = AssetFileTreeCache.ReadZipEntries(cacheDirectory, zipPath, CancellationToken.None);
            var second = AssetFileTreeCache.ReadZipEntries(cacheDirectory, zipPath, CancellationToken.None);

            Assert.That(first.Select(entry => entry.FullName), Is.EqualTo(new[] { "Assets/first.txt" }));
            Assert.That(second.Select(entry => entry.FullName), Is.EqualTo(new[] { "Assets/first.txt" }));
            Assert.That(Directory.GetFiles(cacheDirectory, "*.ftc").Length, Is.EqualTo(1));

            CreateZip(zipPath, "Assets/first.txt", "Assets/second.txt");
            File.SetLastWriteTimeUtc(zipPath, originalWriteTime.AddSeconds(2));
            var updated = AssetFileTreeCache.ReadZipEntries(cacheDirectory, zipPath, CancellationToken.None);

            Assert.That(updated.Select(entry => entry.FullName), Is.EqualTo(new[] { "Assets/first.txt", "Assets/second.txt" }));
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

        private string CreateEagleLibrary(string directoryName, string folderName)
        {
            var libraryPath = Path.Combine(_tempRoot, directoryName);
            var imagesPath = Path.Combine(libraryPath, "images");
            Directory.CreateDirectory(imagesPath);
            File.WriteAllText(
                Path.Combine(libraryPath, "metadata.json"),
                "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"" + folderName + "\",\"children\":[]}]}]}");
            CreateEagleEntry(imagesPath, "file-entry", "avatar-folder", "avatar", "zip", null);
            return libraryPath;
        }

        private static void CreateZip(string path, params string[] entryNames)
        {
            using (var stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                for (var i = 0; i < entryNames.Length; i++)
                {
                    using (var writer = new StreamWriter(archive.CreateEntry(entryNames[i]).Open()))
                    {
                        writer.Write(entryNames[i]);
                    }
                }
            }
        }
    }
}
