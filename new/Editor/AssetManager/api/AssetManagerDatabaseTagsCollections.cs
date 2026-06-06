using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Api.Connecter.Blm;
using Ee4v.AssetManager.Api.Connecter.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Api
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
                EnsureItemExists(connection, itemId);
                SyncItemTags(connection, itemId, tagIds ?? Array.Empty<string>());
            }
        }

        public static IReadOnlyList<AssetCollection> GetCollections()
        {
            using (var connection = OpenConnection())
            {
                return connection.Query<CollectionRow>("SELECT * FROM collection_info ORDER BY name COLLATE NOCASE, id")
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
                }

                var now = Now();
                var id = NewId();
                connection.Execute("INSERT INTO collection_info(id, name, created_at, updated_at) VALUES (?, ?, ?, ?)", id, request.Name, now, now);
                SetCollectionParent(connection, id, request.ParentCollectionId);
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
                }

                var conditions = request.Conditions ?? Array.Empty<SmartCollectionCondition>();
                for (var i = 0; i < conditions.Count; i++)
                {
                    ValidateSmartCondition(conditions[i]);
                }

                var now = Now();
                var collectionId = NewId();
                connection.Execute("INSERT INTO collection_info(id, name, created_at, updated_at) VALUES (?, ?, ?, ?)", collectionId, request.Name, now, now);
                SetCollectionParent(connection, collectionId, request.ParentCollectionId);
                connection.Execute(
                    "INSERT INTO smart_collection_info(collection_info_id, match_mode, created_at, updated_at) VALUES (?, ?, ?, ?)",
                    collectionId,
                    request.MatchMode == SmartCollectionMatchMode.Any ? "any" : "all",
                    now,
                    now);

                for (var i = 0; i < conditions.Count; i++)
                {
                    InsertSmartCondition(connection, collectionId, conditions[i]);
                }

                return ToAssetCollection(connection, connection.Query<CollectionRow>("SELECT * FROM collection_info WHERE id = ?", collectionId).First());
            }
        }

        public static void MoveCollection(string collectionId, string parentCollectionId)
        {
            using (var connection = OpenConnection())
            {
                EnsureCollectionExists(connection, collectionId);
                SetCollectionParent(connection, collectionId, parentCollectionId);
            }
        }

        public static void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds)
        {
            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                SyncItemCollections(connection, itemId, collectionIds ?? Array.Empty<string>());
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
            var now = Now();
            for (var i = 0; i < tagIds.Count; i++)
            {
                connection.Execute("INSERT OR IGNORE INTO item_tag(item_info_id, tag_info_id, created_at) VALUES (?, ?, ?)", itemId, tagIds[i], now);
            }
        }

        private static void SyncItemCollections(SQLiteConnection connection, string itemId, IReadOnlyList<string> collectionIds)
        {
            ValidateRegularCollectionIds(connection, collectionIds);

            connection.Execute("DELETE FROM item_collection WHERE item_info_id = ?", itemId);
            var now = Now();
            for (var i = 0; i < collectionIds.Count; i++)
            {
                connection.Execute("INSERT OR IGNORE INTO item_collection(item_info_id, collection_info_id, created_at) VALUES (?, ?, ?)", itemId, collectionIds[i], now);
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

        private static void SetCollectionParent(SQLiteConnection connection, string collectionId, string parentCollectionId)
        {
            EnsureCollectionExists(connection, collectionId);
            if (string.IsNullOrWhiteSpace(parentCollectionId))
            {
                connection.Execute("DELETE FROM collection_collection WHERE child_collection_id = ?", collectionId);
                return;
            }

            EnsureCollectionExists(connection, parentCollectionId);
            if (collectionId == parentCollectionId || IsCollectionDescendant(connection, parentCollectionId, collectionId))
            {
                throw new AssetManagerException(AssetManagerErrorCode.CollectionCycle, "Collection cycle is not allowed.");
            }

            connection.Execute("DELETE FROM collection_collection WHERE child_collection_id = ?", collectionId);
            connection.Execute("INSERT INTO collection_collection(parent_collection_id, child_collection_id, created_at) VALUES (?, ?, ?)", parentCollectionId, collectionId, Now());
        }

        private static bool IsCollectionDescendant(SQLiteConnection connection, string candidateChildId, string parentId)
        {
            var children = connection.Query<CollectionCollectionRow>("SELECT * FROM collection_collection WHERE parent_collection_id = ?", parentId);
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].child_collection_id == candidateChildId || IsCollectionDescendant(connection, candidateChildId, children[i].child_collection_id))
                {
                    return true;
                }
            }

            return false;
        }

        private static void InsertSmartCondition(SQLiteConnection connection, string collectionId, SmartCollectionCondition condition)
        {
            if (condition == null)
            {
                return;
            }

            ValidateSmartCondition(condition);
            var now = Now();
            connection.Execute(
                "INSERT INTO smart_collection_condition(id, collection_info_id, field, operator, query_text, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?)",
                NewId(),
                collectionId,
                ToDbSmartField(condition.Field),
                ToDbSmartOperator(condition.Operator),
                condition.QueryText,
                now,
                now);
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

        private static bool MatchesSmartCollection(SQLiteConnection connection, string itemId, SmartCollectionRow smartCollection)
        {
            var conditions = connection.Query<SmartConditionRow>(
                "SELECT * FROM smart_collection_condition WHERE collection_info_id = ? ORDER BY id",
                smartCollection.collection_info_id);
            if (conditions.Count == 0)
            {
                return smartCollection.match_mode != "any";
            }

            if (smartCollection.match_mode == "any")
            {
                for (var i = 0; i < conditions.Count; i++)
                {
                    if (MatchesSmartCondition(connection, itemId, conditions[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                if (!MatchesSmartCondition(connection, itemId, conditions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesSmartCondition(SQLiteConnection connection, string itemId, SmartConditionRow condition)
        {
            var values = LoadSmartConditionValues(connection, itemId, FromDbSmartField(condition.field));
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

            if (field == SmartCollectionConditionField.SourceType)
            {
                var values = new List<string>();
                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM file_info INNER JOIN ee4v_file_origin ON ee4v_file_origin.file_info_id = file_info.id WHERE file_info.item_info_id = ?", itemId) > 0) values.Add("ee4v");
                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM file_info INNER JOIN eagle_file_origin ON eagle_file_origin.file_info_id = file_info.id WHERE file_info.item_info_id = ?", itemId) > 0) values.Add("eagle");
                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM file_info INNER JOIN blm_file_origin ON blm_file_origin.file_info_id = file_info.id WHERE file_info.item_info_id = ?", itemId) > 0) values.Add("blm");
                return values;
            }

            if (field == SmartCollectionConditionField.FileName)
            {
                return connection.Query<FileRow>("SELECT file_name FROM file_info WHERE item_info_id = ?", itemId).Select(row => row.file_name).ToArray();
            }

            if (field == SmartCollectionConditionField.Extension)
            {
                return connection.Query<FileRow>("SELECT extension FROM file_info WHERE item_info_id = ?", itemId).Select(row => row.extension).ToArray();
            }

            return connection.Query<FileRow>("SELECT lifecycle FROM file_info WHERE item_info_id = ?", itemId).Select(row => row.lifecycle).ToArray();
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
