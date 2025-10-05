using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyItem.Window {
    public class HiddenObjectList : BaseWindow {
        private List<GameObject> _hiddenObjects = new();
        private VisualElement _listContainer;

        public static void Open(Vector2 screenPosition) {
            var window = OpenSetup<HiddenObjectList>(screenPosition);
            window.position = new Rect(window.position.x, window.position.y, 600, 400);
            window.IsLocked = false;
            window.RefreshHiddenObjects();
            window.ShowPopup();
        }

        protected override VisualElement HeaderContent() {
            var header = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 24,
                    flexGrow = 1
                }
            };

            var titleLabel = new Label("Hidden Objects") {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    flexGrow = 1,
                    marginRight = 4, marginLeft = 16
                }
            };
            header.Add(titleLabel);

            return header;
        }

        protected override VisualElement Content() {
            var root = base.Content();

            root.style.position = Position.Relative;

            var scrollArea = new ScrollView {
                style = {
                    flexGrow = 1,
                    paddingBottom = 45
                }
            };

            _listContainer = new VisualElement {
                style = { flexGrow = 1 }
            };
            scrollArea.Add(_listContainer);

            var buttonRow = new VisualElement {
                style = {
                    position = Position.Absolute,
                    flexDirection = FlexDirection.Row,
                    left = 0, right = 0, bottom = 8,
                    marginRight = 4, marginLeft = 4
                }
            };

            var refreshButton = new Button(RefreshList) {
                text = "Refresh",
                style = {
                    flexGrow = 1,
                    height = 24,
                    marginRight = 4,
                    borderTopRightRadius = 10, borderTopLeftRadius = 10,
                    borderBottomRightRadius = 10, borderBottomLeftRadius = 10
                }
            };
            buttonRow.Add(refreshButton);

            var restoreAllButton = new Button(RestoreAll) {
                text = "Restore All",
                style = {
                    flexGrow = 1,
                    height = 24,
                    backgroundColor = ColorPreset.WarningButton,
                    borderTopRightRadius = 10, borderTopLeftRadius = 10,
                    borderBottomRightRadius = 10, borderBottomLeftRadius = 10
                }
            };
            buttonRow.Add(restoreAllButton);

            root.Add(scrollArea);
            root.Add(buttonRow);

            UpdateList();

            return root;
        }

        private void RefreshList() {
            RefreshHiddenObjects();
            UpdateList();
        }

        private void RefreshHiddenObjects() {
            _hiddenObjects.Clear();
            var allObjects = new List<GameObject>();

            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();

                foreach (var rootObj in rootObjects) CollectHiddenObjects(rootObj, allObjects);
            }

            _hiddenObjects = allObjects;
        }

        private static void CollectHiddenObjects(GameObject obj, List<GameObject> collection) {
            if (obj == null) return;

            if ((obj.hideFlags & HideFlags.HideInHierarchy) != 0) {
                if (EditorPrefsManager.HiddenItemList.Contains(obj.name)) return;
                collection.Add(obj);
            }

            var transform = obj.transform;
            for (var i = 0; i < transform.childCount; i++)
                CollectHiddenObjects(transform.GetChild(i).gameObject, collection);
        }

        private void UpdateList() {
            if (_listContainer == null) return;

            _listContainer.Clear();

            if (_hiddenObjects == null || _hiddenObjects.Count == 0) {
                var emptyLabel = new Label("No hidden objects found") {
                    style = {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        marginTop = 20, marginBottom = 20,
                        opacity = 0.5f
                    }
                };
                _listContainer.Add(emptyLabel);
                return;
            }

            foreach (var obj in _hiddenObjects) {
                if (obj == null) continue;

                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 2,
                        paddingTop = 4, paddingBottom = 4,
                        paddingLeft = 4, paddingRight = 4,
                        backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f),
                        borderTopRightRadius = 4, borderTopLeftRadius = 4,
                        borderBottomRightRadius = 4, borderBottomLeftRadius = 4
                    }
                };

                var icon = new Image {
                    image = AssetPreview.GetMiniThumbnail(obj),
                    scaleMode = ScaleMode.ScaleToFit,
                    style = {
                        width = 16, height = 16,
                        marginRight = 4
                    }
                };
                row.Add(icon);

                var path = GetHierarchyPath(obj);
                var displayName = obj.name;
                if (!string.IsNullOrEmpty(path) && path != obj.name) displayName = $"{obj.name} ({path})";

                var nameLabel = new Label(displayName) {
                    style = {
                        flexGrow = 1, flexShrink = 1,
                        minWidth = 0,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        marginRight = 4
                    }
                };
                row.Add(nameLabel);

                var restoreButton = new Button(() => RestoreObject(obj)) {
                    text = "Restore",
                    style = {
                        width = 60, height = 20,
                        flexShrink = 0,
                        fontSize = 10,
                        borderTopRightRadius = 4, borderTopLeftRadius = 4,
                        borderBottomRightRadius = 4, borderBottomLeftRadius = 4
                    }
                };
                row.Add(restoreButton);

                _listContainer.Add(row);
            }
        }

        private static string GetHierarchyPath(GameObject obj) {
            if (obj == null) return string.Empty;

            var parts = new List<string>();
            var t = obj.transform;
            while (t != null) {
                parts.Add(t.name);
                t = t.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private void RestoreObject(GameObject obj) {
            if (obj == null) return;

            Undo.RecordObject(obj, "Restore Hidden GameObject");
            obj.hideFlags &= ~HideFlags.HideInHierarchy;
            obj.SetActive(true);
            if (obj.CompareTag("EditorOnly")) obj.tag = "Untagged";

            EditorUtility.SetDirty(obj);
            EditorApplication.RepaintHierarchyWindow();

            RefreshList();
        }

        private void RestoreAll() {
            if (_hiddenObjects == null || _hiddenObjects.Count == 0) return;

            var objectsToRestore = _hiddenObjects.Where(obj => obj != null).ToList();
            if (objectsToRestore.Count == 0) return;

            Undo.RecordObjects(objectsToRestore.Select(obj => obj as Object).ToArray(),
                "Restore All Hidden GameObjects");

            foreach (var obj in objectsToRestore) {
                obj.hideFlags &= ~HideFlags.HideInHierarchy;
                obj.SetActive(true);
                if (obj.CompareTag("EditorOnly")) obj.tag = "Untagged";

                EditorUtility.SetDirty(obj);
            }

            EditorApplication.RepaintHierarchyWindow();
            RefreshList();
        }
    }
}