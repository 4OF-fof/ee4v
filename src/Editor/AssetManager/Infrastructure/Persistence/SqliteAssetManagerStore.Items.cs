using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence
{
    internal sealed partial class SqliteAssetManagerStore
    {
        // The tag query binds each ID twice. Stay below SQLite's 999 parameter limit.
        private const int ItemReadBatchSize = 400;

        private static IReadOnlyList<AssetItem> ReadItems(
            SQLiteConnection connection, IReadOnlyList<ItemRow> rows)
        {
            var items = rows.Select(MapItem).ToArray();
            for (var offset = 0; offset < items.Length; offset += ItemReadBatchSize)
            {
                var batch = items.Skip(offset).Take(ItemReadBatchSize).ToArray();
                var parameters = batch.Select(item => (object)item.Id).ToArray();
                var placeholders = string.Join(",", parameters.Select(_ => "?"));
                var booth = connection.Query<BoothMetadataRow>(
                    @"SELECT item_id AS ItemId, item_url AS ItemUrl,
                             shop_name AS ShopName, shop_url AS ShopUrl
                      FROM item_booth_metadata WHERE item_id IN (" + placeholders + ")",
                    parameters);
                var files = connection.Query<FileRow>(
                    FileSelect + " WHERE item_id IN (" + placeholders +
                    ") ORDER BY file_name, id", parameters)
                    .Select(MapFile).ToLookup(file => file.ItemId, StringComparer.Ordinal);
                var tags = connection.Query<TagRow>(
                    @"SELECT links.item_id AS ItemId, tag.id AS Id, tag.path AS Path,
                             MAX(links.source_owned) AS IsSourceOwned
                      FROM (
                        SELECT item_id, tag_id, 0 AS source_owned FROM item_tag
                        WHERE item_id IN (" + placeholders + @")
                        UNION ALL
                        SELECT item_id, tag_id, 1 AS source_owned FROM item_source_tag
                        WHERE item_id IN (" + placeholders + @")
                      ) links JOIN tag ON tag.id = links.tag_id
                      GROUP BY links.item_id, tag.id, tag.path ORDER BY tag.path",
                    parameters.Concat(parameters).ToArray())
                    .ToLookup(tag => tag.ItemId, StringComparer.Ordinal);
                var boothByItem = booth.ToDictionary(row => row.ItemId, StringComparer.Ordinal);
                foreach (var item in batch)
                {
                    if (boothByItem.TryGetValue(item.Id, out var metadata))
                    {
                        item.Booth = new AssetBoothMetadata
                        {
                            ItemUrl = metadata.ItemUrl, ShopName = metadata.ShopName,
                            ShopUrl = metadata.ShopUrl
                        };
                    }
                    item.Files = files[item.Id].ToArray();
                    item.Tags = tags[item.Id].Select(tag => new AssetTag
                    {
                        Id = tag.Id, Path = tag.Path, IsSourceOwned = tag.IsSourceOwned != 0
                    }).ToArray();
                }
            }
            return items;
        }
    }
}
