using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.AssetManager.Views;
using _4OF.ee4v.AssetManager.Window;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Components {
    public class InspectorComponent : IAssetManagerComponent {
        private AssetInfo _assetInfo;

        private Action<AssetMetadata> _assetSelectedHandler;
        private AssetManagerContext _context;
        private AssetPropertyPresenter _presenter;
        private Action<Ulid> _previewFolderHandler;

        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.Inspector;
        public int Priority => 0;

        public void Initialize(AssetManagerContext context) {
            _context = context;
            _assetInfo = new AssetInfo();
            _assetInfo.Initialize(context.Repository, context.TextureService);

            _presenter = new AssetPropertyPresenter(
                context.Repository,
                context.AssetService,
                context.FolderService,
                context.ViewController,
                context.SelectionModel,
                context.ShowToast,
                context.RequestRefresh,
                context.RequestTagListRefresh,
                GetScreenPosition,
                context.ShowDialog,
                TagSelectorWindow.Show,
                (screenPos, repo, selectedId, cb) =>
                    AssetSelectorWindow.Show(screenPos, repo, selectedId ?? Ulid.Empty, cb)
            );

            _assetInfo.OnNameChanged += _presenter.OnNameChanged;
            _assetInfo.OnDescriptionChanged += _presenter.OnDescriptionChanged;
            _assetInfo.OnTagAdded += _presenter.OnTagAdded;
            _assetInfo.OnTagRemoved += _presenter.OnTagRemoved;
            _assetInfo.OnTagClicked += _presenter.OnTagClicked;
            _assetInfo.OnDependencyAdded += _presenter.OnDependencyAdded;
            _assetInfo.OnDependencyRemoved += _presenter.OnDependencyRemoved;
            _assetInfo.OnDependencyClicked += _presenter.OnDependencyClicked;
            _assetInfo.OnFolderClicked += folderId => context.ViewController.SetFolder(folderId);
            _assetInfo.OnDownloadRequested += _presenter.OnDownloadRequested;

            _assetSelectedHandler = asset =>
            {
                var list = asset != null ? new List<object> { asset } : new List<object>();
                _assetInfo.UpdateSelection(list);
                _presenter.UpdateSelection(list);
            };
            _previewFolderHandler = id =>
            {
                if (id != Ulid.Empty) {
                    var folder = context.Repository.GetLibraryMetadata()?.GetFolder(id);
                    var list = new List<object> { folder };
                    _assetInfo.UpdateSelection(list);
                    _presenter.UpdateSelection(list);
                }
                else {
                    if (context.SelectionModel.SelectedAsset.Value != null) return;
                    var list = new List<object>();
                    _assetInfo.UpdateSelection(list);
                    _presenter.UpdateSelection(list);
                }
            };

            context.SelectionModel.SelectedAsset.OnValueChanged += _assetSelectedHandler;
            context.SelectionModel.PreviewFolderId.OnValueChanged += _previewFolderHandler;

            context.ViewController.OnHistoryChanged += () => _presenter.ClearSelection();
        }

        public VisualElement CreateElement() {
            return _assetInfo;
        }

        public void Dispose() {
            if (_assetInfo != null) {
                _assetInfo.OnNameChanged -= _presenter.OnNameChanged;
                _assetInfo.OnDescriptionChanged -= _presenter.OnDescriptionChanged;
                _assetInfo.OnTagAdded -= _presenter.OnTagAdded;
                _assetInfo.OnTagRemoved -= _presenter.OnTagRemoved;
                _assetInfo.OnTagClicked -= _presenter.OnTagClicked;
                _assetInfo.OnDependencyAdded -= _presenter.OnDependencyAdded;
                _assetInfo.OnDependencyRemoved -= _presenter.OnDependencyRemoved;
                _assetInfo.OnDependencyClicked -= _presenter.OnDependencyClicked;
                _assetInfo.OnDownloadRequested -= _presenter.OnDownloadRequested;
            }

            if (_context?.SelectionModel == null) return;
            _context.SelectionModel.SelectedAsset.OnValueChanged -= _assetSelectedHandler;
            _context.SelectionModel.PreviewFolderId.OnValueChanged -= _previewFolderHandler;
        }

        private static Vector2 GetScreenPosition() {
            try {
                return GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
            catch {
                return Vector2.zero;
            }
        }
    }
}