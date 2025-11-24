using System;
using _4OF.ee4v.AssetManager.Adapter;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public static class WaitBoothSyncDialog {
        private const string BoothLibraryUrl = "https://accounts.booth.pm/library";
        private const int LocalHttpPort = 58080;

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

            var title = new Label("Import from Booth") {
                style = {
                    fontSize = 18,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 14
                }
            };

            content.Add(title);

            var message = new Label("ブラウザで BOOTH のライブラリ画面を操作して\nデータを Unity に送信します。") {
                style = {
                    marginBottom = 16, fontSize = 13, unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1,
                    width = new StyleLength(new Length(100, LengthUnit.Percent))
                }
            };
            content.Add(message);

            var userscriptNotice = new Label("Userscript のインストールが必要です") {
                style = {
                    marginBottom = 8,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = new Color(0.8f, 0.2f, 0.2f),
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 13,
                    whiteSpace = WhiteSpace.Normal,
                    flexShrink = 1
                }
            };
            content.Add(userscriptNotice);

            var userscriptDesc1 =
                new Label("この機能はブラウザ側で動作する Userscript (EE4V BOOTH Library Sync) により、\nBOOTH の情報を Unity に送信します。") {
                    style = {
                        marginBottom = 8, fontSize = 12, unityTextAlign = TextAnchor.MiddleLeft,
                        whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1,
                        width = new StyleLength(new Length(100, LengthUnit.Percent))
                    }
                };

            var userscriptDesc2 =
                new Label(
                    "まだインストールしていない場合は、ブラウザ拡張 (例: Tampermonkey) に Userscript を追加して有効化してください。\n送信中はタブと Unity を閉じないでください。") {
                    style = {
                        marginBottom = 20, fontSize = 12, unityTextAlign = TextAnchor.MiddleLeft,
                        whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1,
                        width = new StyleLength(new Length(100, LengthUnit.Percent))
                    }
                };

            userscriptDesc1.style.maxWidth = 480;
            userscriptDesc2.style.maxWidth = 480;
            userscriptDesc1.style.flexShrink = 1;
            userscriptDesc2.style.flexShrink = 1;

            content.Add(userscriptDesc1);
            content.Add(userscriptDesc2);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.FlexStart, marginBottom = 12,
                    width = new StyleLength(new Length(100, LengthUnit.Percent))
                }
            };

            var openBtn = new Button(() => Application.OpenURL(BoothLibraryUrl)) {
                text = "BOOTHライブラリを開く",
                style = {
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 6,
                    paddingBottom = 6,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    fontSize = 13
                }
            };
            buttonRow.Add(openBtn);


            content.Add(buttonRow);

            var closeRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 2,
                    width = new StyleLength(new Length(100, LengthUnit.Percent))
                }
            };

            var closeBtn = new Button {
                text = "閉じる",
                style = {
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 6,
                    paddingBottom = 6,
                    fontSize = 13
                }
            };
            closeBtn.clicked += () => CloseDialog(content);
            closeBtn.style.marginRight = 0;
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