using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        internal static AssetCatalogSnapshot LoadCatalogSnapshot()
        {
            using (var connection = OpenConnection())
            {
                var itemRows = connection.Query<ItemRow>(
                    "SELECT * FROM item_info " +
                    "ORDER BY updated_at DESC, id");
                var collectionIds = connection
                    .Query<SnapshotCollectionRow>(
                        "SELECT id FROM collection_info")
                    .Select(row => row.id)
                    .ToArray();
                var boothItemIds = new HashSet<string>(
                    connection.Query<SnapshotItemIdRow>(
                            "SELECT item_info_id FROM booth_info")
                        .Select(row => row.item_info_id),
                    StringComparer.Ordinal);
                var collectionIdsByItem =
                    itemRows.ToDictionary(
                        row => row.id,
                        row => new HashSet<string>(
                            StringComparer.Ordinal),
                        StringComparer.Ordinal);
                var keywordValuesByItem =
                    itemRows.ToDictionary(
                        row => row.id,
                        row => new List<string>
                        {
                            row.name ?? string.Empty,
                            row.description ?? string.Empty
                        },
                        StringComparer.Ordinal);
                var smartValuesByItem =
                    itemRows.ToDictionary(
                        row => row.id,
                        row => CreateSmartValues(row),
                        StringComparer.Ordinal);

                var regularMemberships =
                    connection.Query<SnapshotMembershipRow>(
                        @"SELECT item_info_id,
                                 collection_info_id
                          FROM item_collection");
                for (var i = 0;
                     i < regularMemberships.Count;
                     i++)
                {
                    var membership =
                        regularMemberships[i];
                    HashSet<string> memberships;
                    if (collectionIdsByItem.TryGetValue(
                            membership.item_info_id,
                            out memberships))
                    {
                        memberships.Add(
                            membership.collection_info_id);
                    }
                }

                var tagValues =
                    connection.Query<SnapshotKeywordValueRow>(
                        @"SELECT item_tag.item_info_id,
                                 tag_info.name AS value
                          FROM item_tag
                          INNER JOIN tag_info
                            ON tag_info.id =
                               item_tag.tag_info_id");
                AddSnapshotValues(
                    keywordValuesByItem,
                    smartValuesByItem,
                    tagValues,
                    SmartCollectionConditionField.Tag);
                var fileValues =
                    connection.Query<SnapshotFileValueRow>(
                        @"SELECT COALESCE(
                                     file_info.item_info_id,
                                     variant_group.item_info_id,
                                     version_group.item_info_id)
                                     AS item_info_id,
                                 file_info.file_name,
                                 file_info.extension
                          FROM file_info
                          LEFT JOIN variant_group
                            ON variant_group.id =
                               file_info.variant_group_id
                          LEFT JOIN version_group
                            ON version_group.id =
                               file_info.version_group_id");
                AddSnapshotFileValues(
                    keywordValuesByItem,
                    smartValuesByItem,
                    fileValues);

                var smartCollections =
                    connection.Query<SmartCollectionRow>(
                        "SELECT * FROM smart_collection_info " +
                        "ORDER BY collection_info_id");
                var smartConditionsByCollection =
                    connection.Query<SmartConditionRow>(
                            "SELECT * FROM " +
                            "smart_collection_condition " +
                            "ORDER BY collection_info_id, sort_order")
                        .GroupBy(
                            condition =>
                                condition.collection_info_id,
                            StringComparer.Ordinal)
                        .ToDictionary(
                            group => group.Key,
                            group =>
                                (IReadOnlyList<
                                    SmartConditionRow>)
                                group.ToArray(),
                            StringComparer.Ordinal);
                for (var collectionIndex = 0;
                     collectionIndex < smartCollections.Count;
                     collectionIndex++)
                {
                    var smartCollection =
                        smartCollections[collectionIndex];
                    IReadOnlyList<SmartConditionRow>
                        conditions;
                    if (!smartConditionsByCollection
                            .TryGetValue(
                                smartCollection
                                    .collection_info_id,
                                out conditions))
                    {
                        conditions =
                            Array.Empty<SmartConditionRow>();
                    }

                    for (var itemIndex = 0;
                         itemIndex < itemRows.Count;
                         itemIndex++)
                    {
                        var itemId = itemRows[itemIndex].id;
                        if (MatchesSmartCollection(
                                smartCollection.match_mode,
                                conditions,
                                field => GetSmartValues(
                                    smartValuesByItem[itemId],
                                    field)))
                        {
                            collectionIdsByItem[itemId].Add(
                                smartCollection
                                    .collection_info_id);
                        }
                    }
                }

                var items =
                    new AssetCatalogSnapshotItem[
                        itemRows.Count];
                for (var i = 0; i < itemRows.Count; i++)
                {
                    var row = itemRows[i];
                    var memberships =
                        collectionIdsByItem[row.id]
                            .ToArray();
                    items[i] =
                        new AssetCatalogSnapshotItem(
                            ToAssetItemSummary(row),
                            boothItemIds.Contains(row.id),
                            memberships.Length == 0,
                            memberships,
                            keywordValuesByItem[row.id]);
                }

                return new AssetCatalogSnapshot(
                    items,
                    collectionIds);
            }
        }

        private static Dictionary<
                SmartCollectionConditionField,
                List<string>>
            CreateSmartValues(ItemRow row)
        {
            return new Dictionary<
                SmartCollectionConditionField,
                List<string>>
            {
                {
                    SmartCollectionConditionField.Name,
                    new List<string>
                    {
                        row.name ?? string.Empty
                    }
                },
                {
                    SmartCollectionConditionField.Description,
                    new List<string>
                    {
                        row.description ?? string.Empty
                    }
                },
                {
                    SmartCollectionConditionField.Tag,
                    new List<string>()
                },
                {
                    SmartCollectionConditionField.FileName,
                    new List<string>()
                },
                {
                    SmartCollectionConditionField.Extension,
                    new List<string>()
                }
            };
        }

        private static IReadOnlyList<string> GetSmartValues(
            IReadOnlyDictionary<
                SmartCollectionConditionField,
                List<string>> values,
            SmartCollectionConditionField field)
        {
            List<string> result;
            return values.TryGetValue(field, out result)
                ? result
                : Array.Empty<string>();
        }

        private static void AddSnapshotValues(
            IReadOnlyDictionary<
                string,
                List<string>> valuesByItem,
            IReadOnlyDictionary<
                string,
                Dictionary<
                    SmartCollectionConditionField,
                    List<string>>>
                smartValuesByItem,
            IReadOnlyList<SnapshotKeywordValueRow> rows,
            SmartCollectionConditionField field)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                List<string> values;
                if (valuesByItem.TryGetValue(
                        rows[i].item_info_id,
                        out values))
                {
                    values.Add(rows[i].value ??
                               string.Empty);
                }

                Dictionary<
                    SmartCollectionConditionField,
                    List<string>> smartValues;
                if (smartValuesByItem.TryGetValue(
                        rows[i].item_info_id,
                        out smartValues))
                {
                    smartValues[field].Add(
                        rows[i].value ?? string.Empty);
                }
            }
        }

        private static void AddSnapshotFileValues(
            IReadOnlyDictionary<
                string,
                List<string>> keywordValuesByItem,
            IReadOnlyDictionary<
                string,
                Dictionary<
                    SmartCollectionConditionField,
                    List<string>>>
                smartValuesByItem,
            IReadOnlyList<SnapshotFileValueRow> rows)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                List<string> keywordValues;
                if (keywordValuesByItem.TryGetValue(
                        rows[i].item_info_id,
                        out keywordValues))
                {
                    keywordValues.Add(
                        rows[i].file_name ??
                        string.Empty);
                }

                Dictionary<
                    SmartCollectionConditionField,
                    List<string>> smartValues;
                if (!smartValuesByItem.TryGetValue(
                        rows[i].item_info_id,
                        out smartValues))
                {
                    continue;
                }

                smartValues[
                    SmartCollectionConditionField
                        .FileName].Add(
                    rows[i].file_name ?? string.Empty);
                smartValues[
                    SmartCollectionConditionField
                        .Extension].Add(
                    rows[i].extension ?? string.Empty);
            }
        }

        private sealed class SnapshotItemIdRow
        {
            public string item_info_id { get; set; }
        }

        private sealed class SnapshotCollectionRow
        {
            public string id { get; set; }
        }

        private sealed class SnapshotMembershipRow
        {
            public string item_info_id { get; set; }

            public string collection_info_id { get; set; }
        }

        private sealed class SnapshotKeywordValueRow
        {
            public string item_info_id { get; set; }

            public string value { get; set; }
        }

        private sealed class SnapshotFileValueRow
        {
            public string item_info_id { get; set; }

            public string file_name { get; set; }

            public string extension { get; set; }
        }
    }
}
