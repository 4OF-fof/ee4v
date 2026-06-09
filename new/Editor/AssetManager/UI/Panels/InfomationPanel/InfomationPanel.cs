using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InfomationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--infomation";
        private const string PreviewClassName = "ee4v-asset-manager-panel__infomation-preview";
        private const string MultiPreviewTextRowClassName = "ee4v-asset-manager-panel__infomation-preview-text";
        private const string NameInputClassName = "ee4v-asset-manager-panel__infomation-name-input";
        private const string NameInputPlaceholder = "名前";
        private const float PreviewMaxSize = 360f;
        private const float HorizontalPadding = 24f;
        private readonly VisualElement _preview;
        private readonly ImageStack _imageStack;
        private readonly VisualElement _multiPreviewTextRow;
        private readonly UiTextElement _multiPreviewCountText;
        private readonly UiTextElement _multiPreviewSuffixText;
        private readonly InputField _nameInput;
        private float _previewSize;

        public InfomationPanel()
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);

            _preview = new VisualElement();
            _preview.AddToClassList(PreviewClassName);
            _imageStack = new ImageStack();
            _preview.Add(_imageStack);

            _multiPreviewTextRow = new VisualElement();
            _multiPreviewTextRow.AddToClassList(MultiPreviewTextRowClassName);
            _multiPreviewCountText = UiTextFactory.Create(string.Empty, UiClassNames.InfomationPanelSelectionCount);
            _multiPreviewCountText.SetWhiteSpace(WhiteSpace.NoWrap);
            _multiPreviewSuffixText = UiTextFactory.Create(string.Empty, UiClassNames.InfomationPanelSelectionCountSuffix);
            _multiPreviewSuffixText.SetWhiteSpace(WhiteSpace.NoWrap);
            _multiPreviewTextRow.Add(_multiPreviewCountText);
            _multiPreviewTextRow.Add(_multiPreviewSuffixText);
            _preview.Add(_multiPreviewTextRow);
            Add(_preview);

            _nameInput = new InputField(new InputFieldState(string.Empty, false, 0f, NameInputPlaceholder));
            _nameInput.AddToClassList(NameInputClassName);
            Add(_nameInput);

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
                _preview.style.display = DisplayStyle.None;
                _nameInput.style.display = DisplayStyle.None;
                ClearPreview();
                return;
            }

            if (items.Count == 1)
            {
                _preview.style.display = DisplayStyle.Flex;
                _nameInput.style.display = DisplayStyle.Flex;
                SetPreview(items, false);
                return;
            }

            _preview.style.display = DisplayStyle.Flex;
            _nameInput.style.display = DisplayStyle.Flex;
            SetPreview(items, true);
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
            UpdatePreviewSize();
            UpdateNameInputSize();
        }

        private void SetPreview(IReadOnlyList<ItemCardState> items, bool showCount)
        {
            _multiPreviewTextRow.style.display = showCount ? DisplayStyle.Flex : DisplayStyle.None;
            _multiPreviewCountText.SetText(showCount ? items.Count.ToString() : string.Empty);
            _multiPreviewSuffixText.SetText(showCount ? "件のアイテムを選択中" : string.Empty);

            var imageStates = new List<ItemImageState>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                imageStates.Add(items[i].ImageState);
            }

            _imageStack.SetStates(imageStates);
            UpdatePreviewSize();
        }

        private void ClearPreview()
        {
            _imageStack.Clear();
            _multiPreviewCountText.SetText(string.Empty);
            _multiPreviewSuffixText.SetText(string.Empty);
            _multiPreviewTextRow.style.display = DisplayStyle.None;
        }

        private void UpdatePreviewSize()
        {
            _imageStack.SetSize(GetImageStackSize());
        }

        private float GetImageStackSize()
        {
            return UnityEngine.Mathf.Max(48f, _previewSize);
        }

        private void UpdateNameInputSize()
        {
            var width = GetImageStackSize();
            _nameInput.style.width = width;
            _nameInput.style.minWidth = width;
            _nameInput.style.maxWidth = width;
        }
    }
}
