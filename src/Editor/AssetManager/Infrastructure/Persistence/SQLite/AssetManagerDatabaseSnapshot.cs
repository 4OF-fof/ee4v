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

                AddSnapshotKeywordValues(
                    keywordValuesByItem,
                    connection.Query<SnapshotKeywordValueRow>(
                        @"SELECT item_tag.item_info_id,
                                 tag_info.name AS value
                          FROM item_tag
                          INNER JOIN tag_info
                            ON tag_info.id =
                               item_tag.tag_info_id"));
                AddSnapshotKeywordValues(
                    keywordValuesByItem,
                    connection.Query<SnapshotKeywordValueRow>(
                        @"SELECT COALESCE(
                                     file_info.item_info_id,
                                     variant_group.item_info_id,
                                     version_group.item_info_id)
                                     AS item_info_id,
                                 file_info.file_name AS value
                          FROM file_info
                          LEFT JOIN variant_group
                            ON variant_group.id =
                               file_info.variant_group_id
                          LEFT JOIN version_group
                            ON version_group.id =
                               file_info.version_group_id"));

                var smartCollections =
                    connection.Query<SmartCollectionRow>(
                        "SELECT * FROM smart_collection_info " +
                        "ORDER BY collection_info_id");
                for (var collectionIndex = 0;
                     collectionIndex < smartCollections.Count;
                     collectionIndex++)
                {
                    var smartCollection =
                        smartCollections[collectionIndex];
                    for (var itemIndex = 0;
                         itemIndex < itemRows.Count;
                         itemIndex++)
                    {
                        var itemId = itemRows[itemIndex].id;
                        if (MatchesSmartCollection(
                                connection,
                                itemId,
                                smartCollection))
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

        private static void AddSnapshotKeywordValues(
            IReadOnlyDictionary<
                string,
                List<string>> valuesByItem,
            IReadOnlyList<SnapshotKeywordValueRow> rows)
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
    }
}
