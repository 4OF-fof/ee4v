using System;
using _4OF.ee4v.AssetManager.Component;
using _4OF.ee4v.AssetManager.Interfaces;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Component {
    public class OverlayComponent : IAssetManagerComponent {
        private VisualElement _rootContainer;
        private ToastManager _toastManager;
        private AssetManagerContext _context;

        public string Name => "Overlay System";
        public string Description => "Handles modal dialogs and toast notifications.";
        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.Overlay;
        
        // 他のコンポーネントが Initialize 中に ShowDialog/ShowToast を参照する可能性があるため、
        // 最優先で初期化されるように設定します。
        public int Priority => -100;

        public void Initialize(AssetManagerContext context) {
            _context = context;
            
            // ルートコンテナの作成（画面全体を覆う、透過・タッチ無視）
            _rootContainer = new VisualElement {
                name = "ee4v-overlay-root",
                pickingMode = PickingMode.Ignore,
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    flexDirection = FlexDirection.Column
                }
            };

            // ToastManagerの初期化
            // ToastManagerはコンテナ内にトースト表示用コンテナを生成して追加します
            _toastManager = new ToastManager(_rootContainer);

            // Contextへの機能提供
            context.ShowDialog = ShowDialogContent;
            context.ShowToast = ShowToastMessage;
        }

        public VisualElement CreateElement() {
            return _rootContainer;
        }

        public void Dispose() {
            _toastManager?.ClearAll();
            
            if (_context != null) {
                _context.ShowDialog = null;
                _context.ShowToast = null;
            }
        }

        private void ShowToastMessage(string message, float? duration, ToastType type) {
            _toastManager?.Show(message, duration, type);
        }

        private VisualElement ShowDialogContent(VisualElement dialogContent) {
            // 背景（暗幕）
            var backdrop = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = ColorPreset.TransparentBlack50Style,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            // ダイアログ本体のコンテナ
            var dialogContainer = new VisualElement {
                style = {
                    backgroundColor = ColorPreset.DefaultBackground,
                    paddingLeft = 20, paddingRight = 20, paddingTop = 20, paddingBottom = 20,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    minWidth = 300,
                    maxWidth = 500
                }
            };

            // コンテンツの追加
            dialogContainer.Add(dialogContent);
            backdrop.Add(dialogContainer);
            
            // オーバーレイに追加
            _rootContainer.Add(backdrop);

            // 閉じるためのヘルパーメソッドを提供できるように、backdrop自体を返す
            // (呼び出し元で parent?.RemoveFromHierarchy() するため)
            return backdrop;
        }
    }
}