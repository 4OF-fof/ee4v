using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerWorkspaceView : VisualElement, IDisposable
    {
        private readonly List<AssetManagerView> _panes = new List<AssetManagerView>();
        private readonly Action<DerivedAssetInfo> _openDerivedAsset;
        private readonly Func<AssetManagerViewMode, AssetManagerView> _createView;
        private bool _disposed;

        internal AssetManagerWorkspaceView(
            Action<DerivedAssetInfo> openDerivedAsset,
            Action<string> createDerivedAsset = null,
            Func<AssetManagerViewMode, Action<string>, AssetManagerView> createView = null)
        {
            _openDerivedAsset = openDerivedAsset;
            var factory = createView ?? AssetManagerWindowSession.CreateView;
            _createView = mode => factory(mode, createDerivedAsset);
            AddToClassList("ee4v-workspace__library");
            if (createView == null)
            {
                AssetManagerWindowSession.ManagerInvalidated += Rebuild;
            }
            try
            {
                Rebuild();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Rebuild()
        {
            if (_disposed)
            {
                return;
            }
            DisposePanes();
            Clear();
            AssetManagerWindowSession.PrepareRoot(this);
            var split = new TwoPaneSplitView(0, 240f, TwoPaneSplitViewOrientation.Horizontal);
            var navigation = AddPane(AssetManagerViewMode.Navigation);
            navigation.style.minWidth = 200f;
            split.Add(navigation);
            var detailSplit = new TwoPaneSplitView(1, 300f, TwoPaneSplitViewOrientation.Horizontal);
            var main = AddPane(AssetManagerViewMode.Main);
            main.style.minWidth = 480f;
            detailSplit.Add(main);
            var information = AddPane(AssetManagerViewMode.Information);
            information.style.minWidth = 260f;
            detailSplit.Add(information);
            split.Add(detailSplit);
            Add(split);
        }

        private AssetManagerView AddPane(AssetManagerViewMode mode)
        {
            var pane = _createView(mode);
            pane.SetVariantModificationHandler(_openDerivedAsset);
            _panes.Add(pane);
            return pane;
        }

        private void DisposePanes()
        {
            foreach (var pane in _panes)
            {
                pane.Dispose();
            }
            _panes.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            AssetManagerWindowSession.ManagerInvalidated -= Rebuild;
            DisposePanes();
        }
    }
}
