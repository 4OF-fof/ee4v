using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.ProjectExtension.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab {
    public static class WorkspaceTab {
        public enum State {
            Default,
            Selected
        }

        public static VisualElement Element(string path, string name = null, State state = State.Default) {
            if (string.IsNullOrEmpty(name)) name = Path.GetFileName(path);

            var tabLabel = TabLabel.Draw(name, path);

            var folderIcon = new Image {
                image = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D,
                style = {
                    width = 16,
                    height = 16
                }
            };

            var tab = new VisualElement {
                name = "ee4v-project-toolbar-workspaceContainer-tab",
                style = {
                    alignItems = Align.Center,
                    flexDirection = FlexDirection.Row,
                    height = Length.Percent(95),
                    marginTop = 1,
                    paddingLeft = 4,
                    backgroundColor = ColorPreset.TabBackground,
                    borderRightWidth = 1,
                    borderTopRightRadius = 4, borderTopLeftRadius = 4,
                    borderRightColor = ColorPreset.TabBorder
                },
                userData = state
            };
            SetState(tab, state);

            tab.RegisterCallback<MouseEnterEvent>(_ =>
            {
                var current = GetState(tab);
                tab.style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabHoveredBackground;
            });

            tab.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var current = GetState(tab);
                tab.style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabBackground;
            });

            tab.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1) {
                    evt.StopPropagation();
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(I18N.Get("UI.ProjectExtension.CloseTab")), false,
                        () => { TabListController.Remove(tab); });
                    menu.ShowAsContext();
                }
            });

            tab.Add(folderIcon);
            tab.Add(tabLabel);

            tab.RegisterCallback<DragEnterEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });

            tab.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });

            tab.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                    return;

                // 先頭大文字じゃないと壊れるので注意
                var labelName = $"Ee4v.ws.{name}";
                
                foreach (var obj in DragAndDrop.objectReferences) {
                    if (obj == null) continue;
                    
                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    var labels = AssetDatabase.GetLabels(obj).ToList();

                    if (labels.Contains(labelName)) continue;
                    
                    labels.Add(labelName);
                    AssetDatabase.SetLabels(obj, labels.ToArray());
                }

                DragAndDrop.AcceptDrag();
                AssetDatabase.SaveAssets();
                TabListController.SelectTab(tab);
                evt.StopPropagation();
            });

            tab.RegisterCallback<DragLeaveEvent>(evt =>
            {
                evt.StopPropagation();
            });

            return tab;
        }
        private static State GetState(VisualElement tab) {
            return tab.userData is State s ? s : State.Default;
        }

        public static void SetState(VisualElement tab, State state) {
            tab.userData = state;
            switch (state) {
                case State.Selected:
                    tab.style.backgroundColor = ColorPreset.TabSelectedBackground;
                    break;
                case State.Default:
                default:
                    tab.style.backgroundColor = ColorPreset.TabBackground;
                    break;
            }
        }
    }
}