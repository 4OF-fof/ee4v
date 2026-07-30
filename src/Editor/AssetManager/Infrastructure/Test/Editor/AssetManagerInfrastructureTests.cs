using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Ee4v.Testing.Contracts;
using Ee4v.AssetManager.Infrastructure.Files;
using Ee4v.AssetManager.Infrastructure.Persistence.SQLite;
using NUnit.Framework;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Tests
{
    public sealed class AssetManagerInfrastructureTests
    {
        private IAssetManager _assetManager;
        private string _tempRoot;
        private TestInfrastructureSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ee4v-asset-manager-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            _settings = new TestInfrastructureSettings
            {
                GlobalPath = _tempRoot
            };
            AssetManagerInfrastructure.ConfigureSettings(_settings);
            _assetManager = AssetManagerInfrastructure.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        [Test]
        [FeatureTestCase(
            "schema version 7 の DB 制約を作成する",
            "AssetManager DB が source origin、availability、collection hierarchy trigger を作成することを確認します。",
            order: 301)]
        public void Schema_CreatesVersion7Constraints()
        {
            var databasePath = GetDatabasePath();

            _assetManager.GetTags();

            using (var connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.PrivateCache))
            {
                Assert.That(connection.ExecuteScalar<int>("SELECT version FROM schema_version LIMIT 1"), Is.EqualTo(7));
                Assert.That(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'file_info'"), Does.Contain("CHECK"));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'item_source_origin'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'datasource_tag'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'file_import_target'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'unique_file_import_target_file_path'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'file_imported_asset_guid'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'index_file_imported_asset_guid_asset'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = 'prevent_collection_collection_cycle_insert'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = 'prevent_smart_collection_parent_insert'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = 'prevent_collection_with_children_becoming_smart_insert'"), Is.EqualTo(1));
                Assert.That(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'collection_info'"), Does.Contain("icon"));
                Assert.That(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'collection_info'"), Does.Contain("icon_asset_guid"));
                Assert.That(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'collection_info'"), Does.Contain("sort_order"));
                var smartConditionSql = connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'smart_collection_condition'");
                Assert.That(smartConditionSql, Does.Not.Contain("source_type"));
                Assert.That(smartConditionSql, Does.Not.Contain("lifecycle"));
            }
        }

        [Test]
        [FeatureTestCase(
            "collection cycle を拒否する",
            "親 collection を子 collection 配下へ移動しようとした場合に CollectionCycle として拒否することを確認します。",
            order: 302)]
        public void MoveCollection_RejectsCycles()
        {
            var parent = _assetManager.CreateCollection(new CreateCollectionRequest { Name = "Parent" });
            var child = _assetManager.CreateCollection(new CreateCollectionRequest { Name = "Child", ParentCollectionId = parent.Id });

            var ex = Assert.Throws<AssetManagerException>(() => _assetManager.MoveCollection(parent.Id, child.Id));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.CollectionCycle));
        }

        [Test]
        [FeatureTestCase(
            "Smart Collection を子にできるが親にはできない",
            "MoveCollection が通常 Collection 配下への Smart Collection 移動だけを許可することを確認します。",
            order: 339)]
        public void MoveCollection_RejectsSmartCollectionAsParent()
        {
            var regular = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Regular" });
            var smart = _assetManager.CreateSmartCollection(
                new CreateSmartCollectionRequest
                {
                    Name = "Smart",
                    Icon = AssetCollectionIcon.Key,
                    MatchMode = SmartCollectionMatchMode.All,
                    Conditions = new[]
                    {
                        new SmartCollectionCondition
                        {
                            Field = SmartCollectionConditionField.Name,
                            Operator =
                                SmartCollectionConditionOperator.Contains,
                            QueryText = "avatar"
                        }
                    }
                });

            _assetManager.MoveCollection(smart.Id, regular.Id);

            Assert.That(
                _assetManager.GetCollections()
                    .Single(item => item.Id == smart.Id)
                    .ParentCollectionId,
                Is.EqualTo(regular.Id));

            _assetManager.MoveCollection(smart.Id, null);
            var ex = Assert.Throws<AssetManagerException>(() =>
                _assetManager.MoveCollection(regular.Id, smart.Id));

            Assert.That(
                ex.Code,
                Is.EqualTo(
                    AssetManagerErrorCode.InvalidCollectionHierarchy));
            Assert.That(
                _assetManager.GetCollections()
                    .Single(item => item.Id == regular.Id)
                    .ParentCollectionId,
                Is.Null);
        }

        [Test]
        [FeatureTestCase(
            "Smart Collection 配下への Collection 作成を拒否する",
            "通常 Collection と Smart Collection のどちらも Smart Collection の子として作成されず、DB に残らないことを確認します。",
            order: 343)]
        public void CreateCollections_RejectSmartCollectionAsParent()
        {
            var smart = _assetManager.CreateSmartCollection(
                new CreateSmartCollectionRequest
                {
                    Name = "Smart",
                    Icon = AssetCollectionIcon.Search,
                    MatchMode = SmartCollectionMatchMode.All,
                    Conditions = new[]
                    {
                        new SmartCollectionCondition
                        {
                            Field = SmartCollectionConditionField.Name,
                            Operator =
                                SmartCollectionConditionOperator.Contains,
                            QueryText = "avatar"
                        }
                    }
                });

            var regularEx = Assert.Throws<AssetManagerException>(() =>
                _assetManager.CreateCollection(
                    new CreateCollectionRequest
                    {
                        Name = "Regular child",
                        ParentCollectionId = smart.Id
                    }));
            var smartEx = Assert.Throws<AssetManagerException>(() =>
                _assetManager.CreateSmartCollection(
                    new CreateSmartCollectionRequest
                    {
                        Name = "Smart child",
                        ParentCollectionId = smart.Id,
                        Icon = AssetCollectionIcon.Search,
                        MatchMode = SmartCollectionMatchMode.All,
                        Conditions = new[]
                        {
                            new SmartCollectionCondition
                            {
                                Field =
                                    SmartCollectionConditionField.Name,
                                Operator =
                                    SmartCollectionConditionOperator.Contains,
                                QueryText = "avatar"
                            }
                        }
                    }));

            Assert.That(
                regularEx.Code,
                Is.EqualTo(
                    AssetManagerErrorCode.InvalidCollectionHierarchy));
            Assert.That(
                smartEx.Code,
                Is.EqualTo(
                    AssetManagerErrorCode.InvalidCollectionHierarchy));
            Assert.That(
                _assetManager.GetCollections()
                    .Select(collection => collection.Name),
                Is.EqualTo(new[] { "Smart" }));
        }

        [Test]
        [FeatureTestCase(
            "Collection の兄弟順を保存する",
            "MoveCollection が root と子階層の指定 index へ移動し、SortOrder を正規化することを確認します。",
            order: 340)]
        public void MoveCollection_PersistsSiblingOrder()
        {
            var first = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "First" });
            var second = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Second" });
            var third = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Third" });

            _assetManager.MoveCollection(third.Id, null, 0);

            var roots = _assetManager.GetCollections()
                .Where(item =>
                    string.IsNullOrWhiteSpace(
                        item.ParentCollectionId))
                .OrderBy(item => item.SortOrder)
                .ToArray();
            Assert.That(
                roots.Select(item => item.Id).ToArray(),
                Is.EqualTo(new[]
                {
                    third.Id,
                    first.Id,
                    second.Id
                }));
            Assert.That(
                roots.Select(item => item.SortOrder).ToArray(),
                Is.EqualTo(new[] { 0, 1, 2 }));

            _assetManager.MoveCollection(first.Id, third.Id, 0);
            _assetManager.MoveCollection(second.Id, third.Id, 0);

            var children = _assetManager.GetCollections()
                .Where(item =>
                    item.ParentCollectionId == third.Id)
                .OrderBy(item => item.SortOrder)
                .ToArray();
            Assert.That(
                children.Select(item => item.Id).ToArray(),
                Is.EqualTo(new[]
                {
                    second.Id,
                    first.Id
                }));
            Assert.That(
                children.Select(item => item.SortOrder).ToArray(),
                Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        [FeatureTestCase(
            "複数 Collection を順序を保って移動する",
            "MoveCollections が選択順を保ったブロックとして兄弟間・親子間を移動することを確認します。",
            order: 341)]
        public void MoveCollections_PersistsSelectionOrderAsBlock()
        {
            var first = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "First" });
            var second = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Second" });
            var third = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Third" });
            var target = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Target" });

            _assetManager.MoveCollections(
                new[] { second.Id, third.Id },
                null,
                0);

            Assert.That(
                _assetManager.GetCollections()
                    .Where(item => string.IsNullOrWhiteSpace(
                        item.ParentCollectionId))
                    .OrderBy(item => item.SortOrder)
                    .Select(item => item.Id)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    second.Id,
                    third.Id,
                    first.Id,
                    target.Id
                }));

            _assetManager.MoveCollections(
                new[] { second.Id, third.Id },
                target.Id,
                0);

            Assert.That(
                _assetManager.GetCollections()
                    .Where(item =>
                        item.ParentCollectionId == target.Id)
                    .OrderBy(item => item.SortOrder)
                    .Select(item => item.Id)
                    .ToArray(),
                Is.EqualTo(new[] { second.Id, third.Id }));
        }

        [Test]
        [FeatureTestCase(
            "Collection 構造変更を専用通知として発行する",
            "Collection の作成・移動が Catalog ではなく Collections 変更を発行することを確認します。",
            order: 342)]
        public void CollectionChanges_PublishCollectionsWithoutCatalog()
        {
            var changes = new List<AssetManagerChange>();
            _assetManager.Changed += changes.Add;

            var first = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "First" });
            var second = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Second" });
            _assetManager.MoveCollection(second.Id, first.Id);

            Assert.That(
                changes.Select(change => change.Kind),
                Is.All.EqualTo(
                    AssetManagerChangeKind.Collections));
            Assert.That(changes.Count, Is.EqualTo(3));
        }

        [Test]
        [FeatureTestCase(
            "Collection の名前・条件・削除を更新する",
            "通常 Collection の表示情報、Smart Collection の条件、子孫 Collection の再帰削除と Item の保持を確認します。",
            order: 344)]
        public void CollectionCommands_UpdateAndDeleteCollections()
        {
            var parent = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Parent" });
            var child = _assetManager.CreateCollection(
                new CreateCollectionRequest
                {
                    Name = "Child",
                    ParentCollectionId = parent.Id
                });
            var sibling = _assetManager.CreateCollection(
                new CreateCollectionRequest
                {
                    Name = "Sibling"
                });
            var smart = _assetManager.CreateSmartCollection(
                new CreateSmartCollectionRequest
                {
                    Name = "Smart",
                    Icon = AssetCollectionIcon.Search,
                    ParentCollectionId = child.Id,
                    MatchMode = SmartCollectionMatchMode.All,
                    Conditions = new[]
                    {
                        new SmartCollectionCondition
                        {
                            Field =
                                SmartCollectionConditionField.Name,
                            Operator =
                                SmartCollectionConditionOperator.Contains,
                            QueryText = "old"
                        }
                    }
                });
            var retainedItem = _assetManager.CreateItem(
                new CreateAssetItemRequest
                {
                    Name = "Retained Item",
                    CollectionIds = new[] { child.Id }
                });

            var renamed = _assetManager.UpdateCollection(
                parent.Id,
                new UpdateCollectionRequest
                {
                    Name = "Renamed",
                    Icon = AssetCollectionIcon.Search,
                    IconAssetGuid = "ignored"
                });
            var updatedPresentation =
                _assetManager.UpdateCollection(
                    smart.Id,
                    new UpdateCollectionRequest
                    {
                        Name = "Smart Renamed",
                        Icon = AssetCollectionIcon.Star
                    });
            var updated = _assetManager.UpdateSmartCollection(
                smart.Id,
                new UpdateSmartCollectionRequest
                {
                    MatchMode = SmartCollectionMatchMode.Any,
                    Conditions = new[]
                    {
                        new SmartCollectionCondition
                        {
                            Field =
                                SmartCollectionConditionField.Tag,
                            Operator =
                                SmartCollectionConditionOperator.Equals,
                            QueryText = "avatar"
                        },
                        new SmartCollectionCondition
                        {
                            Field =
                                SmartCollectionConditionField.Extension,
                            Operator =
                                SmartCollectionConditionOperator.Exists
                        }
                    }
                });
            var deletionChanges = new List<AssetManagerChange>();
            _assetManager.Changed += deletionChanges.Add;
            _assetManager.DeleteCollection(parent.Id);

            Assert.That(renamed.Name, Is.EqualTo("Renamed"));
            Assert.That(
                renamed.Icon,
                Is.EqualTo(AssetCollectionIcon.Folder));
            Assert.That(renamed.IconAssetGuid, Is.Null);
            Assert.That(
                updatedPresentation.Name,
                Is.EqualTo("Smart Renamed"));
            Assert.That(
                updatedPresentation.Icon,
                Is.EqualTo(AssetCollectionIcon.Star));
            Assert.That(
                updated.SmartRule.MatchMode,
                Is.EqualTo(SmartCollectionMatchMode.Any));
            Assert.That(
                updated.SmartRule.Conditions
                    .Select(condition => condition.Field)
                    .ToArray(),
                Is.EquivalentTo(new[]
                {
                    SmartCollectionConditionField.Tag,
                    SmartCollectionConditionField.Extension
                }));
            var collections = _assetManager.GetCollections();
            Assert.That(
                collections.Any(item => item.Id == parent.Id),
                Is.False);
            Assert.That(
                collections.Any(item => item.Id == child.Id),
                Is.False);
            Assert.That(
                collections.Any(item => item.Id == smart.Id),
                Is.False);
            Assert.That(
                collections.Single(item => item.Id == sibling.Id)
                    .SortOrder,
                Is.EqualTo(0));
            Assert.That(
                _assetManager.GetItem(retainedItem.Id).Name,
                Is.EqualTo("Retained Item"));
            Assert.That(
                deletionChanges
                    .Select(change => change.Kind)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    AssetManagerChangeKind.Collections,
                    AssetManagerChangeKind.SmartCollectionRule
                }));
        }

        [Test]
        [FeatureTestCase(
            "複数 Collection を一括削除する",
            "親子を含む複数選択を重複なく削除し、残った兄弟順を正規化することを確認します。",
            order: 346)]
        public void DeleteCollections_DeletesSelectedSubtreesAtomically()
        {
            var parent = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Parent" });
            var child = _assetManager.CreateCollection(
                new CreateCollectionRequest
                {
                    Name = "Child",
                    ParentCollectionId = parent.Id
                });
            var sibling = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Sibling" });
            var retained = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Retained" });

            _assetManager.DeleteCollections(new[]
            {
                parent.Id,
                child.Id,
                sibling.Id
            });

            var collections = _assetManager.GetCollections();
            Assert.That(
                collections.Select(collection => collection.Id),
                Is.EqualTo(new[] { retained.Id }));
            Assert.That(
                collections.Single().SortOrder,
                Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "Smart Collection 条件変更を検索結果へ通知する",
            "Smart Collection の作成・条件更新・削除が Collections と SmartCollectionRule の両方を発行することを確認します。",
            order: 345)]
        public void SmartCollectionCommands_PublishRuleChanges()
        {
            var changes = new List<AssetManagerChange>();
            _assetManager.Changed += changes.Add;

            var smart = _assetManager.CreateSmartCollection(
                new CreateSmartCollectionRequest
                {
                    Name = "Smart",
                    Icon = AssetCollectionIcon.Search,
                    MatchMode = SmartCollectionMatchMode.All,
                    Conditions = new[]
                    {
                        new SmartCollectionCondition
                        {
                            Field =
                                SmartCollectionConditionField.Name,
                            Operator =
                                SmartCollectionConditionOperator.Contains,
                            QueryText = "old"
                        }
                    }
                });

            Assert.That(
                changes.Select(change => change.Kind).ToArray(),
                Is.EqualTo(new[]
                {
                    AssetManagerChangeKind.Collections,
                    AssetManagerChangeKind.SmartCollectionRule
                }));
            Assert.That(
                changes[1].SubjectId,
                Is.EqualTo(smart.Id));

            changes.Clear();
            _assetManager.UpdateSmartCollection(
                smart.Id,
                new UpdateSmartCollectionRequest
                {
                    MatchMode = SmartCollectionMatchMode.Any,
                    Conditions = new[]
                    {
                        new SmartCollectionCondition
                        {
                            Field =
                                SmartCollectionConditionField.Tag,
                            Operator =
                                SmartCollectionConditionOperator.Exists
                        }
                    }
                });
            Assert.That(
                changes.Select(change => change.Kind).ToArray(),
                Is.EqualTo(new[]
                {
                    AssetManagerChangeKind.Collections,
                    AssetManagerChangeKind.SmartCollectionRule
                }));

            changes.Clear();
            _assetManager.DeleteCollection(smart.Id);
            Assert.That(
                changes.Select(change => change.Kind).ToArray(),
                Is.EqualTo(new[]
                {
                    AssetManagerChangeKind.Collections,
                    AssetManagerChangeKind.SmartCollectionRule
                }));
        }

        [Test]
        [FeatureTestCase(
            "存在しない item の更新は NotFound",
            "UpdateItem が missing item に対して NotFound を返すことを確認します。",
            order: 303)]
        public void UpdateItem_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                _assetManager.UpdateItem("missing-item", new UpdateAssetItemRequest { Name = "Updated" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "存在しない item の file 取得は NotFound",
            "GetFiles が missing item に対して NotFound を返すことを確認します。",
            order: 304)]
        public void GetFiles_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() => _assetManager.GetFiles("missing-item"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "存在しない item の thumbnail 取得は NotFound",
            "GetThumbnail が missing item に対して NotFound を返すことを確認します。",
            order: 305)]
        public void GetThumbnail_MissingItem_ThrowsNotFound()
        {
            var ex = Assert.Throws<AssetManagerException>(() => _assetManager.GetThumbnail("missing-item"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "thumbnail URL がない item は missing thumbnail",
            "thumbnail_url を持たない item の GetThumbnail が missing 結果を返すことを確認します。",
            order: 306)]
        public void GetThumbnail_ItemWithoutThumbnailUrl_ReturnsMissing()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });

            var thumbnail = _assetManager.GetThumbnail(item.Id);

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
            var ex = Assert.Throws<AssetManagerException>(() => _assetManager.ArchiveFile("missing-file"));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
        }

        [Test]
        [FeatureTestCase(
            "存在しない tag の設定は NotFound",
            "SetItemTags が missing tag に対して NotFound を返すことを確認します。",
            order: 308)]
        public void SetItemTags_MissingTag_ThrowsNotFound()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });

            var ex = Assert.Throws<AssetManagerException>(() => _assetManager.SetItemTags(item.Id, new[] { "missing-tag" }));

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
                _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item", CollectionIds = new[] { "missing-collection" } }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(_assetManager.SearchItems(new AssetItemQuery()).TotalCount, Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "collection 設定失敗時に既存所属を保持する",
            "SetItemCollections が missing collection で失敗しても既存 item_collection を削除しないことを確認します。",
            order: 310)]
        public void SetItemCollections_MissingCollection_DoesNotClearExistingCollections()
        {
            var collection = _assetManager.CreateCollection(new CreateCollectionRequest { Name = "Collection" });
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item", CollectionIds = new[] { collection.Id } });

            var ex = Assert.Throws<AssetManagerException>(() => _assetManager.SetItemCollections(item.Id, new[] { "missing-collection" }));
            var result = _assetManager.SearchItems(new AssetItemQuery { CollectionId = collection.Id });

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(result.Items.Select(collectionItem => collectionItem.Id).ToArray(), Is.EqualTo(new[] { item.Id }));
        }

        [Test]
        [FeatureTestCase(
            "drop 登録で既存 collection 所属を保持する",
            "AddItemsToCollection が複数 item を追加し、既存の別 collection 所属を置換しないことを確認します。",
            order: 311)]
        public void AddItemsToCollection_PreservesExistingMemberships()
        {
            var existing = _assetManager.CreateCollection(
                new CreateCollectionRequest
                {
                    Name = "Existing"
                });
            var target = _assetManager.CreateCollection(
                new CreateCollectionRequest
                {
                    Name = "Target"
                });
            var first = _assetManager.CreateItem(
                new CreateAssetItemRequest
                {
                    Name = "First",
                    CollectionIds = new[] { existing.Id }
                });
            var second = _assetManager.CreateItem(
                new CreateAssetItemRequest
                {
                    Name = "Second"
                });
            var changes = new List<AssetManagerChange>();
            _assetManager.Changed += changes.Add;

            _assetManager.AddItemsToCollection(
                new[] { first.Id, second.Id, first.Id },
                target.Id);

            Assert.That(
                _assetManager.SearchItems(
                        new AssetItemQuery
                        {
                            CollectionId = existing.Id
                        })
                    .Items.Select(item => item.Id),
                Is.EqualTo(new[] { first.Id }));
            Assert.That(
                _assetManager.SearchItems(
                        new AssetItemQuery
                        {
                            CollectionId = target.Id
                        })
                    .Items.Select(item => item.Id),
                Is.EquivalentTo(
                    new[] { first.Id, second.Id }));
            Assert.That(
                changes.Select(change => change.Kind),
                Is.EqualTo(
                    new[]
                    {
                        AssetManagerChangeKind.ItemCollections
                    }));
            Assert.That(
                changes.Single().RelatedId,
                Is.EqualTo(target.Id));

            changes.Clear();
            _assetManager.AddItemsToCollection(
                new[] { first.Id, second.Id },
                target.Id);
            Assert.That(changes, Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "drop 登録失敗を atomic に扱う",
            "AddItemsToCollection に存在しない item が含まれる場合、ほかの item も追加しないことを確認します。",
            order: 312)]
        public void AddItemsToCollection_MissingItem_AddsNothing()
        {
            var target = _assetManager.CreateCollection(
                new CreateCollectionRequest
                {
                    Name = "Target"
                });
            var item = _assetManager.CreateItem(
                new CreateAssetItemRequest
                {
                    Name = "Item"
                });

            var exception =
                Assert.Throws<AssetManagerException>(() =>
                    _assetManager.AddItemsToCollection(
                        new[] { item.Id, "missing-item" },
                        target.Id));

            Assert.That(
                exception.Code,
                Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(
                _assetManager.SearchItems(
                        new AssetItemQuery
                        {
                            CollectionId = target.Id
                        })
                    .Items,
                Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "Booth 情報の有無で item を絞り込む",
            "HasBoothInformation が BLM source ではなく booth_info snapshot の存在だけを条件にすることを確認します。",
            order: 336)]
        public void SearchItems_HasBoothInformation_FiltersByBoothSnapshot()
        {
            var boothItem = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Booth Item" });
            _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Plain Item" });
            using (var connection = new SQLiteConnection(GetDatabasePath()))
            {
                connection.Execute(
                    "INSERT INTO shop_info(id, name, subdomain) VALUES (?, ?, ?)",
                    "shop-1",
                    "Shop",
                    "shop");
                connection.Execute(
                    "INSERT INTO booth_info(id, item_info_id, booth_item_id, shop_info_id, name, description) VALUES (?, ?, ?, ?, ?, ?)",
                    "booth-1",
                    boothItem.Id,
                    123L,
                    "shop-1",
                    "Booth Item",
                    string.Empty);
            }

            var result = _assetManager.SearchItems(new AssetItemQuery
            {
                HasBoothInformation = true
            });

            Assert.That(result.Items.Select(item => item.Id).ToArray(), Is.EqualTo(new[] { boothItem.Id }));
        }

        [Test]
        [FeatureTestCase(
            "通常・Smart Collection 未所属の item を絞り込む",
            "UncategorizedOnly が直接所属と Smart Collection 条件一致の両方を除外することを確認します。",
            order: 337)]
        public void SearchItems_UncategorizedOnly_ExcludesRegularAndSmartCollectionMembers()
        {
            var regularCollection = _assetManager.CreateCollection(
                new CreateCollectionRequest { Name = "Regular" });
            var uncategorizedItem = _assetManager.CreateItem(
                new CreateAssetItemRequest { Name = "Uncategorized" });
            _assetManager.CreateItem(new CreateAssetItemRequest
            {
                Name = "Regular Member",
                CollectionIds = new[] { regularCollection.Id }
            });
            _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Smart Member" });
            _assetManager.CreateSmartCollection(new CreateSmartCollectionRequest
            {
                Name = "Smart",
                MatchMode = SmartCollectionMatchMode.All,
                Conditions = new[]
                {
                    new SmartCollectionCondition
                    {
                        Field = SmartCollectionConditionField.Name,
                        Operator = SmartCollectionConditionOperator.Equals,
                        QueryText = "Smart Member"
                    }
                }
            });

            var result = _assetManager.SearchItems(new AssetItemQuery
            {
                UncategorizedOnly = true
            });

            Assert.That(
                result.Items.Select(item => item.Id).ToArray(),
                Is.EqualTo(new[] { uncategorizedItem.Id }));
        }

        [Test]
        [FeatureTestCase(
            "一覧 snapshot で Smart Collection 条件を一括評価する",
            "name、description、tag、file name、extension の条件が DB 再問い合わせなしの一覧 snapshot に反映されることを確認します。",
            order: 338)]
        public void SearchItemSummaries_SnapshotEvaluatesAllSmartFields()
        {
            var item = _assetManager.CreateItem(
                new CreateAssetItemRequest
                {
                    Name = "Avatar Package",
                    Description = "Summer outfit"
                });
            var tag = _assetManager.CreateTag("favorite");
            _assetManager.SetItemTags(
                item.Id,
                new[] { tag.Id });
            var filePath =
                Path.Combine(_tempRoot, "avatar.unitypackage");
            File.WriteAllText(filePath, "package");
            _assetManager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    FilePath = filePath,
                    FileName = "avatar.unitypackage"
                });
            var smart =
                _assetManager.CreateSmartCollection(
                    new CreateSmartCollectionRequest
                    {
                        Name = "Matched",
                        MatchMode =
                            SmartCollectionMatchMode.All,
                        Conditions = new[]
                        {
                            new SmartCollectionCondition
                            {
                                Field =
                                    SmartCollectionConditionField
                                        .Name,
                                Operator =
                                    SmartCollectionConditionOperator
                                        .Contains,
                                QueryText = "Avatar"
                            },
                            new SmartCollectionCondition
                            {
                                Field =
                                    SmartCollectionConditionField
                                        .Description,
                                Operator =
                                    SmartCollectionConditionOperator
                                        .Contains,
                                QueryText = "Summer"
                            },
                            new SmartCollectionCondition
                            {
                                Field =
                                    SmartCollectionConditionField
                                        .Tag,
                                Operator =
                                    SmartCollectionConditionOperator
                                        .Equals,
                                QueryText = "favorite"
                            },
                            new SmartCollectionCondition
                            {
                                Field =
                                    SmartCollectionConditionField
                                        .FileName,
                                Operator =
                                    SmartCollectionConditionOperator
                                        .Contains,
                                QueryText = "avatar"
                            },
                            new SmartCollectionCondition
                            {
                                Field =
                                    SmartCollectionConditionField
                                        .Extension,
                                Operator =
                                    SmartCollectionConditionOperator
                                        .In,
                                QueryText =
                                    "zip, unitypackage"
                            }
                        }
                    });

            var result =
                _assetManager.SearchItemSummaries(
                    new AssetItemQuery
                    {
                        CollectionId = smart.Id,
                        Limit = 200
                    });

            Assert.That(
                result.Items.Select(entry => entry.Id),
                Is.EqualTo(new[] { item.Id }));
        }

        [Test]
        [FeatureTestCase(
            "通常 Collection はフォルダー、Smart Collection は指定アイコンを保存する",
            "通常 Collection の固定フォルダーアイコンと Smart Collection の icon contract、および exists 条件を確認します。",
            order: 338)]
        public void CreateCollections_UsesFixedRegularAndCustomSmartIcons()
        {
            var regular = _assetManager.CreateCollection(
                new CreateCollectionRequest
                {
                    Name = "Regular"
                });
            var smart = _assetManager.CreateSmartCollection(
                new CreateSmartCollectionRequest
                {
                    Name = "Smart",
                    Icon = AssetCollectionIcon.Search,
                    IconAssetGuid = "smart-icon-guid",
                    MatchMode = SmartCollectionMatchMode.All,
                    Conditions = new[]
                    {
                        new SmartCollectionCondition
                        {
                            Field = SmartCollectionConditionField.Tag,
                            Operator = SmartCollectionConditionOperator.Exists
                        }
                    }
                });

            var collections = _assetManager.GetCollections();

            Assert.That(
                regular.Icon,
                Is.EqualTo(AssetCollectionIcon.Folder));
            Assert.That(
                regular.IconAssetGuid,
                Is.Null);
            Assert.That(
                smart.Icon,
                Is.EqualTo(AssetCollectionIcon.Key));
            Assert.That(
                smart.IconAssetGuid,
                Is.EqualTo("smart-icon-guid"));
            Assert.That(
                collections.Single(item => item.Id == regular.Id).Icon,
                Is.EqualTo(AssetCollectionIcon.Folder));
            Assert.That(
                collections.Single(item => item.Id == smart.Id).Icon,
                Is.EqualTo(AssetCollectionIcon.Key));
            Assert.That(
                collections.Single(item => item.Id == regular.Id)
                    .IconAssetGuid,
                Is.Null);
            Assert.That(
                collections.Single(item => item.Id == smart.Id)
                    .IconAssetGuid,
                Is.EqualTo("smart-icon-guid"));
            Assert.That(
                smart.SmartRule.Conditions.Single().QueryText,
                Is.Null);
        }

        [Test]
        [FeatureTestCase(
            "file dependency 設定失敗時に既存依存を保持する",
            "SetFileDependencies が self dependency で失敗しても既存 dependency を削除しないことを確認します。",
            order: 311)]
        public void SetFileDependencies_InvalidRequest_DoesNotClearExistingDependencies()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var dependentPath = Path.Combine(_tempRoot, "dependent.txt");
            var dependencyPath = Path.Combine(_tempRoot, "dependency.txt");
            File.WriteAllText(dependentPath, "dependent");
            File.WriteAllText(dependencyPath, "dependency");
            var dependent = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = dependentPath, FileName = "dependent.txt" });
            var dependency = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = dependencyPath, FileName = "dependency.txt" });
            _assetManager.SetFileDependencies(dependent.Id, new[] { dependency.Id });

            var ex = Assert.Throws<AssetManagerException>(() => _assetManager.SetFileDependencies(dependent.Id, new[] { dependent.Id }));
            var dependencies = _assetManager.GetFileDependencies(dependent.Id);

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
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var sourcePath = Path.Combine(_tempRoot, "source.zip");
            var targetPath = Path.Combine(_tempRoot, "target.zip");
            File.WriteAllText(sourcePath, "source");
            File.WriteAllText(targetPath, "target");
            var source = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = sourcePath, FileName = "source.zip" });
            var target = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = targetPath, FileName = "target.zip" });
            var version = _assetManager.CreateVersionGroup(item.Id, new CreateVersionGroupRequest { Name = "1.0" });
            var sourceEndpoint = new DependencyEndpointRequest { Type = AssetDependencyEndpointType.File, Id = source.Id };
            _assetManager.SetDependencies(
                sourceEndpoint,
                new[] { new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VersionGroup, Id = version.Id } });

            _assetManager.SetFileDependencies(source.Id, new[] { target.Id });
            var dependencies = _assetManager.GetDependencies(sourceEndpoint);

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
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var variant = _assetManager.CreateVariantGroup(item.Id, new CreateVariantGroupRequest { Name = "Quest" });
            var version = _assetManager.CreateVersionGroup(item.Id, new CreateVersionGroupRequest { Name = "1.0", VariantGroupId = variant.Id });
            var versionFilePath = Path.Combine(_tempRoot, "version.zip");
            var variantFilePath = Path.Combine(_tempRoot, "variant.zip");
            File.WriteAllText(versionFilePath, "version");
            File.WriteAllText(variantFilePath, "variant");

            var versionFile = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = versionFilePath, FileName = "version.zip", VersionGroupId = version.Id });
            var variantFile = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = variantFilePath, FileName = "variant.zip", VariantGroupId = variant.Id });
            _assetManager.SetVersionGroupPrimaryFile(version.Id, versionFile.Id);
            _assetManager.SetDependencies(
                new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VariantGroup, Id = variant.Id },
                new[] { new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VersionGroup, Id = version.Id } });

            var files = _assetManager.GetFiles(item.Id).OrderBy(file => file.FileName).ToArray();
            var dependencies = _assetManager.GetDependencies(new DependencyEndpointRequest { Type = AssetDependencyEndpointType.VariantGroup, Id = variant.Id });
            var updatedVersion = _assetManager.GetVersionGroups(item.Id).Single(group => group.Id == version.Id);

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
            _settings.AvatarNames = "Mafuyu,Manuka";
            _settings.VersionGroupRegex = @"(?i)(?:v|ver)(?<name>\d+(?:\.\d+)*)";
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var manukaV2Path = Path.Combine(_tempRoot, "Chibi_Manuka_ver2.01.zip");
            var mafuyuV2Path = Path.Combine(_tempRoot, "Chibi_Mafuyu_ver2.01.zip");
            var manukaV3Path = Path.Combine(_tempRoot, "Chibi_Manuka_ver3.00.zip");
            var mafuyuV3Path = Path.Combine(_tempRoot, "Chibi_Mafuyu_ver3.00.zip");
            File.WriteAllText(manukaV2Path, "zip");
            File.WriteAllText(mafuyuV2Path, "zip");
            File.WriteAllText(manukaV3Path, "zip");
            File.WriteAllText(mafuyuV3Path, "zip");

            var manukaV2 = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = manukaV2Path, FileName = "Chibi_Manuka_ver2.01.zip" });
            var singleFile = _assetManager.GetFiles(item.Id).Single();

            Assert.That(_assetManager.GetVariantGroups(item.Id), Is.Empty);
            Assert.That(_assetManager.GetVersionGroups(item.Id), Is.Empty);
            Assert.That(singleFile.ItemId, Is.EqualTo(item.Id));
            Assert.That(singleFile.VersionGroupId, Is.Null);
            Assert.That(singleFile.VariantGroupId, Is.Null);

            var mafuyuV2 = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = mafuyuV2Path, FileName = "Chibi_Mafuyu_ver2.01.zip" });
            var variants = _assetManager.GetVariantGroups(item.Id).OrderBy(group => group.Name).ToArray();
            var files = _assetManager.GetFiles(item.Id).OrderBy(file => file.FileName).ToArray();
            var variant = variants.Single(group => group.Name == "Chibi");
            var updatedManukaV2 = files.Single(file => file.Id == manukaV2.Id);
            var updatedMafuyuV2 = files.Single(file => file.Id == mafuyuV2.Id);

            Assert.That(variants.Select(group => group.Name).ToArray(), Is.EqualTo(new[] { "Chibi" }));
            Assert.That(_assetManager.GetVersionGroups(item.Id), Is.Empty);
            Assert.That(updatedManukaV2.VariantGroupId, Is.EqualTo(variant.Id));
            Assert.That(updatedMafuyuV2.VariantGroupId, Is.EqualTo(variant.Id));

            var manukaV3 = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = manukaV3Path, FileName = "Chibi_Manuka_ver3.00.zip" });
            var mafuyuV3 = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = mafuyuV3Path, FileName = "Chibi_Mafuyu_ver3.00.zip" });
            var versions = _assetManager.GetVersionGroups(item.Id).OrderBy(group => group.Name).ToArray();
            files = _assetManager.GetFiles(item.Id).OrderBy(file => file.FileName).ToArray();
            var mafuyuVersion = versions.Single(group => group.Name == "Mafuyu");
            var manukaVersion = versions.Single(group => group.Name == "Manuka");

            Assert.That(versions.All(group => group.VariantGroupId == variant.Id), Is.True);
            Assert.That(mafuyuVersion.PrimaryFileId, Is.EqualTo(mafuyuV3.Id));
            Assert.That(manukaVersion.PrimaryFileId, Is.EqualTo(manukaV3.Id));
            Assert.That(files.Single(file => file.Id == mafuyuV2.Id).VersionGroupId, Is.EqualTo(mafuyuVersion.Id));
            Assert.That(files.Single(file => file.Id == mafuyuV3.Id).VersionGroupId, Is.EqualTo(mafuyuVersion.Id));
            Assert.That(files.Single(file => file.Id == manukaV2.Id).VersionGroupId, Is.EqualTo(manukaVersion.Id));
            Assert.That(files.Single(file => file.Id == manukaV3.Id).VersionGroupId, Is.EqualTo(manukaVersion.Id));
        }

        [Test]
        [FeatureTestCase(
            "version group の代表を SemVer で自動選択する",
            "代表が未設定の version group では、文字列順ではなく SemVer の最大値を primary file に設定することを確認します。",
            order: 313)]
        public void RegisterFile_AutoGroupSelectsHighestSemanticVersionAsPrimary()
        {
            _settings.AvatarNames = string.Empty;
            _settings.VersionGroupRegex = @"(?i)(?:v|ver)(?<name>\d+(?:\.\d+)*)";
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var v29Path = Path.Combine(_tempRoot, "Avatar_ver2.9.zip");
            var v210Path = Path.Combine(_tempRoot, "Avatar_ver2.10.zip");
            var v211Path = Path.Combine(_tempRoot, "Avatar_ver2.11.zip");
            File.WriteAllText(v29Path, "zip");
            File.WriteAllText(v210Path, "zip");
            File.WriteAllText(v211Path, "zip");

            _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = v29Path, FileName = "Avatar_ver2.9.zip" });
            var v210 = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = v210Path, FileName = "Avatar_ver2.10.zip" });

            var version = _assetManager.GetVersionGroups(item.Id).Single();
            Assert.That(version.PrimaryFileId, Is.EqualTo(v210.Id));

            _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = v211Path, FileName = "Avatar_ver2.11.zip" });
            version = _assetManager.GetVersionGroups(item.Id).Single();
            Assert.That(version.PrimaryFileId, Is.EqualTo(v210.Id), "既存の代表は後続の分類で上書きしない");
        }

        [Test]
        [FeatureTestCase(
            "version group 代表設定は file tree だけへ通知する",
            "SetVersionGroupPrimaryFile が asset 一覧更新を通知せず、変更対象を file tree へ通知することを確認します。",
            order: 313)]
        public void SetVersionGroupPrimaryFile_RaisesOnlyGranularFileTreeChange()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var version = _assetManager.CreateVersionGroup(item.Id, new CreateVersionGroupRequest { Name = "1.0" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = _assetManager.RegisterFile(
                item.Id,
                new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip", VersionGroupId = version.Id });
            var changed = false;
            var fileTreeChanged = false;
            string notifiedVersionGroupId = null;
            string notifiedPrimaryFileId = null;
            Action<AssetManagerChange> handler = change =>
            {
                if (change.Kind == AssetManagerChangeKind.Catalog)
                {
                    changed = true;
                }
                else if (change.Kind == AssetManagerChangeKind.FileTree)
                {
                    fileTreeChanged = true;
                }
                else if (change.Kind == AssetManagerChangeKind.VersionGroupPrimaryFile)
                {
                    notifiedVersionGroupId = change.SubjectId;
                    notifiedPrimaryFileId = change.RelatedId;
                }
            };

            _assetManager.Changed += handler;
            try
            {
                _assetManager.SetVersionGroupPrimaryFile(version.Id, file.Id);
            }
            finally
            {
                _assetManager.Changed -= handler;
            }

            Assert.That(changed, Is.False);
            Assert.That(fileTreeChanged, Is.True);
            Assert.That(notifiedVersionGroupId, Is.EqualTo(version.Id));
            Assert.That(notifiedPrimaryFileId, Is.EqualTo(file.Id));
        }

        [Test]
        [FeatureTestCase(
            "file import target を複数保持する",
            "SetFileImportTargets が zip / directory 配下の複数 target を file 単位で保存することを確認します。",
            order: 314)]
        public void SetFileImportTargets_StoresMultipleTargetsForFile()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip" });

            _assetManager.SetFileImportTargets(
                file.Id,
                new[]
                {
                    new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" },
                    new AssetFileImportTargetRequest { RelativePath = "\\Textures\\albedo.png" },
                    new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" }
                });

            var targets = _assetManager.GetFileImportTargets(file.Id);

            Assert.That(targets.Select(target => target.RelativePath).ToArray(), Is.EqualTo(new[] { "Packages/avatar.unitypackage", "Textures/albedo.png" }));
        }

        [Test]
        [FeatureTestCase(
            "import 済み GUID を file と item から取得する",
            "file 単位の GUID 保存と、Item 直下および Version Group 配下の全 file を Item 単位で集約することを確認します。",
            order: 315)]
        public void ImportedAssetGuids_AreStoredPerFileAndAggregatedByItem()
        {
            var item = _assetManager.CreateItem(
                new CreateAssetItemRequest { Name = "Item" });
            var version = _assetManager.CreateVersionGroup(
                item.Id,
                new CreateVersionGroupRequest { Name = "1.0" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = _assetManager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    FilePath = filePath,
                    FileName = "avatar.zip",
                    VersionGroupId = version.Id
                });
            var directFilePath =
                Path.Combine(_tempRoot, "materials.zip");
            File.WriteAllText(directFilePath, "zip");
            var directFile = _assetManager.RegisterFile(
                item.Id,
                new RegisterFileRequest
                {
                    FilePath = directFilePath,
                    FileName = "materials.zip"
                });
            var firstGuid =
                "11111111111111111111111111111111";
            var secondGuid =
                "22222222222222222222222222222222";
            var directFileGuid =
                "33333333333333333333333333333333";

            AssetManagerDatabase.ReplaceFileImportedAssetGuids(
                file.Id,
                new[] { firstGuid, secondGuid });
            AssetManagerDatabase.ReplaceFileImportedAssetGuids(
                directFile.Id,
                new[] { directFileGuid });

            Assert.That(
                _assetManager.GetFileImportedAssetGuids(file.Id),
                Is.EqualTo(new[] { firstGuid, secondGuid }));
            Assert.That(
                _assetManager.GetItemImportedAssetGuids(item.Id),
                Is.EqualTo(new[]
                {
                    firstGuid,
                    secondGuid,
                    directFileGuid
                }));
            var association =
                _assetManager.GetImportedAssetAssociations()
                    .Single(candidate =>
                        candidate.AssetGuid == firstGuid);
            Assert.That(association.ItemId, Is.EqualTo(item.Id));
            Assert.That(association.FileId, Is.EqualTo(file.Id));
        }

        [Test]
        [FeatureTestCase(
            "不正な import target path では既存 target を保持する",
            "SetFileImportTargets が file root と parent traversal を拒否し、既存 file_import_target を削除しないことを確認します。",
            order: 315)]
        public void SetFileImportTargets_InvalidPath_DoesNotClearExistingTargets()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip" });
            _assetManager.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" } });

            var rootEx = Assert.Throws<AssetManagerException>(() =>
                _assetManager.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = string.Empty } }));
            var traversalEx = Assert.Throws<AssetManagerException>(() =>
                _assetManager.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = "../outside.unitypackage" } }));
            var targets = _assetManager.GetFileImportTargets(file.Id);

            Assert.That(rootEx.Code, Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(traversalEx.Code, Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(targets.Select(target => target.RelativePath).ToArray(), Is.EqualTo(new[] { "Packages/avatar.unitypackage" }));
        }

        [Test]
        [FeatureTestCase(
            "file import target 設定は asset 一覧更新を通知しない",
            "SetFileImportTargets が catalog change を発火せず、asset grid reload を誘発しないことを確認します。",
            order: 316)]
        public void SetFileImportTargets_DoesNotRaiseAssetManagerChanged()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var filePath = Path.Combine(_tempRoot, "avatar.zip");
            File.WriteAllText(filePath, "zip");
            var file = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = filePath, FileName = "avatar.zip" });
            var changed = false;
            var fileTreeChanged = false;
            string notifiedFileId = null;
            IReadOnlyList<AssetFileImportTarget> notifiedTargets = null;
            Action<AssetManagerChange> handler = change =>
            {
                if (change.Kind == AssetManagerChangeKind.Catalog)
                {
                    changed = true;
                }
                else if (change.Kind == AssetManagerChangeKind.FileTree)
                {
                    fileTreeChanged = true;
                }
                else if (change.Kind == AssetManagerChangeKind.FileImportTargets)
                {
                    notifiedFileId = change.SubjectId;
                    notifiedTargets = change.ImportTargets;
                }
            };

            _assetManager.Changed += handler;
            try
            {
                _assetManager.SetFileImportTargets(file.Id, new[] { new AssetFileImportTargetRequest { RelativePath = "Packages/avatar.unitypackage" } });
            }
            finally
            {
                _assetManager.Changed -= handler;
            }

            Assert.That(changed, Is.False);
            Assert.That(fileTreeChanged, Is.True);
            Assert.That(notifiedFileId, Is.EqualTo(file.Id));
            Assert.That(notifiedTargets.Select(target => target.RelativePath).ToArray(), Is.EqualTo(new[] { "Packages/avatar.unitypackage" }));
        }

        [Test]
        [FeatureTestCase(
            "存在しない親 collection 指定では collection を作成しない",
            "CreateCollection が missing parent を検出した場合に collection_info を残さないことを確認します。",
            order: 317)]
        public void CreateCollection_MissingParent_ThrowsNotFoundWithoutCreatingCollection()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                _assetManager.CreateCollection(new CreateCollectionRequest { Name = "Child", ParentCollectionId = "missing-parent" }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.NotFound));
            Assert.That(_assetManager.GetCollections().Count, Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "不正な smart collection 条件では collection を作成しない",
            "CreateSmartCollection が query text のない条件を拒否し、collection_info を残さないことを確認します。",
            order: 318)]
        public void CreateSmartCollection_InvalidCondition_ThrowsWithoutCreatingCollection()
        {
            var ex = Assert.Throws<AssetManagerException>(() =>
                _assetManager.CreateSmartCollection(new CreateSmartCollectionRequest
                {
                    Name = "Smart",
                    Conditions = new[] { new SmartCollectionCondition { Field = SmartCollectionConditionField.Name, Operator = SmartCollectionConditionOperator.Contains } }
                }));

            Assert.That(ex.Code, Is.EqualTo(AssetManagerErrorCode.InvalidSmartCollectionCondition));
            Assert.That(_assetManager.GetCollections().Count, Is.EqualTo(0));
        }

        [Test]
        [FeatureTestCase(
            "source priority に従って file path を解決する",
            "ResolveFilePath が assetManager.sourcePriority の順序で origin path を選ぶことを確認します。",
            order: 319)]
        public void ResolveFilePath_UsesConfiguredSourcePriority()
        {
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Item" });
            var ee4vPath = Path.Combine(_tempRoot, "manual.txt");
            var eaglePath = Path.Combine(_tempRoot, "eagle-folder");
            File.WriteAllText(ee4vPath, "ee4v");
            Directory.CreateDirectory(eaglePath);
            var file = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = ee4vPath, FileName = "manual.txt" });

            using (var connection = new SQLiteConnection(GetDatabasePath()))
            {
                connection.Execute(
                    "INSERT INTO eagle_file_origin(file_info_id, eagle_item_id, file_path_cache, is_deleted, imported_at) VALUES (?, ?, ?, 0, ?)",
                    file.Id,
                    "eagle-file",
                    eaglePath,
                    DateTime.UtcNow.ToString("O"));
            }

            _settings.SourcePriority = "eagle,ee4v,blm";

            var resolved = _assetManager.ResolveFilePath(file.Id);

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

            var result = _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var items = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items;

            Assert.That(result.CreatedCount, Is.EqualTo(2));
            Assert.That(result.UpdatedCount, Is.EqualTo(0));
            Assert.That(result.UnchangedCount, Is.EqualTo(0));
            Assert.That(result.ErrorCount, Is.EqualTo(0));
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Name, Is.EqualTo("Booth Avatar"));
            Assert.That(items[0].Booth.BoothItemId, Is.EqualTo(123));
            Assert.That(items[0].Files.Select(file => file.FileName).ToArray(), Is.EqualTo(new[] { "avatar.unitypackage" }));

            var secondResult = _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));

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
            "DB 再生成後は保存済み fingerprint を無効化する",
            "同期成功後に AssetManager DB だけを削除した場合、同じ datasource でも再取り込みが必要と判定されることを確認します。",
            order: 322)]
        public void PrepareEagleSync_RecreatedDatabaseRequiresSynchronization()
        {
            var libraryPath = CreateEagleLibrary(
                "recreated-database.library",
                "Avatar");
            var first = AssetManagerDatabase.PrepareEagleSync(
                new EagleSyncRequest(libraryPath));
            AssetManagerDatabase.ApplyPreparedEagleSync(first, true);
            Assert.That(
                File.Exists(AssetSyncFingerprintCache.GetPath(
                    AssetSourceType.Eagle)),
                Is.True);

            File.Delete(GetDatabasePath());

            var prepared = AssetManagerDatabase.PrepareEagleSync(
                new EagleSyncRequest(libraryPath));

            Assert.That(prepared.Preview.HasChanges, Is.True);
        }

        [Test]
        [FeatureTestCase(
            "Eagle 同期は新しい Unity item を競合として検出する",
            "前回取り込み後に Unity item が更新され、Eagle の名前も変わった場合に差分を返し、承認後は同期元の値で上書きすることを確認します。",
            order: 323)]
        public void PrepareEagleSync_NewerUnityItemRequiresOverwriteConfirmation()
        {
            var libraryPath = CreateEagleLibrary("conflict.library", "Before");
            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
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

            Assert.That(_assetManager.GetItem(item.Id).Name, Is.EqualTo("From Eagle"));
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
            _assetManager.SyncBlm(new BlmSyncRequest(databasePath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
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

            Assert.That(_assetManager.GetItem(item.Id).Name, Is.EqualTo("From BLM"));
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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var before = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            File.WriteAllText(metadataPath, "{\"folders\":[{\"id\":\"root\",\"name\":\"VRCAsset\",\"children\":[{\"id\":\"avatar-folder\",\"name\":\"After\",\"children\":[]}]}]}");

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var after = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = _assetManager.GetFiles(item.Id).Single();
            Directory.Delete(Path.Combine(imagesPath, "file-entry.info"), true);

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var unavailable = _assetManager.GetFiles(item.Id, new AssetFileQuery { IncludeUnavailable = true }).Single();

            Assert.That(_assetManager.GetFiles(item.Id), Is.Empty);
            Assert.That(unavailable.Id, Is.EqualTo(file.Id));
            Assert.That(unavailable.IsAvailable, Is.False);
            Assert.That(unavailable.Origins.Single().IsAvailable, Is.False);
            Assert.That(_assetManager.ResolveFilePath(file.Id).Found, Is.False);
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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = _assetManager.GetFiles(item.Id).Single();

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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = _assetManager.GetFiles(item.Id).Single();

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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var files = _assetManager.GetFiles(item.Id).OrderBy(file => file.DownloadId).ToArray();

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

            var result = _assetManager.SyncEagle(new EagleSyncRequest(missingLibraryPath));
            var syncInfo = _assetManager.GetSyncInfo().Single(info => info.SourceType == AssetSourceType.Eagle);

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
            var item = _assetManager.CreateItem(new CreateAssetItemRequest { Name = "Manual" });
            var manualPath = Path.Combine(_tempRoot, "manual.txt");
            File.WriteAllText(manualPath, "manual");
            var file = _assetManager.RegisterFile(item.Id, new RegisterFileRequest { FilePath = manualPath, FileName = "manual.txt" });
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

            var result = _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var syncInfo = _assetManager.GetSyncInfo().Single(info => info.SourceType == AssetSourceType.Eagle);

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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();

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

            _assetManager.SyncEagle(new EagleSyncRequest(libraryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = _assetManager.GetFiles(item.Id).Single();
            var resolved = _assetManager.ResolveFilePath(file.Id);

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

            _assetManager.SyncBlm(new BlmSyncRequest(databasePath, itemDirectoryPath));
            var item = _assetManager.SearchItems(new AssetItemQuery { Limit = 10 }).Items.Single();
            var file = _assetManager.GetFiles(item.Id).Single();
            var resolved = _assetManager.ResolveFilePath(file.Id);

            Assert.That(file.FileName, Is.EqualTo("Avatar"));
            Assert.That(file.Origins.Single().FilePathCache, Is.EqualTo(innerPath));
            Assert.That(resolved.Path, Is.EqualTo(innerPath));
        }

        [Test]
        [FeatureTestCase(
            "file tree の filesystem access を adapter に隔離する",
            "directory列挙とZIP entryのstream取得をInfrastructure adapterが提供することを確認します。",
            order: 330)]
        public void FileSystemReader_ProvidesDirectoryAndArchiveAccess()
        {
            var directoryPath = Path.Combine(_tempRoot, "tree");
            var childDirectory = Path.Combine(directoryPath, "Folder");
            Directory.CreateDirectory(childDirectory);
            File.WriteAllText(
                Path.Combine(directoryPath, "preview.png"),
                "preview");
            var zipPath = Path.Combine(_tempRoot, "archive.zip");
            CreateZip(zipPath, "Images/preview.png");
            var reader = new AssetFileSystemReader();

            var entries = reader.GetDirectoryEntries(
                directoryPath,
                CancellationToken.None);

            Assert.That(
                entries.Select(entry => entry.Name),
                Is.EquivalentTo(new[] { "Folder", "preview.png" }));
            using (var stream = reader.OpenZipEntry(
                       zipPath,
                       "Images/preview.png",
                       1024))
            using (var textReader = new StreamReader(stream))
            {
                Assert.That(
                    textReader.ReadToEnd(),
                    Is.EqualTo("Images/preview.png"));
            }
        }

        [Test]
        [FeatureTestCase(
            "file tree archive cache を library の cache に保存する",
            "ZIP metadata cache を永続化し、source 更新時に非同期読み込み用snapshotを再生成できることを確認します。",
            order: 331)]
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

        [Test]
        [FeatureTestCase(
            "ZIP と同名の単一 root folder を file tree から省略する",
            "archive 全体が ZIP 名と同じ folder に包まれている場合だけ、cache の表示 path から先頭 folder を除くことを確認します。",
            order: 334)]
        public void FileTreeCache_IgnoresSingleRootFolderMatchingZipName()
        {
            var zipPath = Path.Combine(_tempRoot, "Avatar.zip");
            CreateZip(zipPath, "Avatar/Textures/albedo.png", "Avatar/Models/avatar.fbx");
            var cacheDirectory = AssetFileTreeCache.ResolveCacheDirectory();

            var entries = AssetFileTreeCache.ReadZipEntries(cacheDirectory, zipPath, CancellationToken.None);
            var cachedEntries = AssetFileTreeCache.ReadZipEntries(cacheDirectory, zipPath, CancellationToken.None);

            Assert.That(
                entries.Select(entry => entry.FullName),
                Is.EqualTo(new[] { "Textures/albedo.png", "Models/avatar.fbx" }));
            Assert.That(
                cachedEntries.Select(entry => entry.FullName),
                Is.EqualTo(new[] { "Textures/albedo.png", "Models/avatar.fbx" }));
            Assert.That(
                entries.Select(entry => entry.ArchiveFullName),
                Is.EqualTo(new[] { "Avatar/Textures/albedo.png", "Avatar/Models/avatar.fbx" }));
            Assert.That(
                cachedEntries.Select(entry => entry.ArchiveFullName),
                Is.EqualTo(new[] { "Avatar/Textures/albedo.png", "Avatar/Models/avatar.fbx" }));
        }

        [Test]
        [FeatureTestCase(
            "ZIP root に兄弟 entry がある場合は同名 folder を維持する",
            "ZIP と同名の folder 以外にも root entry がある archive では階層を省略しないことを確認します。",
            order: 335)]
        public void FileTreeCache_KeepsMatchingFolderWhenArchiveHasRootSibling()
        {
            var zipPath = Path.Combine(_tempRoot, "Avatar.zip");
            CreateZip(zipPath, "Avatar/Textures/albedo.png", "README.txt");

            var entries = AssetFileTreeCache.ReadZipEntries(
                AssetFileTreeCache.ResolveCacheDirectory(),
                zipPath,
                CancellationToken.None);

            Assert.That(
                entries.Select(entry => entry.FullName),
                Is.EqualTo(new[] { "Avatar/Textures/albedo.png", "README.txt" }));
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

        private sealed class TestInfrastructureSettings : IAssetManagerInfrastructureSettings
        {
            public string GlobalPath { get; set; }
            public string BlmDatabasePath { get; set; } = string.Empty;
            public string EagleLibraryPath { get; set; } = string.Empty;
            public string SourcePriority { get; set; } = "ee4v,eagle,blm";
            public string AvatarNames { get; set; } = "Mafuyu,Manuka";
            public string VersionGroupRegex { get; set; } =
                @"(?i)(?:v|ver)(?<name>\d+(?:\.\d+)*)";
            public bool ShowUnityPackageImportDialog { get; set; } = true;
        }
    }
}
