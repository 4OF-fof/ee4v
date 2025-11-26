using _4OF.ee4v.AssetManager.Adapter;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.i18n;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public static class DownloadThumbnailDialog {
        public static VisualElement CreateContent() {
            var content = new VisualElement {
                style = {
                    paddingLeft = 18,
                    paddingRight = 18,
                    paddingTop = 14,
                    paddingBottom = 14,
                    alignSelf = Align.Stretch,
                    maxWidth = 820,
                    flexDirection = FlexDirection.Column
                }
            };

            var title = new Label(I18N.Get("UI.AssetManager.DownloadThumbnail.Title")) {
                style = {
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6
                }
            };
            content.Add(title);

            var progressText = new Label(I18N.Get("UI.AssetManager.DownloadThumbnail.ProgressFmt", 0, 0)) {
                style = { marginBottom = 8, fontSize = 12, color = ColorPreset.TextColor }
            };
            content.Add(progressText);

            var barWrapper = new VisualElement {
                style = {
                    height = 10,
                    backgroundColor = ColorPreset.TransparentBlack50Style,
                    marginBottom = 10,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    alignSelf = Align.Stretch
                }
            };

            var barInner = new VisualElement {
                style = {
                    width = 0,
                    height = 18,
                    backgroundColor = ColorPreset.AccentBlueStyle,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            barWrapper.Add(barInner);
            content.Add(barWrapper);

            var closeLabel = new Label("✕") {
                style = {
                    position = Position.Absolute,
                    right = 8,
                    top = 6,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    width = 24,
                    height = 20,
                    backgroundColor = Color.clear,
                    unityTextAlign = TextAnchor.MiddleCenter
                },
                tooltip = I18N.Get("UI.AssetManager.Download.Close")
            };
            closeLabel.RegisterCallback<MouseUpEvent>(_ => CloseDialog(content));
            content.Add(closeLabel);

            BoothThumbnailDownloader.OnProgressChanged += UpdateProgress;
            BoothThumbnailDownloader.OnStarted += OnStarted;
            BoothThumbnailDownloader.OnCompleted += OnCompleted;

            content.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                try {
                    BoothThumbnailDownloader.OnProgressChanged -= UpdateProgress;
                    BoothThumbnailDownloader.OnStarted -= OnStarted;
                    BoothThumbnailDownloader.OnCompleted -= OnCompleted;
                }
                catch {
                    /* ignore */
                }
            });


            return content;

            void OnCompleted() {
                UpdateProgress(BoothThumbnailDownloader.TotalCount, BoothThumbnailDownloader.CompletedCount);
                progressText.text = I18N.Get("UI.AssetManager.DownloadThumbnail.CompletedFmt", BoothThumbnailDownloader.CompletedCount, BoothThumbnailDownloader.TotalCount);
            }

            void OnStarted() {
                UpdateProgress(BoothThumbnailDownloader.TotalCount, BoothThumbnailDownloader.CompletedCount);
            }

            void UpdateProgress(int total, int completed) {
                progressText.text = total == 0 ? I18N.Get("UI.AssetManager.DownloadThumbnail.ProgressFmt", 0, 0) : I18N.Get("UI.AssetManager.DownloadThumbnail.ProgressFmt", completed, total);
                var pct = total == 0 ? 0f : (float)completed / total;
                barInner.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));
            }
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }
    }
}