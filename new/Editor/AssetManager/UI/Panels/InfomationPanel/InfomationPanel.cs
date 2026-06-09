using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InfomationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--infomation";
        private const string PreviewClassName = "ee4v-asset-manager-panel__infomation-preview";
        private const string SinglePreviewClassName = "ee4v-asset-manager-panel__infomation-preview--single";
        private const string MultiPreviewClassName = "ee4v-asset-manager-panel__infomation-preview--multi";
        private const string MultiPreviewTextRowClassName = "ee4v-asset-manager-panel__infomation-preview-text";
        private const float PreviewMaxSize = 360f;
        private const float HorizontalPadding = 24f;
        private const float MultiPreviewTextHeight = 32f;
        private const float MultiPreviewTextGap = 12f;
        private readonly VisualElement _singlePreview;
        private readonly ItemImage _previewImage;
        private readonly VisualElement _multiPreview;
        private readonly ImageStack _imageStack;
        private readonly VisualElement _multiPreviewTextRow;
        private readonly UiTextElement _multiPreviewCountText;
        private readonly UiTextElement _multiPreviewSuffixText;
        private float _previewSize;

        public InfomationPanel()
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);

            _singlePreview = new VisualElement();
            _singlePreview.AddToClassList(PreviewClassName);
            _singlePreview.AddToClassList(SinglePreviewClassName);
            _previewImage = new ItemImage();
            _singlePreview.Add(_previewImage);
            Add(_singlePreview);

            _multiPreview = new VisualElement();
            _multiPreview.AddToClassList(PreviewClassName);
            _multiPreview.AddToClassList(MultiPreviewClassName);
            _imageStack = new ImageStack();
            _multiPreview.Add(_imageStack);

            _multiPreviewTextRow = new VisualElement();
            _multiPreviewTextRow.AddToClassList(MultiPreviewTextRowClassName);
            _multiPreviewCountText = UiTextFactory.Create(string.Empty, UiClassNames.InfomationPanelSelectionCount);
            _multiPreviewCountText.SetWhiteSpace(WhiteSpace.NoWrap);
            _multiPreviewSuffixText = UiTextFactory.Create(string.Empty, UiClassNames.InfomationPanelSelectionCountSuffix);
            _multiPreviewSuffixText.SetWhiteSpace(WhiteSpace.NoWrap);
            _multiPreviewTextRow.Add(_multiPreviewCountText);
            _multiPreviewTextRow.Add(_multiPreviewSuffixText);
            _multiPreview.Add(_multiPreviewTextRow);
            Add(_multiPreview);

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
                _singlePreview.style.display = DisplayStyle.None;
                _multiPreview.style.display = DisplayStyle.None;
                _previewImage.SetState(new ItemImageState());
                ClearMultiPreview();
                return;
            }

            if (items.Count == 1)
            {
                _singlePreview.style.display = DisplayStyle.Flex;
                _multiPreview.style.display = DisplayStyle.None;
                _previewImage.SetState(items[0].ImageState);
                ClearMultiPreview();
                return;
            }

            _singlePreview.style.display = DisplayStyle.None;
            _previewImage.SetState(new ItemImageState());
            SetMultiPreview(items);
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
            _previewSize = UnityEngine.Mathf.Min(PreviewMaxSize, contentWidth);
            SetPreviewRootSize(_singlePreview, _previewSize);
            _previewImage.SetSize(_previewSize);
            SetPreviewRootSize(_multiPreview, _previewSize);
            UpdateMultiPreviewSizes();
        }

        private void SetMultiPreview(IReadOnlyList<ItemCardState> items)
        {
            _multiPreview.style.display = DisplayStyle.Flex;
            _multiPreviewCountText.SetText(items.Count.ToString());
            _multiPreviewSuffixText.SetText("件のアイテムを選択中");

            var imageStates = new List<ItemImageState>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                imageStates.Add(items[i].ImageState);
            }

            _imageStack.SetStates(imageStates);
            UpdateMultiPreviewSizes();
        }

        private void ClearMultiPreview()
        {
            _imageStack.Clear();
            _multiPreviewCountText.SetText(string.Empty);
            _multiPreviewSuffixText.SetText(string.Empty);
        }

        private void UpdateMultiPreviewSizes()
        {
            _imageStack.SetSize(GetImageStackSize());
            _multiPreviewTextRow.style.height = MultiPreviewTextHeight;
        }

        private void SetPreviewRootSize(VisualElement preview, float size)
        {
            preview.style.width = size;
            preview.style.height = size;
            preview.style.minWidth = size;
            preview.style.minHeight = size;
            preview.style.maxWidth = size;
            preview.style.maxHeight = size;
        }

        private float GetImageStackSize()
        {
            return UnityEngine.Mathf.Max(48f, _previewSize - MultiPreviewTextHeight - MultiPreviewTextGap);
        }
    }
}
