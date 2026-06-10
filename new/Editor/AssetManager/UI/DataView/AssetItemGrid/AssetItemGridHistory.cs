using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager
{
    internal enum AssetItemGridHistoryEntryKind
    {
        View,
        FileList
    }

    internal sealed class AssetItemGridHistoryEntry
    {
        public AssetItemGridHistoryEntry(
            AssetItemGridHistoryEntryKind kind,
            string viewId,
            string viewLabel,
            string itemId = null,
            string itemName = null)
        {
            Kind = kind;
            ViewId = viewId ?? string.Empty;
            ViewLabel = viewLabel ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
        }

        public AssetItemGridHistoryEntryKind Kind { get; }

        public string ViewId { get; }

        public string ViewLabel { get; }

        public string ItemId { get; }

        public string ItemName { get; }

        public IReadOnlyList<string> Breadcrumbs
        {
            get
            {
                if (Kind == AssetItemGridHistoryEntryKind.FileList && !string.IsNullOrWhiteSpace(ItemName))
                {
                    return new[] { ViewLabel, ItemName };
                }

                return new[] { ViewLabel };
            }
        }

        public bool IsSameLocation(AssetItemGridHistoryEntry other)
        {
            return other != null
                && Kind == other.Kind
                && string.Equals(ViewId, other.ViewId, StringComparison.Ordinal)
                && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal);
        }
    }

    internal sealed class AssetItemGridHistoryState
    {
        public AssetItemGridHistoryState(
            AssetItemGridHistoryEntry current,
            bool canGoBack,
            bool canGoForward)
        {
            Current = current;
            CanGoBack = canGoBack;
            CanGoForward = canGoForward;
        }

        public AssetItemGridHistoryEntry Current { get; }

        public bool CanGoBack { get; }

        public bool CanGoForward { get; }
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
            entry = null;
            if (_backStack.Count == 0 || _current == null)
            {
                return false;
            }

            _forwardStack.Push(_current);
            _current = _backStack.Pop();
            entry = _current;
            NotifyChanged();
            return true;
        }

        public bool TryGoForward(out AssetItemGridHistoryEntry entry)
        {
            entry = null;
            if (_forwardStack.Count == 0 || _current == null)
            {
                return false;
            }

            _backStack.Push(_current);
            _current = _forwardStack.Pop();
            entry = _current;
            NotifyChanged();
            return true;
        }

        private AssetItemGridHistoryState CreateState()
        {
            return new AssetItemGridHistoryState(_current, _backStack.Count > 0, _forwardStack.Count > 0);
        }

        private void NotifyChanged()
        {
            Changed?.Invoke(CreateState());
        }
    }
}
