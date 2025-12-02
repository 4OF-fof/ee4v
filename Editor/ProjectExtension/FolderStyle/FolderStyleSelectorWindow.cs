using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.API;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI.Component;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace _4OF.ee4v.ProjectExtension.FolderStyle {
    public class FolderStyleSelectorWindow : BaseWindow {
        private List<string> _pathList = new();

        public static void Open(string path, Vector2 screenPosition) {
            OpenInternal(new List<string> { path }, screenPosition);
        }

        public static void Open(List<string> pathList, Vector2 screenPosition) {
            OpenInternal(pathList, screenPosition);
        }

        private static void OpenInternal(List<string> pathList, Vector2 screenPosition) {
            var window = OpenSetup<FolderStyleSelectorWindow>(screenPosition);
            window.IsLocked = true;
            window._pathList = pathList;

            window.position = new Rect(screenPosition.x, screenPosition.y, 340, 120);

            window.ShowPopup();
            window.UpdateContent();

            EditorApplication.delayCall += FocusProjectWindow;
        }

        private static void FocusProjectWindow() {
            if (ReflectionWrapper.ProjectBrowserType != null)
                FocusWindowIfItsOpen(ReflectionWrapper.ProjectBrowserType);
        }

        private void UpdateContent() {
            rootVisualElement.Clear();
            CreateGUI();
        }

        protected override VisualElement HeaderContent() {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 24,
                    flexGrow = 1
                }
            };
            if (_pathList == null) return root;

            var titleText = _pathList.Count == 1
                ? Path.GetFileName(_pathList[0])
                : I18N.Get("UI.ProjectExtension.SelectedObjectsFmt", _pathList.Count);
            var titleLabel = new Label(titleText) {
                tooltip = _pathList[0],
                style = {
                    marginLeft = 8,
                    flexShrink = 1,
                    fontSize = 14,
                    marginRight = 4,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            root.Add(titleLabel);
            return root;
        }

        protected override VisualElement Content() {
            var root = base.Content();
            var colorSelector = new ColorSelector(_pathList);
            var anyHasIcon = false;

            if (_pathList != null)
                if (_pathList.Any(p =>
                    {
                        var guid = AssetDatabase.AssetPathToGUID(p);
                        var s = FolderStyleList.instance.Contents.FirstOrDefault(x => x.guid == guid);
                        return s?.icon != null || !string.IsNullOrEmpty(s?.assetUlid);
                    }))
                    anyHasIcon = true;

            if (anyHasIcon) colorSelector.SetEnabled(false);
            root.Add(colorSelector);
            root.Add(Spacer(12));

            var iconFiled = new ObjectField(I18N.Get("UI.ProjectExtension.Icon")) {
                objectType = typeof(Texture)
            };

            if (_pathList is { Count: 1 }) {
                var guid = AssetDatabase.AssetPathToGUID(_pathList[0]);
                var style = FolderStyleList.instance.Contents.FirstOrDefault(s => s.guid == guid);
                if (style?.icon != null) iconFiled.value = style.icon;
            }

            iconFiled.RegisterValueChangedCallback(evt =>
            {
                var newIcon = evt.newValue as Texture;
                foreach (var p in _pathList) {
                    var guid = AssetDatabase.AssetPathToGUID(p);
                    if (string.IsNullOrEmpty(guid)) continue;

                    var idx = FolderStyleService.IndexOfGuid(guid);
                    if (idx == -1)
                        FolderStyleList.instance.AddFolderStyle(guid, Color.clear, newIcon, "");
                    else
                        FolderStyleList.instance.UpdateFolderStyle(idx, icon: newIcon, setIcon: true, assetUlid: "");
                }

                var hasIcon = newIcon != null;
                colorSelector.SetEnabled(!hasIcon);
                UpdateContent();
            });

            root.Add(iconFiled);

            if (_pathList is not { Count: 1 }) return root;
            var folderGuid = AssetDatabase.AssetPathToGUID(_pathList[0]);
            var folderStyle = FolderStyleList.instance.Contents.FirstOrDefault(s => s.guid == folderGuid);

            var assets = AssetManagerAPI.GetAssetsAssociatedWithGuid(folderGuid);
            var hasAssets = assets.Count > 0;
            var currentLinkedUlid = folderStyle?.assetUlid;
            var isLinked = !string.IsNullOrEmpty(currentLinkedUlid);

            root.Add(Spacer(8));

            if (isLinked) {
                var unlinkBtn = new Button(() =>
                {
                    var idx = FolderStyleService.IndexOfGuid(folderGuid);
                    if (idx != -1) FolderStyleList.instance.UpdateFolderStyle(idx, assetUlid: "");
                    UpdateContent();
                }) {
                    text = I18N.Get("UI.ProjectExtension.UnlinkAutoIcon"),
                    style = { height = 24 }
                };
                root.Add(unlinkBtn);
            }
            else {
                var selectBtn = new Button(() =>
                {
                    GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    var pos = position.position + new Vector2(20, 80);

                    AutoIconSelectorWindow.Open(folderGuid, pos, selectedUlid =>
                    {
                        var idx = FolderStyleService.IndexOfGuid(folderGuid);
                        if (idx == -1)
                            FolderStyleList.instance.AddFolderStyle(folderGuid, Color.clear, null, selectedUlid);
                        else
                            FolderStyleList.instance.UpdateFolderStyle(idx, assetUlid: selectedUlid, icon: null,
                                setIcon: true);

                        UpdateContent();
                    });
                }) {
                    text = I18N.Get("UI.ProjectExtension.AutoIconFromAssets"),
                    style = { height = 24 }
                };

                selectBtn.SetEnabled(hasAssets);
                if (!hasAssets) selectBtn.tooltip = I18N.Get("UI.ProjectExtension.NoAssociatedAssetsTooltip");
                root.Add(selectBtn);
            }

            return root;
        }
    }
}