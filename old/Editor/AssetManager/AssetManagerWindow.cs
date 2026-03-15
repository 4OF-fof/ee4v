using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.AssetManager.Views.Dialog;
using _4OF.ee4v.AssetManager.Views.Toast;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Manager;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager {
    public class AssetManagerWindow : EditorWindow {
        private AssetManagerComponentManager _componentManager;
        private AssetManagerContext _context;

        private void OnEnable() {
            if (_context == null) Initialize();
        }

        private void OnDisable() {
            _componentManager?.Dispose();
            _context?.ViewController?.Dispose();
        }

        private void CreateGUI() {
            Initialize();
        }

        [MenuItem("ee4v/Asset Manager", false, 0)]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>(I18N.Get("UI.AssetManager.Window.Title"));
            window.minSize = new Vector2(800, 400);
            AssetManagerContainer.Repository.Load();
            window.Show();
            window.CheckLibraryIntegrity();
        }

        [MenuItem("ee4v/AssetManager/Refresh", false, 1000)]
        public static void RefreshLibrary() {
            if (!HasOpenInstances<AssetManagerWindow>()) return;
            var window = GetWindow<AssetManagerWindow>(I18N.Get("UI.AssetManager.Window.Title"));
            window.CheckLibraryIntegrity();
        }

        private async void CheckLibraryIntegrity() {
            if (AssetManagerContainer.Repository == null) return;

            AssetManagerContainer.Repository.Load();

            var result = await AssetManagerContainer.Repository.LoadAndVerifyAsync();
            if (result.Error != null) {
                Debug.LogError(result.Error);
                return;
            }

            if (!result.HasChanges) return;
            var dialog = new VerificationResultDialog();
            var content = dialog.CreateContent(result, AssetManagerContainer.Repository);

            if (_context == null) Initialize();
            _context?.ShowDialog?.Invoke(content);
        }

        private void Initialize() {
            _context = new AssetManagerContext {
                Repository = AssetManagerContainer.Repository,
                AssetService = AssetManagerContainer.AssetService,
                FolderService = AssetManagerContainer.FolderService,
                TextureService = AssetManagerContainer.TextureService,
                ViewController = new AssetListService(AssetManagerContainer.Repository),
                SelectionService = new SelectionService(),
                RequestRefresh = RefreshUI
            };

            _context.ViewController.AssetSelected += asset =>
            {
                _context.SelectionService.SetSelectedAsset(asset);
                _context.SelectionService.SetPreviewFolder(Ulid.Empty);
            };
            _context.ViewController.FolderPreviewSelected += folder =>
            {
                _context.SelectionService.SetPreviewFolder(folder?.ID ?? Ulid.Empty);
                if (folder != null) _context.SelectionService.SetSelectedAsset(null);
            };

            var root = rootVisualElement;
            root.Clear();

            var contentRoot = new VisualElement {
                name = "ee4v-content-root",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    height = Length.Percent(100)
                }
            };
            root.Add(contentRoot);

            var leftContainer = new VisualElement {
                name = "Region-Navigation",
                style = {
                    width = 200,
                    minWidth = 150,
                    flexShrink = 0,
                    borderRightWidth = 1,
                    borderRightColor = ColorPreset.WindowBorder
                }
            };
            contentRoot.Add(leftContainer);

            var mainContainer = new VisualElement {
                name = "Region-Main",
                style = { flexGrow = 1 }
            };
            contentRoot.Add(mainContainer);

            var rightContainer = new VisualElement {
                name = "Region-Inspector",
                style = {
                    width = 300,
                    minWidth = 250,
                    flexShrink = 0,
                    borderLeftWidth = 1,
                    borderLeftColor = ColorPreset.WindowBorder
                }
            };
            contentRoot.Add(rightContainer);

            var overlayContainer = new VisualElement {
                name = "Region-Overlay",
                pickingMode = PickingMode.Ignore,
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0
                }
            };
            root.Add(overlayContainer);

            _componentManager = new AssetManagerComponentManager();
            _componentManager.Initialize(_context);

            MountComponents(AssetManagerComponentLocation.Navigation, leftContainer);
            MountComponents(AssetManagerComponentLocation.MainView, mainContainer);
            MountComponents(AssetManagerComponentLocation.Inspector, rightContainer);
            MountComponents(AssetManagerComponentLocation.Overlay, overlayContainer);

            _context.ViewController.Refresh();
        }

        private void MountComponents(AssetManagerComponentLocation location, VisualElement container) {
            foreach (var component in _componentManager.GetComponents(location)) {
                var element = component.CreateElement();
                if (element != null) container.Add(element);
            }
        }

        private void RefreshUI(bool fullRefresh) {
            if (fullRefresh) _context.ViewController.Refresh();

            var selectedAsset = _context.SelectionService.SelectedAsset.Value;
            if (selectedAsset == null) return;
            var freshAsset = _context.Repository.GetAsset(selectedAsset.ID);
            _context.SelectionService.SetSelectedAsset(freshAsset);
        }

        public static void ShowToastMessage(string message, float? duration = 3f, ToastType type = ToastType.Info) {
            var window = GetWindow<AssetManagerWindow>();
            window?._context?.ShowToast?.Invoke(message, duration, type);
        }
    }
}