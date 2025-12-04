using _4OF.ee4v.AssetManager.Booth;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Dialog {
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
                    backgroundColor = ColorPreset.ProgressBarBackground,
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
                    backgroundColor = ColorPreset.ProgressBar,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            barWrapper.Add(barInner);
            content.Add(barWrapper);

            var buttonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = 4,
                    display = DisplayStyle.None
                }
            };
            content.Add(buttonContainer);

            var doneButton = new Button(() => CloseDialog(content)) {
                text = I18N.Get("UI.AssetManager.Download.Close"),
                style = {
                    width = 80,
                    height = 26
                }
            };
            buttonContainer.Add(doneButton);

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

            if (BoothThumbnailDownloader.TotalCount == 0)
                content.schedule.Execute(() => CloseDialog(content));

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
                if (BoothThumbnailDownloader.TotalCount == 0) {
                    CloseDialog(content);
                    return;
                }

                UpdateProgress(BoothThumbnailDownloader.TotalCount, BoothThumbnailDownloader.CompletedCount);
                progressText.text = I18N.Get("UI.AssetManager.DownloadThumbnail.CompletedFmt",
                    BoothThumbnailDownloader.CompletedCount, BoothThumbnailDownloader.TotalCount);

                barInner.style.backgroundColor = ColorPreset.Success;
                buttonContainer.style.display = DisplayStyle.Flex;
            }

            void OnStarted() {
                barInner.style.backgroundColor = ColorPreset.ProgressBar;
                buttonContainer.style.display = DisplayStyle.None;

                UpdateProgress(BoothThumbnailDownloader.TotalCount, BoothThumbnailDownloader.CompletedCount);
            }

            void UpdateProgress(int total, int completed) {
                progressText.text = total == 0
                    ? I18N.Get("UI.AssetManager.DownloadThumbnail.ProgressFmt", 0, 0)
                    : I18N.Get("UI.AssetManager.DownloadThumbnail.ProgressFmt", completed, total);
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