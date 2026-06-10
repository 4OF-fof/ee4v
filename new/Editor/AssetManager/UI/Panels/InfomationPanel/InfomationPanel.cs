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
        private const string DetailContentClassName = "ee4v-asset-manager-panel__infomation-detail-content";
        private const string SelectionModeRowClassName = "ee4v-asset-manager-panel__infomation-selection-mode";
        private const string MultiPreviewTextRowClassName = "ee4v-asset-manager-panel__infomation-preview-text";
        private const string AssetInfoTabId = "asset-info";
        private const string FileTreeTabId = "file-tree";
        private const float PreviewMaxSize = 360f;
        private const float SelectionModeBaseHeight = 24f;
        private const float SelectionModeBaseMarginTop = 8f;
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
        private readonly VisualElement _detailContent;
        private readonly SearchableFileTree _fileTree;
        private float _previewSize;
        private IReadOnlyList<ItemCardState> _selectedItems;

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

            _detailContent = new VisualElement();
            _detailContent.AddToClassList(DetailContentClassName);
            _fileTree = new SearchableFileTree();
            _detailContent.Add(_fileTree);
            Add(_detailContent);

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
            _selectedItems = items;
            if (items == null || items.Count == 0)
            {
                _preview.style.display = DisplayStyle.None;
                ClearPreview();
                UpdateDetailContent();
                return;
            }

            if (items.Count == 1)
            {
                _preview.style.display = DisplayStyle.Flex;
                SetPreview(items, false);
                _selectionModeRow.style.display = DisplayStyle.Flex;
                _multiPreviewTextRow.style.display = DisplayStyle.None;
                _selectionDetailTabs.style.display = DisplayStyle.Flex;
                UpdateDetailContent();
                return;
            }

            _preview.style.display = DisplayStyle.Flex;
            SetPreview(items, true);
            _selectionModeRow.style.display = DisplayStyle.Flex;
            _selectionDetailTabs.style.display = DisplayStyle.None;
            UpdateDetailContent();
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
            UpdateDetailContent();
        }

        private void ClearPreview()
        {
            _imageStack.Clear();
            _multiPreviewCountText.SetText(string.Empty);
            _multiPreviewSuffixText.SetText(string.Empty);
            _selectionModeRow.style.display = DisplayStyle.None;
            _multiPreviewTextRow.style.display = DisplayStyle.None;
            _selectionDetailTabs.style.display = DisplayStyle.None;
            _fileTree.Clear();
        }

        private void UpdateDetailContent()
        {
            var hasSingleSelection = _selectedItems != null && _selectedItems.Count == 1;
            var showFileTree = hasSingleSelection && string.Equals(AssetManagerViewState.SelectedAssetDetailTabId, FileTreeTabId, System.StringComparison.Ordinal);
            _detailContent.style.display = showFileTree ? DisplayStyle.Flex : DisplayStyle.None;
            _fileTree.style.display = showFileTree ? DisplayStyle.Flex : DisplayStyle.None;

            if (showFileTree)
            {
                _fileTree.SetItemId(_selectedItems[0].ItemId);
            }
            else
            {
                _fileTree.Clear();
            }
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
            _selectionModeRow.style.marginTop = SelectionModeBaseMarginTop;
            _selectionModeRow.style.marginBottom = 0f;
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
