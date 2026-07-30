using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Domain;
using Ee4v.AssetManager.Infrastructure.Datasources.Blm;
using Ee4v.AssetManager.Infrastructure.Datasources.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        public static IReadOnlyList<AssetTag> GetTags(string keyword)
        {
            using (var connection = OpenConnection())
            {
                var rows = string.IsNullOrWhiteSpace(keyword)
                    ? connection.Query<TagRow>("SELECT * FROM tag_info ORDER BY name COLLATE NOCASE")
                    : connection.Query<TagRow>("SELECT * FROM tag_info WHERE name LIKE ? ORDER BY name COLLATE NOCASE", "%" + keyword + "%");
                return rows.Select(ToAssetTag).ToArray();
            }
        }

        public static AssetTag CreateTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Tag name is required.");
            }

            using (var connection = OpenConnection())
            {
                return ToAssetTag(EnsureTag(connection, name));
            }
        }

        public static void SetItemTags(string itemId, IReadOnlyList<string> tagIds)
        {
            using (var connection = OpenConnection())
            {
                InTransaction(connection, () =>
                {
                    EnsureItemExists(connection, itemId);
                    SyncItemTags(connection, itemId, tagIds ?? Array.Empty<string>());
                });
            }
        }

        public static IReadOnlyList<AssetCollection> GetCollections()
        {
            using (var connection = OpenConnection())
            {
                return connection.Query<CollectionRow>("SELECT * FROM collection_info ORDER BY sort_order, name COLLATE NOCASE, id")
                    .Select(row => ToAssetCollection(connection, row))
                    .ToArray();
            }
        }

        public static AssetCollection CreateCollection(CreateCollectionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Collection name is required.");
            }

            using (var connection = OpenConnection())
            {
                if (!string.IsNullOrWhiteSpace(request.ParentCollectionId))
                {
                    EnsureCollectionExists(connection, request.ParentCollectionId);
                    EnsureCollectionCanContainChildren(
                        connection,
                        request.ParentCollectionId);
                }

                var id = InTransaction(connection, () =>
                {
                    var now = Now();
                    var nextId = NewId();
                    connection.Execute(
                        "INSERT INTO collection_info(id, name, icon, icon_asset_guid, sort_order, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?)",
                        nextId,
                        request.Name,
                        ToDbCollectionIcon(
                            AssetCollectionIcon.Folder),
                        null,
                        0,
                        now,
                        now);
                    PlaceCollection(
                        connection,
                        nextId,
                        request.ParentCollectionId,
                        -1);
                    return nextId;
                });
                return ToAssetCollection(connection, connection.Query<CollectionRow>("SELECT * FROM collection_info WHERE id = ?", id).First());
            }
        }

        public static AssetCollection CreateSmartCollection(CreateSmartCollectionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Smart collection name is required.");
            }

            using (var connection = OpenConnection())
            {
                if (!string.IsNullOrWhiteSpace(request.ParentCollectionId))
                {
                    EnsureCollectionExists(connection, request.ParentCollectionId);
                    EnsureCollectionCanContainChildren(
                        connection,
                        request.ParentCollectionId);
                }

                var conditions = request.Conditions ?? Array.Empty<SmartCollectionCondition>();
                for (var i = 0; i < conditions.Count; i++)
                {
                    ValidateSmartCondition(conditions[i]);
                }

                var collectionId = InTransaction(connection, () =>
                {
                    var now = Now();
                    var nextId = NewId();
                    connection.Execute(
                        "INSERT INTO collection_info(id, name, icon, icon_asset_guid, sort_order, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?)",
                        nextId,
                        request.Name,
                        ToDbCollectionIcon(request.Icon),
                        string.IsNullOrWhiteSpace(request.IconAssetGuid)
                            ? null
                            : request.IconAssetGuid.Trim(),
                        0,
                        now,
                        now);
                    PlaceCollection(
                        connection,
                        nextId,
                        request.ParentCollectionId,
                        -1);
                    connection.Execute(
                        "INSERT INTO smart_collection_info(collection_info_id, match_mode) VALUES (?, ?)",
                        nextId,
                        request.MatchMode == SmartCollectionMatchMode.Any ? "any" : "all");

                    for (var i = 0; i < conditions.Count; i++)
                    {
                        InsertSmartCondition(connection, nextId, i, conditions[i]);
                    }

                    return nextId;
                });

                return ToAssetCollection(connection, connection.Query<CollectionRow>("SELECT * FROM collection_info WHERE id = ?", collectionId).First());
            }
        }

        public static AssetCollection UpdateCollection(
            string collectionId,
            UpdateCollectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(collectionId) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Collection id and update request are required.");
            }

            using (var connection = OpenConnection())
            {
                EnsureCollectionExists(connection, collectionId);
                var isSmart = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM smart_collection_info WHERE collection_info_id = ?",
                    collectionId) > 0;
                connection.Execute(
                    "UPDATE collection_info SET name = ?, icon = ?, icon_asset_guid = ?, updated_at = ? WHERE id = ?",
                    request.Name.Trim(),
                    ToDbCollectionIcon(
                        isSmart
                            ? request.Icon
                            : AssetCollectionIcon.Folder),
                    isSmart &&
                    !string.IsNullOrWhiteSpace(request.IconAssetGuid)
                        ? request.IconAssetGuid.Trim()
                        : null,
                    Now(),
                    collectionId);
                return ToAssetCollection(
                    connection,
                    connection.Query<CollectionRow>(
                        "SELECT * FROM collection_info WHERE id = ?",
                        collectionId).First());
            }
        }

        public static AssetCollection UpdateSmartCollection(
            string collectionId,
            UpdateSmartCollectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(collectionId) ||
                request == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Smart Collection id and update request are required.");
            }

            var conditions = request.Conditions ??
                             Array.Empty<SmartCollectionCondition>();
            for (var i = 0; i < conditions.Count; i++)
            {
                ValidateSmartCondition(conditions[i]);
            }

            using (var connection = OpenConnection())
            {
                EnsureCollectionExists(connection, collectionId);
                var isSmart = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM smart_collection_info WHERE collection_info_id = ?",
                    collectionId) > 0;
                if (!isSmart)
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Collection is not a Smart Collection.");
                }

                InTransaction(connection, () =>
                {
                    connection.Execute(
                        "UPDATE smart_collection_info SET match_mode = ? WHERE collection_info_id = ?",
                        request.MatchMode ==
                        SmartCollectionMatchMode.Any
                            ? "any"
                            : "all",
                        collectionId);
                    connection.Execute(
                        "DELETE FROM smart_collection_condition WHERE collection_info_id = ?",
                        collectionId);
                    for (var i = 0; i < conditions.Count; i++)
                    {
                        InsertSmartCondition(
                            connection,
                            collectionId,
                            i,
                            conditions[i]);
                    }

                    connection.Execute(
                        "UPDATE collection_info SET updated_at = ? WHERE id = ?",
                        Now(),
                        collectionId);
                });

                return ToAssetCollection(
                    connection,
                    connection.Query<CollectionRow>(
                        "SELECT * FROM collection_info WHERE id = ?",
                        collectionId).First());
            }
        }

        public static bool DeleteCollections(
            IReadOnlyList<string> collectionIds)
        {
            var requestedIds = (collectionIds ??
                                Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (requestedIds.Length == 0 ||
                collectionIds == null ||
                requestedIds.Length != collectionIds.Count)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "At least one unique collection id is required.");
            }

            using (var connection = OpenConnection())
            {
                return InTransaction(connection, () =>
                {
                    var subtreeById =
                        new Dictionary<string, CollectionSubtreeRow>(
                            StringComparer.Ordinal);
                    for (var i = 0; i < requestedIds.Length; i++)
                    {
                        var collectionId = requestedIds[i];
                        EnsureCollectionExists(
                            connection,
                            collectionId);
                        var subtree = connection
                            .Query<CollectionSubtreeRow>(
                                @"WITH RECURSIVE collection_subtree(id, depth) AS (
                                    SELECT ?, 0
                                    UNION ALL
                                    SELECT
                                        relation.child_collection_id,
                                        subtree.depth + 1
                                    FROM collection_collection relation
                                    INNER JOIN collection_subtree subtree
                                        ON relation.parent_collection_id = subtree.id
                                )
                                SELECT id, depth
                                FROM collection_subtree",
                                collectionId);
                        for (var j = 0; j < subtree.Count; j++)
                        {
                            CollectionSubtreeRow existing;
                            if (!subtreeById.TryGetValue(
                                    subtree[j].id,
                                    out existing) ||
                                subtree[j].depth > existing.depth)
                            {
                                subtreeById[subtree[j].id] =
                                    subtree[j];
                            }
                        }
                    }

                    var deletedIds = new HashSet<string>(
                        subtreeById.Keys,
                        StringComparer.Ordinal);
                    var affectedParentIds =
                        new HashSet<string>(StringComparer.Ordinal);
                    for (var i = 0;
                         i < requestedIds.Length;
                         i++)
                    {
                        var relation = connection
                            .Query<CollectionCollectionRow>(
                                "SELECT * FROM collection_collection WHERE child_collection_id = ? LIMIT 1",
                                requestedIds[i])
                            .FirstOrDefault();
                        var parentId = relation != null
                            ? relation.parent_collection_id
                            : null;
                        if (!deletedIds.Contains(
                                parentId ?? string.Empty))
                        {
                            affectedParentIds.Add(
                                parentId ?? string.Empty);
                        }
                    }

                    var affectsSmartCollection =
                        subtreeById.Values.Any(item =>
                            connection.ExecuteScalar<int>(
                                "SELECT COUNT(*) FROM smart_collection_info WHERE collection_info_id = ?",
                                item.id) > 0);
                    var orderedSubtree = subtreeById.Values
                        .OrderByDescending(item => item.depth)
                        .ToArray();
                    for (var i = 0;
                         i < orderedSubtree.Length;
                         i++)
                    {
                        connection.Execute(
                            "DELETE FROM collection_info WHERE id = ?",
                            orderedSubtree[i].id);
                    }

                    foreach (var parentId in affectedParentIds)
                    {
                        NormalizeCollectionOrder(
                            connection,
                            GetCollectionSiblingIds(
                                connection,
                                string.IsNullOrEmpty(parentId)
                                    ? null
                                    : parentId,
                                null));
                    }

                    return affectsSmartCollection;
                });
            }
        }

        public static void MoveCollection(
            string collectionId,
            string parentCollectionId,
            int siblingIndex)
        {
            MoveCollections(
                new[] { collectionId },
                parentCollectionId,
                siblingIndex);
        }

        public static void MoveCollections(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex)
        {
            using (var connection = OpenConnection())
            {
                InTransaction(connection, () =>
                {
                    PlaceCollections(
                        connection,
                        collectionIds,
                        parentCollectionId,
                        siblingIndex);
                });
            }
        }

        public static void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds)
        {
            using (var connection = OpenConnection())
            {
                InTransaction(connection, () =>
                {
                    EnsureItemExists(connection, itemId);
                    SyncItemCollections(connection, itemId, collectionIds ?? Array.Empty<string>());
                });
            }
        }

        public static bool AddItemsToCollection(
            IReadOnlyList<string> itemIds,
            string collectionId)
        {
            using (var connection = OpenConnection())
            {
                return InTransaction(connection, () =>
                {
                    EnsureRegularCollection(connection, collectionId);
                    var distinctItemIds = (itemIds ??
                                           Array.Empty<string>())
                        .Where(id =>
                            !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    for (var i = 0; i < distinctItemIds.Length; i++)
                    {
                        EnsureItemExists(
                            connection,
                            distinctItemIds[i]);
                    }

                    var changed = false;
                    for (var i = 0; i < distinctItemIds.Length; i++)
                    {
                        changed |= connection.Execute(
                                       "INSERT OR IGNORE INTO item_collection(item_info_id, collection_info_id) VALUES (?, ?)",
                                       distinctItemIds[i],
                                       collectionId) > 0;
                    }

                    return changed;
                });
            }
        }

        private static TagRow EnsureTag(SQLiteConnection connection, string name)
        {
            var existing = connection.Query<TagRow>("SELECT * FROM tag_info WHERE name = ? LIMIT 1", name).FirstOrDefault();
            if (existing != null)
            {
                return existing;
            }

            var now = Now();
            var id = NewId();
            connection.Execute("INSERT INTO tag_info(id, name, created_at, updated_at) VALUES (?, ?, ?, ?)", id, name, now, now);
            return connection.Query<TagRow>("SELECT * FROM tag_info WHERE id = ?", id).First();
        }

        private static void SyncItemTags(SQLiteConnection connection, string itemId, IReadOnlyList<string> tagIds)
        {
            ValidateTagIds(connection, tagIds);

            connection.Execute("DELETE FROM item_tag WHERE item_info_id = ?", itemId);
            for (var i = 0; i < tagIds.Count; i++)
            {
                connection.Execute("INSERT OR IGNORE INTO item_tag(item_info_id, tag_info_id) VALUES (?, ?)", itemId, tagIds[i]);
            }
        }

        private static void SyncItemCollections(SQLiteConnection connection, string itemId, IReadOnlyList<string> collectionIds)
        {
            ValidateRegularCollectionIds(connection, collectionIds);

            connection.Execute("DELETE FROM item_collection WHERE item_info_id = ?", itemId);
            for (var i = 0; i < collectionIds.Count; i++)
            {
                connection.Execute("INSERT OR IGNORE INTO item_collection(item_info_id, collection_info_id) VALUES (?, ?)", itemId, collectionIds[i]);
            }
        }

        private static void ValidateTagIds(SQLiteConnection connection, IReadOnlyList<string> tagIds)
        {
            for (var i = 0; i < tagIds.Count; i++)
            {
                EnsureTagExists(connection, tagIds[i]);
            }
        }

        private static void ValidateRegularCollectionIds(SQLiteConnection connection, IReadOnlyList<string> collectionIds)
        {
            for (var i = 0; i < collectionIds.Count; i++)
            {
                EnsureRegularCollection(connection, collectionIds[i]);
            }
        }

        private static void PlaceCollection(
            SQLiteConnection connection,
            string collectionId,
            string parentCollectionId,
            int siblingIndex)
        {
            PlaceCollections(
                connection,
                new[] { collectionId },
                parentCollectionId,
                siblingIndex);
        }

        private static void PlaceCollections(
            SQLiteConnection connection,
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex)
        {
            var requestedIds = (collectionIds ??
                    Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (requestedIds.Length == 0)
            {
                return;
            }

            for (var i = 0; i < requestedIds.Length; i++)
            {
                EnsureCollectionExists(connection, requestedIds[i]);
            }

            if (!string.IsNullOrWhiteSpace(parentCollectionId))
            {
                EnsureCollectionExists(connection, parentCollectionId);
            }

            var nodes = LoadCollectionPlacementNodes(connection);
            var placement = CollectionPlacementPolicy.Evaluate(
                nodes,
                requestedIds,
                parentCollectionId,
                siblingIndex);
            ThrowIfInvalidPlacement(placement);
            if (!placement.ChangesPlacement)
            {
                return;
            }

            var movingIds = placement.MovingIds;
            var targetParentId =
                placement.TargetParentId.Length == 0
                    ? null
                    : placement.TargetParentId;
            var nodesById = nodes.ToDictionary(
                node => node.Id,
                StringComparer.Ordinal);
            var currentParentIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (var i = 0; i < movingIds.Count; i++)
            {
                currentParentIds.Add(
                    nodesById[movingIds[i]].ParentId);
            }

            for (var i = 0; i < movingIds.Count; i++)
            {
                connection.Execute(
                    "DELETE FROM collection_collection WHERE child_collection_id = ?",
                    movingIds[i]);
                if (targetParentId != null)
                {
                    connection.Execute(
                        "INSERT INTO collection_collection(parent_collection_id, child_collection_id) VALUES (?, ?)",
                        targetParentId,
                        movingIds[i]);
                }
            }

            NormalizeCollectionOrder(
                connection,
                placement.TargetSiblingIds);
            foreach (var currentParentId in currentParentIds)
            {
                var normalizedCurrentParentId =
                    string.IsNullOrWhiteSpace(currentParentId)
                        ? null
                        : currentParentId;
                if (string.Equals(
                        normalizedCurrentParentId,
                        targetParentId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                NormalizeCollectionOrder(
                    connection,
                    GetCollectionSiblingIds(
                        connection,
                        normalizedCurrentParentId,
                        null));
            }

            var now = Now();
            for (var i = 0; i < movingIds.Count; i++)
            {
                connection.Execute(
                    "UPDATE collection_info SET updated_at = ? WHERE id = ?",
                    now,
                    movingIds[i]);
            }
        }

        private static IReadOnlyList<CollectionPlacementNode>
            LoadCollectionPlacementNodes(SQLiteConnection connection)
        {
            var parents = connection
                .Query<CollectionCollectionRow>(
                    "SELECT * FROM collection_collection")
                .ToDictionary(
                    row => row.child_collection_id,
                    row => row.parent_collection_id,
                    StringComparer.Ordinal);
            var smartIds = new HashSet<string>(
                connection.Query<SmartCollectionRow>(
                        "SELECT * FROM smart_collection_info")
                    .Select(row => row.collection_info_id),
                StringComparer.Ordinal);
            return connection.Query<CollectionRow>(
                    "SELECT * FROM collection_info")
                .Select(row =>
                {
                    string parentId;
                    parents.TryGetValue(row.id, out parentId);
                    return new CollectionPlacementNode(
                        row.id,
                        parentId,
                        smartIds.Contains(row.id),
                        row.sort_order);
                })
                .ToArray();
        }

        private static void ThrowIfInvalidPlacement(
            CollectionPlacementResult placement)
        {
            if (placement == null ||
                placement.Error == CollectionPlacementError.EmptySelection)
            {
                return;
            }

            if (placement.Error == CollectionPlacementError.Cycle)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.CollectionCycle,
                    "Collection cycle is not allowed.");
            }

            if (placement.Error ==
                CollectionPlacementError.SmartCollectionParent)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidCollectionHierarchy,
                    "Smart Collection cannot contain child collections.");
            }

            if (!placement.IsValid)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Collection placement is invalid.");
            }
        }

        private static List<string> GetCollectionSiblingIds(
            SQLiteConnection connection,
            string parentCollectionId,
            string excludedCollectionId)
        {
            List<CollectionRow> rows;
            if (string.IsNullOrWhiteSpace(parentCollectionId))
            {
                rows = connection.Query<CollectionRow>(
                    @"SELECT collection_info.*
                      FROM collection_info
                      WHERE NOT EXISTS (
                        SELECT 1
                        FROM collection_collection
                        WHERE child_collection_id = collection_info.id
                      )
                      ORDER BY collection_info.sort_order, collection_info.id");
            }
            else
            {
                rows = connection.Query<CollectionRow>(
                    @"SELECT collection_info.*
                      FROM collection_info
                      INNER JOIN collection_collection
                        ON collection_collection.child_collection_id = collection_info.id
                      WHERE collection_collection.parent_collection_id = ?
                      ORDER BY collection_info.sort_order, collection_info.id",
                    parentCollectionId);
            }

            return rows
                .Where(row =>
                    !string.Equals(
                        row.id,
                        excludedCollectionId,
                        StringComparison.Ordinal))
                .Select(row => row.id)
                .ToList();
        }

        private static void NormalizeCollectionOrder(
            SQLiteConnection connection,
            IReadOnlyList<string> collectionIds)
        {
            for (var i = 0; i < collectionIds.Count; i++)
            {
                connection.Execute(
                    "UPDATE collection_info SET sort_order = ? WHERE id = ?",
                    i,
                    collectionIds[i]);
            }
        }

        private static void InsertSmartCondition(
            SQLiteConnection connection,
            string collectionId,
            int sortOrder,
            SmartCollectionCondition condition)
        {
            if (condition == null)
            {
                return;
            }

            ValidateSmartCondition(condition);
            connection.Execute(
                "INSERT INTO smart_collection_condition(collection_info_id, sort_order, field, operator, query_text) VALUES (?, ?, ?, ?, ?)",
                collectionId,
                sortOrder,
                ToDbSmartField(condition.Field),
                ToDbSmartOperator(condition.Operator),
                condition.QueryText);
        }

        private static void EnsureRegularCollection(SQLiteConnection connection, string collectionId)
        {
            if (string.IsNullOrWhiteSpace(collectionId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Collection id is required.");
            }

            var exists = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM collection_info WHERE id = ?", collectionId) > 0;
            if (!exists)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "Collection was not found.");
            }

            var isSmart = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM smart_collection_info WHERE collection_info_id = ?", collectionId) > 0;
            if (isSmart)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Smart Collection cannot be assigned directly.");
            }
        }

        private static void ValidateSmartCondition(SmartCollectionCondition condition)
        {
            if (condition == null)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidSmartCollectionCondition, "Smart Collection condition is required.");
            }

            if (condition.Operator != SmartCollectionConditionOperator.Exists &&
                string.IsNullOrWhiteSpace(condition.QueryText))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidSmartCollectionCondition, "Smart Collection condition query text is required.");
            }
        }

        private static void EnsureItemExists(SQLiteConnection connection, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Item id is required.");
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM item_info WHERE id = ?", itemId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "Item was not found.");
            }
        }

        private static void EnsureFileExists(SQLiteConnection connection, string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "File id is required.");
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM file_info WHERE id = ?", fileId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "File was not found.");
            }
        }

        private static void EnsureTagExists(SQLiteConnection connection, string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Tag id is required.");
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM tag_info WHERE id = ?", tagId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "Tag was not found.");
            }
        }

        private static void EnsureCollectionExists(SQLiteConnection connection, string collectionId)
        {
            if (string.IsNullOrWhiteSpace(collectionId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Collection id is required.");
            }

            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM collection_info WHERE id = ?", collectionId) == 0)
            {
                throw new AssetManagerException(AssetManagerErrorCode.NotFound, "Collection was not found.");
            }
        }

        private static void EnsureCollectionCanContainChildren(
            SQLiteConnection connection,
            string collectionId)
        {
            var isSmart = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM smart_collection_info WHERE collection_info_id = ?",
                collectionId) > 0;
            if (isSmart)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidCollectionHierarchy,
                    "Smart Collection cannot contain child collections.");
            }
        }

        private static bool MatchesSmartCollection(SQLiteConnection connection, string itemId, SmartCollectionRow smartCollection)
        {
            var conditions = connection.Query<SmartConditionRow>(
                "SELECT * FROM smart_collection_condition WHERE collection_info_id = ? ORDER BY sort_order",
                smartCollection.collection_info_id);
            return MatchesSmartCollection(
                smartCollection.match_mode,
                conditions,
                field => LoadSmartConditionValues(
                    connection,
                    itemId,
                    field));
        }

        private static bool MatchesSmartCollection(
            string matchMode,
            IReadOnlyList<SmartConditionRow> conditions,
            Func<
                SmartCollectionConditionField,
                IReadOnlyList<string>> loadValues)
        {
            if (conditions.Count == 0)
            {
                return matchMode != "any";
            }

            if (matchMode == "any")
            {
                for (var i = 0; i < conditions.Count; i++)
                {
                    if (MatchesSmartCondition(
                            loadValues(
                                FromDbSmartField(
                                    conditions[i].field)),
                            conditions[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                if (!MatchesSmartCondition(
                        loadValues(
                            FromDbSmartField(
                                conditions[i].field)),
                        conditions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesSmartCondition(
            IReadOnlyList<string> values,
            SmartConditionRow condition)
        {
            var op = FromDbSmartOperator(condition.@operator);
            if (op == SmartCollectionConditionOperator.Exists)
            {
                return values.Any(value => !string.IsNullOrWhiteSpace(value));
            }

            var queryValues = SplitQueryValues(condition.query_text);
            if (queryValues.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i] ?? string.Empty;
                for (var j = 0; j < queryValues.Length; j++)
                {
                    if (op == SmartCollectionConditionOperator.Contains &&
                        value.IndexOf(queryValues[j], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    if ((op == SmartCollectionConditionOperator.Equals || op == SmartCollectionConditionOperator.In) &&
                        string.Equals(value, queryValues[j], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IReadOnlyList<string> LoadSmartConditionValues(SQLiteConnection connection, string itemId, SmartCollectionConditionField field)
        {
            if (field == SmartCollectionConditionField.Name)
            {
                return connection.Query<ItemRow>("SELECT name FROM item_info WHERE id = ?", itemId).Select(row => row.name).ToArray();
            }

            if (field == SmartCollectionConditionField.Description)
            {
                return connection.Query<ItemRow>("SELECT description FROM item_info WHERE id = ?", itemId).Select(row => row.description).ToArray();
            }

            if (field == SmartCollectionConditionField.Tag)
            {
                return connection.Query<TagRow>(
                    @"SELECT tag_info.*
                      FROM tag_info
                      INNER JOIN item_tag ON item_tag.tag_info_id = tag_info.id
                      WHERE item_tag.item_info_id = ?",
                    itemId).Select(row => row.name).ToArray();
            }

            if (field == SmartCollectionConditionField.FileName)
            {
                return connection.Query<FileRow>("SELECT file_name FROM file_info WHERE " + FileBelongsToItemWhereClause(), itemId, itemId, itemId).Select(row => row.file_name).ToArray();
            }

            if (field == SmartCollectionConditionField.Extension)
            {
                return connection.Query<FileRow>("SELECT extension FROM file_info WHERE " + FileBelongsToItemWhereClause(), itemId, itemId, itemId).Select(row => row.extension).ToArray();
            }

            return Array.Empty<string>();
        }

        private static string FileBelongsToItemWhereClause()
        {
            return @"(file_info.item_info_id = ?
                      OR file_info.variant_group_id IN (SELECT id FROM variant_group WHERE item_info_id = ?)
                      OR file_info.version_group_id IN (SELECT id FROM version_group WHERE item_info_id = ?))";
        }

        private static string[] SplitQueryValues(string queryText)
        {
            return (queryText ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToArray();
        }
    }
}
