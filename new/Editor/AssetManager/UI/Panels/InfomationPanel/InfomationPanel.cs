using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class InfomationPanel : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-panel--infomation";
        private const string PreviewClassName = "ee4v-asset-manager-panel__infomation-preview";
        private const string SelectionModeRowClassName = "ee4v-asset-manager-panel__infomation-selection-mode";
        private const string MultiPreviewTextRowClassName = "ee4v-asset-manager-panel__infomation-preview-text";
        private const string AssetInfoTabId = "asset-info";
        private const string FileTreeTabId = "file-tree";
        private const float PreviewMaxSize = 360f;
        private const float SelectionModeBaseHeight = 24f;
        private const float SelectionModeBaseMarginTop = 4f;
        private const float SelectionModeScaleStartSize = 260f;
        private const float SelectionModeScaleEndSize = 360f;
        private const float SelectionModeMaxScale = 1.28f;
        private const float HorizontalPadding = 24f;
        private readonly VisualElement _preview;
        private readonly ImageStack _imageStack;
        private readonly VisualElement _selectionModeRow;
        private readonly VisualElement _multiPreviewTextRow;
        private readonly UiTextElement _multiPreviewCountText;
        private readonly UiTextElement _multiPreviewSuffixText;
        private readonly ViewToggleTabs _selectionDetailTabs;
        private float _previewSize;

        public InfomationPanel()
        {
            AddToClassList("ee4v-asset-manager-panel");
            AddToClassList(RootClassName);

            _preview = new VisualElement();
            _preview.AddToClassList(PreviewClassName);
            _imageStack = new ImageStack();
            _preview.Add(_imageStack);

            _selectionModeRow = new VisualElement();
            _selectionModeRow.AddToClassList(SelectionModeRowClassName);

            _multiPreviewTextRow = new VisualElement();
            _multiPreviewTextRow.AddToClassList(MultiPreviewTextRowClassName);
            _multiPreviewCountText = UiTextFactory.Create(string.Empty, UiClassNames.InfomationPanelSelectionCount);
            _multiPreviewCountText.SetWhiteSpace(WhiteSpace.NoWrap);
            _multiPreviewSuffixText = UiTextFactory.Create(string.Empty, UiClassNames.InfomationPanelSelectionCountSuffix);
            _multiPreviewSuffixText.SetWhiteSpace(WhiteSpace.NoWrap);
            _multiPreviewTextRow.Add(_multiPreviewCountText);
            _multiPreviewTextRow.Add(_multiPreviewSuffixText);
            _selectionModeRow.Add(_multiPreviewTextRow);
            _selectionDetailTabs = new ViewToggleTabs(CreateDetailTabsState(AssetManagerViewState.SelectedAssetDetailTabId));
            _selectionDetailTabs.SelectionChanged += tabId => AssetManagerViewState.SetSelectedAssetDetailTab(tabId);
            _selectionModeRow.Add(_selectionDetailTabs);
            _preview.Add(_selectionModeRow);
            Add(_preview);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            SetSelectedAssetItems(AssetManagerViewState.SelectedAssetItems);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssetManagerViewState.SelectedAssetItemsChanged += SetSelectedAssetItems;
            AssetManagerViewState.SelectedAssetDetailTabChanged += SetSelectedAssetDetailTab;
            SetSelectedAssetDetailTab(AssetManagerViewState.SelectedAssetDetailTabId);
            SetSelectedAssetItems(AssetManagerViewState.SelectedAssetItems);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssetManagerViewState.SelectedAssetItemsChanged -= SetSelectedAssetItems;
            AssetManagerViewState.SelectedAssetDetailTabChanged -= SetSelectedAssetDetailTab;
        }

        private void SetSelectedAssetItems(IReadOnlyList<ItemCardState> items)
        {
            if (items == null || items.Count == 0)
            {
                _preview.style.display = DisplayStyle.None;
                ClearPreview();
                return;
            }

            if (items.Count == 1)
            {
                _preview.style.display = DisplayStyle.Flex;
                SetPreview(items, false);
                _selectionModeRow.style.display = DisplayStyle.Flex;
                _multiPreviewTextRow.style.display = DisplayStyle.None;
                _selectionDetailTabs.style.display = DisplayStyle.Flex;
                return;
            }

            _preview.style.display = DisplayStyle.Flex;
            SetPreview(items, true);
            _selectionModeRow.style.display = DisplayStyle.Flex;
            _selectionDetailTabs.style.display = DisplayStyle.None;
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
        }

        private void SetPreview(IReadOnlyList<ItemCardState> items, bool showCount)
        {
            _multiPreviewTextRow.style.display = showCount ? DisplayStyle.Flex : DisplayStyle.None;
            _multiPreviewCountText.SetText(showCount ? items.Count.ToString() : string.Empty);
            _multiPreviewSuffixText.SetText(showCount ? I18N.Get("assetManager.infomationPanel.selectedItemsSuffix") : string.Empty);

            var firstPreviewIndex = UnityEngine.Mathf.Max(0, items.Count - 3);
            var imageStates = new List<ItemImageState>(items.Count - firstPreviewIndex);
            for (var i = firstPreviewIndex; i < items.Count; i++)
            {
                imageStates.Add(items[i].ImageState);
            }

            _imageStack.SetStates(imageStates);
            UpdatePreviewSize();
        }

        private static ViewToggleTabsState CreateDetailTabsState(string selectedTabId)
        {
            return new ViewToggleTabsState(
                new[]
                {
                    new ViewToggleTabState(AssetInfoTabId, I18N.Get("assetManager.infomationPanel.detailTabs.assetInfo")),
                    new ViewToggleTabState(FileTreeTabId, I18N.Get("assetManager.infomationPanel.detailTabs.fileTree"))
                },
                selectedTabId);
        }

        private void SetSelectedAssetDetailTab(string tabId)
        {
            _selectionDetailTabs.SetSelectedTab(tabId, notify: false);
        }

        private void ClearPreview()
        {
            _imageStack.Clear();
            _multiPreviewCountText.SetText(string.Empty);
            _multiPreviewSuffixText.SetText(string.Empty);
            _selectionModeRow.style.display = DisplayStyle.None;
            _multiPreviewTextRow.style.display = DisplayStyle.None;
            _selectionDetailTabs.style.display = DisplayStyle.None;
        }

        private void UpdatePreviewSize()
        {
            _imageStack.SetSize(GetImageStackSize());
            UpdateSelectionModeScale();
        }

        private float GetImageStackSize()
        {
            return UnityEngine.Mathf.Max(48f, _previewSize);
        }

        private void UpdateSelectionModeScale()
        {
            var scale = GetSelectionModeScale();
            _selectionModeRow.transform.scale = new Vector3(scale, scale, 1f);
            _selectionModeRow.style.minHeight = SelectionModeBaseHeight * scale;
            _selectionModeRow.style.marginTop = SelectionModeBaseMarginTop * scale;
        }

        private float GetSelectionModeScale()
        {
            if (_previewSize <= SelectionModeScaleStartSize)
            {
                return 1f;
            }

            var progress = Mathf.InverseLerp(SelectionModeScaleStartSize, SelectionModeScaleEndSize, _previewSize);
            return Mathf.Lerp(1f, SelectionModeMaxScale, progress);
        }
    }
}
