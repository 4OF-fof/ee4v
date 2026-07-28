using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Infrastructure.Datasources.Blm;
using Ee4v.AssetManager.Infrastructure.Datasources.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        private const int CurrentSchemaVersion = 4;

        private const string DatabaseFileName = "asset-manager.db";

        public static AssetSearchResult SearchItems(AssetItemQuery query)
        {
            return SearchItems(query, true);
        }

        public static AssetSearchResult SearchItemSummaries(AssetItemQuery query)
        {
            return SearchItems(query, false);
        }

        private static AssetSearchResult SearchItems(AssetItemQuery query, bool includeDetails)
        {
            using (var connection = OpenConnection())
            {
                var where = new List<string>();
                var parameters = new List<object>();
                SmartCollectionRow smartCollection = null;

                if (query == null || !query.IncludeUnavailable)
                {
                    where.Add("item_info.is_available = 1");
                }

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
                        OR EXISTS (
                            SELECT 1
                            FROM file_info
                            WHERE file_info.file_name LIKE ?
                              AND (
                                file_info.item_info_id = item_info.id
                                OR file_info.variant_group_id IN (SELECT id FROM variant_group WHERE item_info_id = item_info.id)
                                OR file_info.version_group_id IN (SELECT id FROM version_group WHERE item_info_id = item_info.id)
                              )
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

                if (query != null && query.HasBoothInformation)
                {
                    where.Add("EXISTS (SELECT 1 FROM booth_info WHERE booth_info.item_info_id = item_info.id)");
                }

                if (query != null && query.UncategorizedOnly)
                {
                    where.Add("NOT EXISTS (SELECT 1 FROM item_collection WHERE item_collection.item_info_id = item_info.id)");
                }

                if (query != null && query.Lifecycle.HasValue)
                {
                    where.Add(@"EXISTS (
                        SELECT 1
                        FROM file_info
                        WHERE lifecycle = ? AND is_available = 1
                          AND (
                            file_info.item_info_id = item_info.id
                            OR file_info.variant_group_id IN (SELECT id FROM variant_group WHERE item_info_id = item_info.id)
                            OR file_info.version_group_id IN (SELECT id FROM version_group WHERE item_info_id = item_info.id)
                          ))");
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
                var uncategorizedOnly = query != null && query.UncategorizedOnly;
                List<ItemRow> rows;
                int total;

                if (smartCollection == null && !uncategorizedOnly)
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
                    var smartCollections = uncategorizedOnly
                        ? connection.Query<SmartCollectionRow>(
                            "SELECT * FROM smart_collection_info ORDER BY collection_info_id")
                        : new List<SmartCollectionRow>();
                    var matched = candidates
                        .Where(row =>
                            (smartCollection == null || MatchesSmartCollection(connection, row.id, smartCollection)) &&
                            (!uncategorizedOnly || !MatchesAnySmartCollection(connection, row.id, smartCollections)))
                        .ToArray();
                    total = matched.Length;
                    rows = matched.Skip(offset).Take(limit).ToList();
                }

                return new AssetSearchResult
                {
                    Items = includeDetails
                        ? rows.Select(row => ToAssetItem(connection, row)).ToArray()
                        : rows.Select(ToAssetItemSummary).ToArray(),
                    TotalCount = total
                };
            }
        }

        private static bool MatchesAnySmartCollection(
            SQLiteConnection connection,
            string itemId,
            IReadOnlyList<SmartCollectionRow> smartCollections)
        {
            for (var i = 0; i < smartCollections.Count; i++)
            {
                if (MatchesSmartCollection(connection, itemId, smartCollections[i]))
                {
                    return true;
                }
            }

            return false;
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
                var itemId = InTransaction(connection, () =>
                {
                    ValidateTagIds(connection, request.TagIds ?? Array.Empty<string>());
                    ValidateRegularCollectionIds(connection, request.CollectionIds ?? Array.Empty<string>());

                    var now = Now();
                    var id = NewId();
                    connection.Execute(
                        "INSERT INTO item_info(id, name, description, created_at, updated_at) VALUES (?, ?, ?, ?, ?)",
                        id,
                        request.Name,
                        request.Description ?? string.Empty,
                        now,
                        now);

                    SyncItemTags(connection, id, request.TagIds ?? Array.Empty<string>());
                    SyncItemCollections(connection, id, request.CollectionIds ?? Array.Empty<string>());
                    return id;
                });
                return ToAssetItem(connection, connection.Query<ItemRow>("SELECT * FROM item_info WHERE id = ?", itemId).First());
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

        public static IReadOnlyList<AssetSyncInfo> GetSyncInfo()
        {
            using (var connection = OpenConnection())
            {
                return connection.Query<SyncInfoRow>("SELECT * FROM sync_info ORDER BY source_type")
                    .Select(row => new AssetSyncInfo
                    {
                        SourceType = FromDbSourceType(row.source_type),
                        LastSyncAt = ParseNullableDate(row.last_sync_at),
                        LastSyncState = FromDbSyncState(row.last_sync_status)
                    })
                    .ToArray();
            }
        }
    }
}
