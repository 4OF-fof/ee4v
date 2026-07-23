using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.UI
{
    internal enum AssetItemGridHistoryEntryKind
    {
        View,
        FileList,
        FileDetail
    }

    internal enum AssetItemGridNodeKind
    {
        Item,
        VariantGroup,
        VersionGroup,
        File
    }

    internal sealed class AssetItemGridHistoryEntry
    {
        public AssetItemGridHistoryEntry(
            AssetItemGridHistoryEntryKind kind,
            string viewId,
            string viewLabel,
            string itemId = null,
            string itemName = null,
            AssetItemGridNodeKind nodeKind = AssetItemGridNodeKind.Item,
            string nodeId = null,
            string nodeName = null,
            string detailId = null,
            string detailName = null,
            string detailParentName = null)
        {
            Kind = kind;
            ViewId = viewId ?? string.Empty;
            ViewLabel = viewLabel ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            NodeKind = nodeKind;
            NodeId = nodeId ?? string.Empty;
            NodeName = nodeName ?? string.Empty;
            DetailId = detailId ?? string.Empty;
            DetailName = detailName ?? string.Empty;
            DetailParentName = detailParentName ?? string.Empty;
        }

        public AssetItemGridHistoryEntryKind Kind { get; }

        public string ViewId { get; }

        public string ViewLabel { get; }

        public string ItemId { get; }

        public string ItemName { get; }

        public AssetItemGridNodeKind NodeKind { get; }

        public string NodeId { get; }

        public string NodeName { get; }

        public string DetailId { get; }

        public string DetailName { get; }

        public string DetailParentName { get; }

        public IReadOnlyList<string> Breadcrumbs
        {
            get
            {
                if ((Kind == AssetItemGridHistoryEntryKind.FileList || Kind == AssetItemGridHistoryEntryKind.FileDetail) &&
                    !string.IsNullOrWhiteSpace(ItemName))
                {
                    if (Kind == AssetItemGridHistoryEntryKind.FileDetail && !string.IsNullOrWhiteSpace(DetailName))
                    {
                        if (!string.IsNullOrWhiteSpace(DetailParentName))
                        {
                            return new[] { ViewLabel, ItemName, DetailParentName, DetailName };
                        }

                        return new[] { ViewLabel, ItemName, DetailName };
                    }

                    if (!string.IsNullOrWhiteSpace(NodeName))
                    {
                        return new[] { ViewLabel, ItemName, NodeName };
                    }

                    return new[] { ViewLabel, ItemName };
                }

                if (Kind == AssetItemGridHistoryEntryKind.FileDetail && !string.IsNullOrWhiteSpace(DetailName))
                {
                    if (!string.IsNullOrWhiteSpace(DetailParentName))
                    {
                        return new[] { ViewLabel, DetailParentName, DetailName };
                    }

                    return new[] { ViewLabel, DetailName };
                }

                return new[] { ViewLabel };
            }
        }

        public bool IsSameLocation(AssetItemGridHistoryEntry other)
        {
            return other != null
                && Kind == other.Kind
                && string.Equals(ViewId, other.ViewId, StringComparison.Ordinal)
                && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
                && NodeKind == other.NodeKind
                && string.Equals(NodeId, other.NodeId, StringComparison.Ordinal)
                && string.Equals(DetailId, other.DetailId, StringComparison.Ordinal)
                && string.Equals(DetailParentName, other.DetailParentName, StringComparison.Ordinal);
        }
    }

    internal sealed class AssetItemGridHistoryState
    {
        public AssetItemGridHistoryState(
            AssetItemGridHistoryEntry current,
            bool canGoBack,
            bool canGoForward,
            IReadOnlyList<AssetItemGridHistoryEntry> backEntries = null,
            IReadOnlyList<AssetItemGridHistoryEntry> forwardEntries = null)
        {
            Current = current;
            CanGoBack = canGoBack;
            CanGoForward = canGoForward;
            BackEntries = backEntries ?? Array.Empty<AssetItemGridHistoryEntry>();
            ForwardEntries = forwardEntries ?? Array.Empty<AssetItemGridHistoryEntry>();
        }

        public AssetItemGridHistoryEntry Current { get; }

        public bool CanGoBack { get; }

        public bool CanGoForward { get; }

        public IReadOnlyList<AssetItemGridHistoryEntry> BackEntries { get; }

        public IReadOnlyList<AssetItemGridHistoryEntry> ForwardEntries { get; }
    }

    internal sealed class AssetItemGridHistory
    {
        private readonly Stack<AssetItemGridHistoryEntry> _backStack = new Stack<AssetItemGridHistoryEntry>();
        private readonly Stack<AssetItemGridHistoryEntry> _forwardStack = new Stack<AssetItemGridHistoryEntry>();
        private AssetItemGridHistoryEntry _current;

        public event Action<AssetItemGridHistoryState> Changed;

        public AssetItemGridHistoryState State
        {
            get { return CreateState(); }
        }

        public void SetCurrent(AssetItemGridHistoryEntry entry)
        {
            if (entry == null || (_current != null && _current.IsSameLocation(entry)))
            {
                return;
            }

            if (_current != null)
            {
                _backStack.Push(_current);
            }

            _current = entry;
            _forwardStack.Clear();
            NotifyChanged();
        }

        public bool TryGoBack(out AssetItemGridHistoryEntry entry)
        {
            return TryGoBack(1, out entry);
        }

        public bool TryGoBack(int steps, out AssetItemGridHistoryEntry entry)
        {
            entry = null;
            if (steps <= 0 || _backStack.Count == 0 || _current == null)
            {
                return false;
            }

            var moved = 0;
            while (moved < steps && _backStack.Count > 0)
            {
                _forwardStack.Push(_current);
                _current = _backStack.Pop();
                moved++;
            }

            entry = _current;
            NotifyChanged();
            return true;
        }

        public bool TryGoForward(out AssetItemGridHistoryEntry entry)
        {
            return TryGoForward(1, out entry);
        }

        public bool TryGoForward(int steps, out AssetItemGridHistoryEntry entry)
        {
            entry = null;
            if (steps <= 0 || _forwardStack.Count == 0 || _current == null)
            {
                return false;
            }

            var moved = 0;
            while (moved < steps && _forwardStack.Count > 0)
            {
                _backStack.Push(_current);
                _current = _forwardStack.Pop();
                moved++;
            }

            entry = _current;
            NotifyChanged();
            return true;
        }

        private AssetItemGridHistoryState CreateState()
        {
            return new AssetItemGridHistoryState(
                _current,
                _backStack.Count > 0,
                _forwardStack.Count > 0,
                _backStack.ToArray(),
                _forwardStack.ToArray());
        }

        private void NotifyChanged()
        {
            Changed?.Invoke(CreateState());
        }
    }
}
