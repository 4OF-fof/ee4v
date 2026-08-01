using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
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
        private const float SelectionModeBaseHeight = UiSizeTokens.ControlHeightDefault;
        private const float SelectionModeBaseMarginTop = UiSpacingTokens.Medium;
        private const float SelectionModeScaleStartSize = 260f;
        private const float SelectionModeScaleEndSize = 360f;
        private const float SelectionModeMaxScale = 1.28f;
        private const float HorizontalPadding = UiSpacingTokens.Xxxl;
        private readonly VisualElement _preview;
        private readonly ImageStack _imageStack;
        private readonly VisualElement _selectionModeRow;
        private readonly VisualElement _multiPreviewTextRow;
        private readonly UiTextElement _multiPreviewCountText;
        private readonly UiTextElement _multiPreviewSuffixText;
        private readonly ViewToggleTabs _selectionDetailTabs;
        private readonly VisualElement _detailContent;
        private readonly AssetInfoView _assetInfo;
        private readonly AssetInfoController _assetInfoController;
        private readonly SearchableFileTree _fileTree;
        private float _previewSize;
        private IReadOnlyList<ItemCardState> _selectedItems;
        private AssetSelectionContentKind _selectionContentKind = AssetSelectionContentKind.AssetItem;
        private string _selectedDetailTabId = AssetInfoTabId;

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
            _selectionDetailTabs = new ViewToggleTabs(CreateDetailTabsState(_selectedDetailTabId));
            _selectionDetailTabs.SelectionChanged += SetSelectedAssetDetailTab;
            _selectionModeRow.Add(_selectionDetailTabs);
            _preview.Add(_selectionModeRow);
            Add(_preview);

            _detailContent = new VisualElement();
            _detailContent.AddToClassList(DetailContentClassName);
            _assetInfo = new AssetInfoView();
            _assetInfoController = new AssetInfoController();
            _assetInfo.UpdateRequested += _assetInfoController.Save;
            _assetInfo.AddFileRequested += _assetInfoController.AddFile;
            _assetInfoController.StateChanged += _assetInfo.SetState;
            _assetInfoController.ErrorChanged += _assetInfo.SetError;
            _assetInfoController.NoticeChanged += _assetInfo.SetNotice;
            _detailContent.Add(_assetInfo);
            _fileTree = new SearchableFileTree();
            _detailContent.Add(_fileTree);
            Add(_detailContent);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<AttachToPanelEvent>(_ =>
                _assetInfoController.Activate());
            RegisterCallback<DetachFromPanelEvent>(_ =>
                _assetInfoController.Deactivate());
            SetSelectedAssetItems(null, AssetSelectionContentKind.AssetItem);
        }

        internal IReadOnlyList<ItemCardState> SelectedItems =>
            _selectedItems ?? Array.Empty<ItemCardState>();

        internal AssetSelectionContentKind SelectionContentKind =>
            _selectionContentKind;

        internal string SelectedDetailTabId =>
            _selectedDetailTabId;

        internal void SetSelectedAssetItems(
            IReadOnlyList<ItemCardState> items,
            AssetSelectionContentKind contentKind)
        {
            _selectedItems = items;
            _selectionContentKind = contentKind;
            if (items == null || items.Count == 0)
            {
                _assetInfoController.Clear();
                _preview.style.display = DisplayStyle.None;
                ClearPreview();
                UpdateDetailContent();
                return;
            }

            if (items.Count == 1)
            {
                if (contentKind !=
                    AssetSelectionContentKind.AssetFile)
                {
                    if (contentKind ==
                        AssetSelectionContentKind.AssetItem)
                    {
                        _selectedDetailTabId = AssetInfoTabId;
                    }

                    _assetInfoController.SetSelection(
                        items[0],
                        contentKind);
                }
                else
                {
                    _selectedDetailTabId = ResolveDetailTabId(
                        contentKind,
                        _selectedDetailTabId);
                    _assetInfoController.Clear();
                }

                _selectionDetailTabs.SetSelectedTab(
                    _selectedDetailTabId,
                    notify: false);
                _preview.style.display = DisplayStyle.Flex;
                SetPreview(items, false);
                _multiPreviewTextRow.style.display = DisplayStyle.None;
                var showDetailTabs = contentKind !=
                                     AssetSelectionContentKind.AssetFile;
                _selectionModeRow.style.display = showDetailTabs
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _selectionDetailTabs.style.display = showDetailTabs
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                UpdateDetailContent();
                return;
            }

            _preview.style.display = DisplayStyle.Flex;
            _assetInfoController.Clear();
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
            var previewStates = new List<ItemCardState>(items.Count - firstPreviewIndex);
            for (var i = firstPreviewIndex; i < items.Count; i++)
            {
                previewStates.Add(items[i]);
            }

            _imageStack.SetItemStates(previewStates);
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

        internal void SetSelectedAssetDetailTab(string tabId)
        {
            _selectedDetailTabId = ResolveDetailTabId(
                _selectionContentKind,
                tabId);
            _selectionDetailTabs.SetSelectedTab(_selectedDetailTabId, notify: false);
            UpdateDetailContent();
        }

        internal static string ResolveDetailTabId(
            AssetSelectionContentKind contentKind,
            string requestedTabId)
        {
            if (contentKind ==
                AssetSelectionContentKind.AssetFile)
            {
                return FileTreeTabId;
            }

            return string.Equals(
                requestedTabId,
                FileTreeTabId,
                StringComparison.Ordinal)
                ? FileTreeTabId
                : AssetInfoTabId;
        }

        private void ClearPreview()
        {
            _imageStack.ClearImages();
            _multiPreviewCountText.SetText(string.Empty);
            _multiPreviewSuffixText.SetText(string.Empty);
            _selectionModeRow.style.display = DisplayStyle.None;
            _multiPreviewTextRow.style.display = DisplayStyle.None;
            _selectionDetailTabs.style.display = DisplayStyle.None;
            _assetInfo.SetState(null);
            _fileTree.ClearTree();
        }

        private void UpdateDetailContent()
        {
            var hasSingleSelection = _selectedItems != null && _selectedItems.Count == 1;
            var isFile = _selectionContentKind ==
                         AssetSelectionContentKind.AssetFile;
            var usesDetailTabs =
                _selectionContentKind ==
                AssetSelectionContentKind.AssetItem ||
                _selectionContentKind ==
                AssetSelectionContentKind.AssetVariantGroup ||
                _selectionContentKind ==
                AssetSelectionContentKind.AssetVersionGroup;
            var showFileTree = hasSingleSelection &&
                               (isFile ||
                                usesDetailTabs && string.Equals(
                                    _selectedDetailTabId,
                                    FileTreeTabId,
                                    StringComparison.Ordinal));
            var showAssetInfo = hasSingleSelection &&
                                usesDetailTabs &&
                                string.Equals(
                                    _selectedDetailTabId,
                                    AssetInfoTabId,
                                    StringComparison.Ordinal);
            _detailContent.style.display = showFileTree || showAssetInfo
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _assetInfo.style.display = showAssetInfo
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _fileTree.style.display = showFileTree ? DisplayStyle.Flex : DisplayStyle.None;

            if (showFileTree)
            {
                if (_selectionContentKind == AssetSelectionContentKind.AssetFile)
                {
                    _fileTree.SetFileId(_selectedItems[0].ParentItemId, _selectedItems[0].ItemId);
                }
                else if (_selectionContentKind == AssetSelectionContentKind.AssetItem)
                {
                    _fileTree.SetItemId(_selectedItems[0].ItemId);
                }
                else if (
                    _selectionContentKind == AssetSelectionContentKind.AssetVariantGroup ||
                    _selectionContentKind == AssetSelectionContentKind.AssetVersionGroup)
                {
                    _fileTree.SetGroupId(
                        _selectedItems[0].ParentItemId,
                        _selectionContentKind ==
                        AssetSelectionContentKind.AssetVariantGroup
                            ? FileTreeGroupKind.Variant
                            : FileTreeGroupKind.Version,
                        _selectedItems[0].ItemId);
                }
                else
                {
                    _fileTree.ClearTree();
                }
            }
            else
            {
                _fileTree.ClearTree();
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
            _selectionModeRow.style.marginBottom = UiSpacingTokens.None;
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
