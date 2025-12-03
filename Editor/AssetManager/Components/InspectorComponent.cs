using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Modules;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.AssetManager.State;
using _4OF.ee4v.AssetManager.Views;
using _4OF.ee4v.AssetManager.Window;
using _4OF.ee4v.Core.i18n;
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

        public string Name => "Inspector";
        public string Description => "Displays detailed information about the selected asset or folder.";
        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.Inspector;
        public int Priority => 0;

        public void Initialize(AssetManagerContext context) {
            _context = context;
            _assetInfo = new AssetInfo();
            _assetInfo.Initialize(context.Repository, context.TextureService);

            // Presenter初期化
            _presenter = new AssetPropertyPresenter(
                context.Repository,
                context.AssetService,
                context.FolderService,
                context.ViewController,
                context.SelectionModel,
                context.ShowToast,
                context.RequestRefresh,
                _ => { }, // Navigation更新はここでは行わない
                context.RequestTagListRefresh,
                GetScreenPosition,
                context.ShowDialog,
                TagSelectorWindow.Show,
                (screenPos, repo, selectedId, cb) =>
                    AssetSelectorWindow.Show(screenPos, repo, selectedId ?? Ulid.Empty, cb)
            );

            // イベント購読
            _assetInfo.OnNameChanged += _presenter.OnNameChanged;
            _assetInfo.OnDescriptionChanged += _presenter.OnDescriptionChanged;
            _assetInfo.OnTagAdded += _presenter.OnTagAdded;
            _assetInfo.OnTagRemoved += _presenter.OnTagRemoved;
            _assetInfo.OnTagClicked += tag =>
            {
                // タグクリックはNavigationの変更を伴うため、PresenterではなくControllerを介すか
                // NavigationPresenterのロジックが必要だが、ここでは簡易的にPresenterには無いので
                // Controllerを操作するヘルパーメソッドが必要、あるいはContext経由
                // 現状のAssetPropertyPresenterにはOnTagClickedがないため、独自実装
                context.ViewController.SetMode(NavigationMode.Tag,
                    $"{I18N.Get("UI.AssetManager.Navigation.TagPrefix")}{tag}",
                    a => !a.IsDeleted && a.Tags.Contains(tag));
            };
            _assetInfo.OnDependencyAdded += _presenter.OnDependencyAdded;
            _assetInfo.OnDependencyRemoved += _presenter.OnDependencyRemoved;
            _assetInfo.OnDependencyClicked += OnDependencyClicked;
            _assetInfo.OnFolderClicked += folderId => context.ViewController.SetFolder(folderId);
            _assetInfo.OnDownloadRequested += _presenter.OnDownloadRequested;

            // SelectionModelの監視
            _assetSelectedHandler = asset =>
            {
                _assetInfo.UpdateSelection(asset != null ? new List<object> { asset } : new List<object>());
            };
            _previewFolderHandler = id =>
            {
                if (id != Ulid.Empty) {
                    var folder = context.Repository.GetLibraryMetadata()?.GetFolder(id);
                    _assetInfo.UpdateSelection(new List<object> { folder });
                }
                else {
                    if (context.SelectionModel.SelectedAsset.Value == null)
                        _assetInfo.UpdateSelection(new List<object>());
                }
            };

            context.SelectionModel.SelectedAsset.OnValueChanged += _assetSelectedHandler;
            context.SelectionModel.PreviewFolderId.OnValueChanged += _previewFolderHandler;

            // 履歴変更時に選択解除
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
                _assetInfo.OnDependencyAdded -= _presenter.OnDependencyAdded;
                _assetInfo.OnDependencyRemoved -= _presenter.OnDependencyRemoved;
                _assetInfo.OnDependencyClicked -= OnDependencyClicked;
                _assetInfo.OnDownloadRequested -= _presenter.OnDownloadRequested;
            }

            if (_context?.SelectionModel != null) {
                _context.SelectionModel.SelectedAsset.OnValueChanged -= _assetSelectedHandler;
                _context.SelectionModel.PreviewFolderId.OnValueChanged -= _previewFolderHandler;
            }
        }

        private Vector2 GetScreenPosition() {
            try {
                return GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
            catch {
                return Vector2.zero;
            }
        }

        private void OnDependencyClicked(Ulid dependencyId) {
            var depAsset = _context.Repository.GetAsset(dependencyId);
            if (depAsset == null || depAsset.IsDeleted) {
                _context.ShowToast(I18N.Get("UI.AssetManager.Toast.DependencyAssetNotSelected"), 3, ToastType.Error);
                return;
            }

            var folder = depAsset.Folder;
            if (folder != Ulid.Empty) _context.ViewController.SetFolder(folder);
            // 選択状態にするにはGrid側への通知が必要だが、ここではフォルダ移動のみ
        }
    }
}