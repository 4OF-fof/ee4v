using _4OF.ee4v.AssetManager.Views.Toast;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.UI;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Components {
    public class OverlayComponent : IAssetManagerComponent {
        private AssetManagerContext _context;
        private VisualElement _rootContainer;
        private ToastManager _toastManager;

        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.Overlay;
        public int Priority => -100;

        public void Initialize(AssetManagerContext context) {
            _context = context;

            _rootContainer = new VisualElement {
                name = "ee4v-overlay-root",
                pickingMode = PickingMode.Ignore,
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    flexDirection = FlexDirection.Column
                }
            };

            _toastManager = new ToastManager(_rootContainer);

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
            var backdrop = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = ColorPreset.OverlayBackground,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

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

            dialogContainer.Add(dialogContent);
            backdrop.Add(dialogContainer);
            _rootContainer.Add(backdrop);

            return backdrop;
        }
    }
}