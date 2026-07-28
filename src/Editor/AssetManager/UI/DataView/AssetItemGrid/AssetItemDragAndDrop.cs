using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetItemDragAndDrop
    {
        private const string PayloadKey =
            "Ee4v.AssetManager.AssetItemIds";

        internal static bool Start(
            IReadOnlyList<ItemCardState> items)
        {
            var itemIds = GetAssetItemIds(items);
            if (itemIds.Count == 0)
            {
                return false;
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(
                PayloadKey,
                itemIds.ToArray());
            DragAndDrop.StartDrag(
                I18N.Get(
                    "assetManager.mainView.dragItems"));
            return true;
        }

        internal static bool TryGetItemIds(
            out IReadOnlyList<string> itemIds)
        {
            var payload =
                DragAndDrop.GetGenericData(PayloadKey);
            var values = payload as string[];
            if (values == null)
            {
                itemIds = Array.Empty<string>();
                return false;
            }

            itemIds = values
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return itemIds.Count > 0;
        }

        internal static IReadOnlyList<string> GetAssetItemIds(
            IReadOnlyList<ItemCardState> items)
        {
            if (items == null)
            {
                return Array.Empty<string>();
            }

            return items
                .Where(item => item != null)
                .Where(item =>
                {
                    AssetItemGridNodeKind kind;
                    string id;
                    return !AssetItemGridNodeKey.TryDecode(
                               item.ItemId,
                               out kind,
                               out id) ||
                           kind !=
                           AssetItemGridNodeKind.Collection;
                })
                .Select(item =>
                    !string.IsNullOrWhiteSpace(
                        item.ParentItemId)
                        ? item.ParentItemId
                        : item.ItemId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
