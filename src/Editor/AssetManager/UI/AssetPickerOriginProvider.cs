using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.EditorIntegration;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetPickerOriginProvider
    {
        internal static IReadOnlyDictionary<string, AssetPickerOrigin> Snapshot(IAssetManager manager)
        {
            var names = new Dictionary<string, AssetPickerOrigin>(StringComparer.Ordinal);
            var result = new Dictionary<string, AssetPickerOrigin>(StringComparer.Ordinal);
            // A GUID may have been imported from several files. Latest import owns its display.
            foreach (var association in manager.GetImportedAssetAssociations()
                         .OrderByDescending(value => value.ImportedAt)
                         .ThenBy(value => value.ItemId, StringComparer.Ordinal)
                         .ThenBy(value => value.FileId, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(association.AssetGuid) || string.IsNullOrEmpty(association.ItemId) ||
                    result.ContainsKey(association.AssetGuid)) continue;
                if (!names.TryGetValue(association.ItemId, out var origin))
                {
                    var item = manager.GetItem(association.ItemId);
                    if (item == null) continue;
                    names.Add(association.ItemId, origin = new AssetPickerOrigin(item.Id, item.Name,
                        async cancellationToken =>
                        {
                            var thumbnail = await manager.GetThumbnail(item.Id, cancellationToken);
                            return thumbnail != null && thumbnail.Found ? thumbnail.Data : null;
                        }));
                }
                result.Add(association.AssetGuid, origin);
            }
            return result;
        }
    }
}
