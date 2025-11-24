using System;
using _4OF.ee4v.AssetManager.Adapter;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public static class WaitBoothSyncDialog {
        private const string BoothLibraryUrl = "https://accounts.booth.pm/library";
        private const int LocalHttpPort = 58080;

        public static VisualElement CreateContent() {
            var content = new VisualElement();

            var title = new Label("Import from Booth") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };

            content.Add(title);

            var message = new Label("ブラウザで操作を行ってください。自動的に Booth のライブラリ画面を開きます。") {
                style = { marginBottom = 12, unityTextAlign = TextAnchor.MiddleLeft }
            };
            content.Add(message);

            var buttonRow = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexStart, marginBottom = 12 }
            };

            var openBtn = new Button(() => Application.OpenURL(BoothLibraryUrl)) {
                text = BoothLibraryUrl,
                style = {
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6
                }
            };
            buttonRow.Add(openBtn);

            content.Add(buttonRow);

            var closeRow = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd }
            };

            var closeBtn = new Button { text = "Close" };
            closeBtn.clicked += () => CloseDialog(content);
            closeRow.Add(closeBtn);

            content.Add(closeRow);

            try {
                if (!HttpServer.IsRunning) HttpServer.Start(LocalHttpPort);
            }
            catch (Exception ex) {
                Debug.LogWarning($"Failed to start local HttpServer on port {LocalHttpPort}: {ex}");
                var errLabel = new Label($"ローカル HTTP サーバの起動に失敗しました: {ex.Message}") {
                    style = {
                        marginBottom = 8, unityTextAlign = TextAnchor.MiddleLeft, color = new Color(0.8f, 0.2f, 0.2f)
                    }
                };
                content.Add(errLabel);
            }

            content.schedule.Execute(() => Application.OpenURL(BoothLibraryUrl));

            content.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                try {
                    if (HttpServer.IsRunning) HttpServer.Stop();
                }
                catch (Exception ex) {
                    Debug.LogWarning($"Error while stopping local HttpServer from detach event: {ex}");
                }
            });

            return content;
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;

            try {
                if (HttpServer.IsRunning) HttpServer.Stop();
            }
            catch (Exception ex) {
                Debug.LogWarning($"Error while stopping local HttpServer: {ex}");
            }

            container?.RemoveFromHierarchy();
        }
    }
}