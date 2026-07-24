using System;
using System.Collections.Generic;
using Ee4v.UI;

namespace Ee4v.AssetManager.UI
{
    internal sealed class StandaloneAssetManagerViewSession
    {
        private ItemCardState[] _selectedItems = Array.Empty<ItemCardState>();
        private AssetSelectionContentKind _selectionContentKind =
            AssetSelectionContentKind.AssetItem;
        private string _selectedDetailTabId = string.Empty;

        public event Action<IReadOnlyList<ItemCardState>, AssetSelectionContentKind>
            SelectionChanged;

        public event Action<string> DetailTabRequested;

        public IReadOnlyList<ItemCardState> SelectedItems
        {
            get { return _selectedItems; }
        }

        public AssetSelectionContentKind SelectionContentKind
        {
            get { return _selectionContentKind; }
        }

        public string SelectedDetailTabId
        {
            get { return _selectedDetailTabId; }
        }

        public void SetSelection(
            IReadOnlyList<ItemCardState> items,
            AssetSelectionContentKind contentKind)
        {
            if (items == null || items.Count == 0)
            {
                _selectedItems = Array.Empty<ItemCardState>();
                _selectionContentKind = AssetSelectionContentKind.AssetItem;
            }
            else
            {
                var snapshot = new List<ItemCardState>(items.Count);
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        snapshot.Add(items[i]);
                    }
                }

                _selectedItems = snapshot.ToArray();
                _selectionContentKind = _selectedItems.Length == 0
                    ? AssetSelectionContentKind.AssetItem
                    : contentKind;
            }

            SelectionChanged?.Invoke(_selectedItems, _selectionContentKind);
        }

        public void RequestDetailTab(string tabId)
        {
            _selectedDetailTabId = tabId ?? string.Empty;
            DetailTabRequested?.Invoke(_selectedDetailTabId);
        }
    }
}
