using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Component.Tab {
    public class WorkspaceTabEntry : VisualElement {
        public enum State {
            Default,
            Selected
        }

        public WorkspaceTabEntry(string path, string name = null, State state = State.Default) {
            if (string.IsNullOrEmpty(name))
                name = Path.GetFileName(path);

            this.name = "ee4v-project-toolbar-workspaceContainer-tab";
            userData = state;

            style.alignItems = Align.Center;
            style.flexDirection = FlexDirection.Row;
            style.height = Length.Percent(95);
            style.marginTop = 1;
            style.paddingLeft = 4;
            style.backgroundColor = ColorPreset.TabBackground;
            style.borderRightWidth = 1;
            style.borderTopRightRadius = 4;
            style.borderTopLeftRadius = 4;
            style.borderRightColor = ColorPreset.TabBorder;

            var tabLabel = new TabLabel(name);

            var folderIcon = new Image {
                image = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D,
                style = {
                    width = 16,
                    height = 16
                }
            };

            SetState(this, state);

            RegisterCallback<MouseEnterEvent>(_ =>
            {
                var current = GetState(this);
                style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabHoveredBackground;
            });

            RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var current = GetState(this);
                style.backgroundColor = current == State.Selected
                    ? ColorPreset.TabSelectedBackground
                    : ColorPreset.TabBackground;
            });

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                evt.StopPropagation();

                var menu = new GenericMenu();
                menu.AddItem(
                    new GUIContent(I18N.Get("UI.ProjectExtension.CloseTab")),
                    false,
                    () => { TabManager.Remove(this); }
                );
                menu.ShowAsContext();
            });

            RegisterCallback<DragEnterEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });

            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });

            RegisterCallback<DragPerformEvent>(evt =>
            {
                if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                    return;

                // 先頭大文字じゃないと壊れるので注意
                var labelName = $"Ee4v.ws.{name}";

                foreach (var obj in DragAndDrop.objectReferences) {
                    if (obj == null)
                        continue;

                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    var labels = AssetDatabase.GetLabels(obj).ToList();

                    if (labels.Contains(labelName))
                        continue;

                    labels.Add(labelName);
                    AssetDatabase.SetLabels(obj, labels.ToArray());
                }

                DragAndDrop.AcceptDrag();
                AssetDatabase.SaveAssets();
                TabManager.SelectTab(this);
                evt.StopPropagation();
            });

            RegisterCallback<DragLeaveEvent>(evt => { evt.StopPropagation(); });

            Add(folderIcon);
            Add(tabLabel);
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