using _4OF.ee4v.AssetManager.Component;
using _4OF.ee4v.AssetManager.Interfaces;
using _4OF.ee4v.AssetManager.Manager;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Window {
    public class AssetManagerWindow : EditorWindow {
        private AssetManagerComponentManager _componentManager;
        private AssetManagerContext _context;
        // ToastManagerのフィールドは削除（OverlayComponentへ移動）

        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>(I18N.Get("UI.AssetManager.Window.Title"));
            window.minSize = new Vector2(800, 400);
            AssetManagerContainer.Repository.Load();
            window.Show();
        }

        private void OnEnable() {
            if (_context == null) Initialize();
        }

        private void OnDisable() {
            _componentManager?.Dispose();
            _context?.ViewController?.Dispose();
            // _toastManager?.ClearAll(); // 削除
        }

        private void CreateGUI() {
            Initialize();
        }

        private void Initialize() {
            // コンテキストの作成
            _context = new AssetManagerContext {
                Repository = AssetManagerContainer.Repository,
                AssetService = AssetManagerContainer.AssetService,
                FolderService = AssetManagerContainer.FolderService,
                TextureService = AssetManagerContainer.TextureService,
                ViewController = new AssetViewController(AssetManagerContainer.Repository),
                SelectionModel = new AssetSelectionModel(),
                // ShowDialog, ShowToast は OverlayComponent によって初期化時に設定されるため、ここではnullのまま
                RequestRefresh = RefreshUI,
                RequestTagListRefresh = () => { /* Event will be subscribed by TagListComponent */ }
            };

            // コントローラのイベントをモデルに反映
            _context.ViewController.AssetSelected += asset => {
                _context.SelectionModel.SetSelectedAsset(asset);
                _context.SelectionModel.SetPreviewFolder(Ulid.Empty);
            };
            _context.ViewController.FolderPreviewSelected += folder => {
                _context.SelectionModel.SetPreviewFolder(folder?.ID ?? Ulid.Empty);
                if (folder != null) _context.SelectionModel.SetSelectedAsset(null);
            };

            // UIレイアウトの構築
            var root = rootVisualElement;
            root.Clear();
            
            // メインレイアウト（Navigation, Main, Inspector）用のコンテナ
            var contentRoot = new VisualElement {
                name = "ee4v-content-root",
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    height = Length.Percent(100)
                }
            };
            root.Add(contentRoot);

            // 各領域のコンテナ作成
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

            // オーバーレイレイヤー（全画面を覆うコンテナ）
            // contentRootの後に配置することで手前に描画される
            var overlayContainer = new VisualElement {
                name = "Region-Overlay",
                pickingMode = PickingMode.Ignore, // コンテンツがない場所はクリックを透過
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0
                }
            };
            root.Add(overlayContainer);

            // コンポーネントマネージャーの初期化
            _componentManager = new AssetManagerComponentManager();
            
            // Initialize内で OverlayComponent が先に初期化され、_context.ShowDialog等が設定される
            _componentManager.Initialize(_context);

            // 各コンポーネントの配置
            MountComponents(AssetManagerComponentLocation.Navigation, leftContainer);
            MountComponents(AssetManagerComponentLocation.MainView, mainContainer);
            MountComponents(AssetManagerComponentLocation.Inspector, rightContainer);
            MountComponents(AssetManagerComponentLocation.Overlay, overlayContainer);

            // 初期表示更新
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
            
            var selectedAsset = _context.SelectionModel.SelectedAsset.Value;
            if (selectedAsset != null) {
                var freshAsset = _context.Repository.GetAsset(selectedAsset.ID);
                _context.SelectionModel.SetSelectedAsset(freshAsset);
            }
        }

        // ShowDialog, ShowToast メソッドは OverlayComponent に移譲されたため削除
        
        // 静的ヘルパーメソッドはウィンドウインスタンス経由でContextを呼び出す形に変更可能だが、
        // Contextへのアクセスが難しいため、簡易的に GetWindow して OverlayComponent の機能を使いたいところだが、
        // 現状の静的メソッド ShowToastMessage は Window が _context を持っている前提で修正が必要。
        // ただし、このメソッドは外部からの呼び出し用であり、内部的には Context.ShowToast を使うべき。
        
        public static void ShowToastMessage(string message, float? duration = 3f, ToastType type = ToastType.Info) {
            var window = GetWindow<AssetManagerWindow>();
            // window._context.ShowToast が設定されていれば実行
            window?._context?.ShowToast?.Invoke(message, duration, type);
        }
    }
}