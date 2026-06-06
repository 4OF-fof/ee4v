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
        private const int CurrentSchemaVersion = 1;

        private const string DatabaseFileName = "asset-manager.db";

        public static AssetSearchResult SearchItems(AssetItemQuery query)
        {
            using (var connection = OpenConnection())
            {
                var where = new List<string>();
                var parameters = new List<object>();
                SmartCollectionRow smartCollection = null;

                if (query != null && !string.IsNullOrWhiteSpace(query.Keyword))
                {
                    where.Add(@"(
                        item_info.name LIKE ?
                        OR item_info.description LIKE ?
                        OR item_info.id IN (
                            SELECT item_tag.item_info_id
                            FROM item_tag
                            INNER JOIN tag_info ON tag_info.id = item_tag.tag_info_id
                            WHERE tag_info.name LIKE ?
                        )
                        OR item_info.id IN (
                            SELECT file_info.item_info_id
                            FROM file_info
                            WHERE file_info.file_name LIKE ?
                        )
                    )");
                    var keyword = "%" + query.Keyword + "%";
                    parameters.Add(keyword);
                    parameters.Add(keyword);
                    parameters.Add(keyword);
                    parameters.Add(keyword);
                }

                if (query != null && query.CollectionId != null)
                {
                    EnsureCollectionExists(connection, query.CollectionId);
                    smartCollection = connection.Query<SmartCollectionRow>(
                        "SELECT * FROM smart_collection_info WHERE collection_info_id = ? LIMIT 1",
                        query.CollectionId).FirstOrDefault();
                    if (smartCollection == null)
                    {
                        where.Add("item_info.id IN (SELECT item_info_id FROM item_collection WHERE collection_info_id = ?)");
                        parameters.Add(query.CollectionId);
                    }
                }

                if (query != null && query.Lifecycle.HasValue)
                {
                    where.Add("item_info.id IN (SELECT item_info_id FROM file_info WHERE lifecycle = ?)");
                    parameters.Add(ToDbLifecycle(query.Lifecycle.Value));
                }

                if (query != null && query.TagIds != null && query.TagIds.Count > 0)
                {
                    for (var i = 0; i < query.TagIds.Count; i++)
                    {
                        where.Add("item_info.id IN (SELECT item_info_id FROM item_tag WHERE tag_info_id = ?)");
                        parameters.Add(query.TagIds[i]);
                    }
                }

                if (query != null && query.SourceTypes != null && query.SourceTypes.Count > 0)
                {
                    var sourceClauses = new List<string>();
                    for (var i = 0; i < query.SourceTypes.Count; i++)
                    {
                        sourceClauses.Add(ItemHasSourceClause(query.SourceTypes[i]));
                    }

                    where.Add("(" + string.Join(" OR ", sourceClauses.ToArray()) + ")");
                }

                var limit = query != null && query.Limit > 0 ? query.Limit : 100;
                var offset = query != null && query.Offset > 0 ? query.Offset : 0;
                var whereSql = where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where.ToArray());
                List<ItemRow> rows;
                int total;

                if (smartCollection == null)
                {
                    total = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM item_info" + whereSql, parameters.ToArray());
                    parameters.Add(limit);
                    parameters.Add(offset);
                    rows = connection.Query<ItemRow>(
                        "SELECT * FROM item_info" + whereSql + " ORDER BY updated_at DESC, id LIMIT ? OFFSET ?",
                        parameters.ToArray());
                }
                else
                {
                    var candidates = connection.Query<ItemRow>(
                        "SELECT * FROM item_info" + whereSql + " ORDER BY updated_at DESC, id",
                        parameters.ToArray());
                    var matched = candidates
                        .Where(row => MatchesSmartCollection(connection, row.id, smartCollection))
                        .ToArray();
                    total = matched.Length;
                    rows = matched.Skip(offset).Take(limit).ToList();
                }

                return new AssetSearchResult
                {
                    Items = rows.Select(row => ToAssetItem(connection, row)).ToArray(),
                    TotalCount = total
                };
            }
        }

        public static AssetItem GetItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            using (var connection = OpenConnection())
            {
                var row = connection.Query<ItemRow>("SELECT * FROM item_info WHERE id = ? LIMIT 1", itemId).FirstOrDefault();
                return row == null ? null : ToAssetItem(connection, row);
            }
        }

        public static AssetItem CreateItem(CreateAssetItemRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Item name is required.");
            }

            using (var connection = OpenConnection())
            {
                ValidateTagIds(connection, request.TagIds ?? Array.Empty<string>());
                ValidateRegularCollectionIds(connection, request.CollectionIds ?? Array.Empty<string>());

                var now = Now();
                var itemId = NewId();
                connection.Execute(
                    "INSERT INTO item_info(id, name, description, created_at, updated_at) VALUES (?, ?, ?, ?, ?)",
                    itemId,
                    request.Name,
                    request.Description ?? string.Empty,
                    now,
                    now);

                SyncItemTags(connection, itemId, request.TagIds ?? Array.Empty<string>());
                SyncItemCollections(connection, itemId, request.CollectionIds ?? Array.Empty<string>());
                return GetItem(itemId);
            }
        }

        public static AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request)
        {
            if (string.IsNullOrWhiteSpace(itemId) || request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Item id and name are required.");
            }

            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                connection.Execute(
                    "UPDATE item_info SET name = ?, description = ?, updated_at = ? WHERE id = ?",
                    request.Name,
                    request.Description ?? string.Empty,
                    Now(),
                    itemId);
            }

            return GetItem(itemId);
        }
    }
}
