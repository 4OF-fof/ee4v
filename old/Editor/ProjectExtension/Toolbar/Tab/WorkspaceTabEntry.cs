using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Tab {
    public sealed class WorkspaceTabEntry : BaseTab {
        public WorkspaceTabEntry(string path, string name = null, State state = State.Default) : base(path) {
            if (string.IsNullOrEmpty(name))
                name = Path.GetFileName(path);

            this.name = "ee4v-project-toolbar-workspaceContainer-tab";

            var folderIcon = new Image {
                image = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D,
                style = { width = 16, height = 16 }
            };
            var tabLabel = new TabLabel(name);

            Add(folderIcon);
            Add(tabLabel);

            RegisterCallback<MouseDownEvent>(OnRightClick);
            RegisterCallback<DragEnterEvent>(OnDragEnter);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(evt => OnDragPerform(evt, name));
            RegisterCallback<DragLeaveEvent>(evt => evt.StopPropagation());

            SetState(state);
        }

        private void OnRightClick(MouseDownEvent evt) {
            if (evt.button != 1) return;
            evt.StopPropagation();

            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent(I18N.Get("UI.ProjectExtension.CloseTab")),
                false,
                () => { TabManager.Remove(this); }
            );
            menu.ShowAsContext();
        }

        private static void OnDragEnter(DragEnterEvent evt) {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private static void OnDragUpdated(DragUpdatedEvent evt) {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt, string workspaceName) {
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                return;

            var labelName = $"Ee4v.ws.{workspaceName}";

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
            TabManager.SelectTab(this);
            evt.StopPropagation();
        }
    }
}