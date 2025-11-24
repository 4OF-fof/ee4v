using _4OF.ee4v.AssetManager.Adapter;
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

            var title = new Label("サムネイルのダウンロード") {
                style = {
                    fontSize = 18,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 12
                }
            };
            content.Add(title);

            var statusLabel = new Label("準備中…") {
                style = { marginBottom = 8, fontSize = 13 }
            };
            content.Add(statusLabel);

            var barWrapper = new VisualElement {
                style = {
                    height = 18,
                    backgroundColor = new Color(0.85f, 0.85f, 0.85f),
                    marginBottom = 8,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            var barInner = new VisualElement {
                style = {
                    width = 0,
                    height = 18,
                    backgroundColor = new Color(0.26f, 0.58f, 0.95f),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            barWrapper.Add(barInner);
            content.Add(barWrapper);

            var progressText = new Label("0 / 0") { style = { marginBottom = 12 } };
            content.Add(progressText);

            var buttonRow = new VisualElement
                { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };

            var backgroundBtn = new Button { text = "バックグラウンドで続行", style = { marginRight = 6 } };
            var closeBtn = new Button { text = "閉じる" };
            buttonRow.Add(backgroundBtn);
            buttonRow.Add(closeBtn);

            content.Add(buttonRow);

            void OnStarted() {
                UpdateProgress(BoothThumbnailDownloader.TotalCount, BoothThumbnailDownloader.CompletedCount);
            }

            void OnCompleted() {
                UpdateProgress(BoothThumbnailDownloader.TotalCount, BoothThumbnailDownloader.CompletedCount);
                statusLabel.text = "ダウンロード完了";
            }

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

            backgroundBtn.clicked += () => CloseDialog(content);
            closeBtn.clicked += () => CloseDialog(content);

            return content;

            void UpdateProgress(int total, int completed) {
                progressText.text = $"{completed} / {total}";
                statusLabel.text = total == 0 ? "ダウンロードなし" : $"サムネイルをダウンロード中 ({completed}/{total})";
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