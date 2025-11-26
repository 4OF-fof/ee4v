using System;
using _4OF.ee4v.AssetManager.Adapter;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using _4OF.ee4v.Core.i18n;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public static class WaitBoothSyncDialog {
        private const string BoothLibraryUrl = "https://accounts.booth.pm/library";
        private const int LocalHttpPort = 58080;

        public static VisualElement CreateContent(Func<VisualElement, VisualElement> showDialogCallback = null) {
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

            var title = new Label(I18N.Get("UI.AssetManager.Dialog.WaitBoothSync.Title")) {
                style = {
                    fontSize = 18,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 14
                }
            };

            content.Add(title);

            var message = new Label(I18N.Get("UI.AssetManager.Dialog.WaitBoothSync.Message")) {
                style = {
                    marginBottom = 16, fontSize = 13, unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1,
                    width = new StyleLength(new Length(100, LengthUnit.Percent))
                }
            };
            content.Add(message);

            var userscriptNotice = new Label(I18N.Get("UI.AssetManager.Dialog.WaitBoothSync.UserscriptNotice")) {
                style = {
                    marginBottom = 8,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = ColorPreset.WarningText,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 13,
                    whiteSpace = WhiteSpace.Normal,
                    flexShrink = 1
                }
            };
            content.Add(userscriptNotice);

            var userscriptDesc1 =
                new Label(I18N.Get("UI.AssetManager.Dialog.WaitBoothSync.UserscriptDesc1")) {
                    style = {
                        marginBottom = 8, fontSize = 12, unityTextAlign = TextAnchor.MiddleLeft,
                        whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1,
                        width = new StyleLength(new Length(100, LengthUnit.Percent))
                    }
                };

            var userscriptDesc2 =
                new Label(
                    I18N.Get("UI.AssetManager.Dialog.WaitBoothSync.UserscriptDesc2")) {
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
                text = I18N.Get("UI.AssetManager.Dialog.WaitBoothSync.OpenLibrary"),
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
                text = I18N.Get("UI.AssetManager.Dialog.Button.Close"),
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
                        marginBottom = 8, unityTextAlign = TextAnchor.MiddleLeft, color = ColorPreset.WarningText
                    }
                };
                errLabel.style.color = ColorPreset.WarningText;
                content.Add(errLabel);
            }

            var seenWorking = false;

            BoothLibraryImporter.OnImportCompleted += OnImported;

            EditorApplication.update += Poll;

            content.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                try {
                    if (HttpServer.IsRunning) HttpServer.Stop();
                }
                catch (Exception ex) {
                    Debug.LogWarning($"Error while stopping local HttpServer from detach event: {ex}");
                }

                try {
                    EditorApplication.update -= Poll;
                }
                catch {
                    /* ignore */
                }

                try {
                    BoothLibraryImporter.OnImportCompleted -= OnImported;
                }
                catch {
                    /* ignore */
                }
            });

            return content;

            void Poll() {
                try {
                    var status = BoothLibraryServerState.Status ?? "waiting";
                    if (status == "working") {
                        seenWorking = true;
                    }
                    else if (seenWorking && status == "waiting") {
                        EditorApplication.update -= Poll;
                        CloseDialog(content);
                        try {
                            if (showDialogCallback != null && BoothThumbnailDownloader.TotalCount > 0)
                                showDialogCallback.Invoke(DownloadThumbnailDialog.CreateContent());
                        }
                        catch (Exception ex) {
                            Debug.LogWarning(I18N.Get("Debug.AssetManager.Dialog.WaitBoothSync.FailedOpeningDownloadThumbnailDialogFmt", ex.Message));
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.LogWarning(I18N.Get("Debug.AssetManager.Dialog.WaitBoothSync.PollingFailedFmt", ex.Message));
                }
            }

            void OnImported(int created) {
                try {
                    EditorApplication.delayCall += () =>
                    {
                        Debug.Log(I18N.Get("Debug.AssetManager.Dialog.WaitBoothSync.ImportCompletedFmt", created));
                        try {
                            EditorApplication.update -= Poll;
                        }
                        catch {
                            /* ignore */
                        }

                        try {
                            BoothLibraryImporter.OnImportCompleted -= OnImported;
                        }
                        catch {
                            /* ignore */
                        }

                        CloseDialog(content);
                        try {
                            if (BoothThumbnailDownloader.TotalCount > 0)
                                showDialogCallback?.Invoke(DownloadThumbnailDialog.CreateContent());
                        }
                        catch (Exception ex) {
                            Debug.LogWarning($"Failed opening DownloadThumbnailDialog: {ex}");
                        }
                    };
                }
                catch (Exception ex) {
                    Debug.LogWarning($"OnImported handler failed: {ex}");
                }
            }
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