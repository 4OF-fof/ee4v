using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetItemGrid : SelectableItemGrid
    {
        public AssetItemGrid()
        {
            ItemsDragStarted += items =>
                AssetItemDragAndDrop.Start(items);
        }

        public event Action<VisualElement, ItemCardState, Vector2>
            ItemContextClicked;

        public void SetLoading()
        {
            SetState(new ItemGridState(null));
        }

        public void SetAssetItems(AssetItemGridList itemList, out string statusText)
        {
            var gridState = CreateGridState(itemList, out statusText);
            SetState(gridState);
        }

        protected override ItemCard CreateItemCard()
        {
            return new ItemCard();
        }

        protected override void OnCreateSlot(VisualElement slot)
        {
            base.OnCreateSlot(slot);
            slot.RegisterCallback<PointerUpEvent>(
                OnSlotPointerUp);
        }

        private void OnSlotPointerUp(PointerUpEvent evt)
        {
            if (evt.button != (int)MouseButton.RightMouse)
            {
                return;
            }

            var slot = evt.currentTarget as VisualElement;
            var itemIndex =
                slot != null && slot.userData is int
                    ? (int)slot.userData
                    : -1;
            if (itemIndex < 0 || itemIndex >= Items.Count)
            {
                return;
            }

            var panelPosition = slot.LocalToWorld(
                evt.localPosition);
            evt.StopPropagation();
            ItemContextClicked?.Invoke(
                slot,
                Items[itemIndex],
                panelPosition);
        }

        private static ItemGridState CreateGridState(AssetItemGridList itemList, out string statusText)
        {
            var list = itemList ?? new AssetItemGridList(null);
            var itemCardStates = new List<ItemCardState>(list.Items.Count);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var item = list.Items[i];
                if (item == null)
                {
                    continue;
                }

                itemCardStates.Add(new ItemCardState(
                    item.ItemId,
                    item.ItemName,
                    item.ImageState,
                    item.IconState,
                    item.ParentItemId,
                    item.StackStates,
                    item.NameIconState));
            }

            statusText = itemCardStates.Count == 0 ? list.EmptyText : string.Empty;
            return new ItemGridState(itemCardStates);
        }
    }
}
