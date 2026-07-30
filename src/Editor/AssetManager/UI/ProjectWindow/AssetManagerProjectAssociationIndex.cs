using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerProjectAssociationIndex
    {
        private AssetManagerProjectAssociationIndex(
            IReadOnlyDictionary<string, IReadOnlyList<string>>
                guidsByItem,
            IReadOnlyDictionary<string, IReadOnlyList<string>>
                guidsByFile,
            IReadOnlyDictionary<string, string>
                itemIdByAssetGuid)
        {
            GuidsByItem = guidsByItem;
            GuidsByFile = guidsByFile;
            ItemIdByAssetGuid = itemIdByAssetGuid;
        }

        internal IReadOnlyDictionary<
            string,
            IReadOnlyList<string>> GuidsByItem { get; }

        internal IReadOnlyDictionary<
            string,
            IReadOnlyList<string>> GuidsByFile { get; }

        internal IReadOnlyDictionary<string, string>
            ItemIdByAssetGuid { get; }

        internal static AssetManagerProjectAssociationIndex Create(
            IReadOnlyList<AssetImportedAssetAssociation>
                associations)
        {
            var source = associations ??
                         Array.Empty<
                             AssetImportedAssetAssociation>();
            var itemGuids =
                new Dictionary<string, List<string>>(
                    StringComparer.Ordinal);
            var fileGuids =
                new Dictionary<string, List<string>>(
                    StringComparer.Ordinal);
            var latestByGuid =
                new Dictionary<
                    string,
                    AssetImportedAssetAssociation>(
                    StringComparer.Ordinal);

            for (var i = 0; i < source.Count; i++)
            {
                var association = source[i];
                if (association == null ||
                    string.IsNullOrWhiteSpace(
                        association.ItemId) ||
                    string.IsNullOrWhiteSpace(
                        association.FileId) ||
                    string.IsNullOrWhiteSpace(
                        association.AssetGuid))
                {
                    continue;
                }

                AddDistinct(
                    itemGuids,
                    association.ItemId,
                    association.AssetGuid);
                AddDistinct(
                    fileGuids,
                    association.FileId,
                    association.AssetGuid);

                AssetImportedAssetAssociation current;
                if (!latestByGuid.TryGetValue(
                        association.AssetGuid,
                        out current) ||
                    current.ImportedAt <= association.ImportedAt)
                {
                    latestByGuid[association.AssetGuid] =
                        association;
                }
            }

            var itemIdByAssetGuid =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);
            foreach (var pair in latestByGuid)
            {
                itemIdByAssetGuid[pair.Key] =
                    pair.Value.ItemId;
            }

            return new AssetManagerProjectAssociationIndex(
                ToReadOnlyLists(itemGuids),
                ToReadOnlyLists(fileGuids),
                itemIdByAssetGuid);
        }

        private static void AddDistinct(
            IDictionary<string, List<string>> destination,
            string key,
            string guid)
        {
            List<string> guids;
            if (!destination.TryGetValue(key, out guids))
            {
                guids = new List<string>();
                destination.Add(key, guids);
            }

            if (!guids.Contains(guid))
            {
                guids.Add(guid);
            }
        }

        private static IReadOnlyDictionary<
            string,
            IReadOnlyList<string>> ToReadOnlyLists(
            IReadOnlyDictionary<string, List<string>> source)
        {
            var result =
                new Dictionary<
                    string,
                    IReadOnlyList<string>>(
                    source.Count,
                    StringComparer.Ordinal);
            foreach (var pair in source)
            {
                result[pair.Key] = pair.Value.ToArray();
            }

            return result;
        }
    }
}
