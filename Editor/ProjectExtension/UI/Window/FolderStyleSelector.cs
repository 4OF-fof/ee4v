using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.Core.UI.Window._Component;
using _4OF.ee4v.ProjectExtension.Data;
using _4OF.ee4v.ProjectExtension.Service;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace _4OF.ee4v.ProjectExtension.UI.Window {
    public class FolderStyleSelector : BaseWindow {
        private List<string> _pathList = new();

        public static void Open(string path, Vector2 screenPosition) {
            var window = OpenSetup<FolderStyleSelector>(screenPosition);
            window.IsLocked = true;
            window._pathList = new List<string> { path };
            window.position = new Rect(screenPosition.x, screenPosition.y, 340, 100);
            window.UpdateContent();
            window.ShowPopup();
            EditorApplication.delayCall += FocusProjectWindow;
        }

        public static void Open(List<string> pathList, Vector2 screenPosition) {
            var window = OpenSetup<FolderStyleSelector>(screenPosition);
            window.IsLocked = true;
            window._pathList = pathList;
            window.position = new Rect(screenPosition.x, screenPosition.y, 340, 100);
            window.UpdateContent();
            window.ShowPopup();
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
                : $"Selected {_pathList.Count} Objects";
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
            var colorSelector = ColorSelector.Element(_pathList);
            var anyHasIcon = false;
            if (_pathList != null)
                if (_pathList.Any(p => {
                    var s = FolderStyleList.instance.Contents.FirstOrDefault(x => x.path == FolderStyleService.NormalizePath(p));
                    return s?.icon != null;
                }))
                    anyHasIcon = true;

            if (anyHasIcon) colorSelector.SetEnabled(false);
            root.Add(colorSelector);
            root.Add(Spacer(12));

            var iconFiled = new ObjectField(I18N.Get("UI.ProjectExtension.Icon")) {
                objectType = typeof(Texture)
            };
            if (_pathList is { Count: 1 }) {
                var existing = FolderStyleList.instance.Contents.FirstOrDefault(s => s.path == FolderStyleService.NormalizePath(_pathList[0]))?.icon;
                if (existing != null) iconFiled.value = existing;
            }

            iconFiled.RegisterValueChangedCallback(evt =>
            {
                var newIcon = evt.newValue as Texture;
                foreach (var p in _pathList) {
                    var np = FolderStyleService.NormalizePath(p);
                    var idx = FolderStyleService.IndexOfPath(np);
                    if (idx == -1)
                        FolderStyleList.instance.Add(np, Color.clear, newIcon);
                    else
                        FolderStyleList.instance.Update(idx, icon: newIcon, setIcon: true);
                }
                var hasIcon = newIcon != null;
                colorSelector.SetEnabled(!hasIcon);
            });
            root.Add(iconFiled);
            return root;
        }
    }
}