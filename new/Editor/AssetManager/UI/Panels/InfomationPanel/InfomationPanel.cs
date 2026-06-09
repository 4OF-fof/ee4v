using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InfomationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--infomation";
        private const string PreviewClassName = "ee4v-asset-manager-panel__infomation-preview";
        private const float PreviewMaxSize = 360f;
        private const float HorizontalPadding = 24f;
        private readonly ItemImage _previewImage;

        public InfomationPanel()
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);

            _previewImage = new ItemImage();
            _previewImage.AddToClassList(PreviewClassName);
            Add(_previewImage);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            SetSelectedAssetItems(AssetManagerViewState.SelectedAssetItems);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerViewState.SelectedAssetItemsChanged += SetSelectedAssetItems;
            SetSelectedAssetItems(AssetManagerViewState.SelectedAssetItems);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerViewState.SelectedAssetItemsChanged -= SetSelectedAssetItems;
        }

        private void SetSelectedAssetItems(IReadOnlyList<ItemCardState> items)
        {
            if (items == null || items.Count == 0)
            {
                _previewImage.style.display = DisplayStyle.None;
                _previewImage.SetState(new ItemImageState());
                return;
            }

            _previewImage.style.display = DisplayStyle.Flex;
            var item = items != null && items.Count == 1 ? items[0] : null;
            _previewImage.SetState(item != null ? item.ImageState : new ItemImageState());
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdatePreviewSize(evt.newRect.width);
        }

        private void UpdatePreviewSize(float width)
        {
            if (float.IsNaN(width) || width <= 0f)
            {
                return;
            }

            var contentWidth = UnityEngine.Mathf.Max(0f, width - HorizontalPadding);
            _previewImage.SetSize(UnityEngine.Mathf.Min(PreviewMaxSize, contentWidth));
        }
    }
}
