using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.SQLite;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence
{
    internal sealed class SqliteAssetManagerStore
        : IAssetManagerStore
    {
        private const int SchemaVersion = 1;
        private const string FileSelect =
            @"SELECT id AS Id, item_id AS ItemId,
                     file_name AS FileName, extension AS Extension,
                     source_type AS SourceType,
                     source_id AS SourceId, source_path AS SourcePath,
                     is_archived AS IsArchived,
                     created_at AS CreatedAt, updated_at AS UpdatedAt
              FROM file";
        private readonly string _databasePath;

        internal SqliteAssetManagerStore(string databasePath)
        {
            _databasePath = Path.GetFullPath(databasePath);
            InitializeDatabase();
        }

        public AssetSearchResult SearchItems(AssetItemQuery query)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    query = query ?? new AssetItemQuery();
                    var parameters = new List<object>();
                    var conditions = new List<string>();
                    if (!query.IncludeArchived)
                    {
                        conditions.Add("item.is_archived = 0");
                    }

                    if (query.Filter != null)
                    {
                        conditions.Add(BuildFilterSql(
                            query.Filter,
                            parameters));
                    }

                    var where = conditions.Count == 0
                        ? string.Empty
                        : " WHERE " + string.Join(" AND ", conditions);
                    var total = connection.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM item" + where,
                        parameters.ToArray());
                    var pageParameters = new List<object>(parameters)
                    {
                        query.Limit == 0 ? -1 : query.Limit,
                        query.Offset
                    };
                    var ids = connection.Query<IdRow>(
                            "SELECT item.id AS Id FROM item" + where +
                            " ORDER BY item.name COLLATE NOCASE, item.id" +
                            " LIMIT ? OFFSET ?",
                            pageParameters.ToArray())
                        .Select(row => row.Id)
                        .ToArray();
                    return new AssetSearchResult
                    {
                        Items = ids
                            .Select(id => ReadItem(connection, id))
                            .ToArray(),
                        TotalCount = total
                    };
                }
            });
        }

        public bool MatchesItem(
            string itemId,
            AssetFilterNode filter)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireItem(connection, itemId, null);
                    var parameters = new List<object> { itemId };
                    var condition = filter == null
                        ? "1"
                        : BuildFilterSql(filter, parameters);
                    return connection.ExecuteScalar<int>(
                        @"SELECT EXISTS(
                            SELECT 1 FROM item
                            WHERE item.id = ?
                              AND item.is_archived = 0
                              AND " + condition + ")",
                        parameters.ToArray()) != 0;
                }
            });
        }

        public AssetItem GetItem(string itemId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadItem(connection, itemId);
                }
            });
        }

        public AssetItem GetThumbnailItem(string itemId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var row = connection.Query<ItemRow>(
                            @"SELECT id AS Id,
                                     thumbnail_url AS ThumbnailUrl,
                                     source_type AS SourceType
                              FROM item WHERE id = ?",
                            itemId)
                        .SingleOrDefault();
                    if (row == null)
                    {
                        throw NotFound("Item was not found.");
                    }

                    return new AssetItem
                    {
                        Id = row.Id,
                        ThumbnailUrl = row.ThumbnailUrl,
                        SourceType = row.SourceType == null
                            ? (AssetSourceType?)null
                            : ParseSourceType(row.SourceType)
                    };
                }
            });
        }

        public AssetItem CreateItem(CreateAssetItemRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var id = NewId();
                    var now = Now();
                    Execute(
                        connection,
                        null,
                        @"INSERT INTO item(
                            id, name, description, thumbnail_url,
                            source_type, source_id,
                            is_archived, created_at, updated_at)
                          VALUES(
                            @p0, @p1, @p2, NULL, NULL, NULL,
                            0, @p3, @p3)",
                        id,
                        request.Name.Trim(),
                        request.Description ?? string.Empty,
                        now);
                    return ReadItem(connection, id);
                }
            });
        }

        public AssetItem UpdateItem(
            string itemId,
            UpdateAssetItemRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var changed = Execute(
                        connection,
                        null,
                        @"UPDATE item
                          SET name = @p0,
                              description = @p1,
                              updated_at = @p2
                          WHERE id = @p3",
                        request.Name.Trim(),
                        request.Description ?? string.Empty,
                        Now(),
                        itemId);
                    RequireChanged(changed, "Item was not found.");
                    return ReadItem(connection, itemId);
                }
            });
        }

        public IReadOnlyList<AssetItem> SetItemArchived(
            IReadOnlyList<string> itemIds,
            bool archived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        RequireItem(connection, itemIds[i], transaction);
                    }

                    var now = Now();
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE item
                              SET is_archived = @p0, updated_at = @p1
                              WHERE id = @p2",
                            archived ? 1 : 0,
                            now,
                            itemIds[i]);
                    }

                    transaction.Commit();
                    return itemIds
                        .Select(id => ReadItem(connection, id))
                        .ToArray();
                }
            });
        }

        public void DeleteItem(IReadOnlyList<string> itemIds)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        RequireItem(connection, itemIds[i], transaction);
                    }

                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            "DELETE FROM file WHERE item_id = @p0",
                            itemIds[i]);
                        Execute(
                            connection,
                            transaction,
                            "DELETE FROM item WHERE id = @p0",
                            itemIds[i]);
                    }

                    DeleteUnusedTags(connection, transaction);
                    transaction.Commit();
                    return true;
                }
            });
        }

        public AssetFile GetFile(string fileId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadFile(connection, fileId);
                }
            });
        }

        public AssetItem GetItemBySource(
            AssetSourceType sourceType,
            string sourceId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var id = ScalarString(
                        connection,
                        null,
                        @"SELECT id FROM item
                          WHERE source_type = @p0 AND source_id = @p1",
                        ToSourceType(sourceType),
                        sourceId);
                    if (id == null)
                    {
                        throw NotFound("Item source was not found.");
                    }

                    return ReadItem(connection, id);
                }
            });
        }

        public AssetFile GetFileBySource(
            AssetSourceType sourceType,
            string sourceId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var id = ScalarString(
                        connection,
                        null,
                        @"SELECT id FROM file
                          WHERE source_type = @p0 AND source_id = @p1",
                        ToSourceType(sourceType),
                        sourceId);
                    if (id == null)
                    {
                        throw NotFound("File source was not found.");
                    }

                    return ReadFile(connection, id);
                }
            });
        }

        public IReadOnlyList<AssetFile> GetFiles(
            string itemId,
            bool includeArchived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireItem(connection, itemId, null);
                    return ReadFiles(
                        connection,
                        includeArchived
                            ? "item_id = @p0"
                            : "item_id = @p0 AND is_archived = 0",
                        itemId);
                }
            });
        }

        public IReadOnlyList<AssetFile> GetUnassignedFiles(
            bool includeArchived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadFiles(
                        connection,
                        includeArchived
                            ? "item_id IS NULL"
                            : "item_id IS NULL AND is_archived = 0");
                }
            });
        }

        public IReadOnlyList<AssetFile> SetFileItem(
            IReadOnlyList<string> fileIds,
            string itemId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    if (itemId != null)
                    {
                        RequireItem(connection, itemId, transaction);
                    }

                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        RequireFile(connection, fileIds[i], transaction);
                    }

                    var now = Now();
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE file
                              SET item_id = @p0, updated_at = @p1
                              WHERE id = @p2",
                            itemId,
                            now,
                            fileIds[i]);
                    }

                    transaction.Commit();
                    return fileIds
                        .Select(id => ReadFile(connection, id))
                        .ToArray();
                }
            });
        }

        public IReadOnlyList<AssetFile> SetFileArchived(
            IReadOnlyList<string> fileIds,
            bool archived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        RequireFile(connection, fileIds[i], transaction);
                    }

                    var now = Now();
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE file
                              SET is_archived = @p0, updated_at = @p1
                              WHERE id = @p2",
                            archived ? 1 : 0,
                            now,
                            fileIds[i]);
                    }

                    transaction.Commit();
                    return fileIds
                        .Select(id => ReadFile(connection, id))
                        .ToArray();
                }
            });
        }

        public void DeleteFile(IReadOnlyList<string> fileIds)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        RequireFile(connection, fileIds[i], transaction);
                    }

                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE item
                              SET source_type = NULL,
                                  source_id = NULL,
                                  updated_at = @p0
                              WHERE source_type = 'ee4v'
                                AND source_id = (
                                  SELECT source_id FROM file
                                  WHERE id = @p1
                                    AND source_type = 'ee4v')",
                            Now(),
                            fileIds[i]);
                        Execute(
                            connection,
                            transaction,
                            "DELETE FROM file WHERE id = @p0",
                            fileIds[i]);
                    }

                    transaction.Commit();
                    return true;
                }
            });
        }

        public IReadOnlyList<AssetFileTarget> GetItemTargets(
            string itemId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireItem(connection, itemId, null);
                    return ReadItemTargets(connection, itemId);
                }
            });
        }

        public IReadOnlyList<AssetFileTarget> ReplaceItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> normalizedTargets)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    RequireItem(connection, itemId, transaction);
                    var groupNames = ReadItemTargets(connection, itemId)
                        .ToDictionary(
                            target => target.FileId + "\n" +
                                      target.TargetPath,
                            target => target.GroupName,
                            StringComparer.OrdinalIgnoreCase);
                    Execute(
                        connection,
                        transaction,
                        "DELETE FROM item_target WHERE item_id = @p0",
                        itemId);
                    for (var i = 0; i < normalizedTargets.Count; i++)
                    {
                        var target = normalizedTargets[i];
                        RequireFile(
                            connection,
                            target.FileId,
                            transaction);
                        var key = target.FileId + "\n" + target.TargetPath;
                        Execute(
                            connection,
                            transaction,
                            @"INSERT INTO item_target(
                                item_id, file_id, target_path, group_name)
                              VALUES(@p0, @p1, @p2, @p3)",
                            itemId,
                            target.FileId,
                            target.TargetPath,
                            groupNames.TryGetValue(key, out var groupName)
                                ? groupName
                                : null);
                    }
                    transaction.Commit();
                    return ReadItemTargets(connection, itemId);
                }
            });
        }

        public AssetFileTarget SetItemTargetGroup(
            string itemId,
            string fileId,
            string normalizedTargetPath,
            string normalizedGroupName)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    RequireItem(connection, itemId, transaction);
                    RequireFile(connection, fileId, transaction);
                    var changed = Execute(
                        connection,
                        transaction,
                        @"UPDATE item_target
                          SET group_name = @p3
                          WHERE item_id = @p0
                            AND file_id = @p1
                            AND target_path = @p2",
                        normalizedGroupName,
                        itemId,
                        fileId,
                        normalizedTargetPath);
                    if (changed == 0)
                    {
                        throw new AssetManagerException(
                            AssetManagerErrorCode.NotFound,
                            "Item target was not found: " +
                            normalizedTargetPath);
                    }

                    transaction.Commit();
                    return ReadItemTargets(connection, itemId)
                        .Single(target =>
                            string.Equals(
                                target.FileId,
                                fileId,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                target.TargetPath,
                                normalizedTargetPath,
                                StringComparison.OrdinalIgnoreCase));
                }
            });
        }

        public IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireFile(connection, fileId, null);
                    return ReadFileDependencies(connection, fileId);
                }
            });
        }

        public IReadOnlyList<AssetFileDependency> ReplaceFileDependencies(
            IReadOnlyList<AssetFileTarget> dependentTargets,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < dependentTargets.Count; i++)
                    {
                        RequireFile(
                            connection,
                            dependentTargets[i].FileId,
                            transaction);
                    }

                    for (var i = 0; i < dependencyTargets.Count; i++)
                    {
                        RequireFile(
                            connection,
                            dependencyTargets[i].FileId,
                            transaction);
                    }

                    for (var dependentIndex = 0;
                         dependentIndex < dependentTargets.Count;
                         dependentIndex++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"DELETE FROM file_content_dependency
                              WHERE dependent_file_id = @p0
                                AND dependent_target_path = @p1",
                            dependentTargets[dependentIndex].FileId,
                            dependentTargets[dependentIndex].TargetPath);
                    }

                    for (var dependentIndex = 0;
                         dependentIndex < dependentTargets.Count;
                         dependentIndex++)
                    {
                        for (var dependencyIndex = 0;
                             dependencyIndex < dependencyTargets.Count;
                             dependencyIndex++)
                        {
                            var target = dependencyTargets[dependencyIndex];
                            Execute(
                                connection,
                                transaction,
                                @"INSERT INTO file_content_dependency(
                                    dependent_file_id,
                                    dependent_target_path,
                                    dependency_file_id,
                                    target_path)
                                  VALUES(@p0, @p1, @p2, @p3)",
                                dependentTargets[dependentIndex].FileId,
                                dependentTargets[dependentIndex].TargetPath,
                                target.FileId,
                                target.TargetPath);
                        }
                    }

                    transaction.Commit();
                    return dependentTargets
                        .SelectMany(source => ReadFileDependencies(
                            connection,
                            source.FileId).Where(dependency =>
                            string.Equals(
                                dependency.DependentTargetPath,
                                source.TargetPath,
                                StringComparison.OrdinalIgnoreCase)))
                        .ToArray();
                }
            });
        }

        public IReadOnlyList<string> GetDependentFileIds(
            IReadOnlyList<string> dependencyFileIds)
        {
            return Run(() =>
            {
                if (dependencyFileIds == null ||
                    dependencyFileIds.Count == 0)
                {
                    return Array.Empty<string>();
                }

                using (var connection = OpenConnection())
                {
                    return QueryStrings(
                        connection,
                        null,
                        "SELECT DISTINCT dependent_file_id " +
                        "FROM file_content_dependency " +
                        "WHERE dependency_file_id IN (" +
                        string.Join(",", Enumerable.Repeat(
                            "?",
                            dependencyFileIds.Count)) + ") " +
                        "ORDER BY dependent_file_id",
                        dependencyFileIds.Cast<object>().ToArray());
                }
            });
        }

        public IReadOnlyList<string> GetFileImportedAssetGuids(
            string fileId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireFile(connection, fileId, null);
                    return ReadFileImportedAssetGuids(connection, fileId);
                }
            });
        }

        public IReadOnlyList<string> GetItemImportedAssetGuids(
            string itemId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireItem(connection, itemId, null);
                    return QueryStrings(
                        connection,
                        null,
                        @"SELECT DISTINCT imported.asset_guid
                          FROM file_imported_asset_guid imported
                          INNER JOIN file
                            ON file.id = imported.file_id
                          WHERE file.item_id = @p0
                          ORDER BY imported.asset_guid",
                        itemId);
                }
            });
        }

        public IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations(
                IReadOnlyList<string> assetGuids)
        {
            return Run(() =>
            {
                if (assetGuids != null && assetGuids.Count == 0)
                {
                    return Array.Empty<AssetImportedAssetAssociation>();
                }

                using (var connection = OpenConnection())
                {
                    var where = assetGuids == null
                        ? string.Empty
                        : " WHERE imported.asset_guid IN (" +
                          string.Join(
                              ",",
                              Enumerable.Repeat("?", assetGuids.Count)) +
                          ")";
                    var parameters = assetGuids == null
                        ? Array.Empty<object>()
                        : assetGuids.Cast<object>().ToArray();
                    return connection.Query<ImportedAssetGuidRow>(
                            @"SELECT file.item_id AS ItemId,
                                     imported.file_id AS FileId,
                                     imported.asset_guid AS AssetGuid,
                                     imported.imported_at AS ImportedAt
                              FROM file_imported_asset_guid imported
                              INNER JOIN file
                                ON file.id = imported.file_id" +
                            where +
                            " ORDER BY imported.imported_at, " +
                            "imported.asset_guid, imported.file_id",
                            parameters)
                        .Select(row =>
                            new AssetImportedAssetAssociation
                            {
                                ItemId = row.ItemId,
                                FileId = row.FileId,
                                AssetGuid = row.AssetGuid,
                                ImportedAt = ParseDate(row.ImportedAt)
                            })
                        .ToArray();
                }
            });
        }

        public void ReplaceFileImportedAssetGuids(
            string fileId,
            IReadOnlyList<string> assetGuids)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    RequireFile(connection, fileId, transaction);
                    Execute(
                        connection,
                        transaction,
                        @"DELETE FROM file_imported_asset_guid
                          WHERE file_id = @p0",
                        fileId);
                    var importedAt = Now();
                    for (var i = 0; i < assetGuids.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"INSERT INTO file_imported_asset_guid(
                                file_id, asset_guid, imported_at)
                              VALUES(@p0, @p1, @p2)",
                            fileId,
                            assetGuids[i],
                            importedAt);
                    }

                    transaction.Commit();
                    return true;
                }
            });
        }

        public IReadOnlyList<AssetTag> GetTags()
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadTags(connection, null, null);
                }
            });
        }

        public IReadOnlyList<AssetItem> SetItemTags(
            IReadOnlyList<string> itemIds,
            IReadOnlyList<string> normalizedPaths)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        RequireItem(connection, itemIds[i], transaction);
                    }

                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        var sourcePaths = new HashSet<string>(QueryStrings(
                            connection, transaction,
                            @"SELECT tag.path FROM tag
                              INNER JOIN item_source_tag source_tag
                                ON source_tag.tag_id = tag.id
                              WHERE source_tag.item_id = @p0",
                            itemIds[i]), StringComparer.Ordinal);
                        ReplaceItemTags(
                            connection,
                            transaction,
                            itemIds[i],
                            normalizedPaths.Where(path =>
                                !sourcePaths.Contains(path)).ToArray());
                    }

                    DeleteUnusedTags(connection, transaction);
                    transaction.Commit();
                    return itemIds
                        .Select(id => ReadItem(connection, id))
                        .ToArray();
                }
            });
        }

        public IReadOnlyList<AssetCollection> GetCollections()
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var ids = QueryStrings(
                        connection,
                        null,
                        @"SELECT collection.id FROM collection
                          LEFT JOIN collection_order ordering
                            ON ordering.collection_id = collection.id
                          ORDER BY ordering.sort_order IS NULL,
                            ordering.sort_order, collection.name, collection.id");
                    return ids.Select(id =>
                            ReadCollection(connection, id))
                        .ToArray();
                }
            });
        }

        public AssetCollection GetCollection(string collectionId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadCollection(connection, collectionId);
                }
            });
        }

        public AssetCollection CreateCollection(
            CreateAssetCollectionRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    EnsureCollectionNameAvailable(
                        connection,
                        transaction,
                        request.Name,
                        null);
                    var id = NewId();
                    var now = Now();
                    Execute(
                        connection,
                        transaction,
                        @"INSERT INTO collection(
                            id, name, created_at, updated_at)
                          VALUES(@p0, @p1, @p2, @p2)",
                        id,
                        request.Name.Trim(),
                        now);
                    InsertNode(
                        connection,
                        transaction,
                        id,
                        null,
                        0,
                        request.Root);
                    var orderedIds = QueryStrings(
                        connection,
                        transaction,
                        @"SELECT collection.id FROM collection
                          LEFT JOIN collection_order ordering
                            ON ordering.collection_id = collection.id
                          WHERE collection.id <> @p0
                          ORDER BY ordering.sort_order IS NULL,
                            ordering.sort_order, collection.name, collection.id",
                        id).Concat(new[] { id }).ToArray();
                    ReplaceCollectionOrder(connection, transaction, orderedIds);
                    transaction.Commit();
                    return ReadCollection(connection, id);
                }
            });
        }

        public AssetCollection UpdateCollection(
            string collectionId,
            UpdateAssetCollectionRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    EnsureCollectionExists(
                        connection,
                        transaction,
                        collectionId);
                    EnsureCollectionNameAvailable(
                        connection,
                        transaction,
                        request.Name,
                        collectionId);
                    Execute(
                        connection,
                        transaction,
                        @"UPDATE collection
                          SET name = @p0, updated_at = @p1
                          WHERE id = @p2",
                        request.Name.Trim(),
                        Now(),
                        collectionId);
                    Execute(
                        connection,
                        transaction,
                        @"DELETE FROM collection_node
                          WHERE collection_id = @p0",
                        collectionId);
                    InsertNode(
                        connection,
                        transaction,
                        collectionId,
                        null,
                        0,
                        request.Root);
                    transaction.Commit();
                    return ReadCollection(connection, collectionId);
                }
            });
        }

        public void ReorderCollections(IReadOnlyList<string> collectionIds)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    var existing = QueryStrings(
                        connection,
                        transaction,
                        "SELECT id FROM collection");
                    if (existing.Count != collectionIds.Count ||
                        !new HashSet<string>(existing, StringComparer.Ordinal)
                            .SetEquals(collectionIds))
                    {
                        throw new AssetManagerException(
                            AssetManagerErrorCode.InvalidRequest,
                            "Collection order must contain every existing collection exactly once.");
                    }

                    ReplaceCollectionOrder(connection, transaction, collectionIds);
                    transaction.Commit();
                    return true;
                }
            });
        }

        private static void ReplaceCollectionOrder(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            IReadOnlyList<string> collectionIds)
        {
            Execute(connection, transaction, "DELETE FROM collection_order");
            for (var index = 0; index < collectionIds.Count; index++)
            {
                Execute(
                    connection,
                    transaction,
                    @"INSERT INTO collection_order(collection_id, sort_order)
                      VALUES(@p0, @p1)",
                    collectionIds[index],
                    index);
            }
        }

        public void DeleteCollection(string collectionId)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var changed = Execute(
                        connection,
                        null,
                        "DELETE FROM collection WHERE id = @p0",
                        collectionId);
                    RequireChanged(
                        changed,
                        "Collection was not found.");
                    return true;
                }
            });
        }

        public AssetSyncResult ApplySourceSnapshot(
            AssetSourceType sourceType,
            AssetSourceSnapshot snapshot)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    var errorMessages = new List<string>();
                    var seenItems = new HashSet<string>(
                        StringComparer.Ordinal);
                    var seenFiles = new HashSet<string>(
                        StringComparer.Ordinal);
                    var items = snapshot == null
                        ? Array.Empty<AssetSourceSnapshotItem>()
                        : snapshot.Items;
                    var source = ToSourceType(sourceType);
                    var before = ReadSourceState(
                        connection,
                        transaction,
                        source);
                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (item == null ||
                            string.IsNullOrWhiteSpace(item.SourceId) ||
                            !seenItems.Add(item.SourceId))
                        {
                            errorMessages.Add(
                                "A source item has a missing or duplicate id.");
                            continue;
                        }

                        var itemId = UpsertSourceItem(
                            connection,
                            transaction,
                            source,
                            item);
                        var files = item.Files ??
                                    Array.Empty<AssetSourceSnapshotFile>();
                        for (var fileIndex = 0;
                             fileIndex < files.Count;
                             fileIndex++)
                        {
                            var file = files[fileIndex];
                            if (file == null ||
                                string.IsNullOrWhiteSpace(file.SourceId) ||
                                !seenFiles.Add(file.SourceId))
                            {
                                errorMessages.Add(
                                    "A source file has a missing or duplicate id.");
                                continue;
                            }

                            UpsertSourceFile(
                                connection,
                                transaction,
                                source,
                                itemId,
                                file);
                        }

                        if (sourceType == AssetSourceType.Eagle)
                        {
                            ReplaceSourceItemTags(connection, transaction,
                                itemId, source, item.Tags ?? Array.Empty<string>());
                        }
                        else if (item.Tags != null)
                        {
                            ReplaceItemTags(
                                connection,
                                transaction,
                                itemId,
                                item.Tags);
                        }
                    }

                    var unassignedFiles = snapshot == null
                        ? Array.Empty<AssetSourceSnapshotFile>()
                        : snapshot.Files;
                    for (var i = 0; i < unassignedFiles.Count; i++)
                    {
                        var file = unassignedFiles[i];
                        if (file == null ||
                            string.IsNullOrWhiteSpace(file.SourceId) ||
                            !seenFiles.Add(file.SourceId))
                        {
                            errorMessages.Add(
                                "A source file has a missing or duplicate id.");
                            continue;
                        }

                        UpsertSourceFile(
                            connection,
                            transaction,
                            source,
                            null,
                            file);
                    }

                    DeleteMissingSourceFiles(
                        connection,
                        transaction,
                        source,
                        seenFiles);
                    DeleteMissingSourceItems(
                        connection,
                        transaction,
                        source,
                        seenItems);

                    DeleteUnusedTags(connection, transaction);
                    var after = ReadSourceState(
                        connection,
                        transaction,
                        source);
                    var result = BuildSyncResult(
                        before,
                        after,
                        errorMessages);
                    transaction.Commit();
                    return result;
                }
            });
        }

        public AssetFile ApplySourceFile(
            AssetSourceType sourceType,
            AssetSourceSnapshotFile file,
            string itemId)
        {
            return Run(() =>
            {
                if (file == null ||
                    string.IsNullOrWhiteSpace(file.SourceId))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Source file id is required.");
                }

                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        RequireItem(connection, itemId, transaction);
                    }

                    var source = ToSourceType(sourceType);
                    UpsertSourceFile(
                        connection,
                        transaction,
                        source,
                        string.IsNullOrWhiteSpace(itemId) ? null : itemId,
                        file);
                    var fileId = ScalarString(
                        connection,
                        transaction,
                        @"SELECT id FROM file
                          WHERE source_type = @p0 AND source_id = @p1",
                        source,
                        file.SourceId);
                    var result = ReadFile(connection, fileId);
                    transaction.Commit();
                    return result;
                }
            });
        }

        public AssetItem ApplySourceItem(
            AssetSourceType sourceType,
            AssetSourceSnapshotItem item)
        {
            return Run(() =>
            {
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.SourceId))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Source item id is required.");
                }

                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    var source = ToSourceType(sourceType);
                    var itemId = UpsertSourceItem(
                        connection,
                        transaction,
                        source,
                        item);
                    var seenFiles = new HashSet<string>(
                        StringComparer.Ordinal);
                    var files = item.Files ??
                                Array.Empty<AssetSourceSnapshotFile>();
                    for (var i = 0; i < files.Count; i++)
                    {
                        if (files[i] == null ||
                            string.IsNullOrWhiteSpace(files[i].SourceId) ||
                            !seenFiles.Add(files[i].SourceId))
                        {
                            throw new AssetManagerException(
                                AssetManagerErrorCode.InvalidRequest,
                                "Source file id must be unique.");
                        }

                        UpsertSourceFile(
                            connection,
                            transaction,
                            source,
                            itemId,
                            files[i]);
                    }

                    ReplaceItemTags(
                        connection,
                        transaction,
                        itemId,
                        item.Tags ?? Array.Empty<string>());
                    DeleteUnusedTags(connection, transaction);
                    var result = ReadItem(connection, itemId);
                    transaction.Commit();
                    return result;
                }
            });
        }

        private void InitializeDatabase()
        {
            Run(() =>
            {
                var directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(_databasePath))
                {
                    using (var existing = OpenConnection())
                    {
                        var version = existing.ExecuteScalar<int>(
                            "PRAGMA user_version");
                        if (version != SchemaVersion)
                        {
                            existing.Close();
                            File.Delete(_databasePath);
                        }
                    }
                }

                using (var connection = OpenConnection())
                {
                    ExecuteSchema(connection);
                }

                return true;
            });
        }

        private static void ExecuteSchema(SQLiteConnection connection)
        {
            var statements = new[]
            {
                @"CREATE TABLE IF NOT EXISTS item(
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL CHECK(trim(name) <> ''),
                    description TEXT NOT NULL DEFAULT '',
                    thumbnail_url TEXT,
                    source_type TEXT,
                    source_id TEXT,
                    is_archived INTEGER NOT NULL DEFAULT 0
                      CHECK(is_archived IN (0, 1)),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    CHECK((source_type IS NULL) = (source_id IS NULL)),
                    CHECK(source_type IS NULL OR source_type IN(
                      'eagle', 'ee4v'))
                  )",
                @"CREATE UNIQUE INDEX IF NOT EXISTS ux_item_source
                    ON item(source_type, source_id)
                    WHERE source_type IS NOT NULL",
                @"CREATE TABLE IF NOT EXISTS item_booth_metadata(
                    item_id TEXT PRIMARY KEY
                      REFERENCES item(id) ON DELETE CASCADE,
                    item_url TEXT,
                    shop_name TEXT,
                    shop_url TEXT
                  )",
                @"CREATE TABLE IF NOT EXISTS file(
                    id TEXT PRIMARY KEY,
                    item_id TEXT REFERENCES item(id) ON DELETE SET NULL,
                    file_name TEXT NOT NULL CHECK(trim(file_name) <> ''),
                    extension TEXT,
                    source_type TEXT NOT NULL CHECK(source_type IN(
                      'eagle', 'ee4v')),
                    source_id TEXT NOT NULL,
                    source_path TEXT,
                    is_archived INTEGER NOT NULL DEFAULT 0
                      CHECK(is_archived IN (0, 1)),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    UNIQUE(source_type, source_id)
                  )",
                @"CREATE INDEX IF NOT EXISTS ix_file_item
                    ON file(item_id)",
                @"CREATE INDEX IF NOT EXISTS ix_file_extension_item
                    ON file(extension, item_id)",
                @"CREATE TABLE IF NOT EXISTS item_target(
                    item_id TEXT NOT NULL
                      REFERENCES item(id) ON DELETE CASCADE,
                    file_id TEXT NOT NULL
                      REFERENCES file(id) ON DELETE CASCADE,
                    target_path TEXT NOT NULL COLLATE NOCASE
                      CHECK(target_path = trim(target_path))
                      CHECK(instr(target_path, '\') = 0)
                      CHECK(instr(target_path, ':') = 0)
                      CHECK(instr(target_path, char(0)) = 0)
                      CHECK(substr(target_path, 1, 1) <> '/')
                      CHECK(substr(target_path, -1, 1) <> '/')
                      CHECK(instr(target_path, '//') = 0)
                      CHECK(target_path NOT IN ('.', '..'))
                      CHECK(target_path NOT LIKE './%')
                      CHECK(target_path NOT LIKE '../%')
                      CHECK(target_path NOT LIKE '%/./%')
                      CHECK(target_path NOT LIKE '%/../%')
                      CHECK(target_path NOT LIKE '%/.')
                      CHECK(target_path NOT LIKE '%/..'),
                    group_name TEXT COLLATE NOCASE
                      CHECK(group_name IS NULL OR (
                        trim(group_name) <> '' AND
                        group_name = trim(group_name) AND
                        instr(group_name, char(0)) = 0 AND
                        instr(group_name, char(10)) = 0 AND
                        instr(group_name, char(13)) = 0)),
                    PRIMARY KEY(item_id, file_id, target_path)
                  )",
                @"CREATE INDEX IF NOT EXISTS ix_item_target_file
                    ON item_target(file_id, item_id)",
                @"CREATE TRIGGER IF NOT EXISTS
                    validate_item_target_file_item
                  BEFORE INSERT ON item_target
                  BEGIN
                    SELECT RAISE(
                      ABORT, 'item target file must belong to item')
                    WHERE NOT EXISTS(
                      SELECT 1 FROM file
                      WHERE id = NEW.file_id
                        AND item_id = NEW.item_id);
                  END",
                @"CREATE TRIGGER IF NOT EXISTS
                    cleanup_item_target_on_file_move
                  AFTER UPDATE OF item_id ON file
                  WHEN OLD.item_id IS NOT NEW.item_id
                  BEGIN
                    DELETE FROM item_target
                    WHERE file_id = NEW.id;
                  END",
                @"CREATE TABLE IF NOT EXISTS file_content_dependency(
                    dependent_file_id TEXT NOT NULL
                      REFERENCES file(id) ON DELETE CASCADE,
                    dependent_target_path TEXT NOT NULL COLLATE NOCASE
                      CHECK(dependent_target_path = trim(dependent_target_path))
                      CHECK(instr(dependent_target_path, '\') = 0)
                      CHECK(instr(dependent_target_path, ':') = 0)
                      CHECK(instr(dependent_target_path, char(0)) = 0)
                      CHECK(substr(dependent_target_path, 1, 1) <> '/')
                      CHECK(substr(dependent_target_path, -1, 1) <> '/')
                      CHECK(instr(dependent_target_path, '//') = 0)
                      CHECK(dependent_target_path NOT IN ('.', '..'))
                      CHECK(dependent_target_path NOT LIKE './%')
                      CHECK(dependent_target_path NOT LIKE '../%')
                      CHECK(dependent_target_path NOT LIKE '%/./%')
                      CHECK(dependent_target_path NOT LIKE '%/../%')
                      CHECK(dependent_target_path NOT LIKE '%/.')
                      CHECK(dependent_target_path NOT LIKE '%/..'),
                    dependency_file_id TEXT NOT NULL
                      REFERENCES file(id) ON DELETE CASCADE,
                    target_path TEXT NOT NULL COLLATE NOCASE
                      CHECK(target_path = trim(target_path))
                      CHECK(instr(target_path, '\') = 0)
                      CHECK(instr(target_path, ':') = 0)
                      CHECK(instr(target_path, char(0)) = 0)
                      CHECK(substr(target_path, 1, 1) <> '/')
                      CHECK(substr(target_path, -1, 1) <> '/')
                      CHECK(instr(target_path, '//') = 0)
                      CHECK(target_path NOT IN ('.', '..'))
                      CHECK(target_path NOT LIKE './%')
                      CHECK(target_path NOT LIKE '../%')
                      CHECK(target_path NOT LIKE '%/./%')
                      CHECK(target_path NOT LIKE '%/../%')
                      CHECK(target_path NOT LIKE '%/.')
                      CHECK(target_path NOT LIKE '%/..'),
                    PRIMARY KEY(
                      dependent_file_id,
                      dependent_target_path,
                      dependency_file_id,
                      target_path),
                    CHECK(dependent_file_id <> dependency_file_id
                      OR dependent_target_path <> target_path)
                  )",
                @"CREATE INDEX IF NOT EXISTS
                    ix_file_content_dependency_reverse
                    ON file_content_dependency(
                      dependency_file_id, target_path,
                      dependent_file_id, dependent_target_path)",
                @"CREATE TABLE IF NOT EXISTS file_imported_asset_guid(
                    file_id TEXT NOT NULL
                      REFERENCES file(id) ON DELETE CASCADE,
                    asset_guid TEXT NOT NULL
                      CHECK(length(asset_guid) = 32)
                      CHECK(asset_guid = lower(asset_guid))
                      CHECK(asset_guid NOT GLOB '*[^0-9a-f]*'),
                    imported_at TEXT NOT NULL,
                    PRIMARY KEY(file_id, asset_guid)
                  )",
                @"CREATE INDEX IF NOT EXISTS ix_file_imported_asset_guid
                    ON file_imported_asset_guid(asset_guid, file_id)",
                @"CREATE TRIGGER IF NOT EXISTS
                    prevent_file_content_dependency_cycle
                  BEFORE INSERT ON file_content_dependency
                  BEGIN
                    SELECT RAISE(
                      ABORT, 'file dependency cycle')
                    WHERE EXISTS(
                      WITH RECURSIVE dependencies(file_id, path) AS (
                        SELECT dependency_file_id, target_path
                        FROM file_content_dependency
                        WHERE dependent_file_id =
                          NEW.dependency_file_id
                          AND dependent_target_path = NEW.target_path
                        UNION
                        SELECT edge.dependency_file_id,
                               edge.target_path
                        FROM file_content_dependency edge
                        INNER JOIN dependencies current
                          ON edge.dependent_file_id = current.file_id
                         AND edge.dependent_target_path = current.path
                      )
                      SELECT 1 FROM dependencies
                      WHERE file_id = NEW.dependent_file_id
                        AND path = NEW.dependent_target_path
                    );
                  END",
                @"CREATE TABLE IF NOT EXISTS tag(
                    id TEXT PRIMARY KEY,
                    path TEXT NOT NULL UNIQUE
                      CHECK(trim(path) <> '')
                      CHECK(path = lower(path))
                      CHECK(substr(path, 1, 1) <> '/')
                      CHECK(substr(path, -1, 1) <> '/')
                      CHECK(instr(path, '//') = 0)
                      CHECK(instr(path, '#') = 0)
                  )",
                @"CREATE TABLE IF NOT EXISTS item_tag(
                    item_id TEXT NOT NULL
                      REFERENCES item(id) ON DELETE CASCADE,
                    tag_id TEXT NOT NULL
                      REFERENCES tag(id) ON DELETE CASCADE,
                    PRIMARY KEY(item_id, tag_id)
                  )",
                @"CREATE INDEX IF NOT EXISTS ix_item_tag_reverse
                    ON item_tag(tag_id, item_id)",
                @"CREATE TABLE IF NOT EXISTS item_source_tag(
                    item_id TEXT NOT NULL REFERENCES item(id)
                      ON DELETE CASCADE,
                    tag_id TEXT NOT NULL REFERENCES tag(id)
                      ON DELETE CASCADE,
                    source_type TEXT NOT NULL CHECK(source_type IN (
                      'eagle', 'ee4v')),
                    PRIMARY KEY(item_id, tag_id, source_type)
                  )",
                @"CREATE INDEX IF NOT EXISTS ix_item_source_tag_reverse
                    ON item_source_tag(tag_id, item_id)",
                @"CREATE TABLE IF NOT EXISTS collection(
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL UNIQUE CHECK(trim(name) <> ''),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                  )",
                @"CREATE TABLE IF NOT EXISTS collection_order(
                    collection_id TEXT PRIMARY KEY,
                    sort_order INTEGER NOT NULL UNIQUE CHECK(sort_order >= 0),
                    FOREIGN KEY(collection_id)
                      REFERENCES collection(id) ON DELETE CASCADE
                  )",
                @"CREATE TABLE IF NOT EXISTS collection_node(
                    id TEXT PRIMARY KEY,
                    collection_id TEXT NOT NULL,
                    parent_node_id TEXT,
                    node_type TEXT NOT NULL
                      CHECK(node_type IN ('and', 'or', 'not', 'condition')),
                    condition_type TEXT
                      CHECK(condition_type IS NULL OR condition_type IN(
                        'name_contains', 'description_contains',
                        'has_tag', 'has_file_extension')),
                    value TEXT,
                    sort_order INTEGER NOT NULL CHECK(sort_order >= 0),
                    UNIQUE(collection_id, id),
                    FOREIGN KEY(collection_id)
                      REFERENCES collection(id) ON DELETE CASCADE,
                    FOREIGN KEY(collection_id, parent_node_id)
                      REFERENCES collection_node(collection_id, id)
                      ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED,
                    CHECK(
                      (node_type = 'condition' AND
                       condition_type IS NOT NULL AND
                       value IS NOT NULL AND trim(value) <> '') OR
                      (node_type <> 'condition' AND
                       condition_type IS NULL AND value IS NULL))
                  )",
                @"CREATE UNIQUE INDEX IF NOT EXISTS ux_collection_root
                    ON collection_node(collection_id)
                    WHERE parent_node_id IS NULL",
                @"CREATE UNIQUE INDEX IF NOT EXISTS ux_collection_sibling
                    ON collection_node(
                      collection_id, parent_node_id, sort_order)
                    WHERE parent_node_id IS NOT NULL",
                @"CREATE INDEX IF NOT EXISTS ix_collection_node_tree
                    ON collection_node(
                      collection_id, parent_node_id, sort_order)",
                @"CREATE TRIGGER IF NOT EXISTS
                    prevent_collection_node_cycle
                  BEFORE UPDATE OF parent_node_id ON collection_node
                  WHEN NEW.parent_node_id IS NOT NULL
                  BEGIN
                    SELECT RAISE(ABORT, 'collection node cycle')
                    WHERE NEW.parent_node_id = NEW.id OR EXISTS(
                      WITH RECURSIVE descendants(id) AS (
                        SELECT id FROM collection_node
                        WHERE parent_node_id = NEW.id
                        UNION
                        SELECT node.id FROM collection_node node
                        INNER JOIN descendants child
                          ON node.parent_node_id = child.id
                      )
                      SELECT 1 FROM descendants
                      WHERE id = NEW.parent_node_id
                    );
                  END",
                "PRAGMA user_version = 1"
            };
            for (var i = 0; i < statements.Length; i++)
            {
                Execute(connection, null, statements[i]);
            }
        }

        private static string BuildFilterSql(
            AssetFilterNode node,
            ICollection<object> parameters)
        {
            switch (node.Type)
            {
                case AssetFilterNodeType.And:
                    return BuildFilterGroup(node, " AND ", parameters);
                case AssetFilterNodeType.Or:
                    return BuildFilterGroup(node, " OR ", parameters);
                case AssetFilterNodeType.Not:
                    return "NOT (" + BuildFilterSql(
                        node.Children[0],
                        parameters) + ")";
                case AssetFilterNodeType.Condition:
                    return BuildFilterCondition(node, parameters);
                default:
                    throw new InvalidOperationException(
                        "Unsupported asset filter node.");
            }
        }

        private static string BuildFilterGroup(
            AssetFilterNode node,
            string separator,
            ICollection<object> parameters)
        {
            return "(" + string.Join(
                separator,
                node.Children
                    .Select(child => BuildFilterSql(child, parameters))
                    .ToArray()) + ")";
        }

        private static string BuildFilterCondition(
            AssetFilterNode node,
            ICollection<object> parameters)
        {
            switch (node.ConditionType.Value)
            {
                case AssetFilterConditionType.NameContains:
                    parameters.Add(node.Value ?? string.Empty);
                    return "instr(lower(item.name), lower(?)) > 0";
                case AssetFilterConditionType.DescriptionContains:
                    parameters.Add(node.Value ?? string.Empty);
                    return "instr(lower(item.description), lower(?)) > 0";
                case AssetFilterConditionType.HasTag:
                    var tag = NormalizeTagFilter(node.Value);
                    parameters.Add(tag);
                    parameters.Add(EscapeLike(tag) + "/%");
                    return @"EXISTS(
                        SELECT 1 FROM tag filter_tag
                        WHERE (EXISTS(
                            SELECT 1 FROM item_tag filter_item_tag
                            WHERE filter_item_tag.item_id = item.id
                              AND filter_item_tag.tag_id = filter_tag.id)
                          OR EXISTS(
                            SELECT 1 FROM item_source_tag filter_source_tag
                            WHERE filter_source_tag.item_id = item.id
                              AND filter_source_tag.tag_id = filter_tag.id))
                          AND (filter_tag.path = ? OR
                               filter_tag.path LIKE ? ESCAPE '\'))";
                case AssetFilterConditionType.HasFileExtension:
                    parameters.Add(NormalizeExtensionFilter(node.Value));
                    return @"EXISTS(
                        SELECT 1 FROM file filter_file
                        WHERE filter_file.item_id = item.id
                          AND filter_file.is_archived = 0
                          AND filter_file.extension = ?)";
                default:
                    throw new InvalidOperationException(
                        "Unsupported asset filter condition.");
            }
        }

        private static string NormalizeTagFilter(string value)
        {
            var path = (value ?? string.Empty).Trim();
            if (path.StartsWith("#", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            return string.Join(
                "/",
                path.Split('/')
                    .Select(segment => segment.Trim().ToLowerInvariant())
                    .ToArray());
        }

        private static string NormalizeExtensionFilter(string value)
        {
            var extension = (value ?? string.Empty).Trim();
            while (extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = extension.Substring(1);
            }

            return extension.ToLowerInvariant();
        }

        private static string EscapeLike(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        private static AssetItem ReadItem(
            SQLiteConnection connection,
            string itemId)
        {
            var row = connection.Query<ItemRow>(
                    @"SELECT id AS Id, name AS Name,
                             description AS Description,
                             thumbnail_url AS ThumbnailUrl,
                             source_type AS SourceType,
                             source_id AS SourceId,
                             is_archived AS IsArchived,
                             created_at AS CreatedAt,
                             updated_at AS UpdatedAt
                      FROM item WHERE id = ?",
                    itemId)
                .SingleOrDefault();
            if (row == null)
            {
                throw NotFound("Item was not found.");
            }

            var item = new AssetItem
            {
                Id = row.Id,
                Name = row.Name,
                Description = row.Description,
                Booth = ReadItemBoothMetadata(connection, itemId),
                ThumbnailUrl = row.ThumbnailUrl,
                SourceType = row.SourceType == null
                    ? (AssetSourceType?)null
                    : ParseSourceType(row.SourceType),
                SourceId = row.SourceId,
                IsArchived = row.IsArchived != 0,
                CreatedAt = ParseDate(row.CreatedAt),
                UpdatedAt = ParseDate(row.UpdatedAt)
            };

            item.Tags = ReadTags(connection, itemId, null);
            item.Files = ReadFiles(
                connection,
                "item_id = @p0",
                itemId);
            return item;
        }

        private static AssetBoothMetadata ReadItemBoothMetadata(
            SQLiteConnection connection,
            string itemId)
        {
            var row = connection.Query<BoothMetadataRow>(
                    @"SELECT item_url AS ItemUrl,
                             shop_name AS ShopName,
                             shop_url AS ShopUrl
                      FROM item_booth_metadata WHERE item_id = ?",
                    itemId)
                .SingleOrDefault();
            return row == null
                ? null
                : new AssetBoothMetadata
                {
                    ItemUrl = row.ItemUrl,
                    ShopName = row.ShopName,
                    ShopUrl = row.ShopUrl
                };
        }

        private static AssetFile ReadFile(
            SQLiteConnection connection,
            string fileId)
        {
            var row = connection.Query<FileRow>(
                    FileSelect + " WHERE id = ?",
                    fileId)
                .SingleOrDefault();
            if (row == null)
            {
                throw NotFound("File was not found.");
            }

            return MapFile(row);
        }

        private static IReadOnlyList<AssetFile> ReadFiles(
            SQLiteConnection connection,
            string where,
            params object[] parameters)
        {
            return connection.Query<FileRow>(
                    FileSelect + " WHERE " + where +
                    " ORDER BY file_name, id",
                    parameters)
                .Select(MapFile)
                .ToArray();
        }

        private static AssetFile MapFile(FileRow row)
        {
            return new AssetFile
            {
                Id = row.Id,
                ItemId = row.ItemId,
                FileName = row.FileName,
                Extension = row.Extension,
                SourceType = ParseSourceType(row.SourceType),
                SourceId = row.SourceId,
                SourcePath = row.SourcePath,
                IsArchived = row.IsArchived != 0,
                CreatedAt = ParseDate(row.CreatedAt),
                UpdatedAt = ParseDate(row.UpdatedAt)
            };
        }

        private static IReadOnlyList<AssetFileTarget> ReadItemTargets(
            SQLiteConnection connection,
            string itemId)
        {
            return connection.Query<FileTargetRow>(
                    @"SELECT file_id AS FileId,
                             target_path AS TargetPath,
                             group_name AS GroupName
                      FROM item_target
                      WHERE item_id = ?
                      ORDER BY file_id, target_path COLLATE NOCASE",
                    itemId)
                .Select(row => new AssetFileTarget
                {
                    FileId = row.FileId,
                    TargetPath = row.TargetPath,
                    GroupName = row.GroupName
                })
                .ToArray();
        }

        private static IReadOnlyList<AssetFileDependency>
            ReadFileDependencies(
                SQLiteConnection connection,
                string fileId)
        {
            return connection.Query<FileDependencyRow>(
                    @"SELECT dependent_file_id AS DependentFileId,
                             dependent_target_path AS DependentTargetPath,
                             dependency_file_id AS DependencyFileId,
                             target_path AS TargetPath
                      FROM file_content_dependency
                      WHERE dependent_file_id = ?
                      ORDER BY dependent_target_path COLLATE NOCASE,
                               dependency_file_id,
                               target_path COLLATE NOCASE",
                    fileId)
                .Select(row => new AssetFileDependency
                {
                    DependentFileId = row.DependentFileId,
                    DependentTargetPath = row.DependentTargetPath,
                    DependencyFileId = row.DependencyFileId,
                    TargetPath = row.TargetPath
                })
                .ToArray();
        }

        private static IReadOnlyList<string> ReadFileImportedAssetGuids(
            SQLiteConnection connection,
            string fileId)
        {
            return QueryStrings(
                connection,
                null,
                @"SELECT asset_guid
                  FROM file_imported_asset_guid
                  WHERE file_id = @p0
                  ORDER BY asset_guid",
                fileId);
        }

        private static IReadOnlyList<AssetTag> ReadTags(
            SQLiteConnection connection,
            string itemId,
            DatabaseTransaction transaction)
        {
            var sql = itemId == null
                ? "SELECT id AS Id, path AS Path FROM tag ORDER BY path"
                : @"SELECT tag.id AS Id, tag.path AS Path,
                           EXISTS(SELECT 1 FROM item_source_tag source_tag
                                  WHERE source_tag.item_id = ?
                                    AND source_tag.tag_id = tag.id)
                             AS IsSourceOwned
                    FROM tag
                    WHERE EXISTS(SELECT 1 FROM item_tag
                                 WHERE item_tag.item_id = ?
                                   AND item_tag.tag_id = tag.id)
                       OR EXISTS(SELECT 1 FROM item_source_tag
                                 WHERE item_source_tag.item_id = ?
                                   AND item_source_tag.tag_id = tag.id)
                    ORDER BY tag.path";
            var rows = itemId == null
                ? connection.Query<TagRow>(sql)
                : connection.Query<TagRow>(sql, itemId, itemId, itemId);
            return rows.Select(row => new AssetTag
                {
                    Id = row.Id,
                    Path = row.Path,
                    IsSourceOwned = row.IsSourceOwned != 0
                })
                .ToArray();
        }

        private static void ReplaceSourceItemTags(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string itemId,
            string source,
            IReadOnlyList<string> paths)
        {
            Execute(connection, transaction,
                "DELETE FROM item_source_tag WHERE item_id = @p0 AND source_type = @p1",
                itemId, source);
            foreach (var path in paths ?? Array.Empty<string>())
            {
                var tagId = ScalarString(connection, transaction,
                    "SELECT id FROM tag WHERE path = @p0", path);
                if (tagId == null)
                {
                    tagId = NewId();
                    Execute(connection, transaction,
                        "INSERT INTO tag(id, path) VALUES(@p0, @p1)",
                        tagId, path);
                }

                Execute(connection, transaction,
                    @"INSERT INTO item_source_tag(item_id, tag_id, source_type)
                      VALUES(@p0, @p1, @p2)", itemId, tagId, source);
            }
        }

        private static void ReplaceItemTags(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string itemId,
            IReadOnlyList<string> paths)
        {
            Execute(
                connection,
                transaction,
                "DELETE FROM item_tag WHERE item_id = @p0",
                itemId);
            var source = paths ?? Array.Empty<string>();
            for (var i = 0; i < source.Count; i++)
            {
                var tagId = ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM tag WHERE path = @p0",
                    source[i]);
                if (tagId == null)
                {
                    tagId = NewId();
                    Execute(
                        connection,
                        transaction,
                        @"INSERT INTO tag(id, path)
                          VALUES(@p0, @p1)",
                        tagId,
                        source[i]);
                }

                Execute(
                    connection,
                    transaction,
                    @"INSERT INTO item_tag(item_id, tag_id)
                      VALUES(@p0, @p1)",
                    itemId,
                    tagId);
            }
        }

        private static void DeleteUnusedTags(
            SQLiteConnection connection,
            DatabaseTransaction transaction)
        {
            Execute(
                connection,
                transaction,
                @"DELETE FROM tag
                  WHERE NOT EXISTS(
                    SELECT 1 FROM item_tag
                    WHERE item_tag.tag_id = tag.id)
                    AND NOT EXISTS(
                      SELECT 1 FROM item_source_tag
                      WHERE item_source_tag.tag_id = tag.id)");
        }

        private static AssetCollection ReadCollection(
            SQLiteConnection connection,
            string collectionId)
        {
            var collection = connection.Query<CollectionRow>(
                    @"SELECT name AS Name, created_at AS CreatedAt,
                             updated_at AS UpdatedAt
                      FROM collection WHERE id = ?",
                    collectionId)
                .SingleOrDefault();
            if (collection == null)
            {
                throw NotFound("Collection was not found.");
            }

            var rows = connection.Query<CollectionNodeRow>(
                @"SELECT id AS Id, parent_node_id AS ParentId,
                         node_type AS NodeType,
                         condition_type AS ConditionType,
                         value AS Value, sort_order AS SortOrder
                  FROM collection_node
                  WHERE collection_id = ?
                  ORDER BY sort_order, id",
                collectionId);

            var root = rows.SingleOrDefault(row => row.ParentId == null);
            if (root == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatabaseError,
                    "Collection root was not found.");
            }

            return new AssetCollection
            {
                Id = collectionId,
                Name = collection.Name,
                Root = BuildNode(root, rows),
                CreatedAt = ParseDate(collection.CreatedAt),
                UpdatedAt = ParseDate(collection.UpdatedAt)
            };
        }

        private static AssetFilterNode BuildNode(
            CollectionNodeRow row,
            IReadOnlyList<CollectionNodeRow> rows)
        {
            return new AssetFilterNode
            {
                Type = ParseNodeType(row.NodeType),
                ConditionType = ParseConditionType(row.ConditionType),
                Value = row.Value,
                Children = rows
                    .Where(child => child.ParentId == row.Id)
                    .OrderBy(child => child.SortOrder)
                    .ThenBy(child => child.Id, StringComparer.Ordinal)
                    .Select(child => BuildNode(child, rows))
                    .ToArray()
            };
        }

        private static void InsertNode(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string collectionId,
            string parentId,
            int sortOrder,
            AssetFilterNode node)
        {
            var id = NewId();
            Execute(
                connection,
                transaction,
                @"INSERT INTO collection_node(
                    id, collection_id, parent_node_id, node_type,
                    condition_type, value, sort_order)
                  VALUES(@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                id,
                collectionId,
                parentId,
                NodeType(node.Type),
                node.ConditionType.HasValue
                    ? ConditionType(node.ConditionType.Value)
                    : null,
                node.Type == AssetFilterNodeType.Condition
                    ? node.Value.Trim()
                    : null,
                sortOrder);
            var children = node.Children ??
                           Array.Empty<AssetFilterNode>();
            for (var i = 0; i < children.Count; i++)
            {
                InsertNode(
                    connection,
                    transaction,
                    collectionId,
                    id,
                    i,
                    children[i]);
            }
        }

        private static string UpsertSourceItem(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source,
            AssetSourceSnapshotItem item)
        {
            var itemId = ScalarString(
                connection,
                transaction,
                @"SELECT id FROM item
                  WHERE source_type = @p0 AND source_id = @p1",
                source,
                item.SourceId);
            var name = string.IsNullOrWhiteSpace(item.Name)
                ? item.SourceId
                : item.Name.Trim();
            var description = item.Description ?? string.Empty;
            var now = Now();
            if (itemId == null)
            {
                itemId = NewId();
                Execute(
                    connection,
                    transaction,
                    @"INSERT INTO item(
                        id, name, description, thumbnail_url,
                        source_type, source_id,
                        is_archived, created_at, updated_at)
                      VALUES(
                        @p0, @p1, @p2, @p3, @p4, @p5,
                        0, @p6, @p6)",
                    itemId,
                    name,
                    description,
                    NullIfWhiteSpace(item.ThumbnailUrl),
                    source,
                    item.SourceId,
                    now);
                ReplaceItemBoothMetadata(
                    connection,
                    transaction,
                    itemId,
                    item.Booth);
                return itemId;
            }

            var boothChanged = ReplaceItemBoothMetadata(
                connection,
                transaction,
                itemId,
                item.Booth);
            Execute(
                connection,
                transaction,
                @"UPDATE item
                  SET name = @p0, description = @p1,
                      thumbnail_url = @p2, updated_at = @p3
                  WHERE id = @p4 AND (
                    name <> @p0 OR description <> @p1 OR
                    thumbnail_url IS NOT @p2 OR @p5 <> 0)",
                name,
                description,
                NullIfWhiteSpace(item.ThumbnailUrl),
                now,
                itemId,
                boothChanged ? 1 : 0);

            return itemId;
        }

        private static bool ReplaceItemBoothMetadata(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string itemId,
            AssetBoothMetadata booth)
        {
            var existing = connection.Query<BoothMetadataRow>(
                    @"SELECT item_url AS ItemUrl,
                             shop_name AS ShopName,
                             shop_url AS ShopUrl
                      FROM item_booth_metadata WHERE item_id = ?",
                    itemId)
                .SingleOrDefault();
            var itemUrl = NullIfWhiteSpace(booth?.ItemUrl);
            var shopName = NullIfWhiteSpace(booth?.ShopName);
            var shopUrl = NullIfWhiteSpace(booth?.ShopUrl);
            var changed = existing == null
                ? itemUrl != null || shopName != null || shopUrl != null
                : !string.Equals(
                      existing.ItemUrl,
                      itemUrl,
                      StringComparison.Ordinal) ||
                  !string.Equals(
                      existing.ShopName,
                      shopName,
                      StringComparison.Ordinal) ||
                  !string.Equals(
                      existing.ShopUrl,
                      shopUrl,
                      StringComparison.Ordinal);
            if (!changed)
            {
                return false;
            }

            if (itemUrl == null && shopName == null && shopUrl == null)
            {
                Execute(
                    connection,
                    transaction,
                    "DELETE FROM item_booth_metadata WHERE item_id = @p0",
                    itemId);
                return true;
            }

            Execute(
                connection,
                transaction,
                @"INSERT OR REPLACE INTO item_booth_metadata(
                    item_id, item_url, shop_name, shop_url)
                  VALUES(@p0, @p1, @p2, @p3)",
                itemId,
                itemUrl,
                shopName,
                shopUrl);
            return true;
        }

        private static void UpsertSourceFile(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source,
            string itemId,
            AssetSourceSnapshotFile file)
        {
            var fileId = ScalarString(
                connection,
                transaction,
                @"SELECT id FROM file
                  WHERE source_type = @p0 AND source_id = @p1",
                source,
                file.SourceId);
            var fileName = string.IsNullOrWhiteSpace(file.FileName)
                ? file.SourceId
                : file.FileName.Trim();
            var extension = string.IsNullOrWhiteSpace(file.Extension)
                ? null
                : file.Extension.Trim().TrimStart('.').ToLowerInvariant();
            var now = Now();
            if (fileId == null)
            {
                Execute(
                    connection,
                    transaction,
                    @"INSERT INTO file(
                        id, item_id, file_name, extension, source_type,
                        source_id, source_path,
                        is_archived, created_at, updated_at)
                      VALUES(
                        @p0, @p1, @p2, @p3, @p4, @p5, @p6,
                        0, @p7, @p7)",
                    NewId(),
                    itemId,
                    fileName,
                    extension,
                    source,
                    file.SourceId,
                    file.SourcePath,
                    now);
                return;
            }

            Execute(
                connection,
                transaction,
                @"UPDATE file
                  SET item_id = CASE
                        WHEN @p0 IS NULL THEN item_id
                        ELSE @p0
                      END,
                      file_name = @p1,
                      extension = @p2,
                      source_path = @p3,
                      updated_at = @p4
                  WHERE id = @p5 AND (
                    (@p0 IS NOT NULL AND item_id IS NOT @p0) OR
                    file_name <> @p1 OR
                    extension IS NOT @p2 OR
                    source_path IS NOT @p3)",
                itemId,
                fileName,
                extension,
                file.SourcePath,
                now,
                fileId);
        }

        private static void DeleteMissingSourceFiles(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source,
            ISet<string> seenFiles)
        {
            var sourceIds = QueryStrings(
                connection,
                transaction,
                @"SELECT source_id FROM file
                  WHERE source_type = @p0",
                source);
            foreach (var sourceId in sourceIds)
            {
                if (seenFiles.Contains(sourceId))
                {
                    continue;
                }

                Execute(
                    connection,
                    transaction,
                    @"DELETE FROM file
                      WHERE source_type = @p0
                        AND source_id = @p1",
                    source,
                    sourceId);
            }
        }

        private static void DeleteMissingSourceItems(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source,
            ISet<string> seenItems)
        {
            var sourceIds = QueryStrings(
                connection,
                transaction,
                @"SELECT source_id FROM item
                  WHERE source_type = @p0",
                source);
            foreach (var sourceId in sourceIds)
            {
                if (seenItems.Contains(sourceId))
                {
                    continue;
                }

                Execute(
                    connection,
                    transaction,
                    @"DELETE FROM item
                      WHERE source_type = @p0
                        AND source_id = @p1",
                    source,
                    sourceId);
            }
        }

        private static SourceState ReadSourceState(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source)
        {
            var state = new SourceState();
            var itemIds = QueryStrings(
                connection,
                transaction,
                @"SELECT id FROM item
                  WHERE source_type = @p0",
                source);
            for (var i = 0; i < itemIds.Count; i++)
            {
                var item = ReadItem(connection, itemIds[i]);
                state.Items[item.Id] = new SourceItemState(item);
            }

            var files = connection.Query<FileRow>(
                    FileSelect + " WHERE source_type = ?",
                    source)
                .Select(MapFile)
                .ToArray();
            for (var i = 0; i < files.Length; i++)
            {
                state.Files[files[i].Id] =
                    new SourceFileState(files[i]);
            }

            var dependentIds = QueryStrings(
                connection,
                transaction,
                @"SELECT DISTINCT dependency.dependent_file_id
                  FROM file_content_dependency dependency
                  INNER JOIN file source_file
                    ON source_file.id = dependency.dependency_file_id
                  WHERE source_file.source_type = @p0",
                source);
            state.DependentFileIds.UnionWith(dependentIds);
            var attachments = connection.Query<FileAttachmentRow>(
                @"SELECT file.id AS FileId, file.item_id AS ItemId
                  FROM file
                  INNER JOIN item ON item.id = file.item_id
                  WHERE item.source_type = ?
                    AND file.source_type <> ?",
                source,
                source);
            for (var i = 0; i < attachments.Count; i++)
            {
                state.ExternalAttachments[attachments[i].FileId] =
                    attachments[i].ItemId;
            }

            return state;
        }

        private static AssetSyncResult BuildSyncResult(
            SourceState before,
            SourceState after,
            IReadOnlyList<string> errorMessages)
        {
            var createdItems = NewIds(after.Items, before.Items);
            var deletedItems = NewIds(before.Items, after.Items);
            var updatedItems = ChangedIds(
                before.Items,
                after.Items,
                (left, right) => left.Equals(right));
            var createdFiles = NewIds(after.Files, before.Files);
            var deletedFiles = NewIds(before.Files, after.Files);
            var updatedFiles = ChangedIds(
                before.Files,
                after.Files,
                (left, right) => left.Equals(right));
            var affectedItems = new HashSet<string>(
                createdItems
                    .Concat(updatedItems)
                    .Concat(deletedItems),
                StringComparer.Ordinal);
            var affectedFiles = new HashSet<string>(
                createdFiles
                    .Concat(updatedFiles)
                    .Concat(deletedFiles),
                StringComparer.Ordinal);
            foreach (var fileId in createdFiles.Concat(updatedFiles))
            {
                AddId(affectedItems, after.Files[fileId].ItemId);
            }

            foreach (var fileId in deletedFiles.Concat(updatedFiles))
            {
                AddId(affectedItems, before.Files[fileId].ItemId);
            }

            foreach (var fileId in before.DependentFileIds.Except(
                         after.DependentFileIds,
                         StringComparer.Ordinal))
            {
                affectedFiles.Add(fileId);
            }

            var attachmentIds = new HashSet<string>(
                before.ExternalAttachments.Keys.Concat(
                    after.ExternalAttachments.Keys),
                StringComparer.Ordinal);
            foreach (var fileId in attachmentIds)
            {
                before.ExternalAttachments.TryGetValue(
                    fileId,
                    out var beforeItemId);
                after.ExternalAttachments.TryGetValue(
                    fileId,
                    out var afterItemId);
                if (string.Equals(
                        beforeItemId,
                        afterItemId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                affectedFiles.Add(fileId);
                AddId(affectedItems, beforeItemId);
                AddId(affectedItems, afterItemId);
            }

            var unchangedCount = UnchangedCount(
                                     before.Items,
                                     after.Items,
                                     (left, right) => left.Equals(right)) +
                                 UnchangedCount(
                                     before.Files,
                                     after.Files,
                                     (left, right) => left.Equals(right));
            return new AssetSyncResult(
                createdItems,
                updatedItems,
                deletedItems,
                createdFiles,
                updatedFiles,
                deletedFiles,
                affectedItems.OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                affectedFiles.OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                unchangedCount,
                errorMessages);
        }

        private static string[] NewIds<T>(
            IReadOnlyDictionary<string, T> source,
            IReadOnlyDictionary<string, T> other)
        {
            return source.Keys
                .Where(id => !other.ContainsKey(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ChangedIds<T>(
            IReadOnlyDictionary<string, T> before,
            IReadOnlyDictionary<string, T> after,
            Func<T, T, bool> equals)
        {
            return before.Keys
                .Where(after.ContainsKey)
                .Where(id => !equals(before[id], after[id]))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        private static int UnchangedCount<T>(
            IReadOnlyDictionary<string, T> before,
            IReadOnlyDictionary<string, T> after,
            Func<T, T, bool> equals)
        {
            return before.Keys.Count(id =>
                after.ContainsKey(id) && equals(before[id], after[id]));
        }

        private static void AddId(ISet<string> ids, string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        private static void EnsureCollectionNameAvailable(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string name,
            string excludingId)
        {
            var existing = excludingId == null
                ? ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM collection WHERE name = @p0",
                    name.Trim())
                : ScalarString(
                    connection,
                    transaction,
                    @"SELECT id FROM collection
                      WHERE name = @p0 AND id <> @p1",
                    name.Trim(),
                    excludingId);
            if (existing != null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.Duplicate,
                    "Collection name already exists.");
            }
        }

        private static void EnsureCollectionExists(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string collectionId)
        {
            if (ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM collection WHERE id = @p0",
                    collectionId) == null)
            {
                throw NotFound("Collection was not found.");
            }
        }

        private static void RequireItem(
            SQLiteConnection connection,
            string itemId,
            DatabaseTransaction transaction)
        {
            if (ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM item WHERE id = @p0",
                    itemId) == null)
            {
                throw NotFound("Item was not found.");
            }
        }

        private static void RequireFile(
            SQLiteConnection connection,
            string fileId,
            DatabaseTransaction transaction)
        {
            if (ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM file WHERE id = @p0",
                    fileId) == null)
            {
                throw NotFound("File was not found.");
            }
        }

        private SQLiteConnection OpenConnection()
        {
            SqliteBootstrap.EnsureInitialized();
            var connection = new SQLiteConnection(
                _databasePath,
                SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create |
                SQLiteOpenFlags.FullMutex |
                SQLiteOpenFlags.PrivateCache);
            Execute(connection, null, "PRAGMA foreign_keys = ON");
            return connection;
        }

        private static int Execute(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string sql,
            params object[] parameters)
        {
            return connection.Execute(sql, parameters);
        }

        private static string ScalarString(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string sql,
            params object[] parameters)
        {
            return connection.ExecuteScalar<string>(
                sql,
                parameters);
        }

        private static IReadOnlyList<string> QueryStrings(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string sql,
            params object[] parameters)
        {
            return connection.QueryScalars<string>(
                sql,
                parameters);
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string Now()
        {
            return DateTime.UtcNow.ToString("O");
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        private static void RequireChanged(int changed, string message)
        {
            if (changed == 0)
            {
                throw NotFound(message);
            }
        }

        private static AssetManagerException NotFound(string message)
        {
            return new AssetManagerException(
                AssetManagerErrorCode.NotFound,
                message);
        }

        private static string ToSourceType(AssetSourceType sourceType)
        {
            return sourceType == AssetSourceType.Ee4v
                ? "ee4v"
                : "eagle";
        }

        private static AssetSourceType ParseSourceType(string value)
        {
            return value == "ee4v"
                ? AssetSourceType.Ee4v
                : AssetSourceType.Eagle;
        }

        private static string NodeType(AssetFilterNodeType type)
        {
            return type == AssetFilterNodeType.And
                ? "and"
                : type == AssetFilterNodeType.Or
                    ? "or"
                    : type == AssetFilterNodeType.Not
                        ? "not"
                        : "condition";
        }

        private static AssetFilterNodeType ParseNodeType(string value)
        {
            return value == "and"
                ? AssetFilterNodeType.And
                : value == "or"
                    ? AssetFilterNodeType.Or
                    : value == "not"
                        ? AssetFilterNodeType.Not
                        : AssetFilterNodeType.Condition;
        }

        private static string ConditionType(
            AssetFilterConditionType type)
        {
            switch (type)
            {
                case AssetFilterConditionType.NameContains:
                    return "name_contains";
                case AssetFilterConditionType.DescriptionContains:
                    return "description_contains";
                case AssetFilterConditionType.HasTag:
                    return "has_tag";
                default:
                    return "has_file_extension";
            }
        }

        private static AssetFilterConditionType? ParseConditionType(
            string value)
        {
            if (value == null)
            {
                return null;
            }

            return value == "name_contains"
                ? AssetFilterConditionType.NameContains
                : value == "description_contains"
                    ? AssetFilterConditionType.DescriptionContains
                    : value == "has_tag"
                        ? AssetFilterConditionType.HasTag
                        : AssetFilterConditionType.HasFileExtension;
        }

        private T Run<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatabaseError,
                    "AssetManager database operation failed.",
                    exception);
            }
        }

        private sealed class DatabaseTransaction : IDisposable
        {
            private readonly SQLiteConnection _connection;
            private bool _committed;

            internal DatabaseTransaction(SQLiteConnection connection)
            {
                _connection = connection;
                _connection.BeginTransaction();
            }

            internal void Commit()
            {
                _connection.Commit();
                _committed = true;
            }

            public void Dispose()
            {
                if (!_committed)
                {
                    _connection.Rollback();
                }
            }
        }

        private sealed class ItemRow
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string ThumbnailUrl { get; set; }
            public string SourceType { get; set; }
            public string SourceId { get; set; }
            public int IsArchived { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
        }

        private sealed class BoothMetadataRow
        {
            public string ItemUrl { get; set; }
            public string ShopName { get; set; }
            public string ShopUrl { get; set; }
        }

        private sealed class IdRow
        {
            public string Id { get; set; }
        }

        private sealed class SourceState
        {
            internal Dictionary<string, SourceItemState> Items { get; } =
                new Dictionary<string, SourceItemState>(
                    StringComparer.Ordinal);
            internal Dictionary<string, SourceFileState> Files { get; } =
                new Dictionary<string, SourceFileState>(
                    StringComparer.Ordinal);
            internal HashSet<string> DependentFileIds { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            internal Dictionary<string, string> ExternalAttachments { get; } =
                new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private sealed class SourceItemState
        {
            internal SourceItemState(AssetItem item)
            {
                Name = item.Name ?? string.Empty;
                Description = item.Description ?? string.Empty;
                ThumbnailUrl = item.ThumbnailUrl ?? string.Empty;
                BoothItemUrl = item.Booth?.ItemUrl ?? string.Empty;
                BoothShopName = item.Booth?.ShopName ?? string.Empty;
                BoothShopUrl = item.Booth?.ShopUrl ?? string.Empty;
                IsArchived = item.IsArchived;
                Tags = (item.Tags ?? Array.Empty<AssetTag>())
                    .Select(tag => (tag.Path ?? string.Empty) +
                                   (tag.IsSourceOwned ? "\u0001" : "\u0000"))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
            }

            private string Name { get; }
            private string Description { get; }
            private string ThumbnailUrl { get; }
            private string BoothItemUrl { get; }
            private string BoothShopName { get; }
            private string BoothShopUrl { get; }
            private bool IsArchived { get; }
            private IReadOnlyList<string> Tags { get; }

            internal bool Equals(SourceItemState other)
            {
                return other != null &&
                       string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                       string.Equals(
                           Description,
                           other.Description,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           ThumbnailUrl,
                           other.ThumbnailUrl,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           BoothItemUrl,
                           other.BoothItemUrl,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           BoothShopName,
                           other.BoothShopName,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           BoothShopUrl,
                           other.BoothShopUrl,
                           StringComparison.Ordinal) &&
                       IsArchived == other.IsArchived &&
                       Tags.SequenceEqual(other.Tags, StringComparer.Ordinal);
            }
        }

        private sealed class SourceFileState
        {
            internal SourceFileState(AssetFile file)
            {
                ItemId = file.ItemId ?? string.Empty;
                FileName = file.FileName ?? string.Empty;
                Extension = file.Extension ?? string.Empty;
                SourcePath = file.SourcePath ?? string.Empty;
                IsArchived = file.IsArchived;
            }

            internal string ItemId { get; }
            private string FileName { get; }
            private string Extension { get; }
            private string SourcePath { get; }
            private bool IsArchived { get; }

            internal bool Equals(SourceFileState other)
            {
                return other != null &&
                       string.Equals(
                           ItemId,
                           other.ItemId,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           FileName,
                           other.FileName,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           Extension,
                           other.Extension,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           SourcePath,
                           other.SourcePath,
                           StringComparison.Ordinal) &&
                       IsArchived == other.IsArchived;
            }
        }

        private sealed class FileAttachmentRow
        {
            public string FileId { get; set; }
            public string ItemId { get; set; }
        }

        private sealed class FileRow
        {
            public string Id { get; set; }
            public string ItemId { get; set; }
            public string FileName { get; set; }
            public string Extension { get; set; }
            public string SourceType { get; set; }
            public string SourceId { get; set; }
            public string SourcePath { get; set; }
            public int IsArchived { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
        }

        private sealed class FileTargetRow
        {
            public string FileId { get; set; }
            public string TargetPath { get; set; }
            public string GroupName { get; set; }
        }

        private sealed class FileDependencyRow
        {
            public string DependentFileId { get; set; }
            public string DependentTargetPath { get; set; }
            public string DependencyFileId { get; set; }
            public string TargetPath { get; set; }
        }

        private sealed class ImportedAssetGuidRow
        {
            public string ItemId { get; set; }
            public string FileId { get; set; }
            public string AssetGuid { get; set; }
            public string ImportedAt { get; set; }
        }

        private sealed class TagRow
        {
            public string Id { get; set; }
            public string Path { get; set; }
            public int IsSourceOwned { get; set; }
        }

        private sealed class CollectionRow
        {
            public string Name { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
        }

        private sealed class CollectionNodeRow
        {
            public string Id { get; set; }
            public string ParentId { get; set; }
            public string NodeType { get; set; }
            public string ConditionType { get; set; }
            public string Value { get; set; }
            public int SortOrder { get; set; }
        }
    }
}
