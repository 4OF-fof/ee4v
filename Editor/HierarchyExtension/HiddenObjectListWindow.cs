using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _4OF.ee4v.HierarchyExtension {
    public class HiddenObjectListWindow : BaseWindow {
        private readonly HashSet<GameObject> _expandedObjects = new();
        private readonly HashSet<GameObject> _selectedObjects = new();
        private readonly List<TreeNode> _treeNodes = new();
        private VisualElement _treeContainer;

        [MenuItem("ee4v/Window/Hidden Object")]
        private static void OpenFromMenu() {
            var pos = new Vector2(300, 100);
            Open(pos);
        }

        public static void Open(Vector2 screenPosition) {
            var window = OpenSetup<HiddenObjectListWindow>(screenPosition);
            window.position = new Rect(window.position.x, window.position.y, 340, 500);
            window.IsLocked = false;
            window.BuildTree();
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

            var titleLabel = new Label(I18N.Get("UI.HierarchyExtension.HiddenObjectWindowTitle")) {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    flexGrow = 1,
                    marginRight = 4,
                    marginLeft = 16
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
                    paddingBottom = 40
                }
            };

            _treeContainer = new VisualElement {
                style = { flexGrow = 1 }
            };
            scrollArea.Add(_treeContainer);

            var buttonRow = new VisualElement {
                style = {
                    position = Position.Absolute,
                    flexDirection = FlexDirection.Row,
                    left = 0,
                    right = 0,
                    bottom = 8,
                    marginRight = 8,
                    marginLeft = 8
                }
            };

            var restoreSelectedButton = new Button(RestoreSelected) {
                text = I18N.Get("UI.HierarchyExtension.RestoreSelected"),
                style = {
                    flexGrow = 1,
                    height = 28,
                    backgroundColor = ColorPreset.WarningButton,
                    borderTopRightRadius = 10,
                    borderTopLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    borderBottomLeftRadius = 10
                }
            };
            buttonRow.Add(restoreSelectedButton);

            root.Add(scrollArea);
            root.Add(buttonRow);

            RenderTree();

            return root;
        }

        private void BuildTree() {
            _treeNodes.Clear();
            _expandedObjects.Clear();
            var hiddenObjects = new HashSet<GameObject>();

            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObj in rootObjects)
                    CollectHiddenObjects(rootObj, hiddenObjects);
            }

            var processedObjects = new HashSet<GameObject>();
            foreach (var hiddenObj in hiddenObjects.Where(hiddenObj =>
                         hiddenObj != null && !processedObjects.Contains(hiddenObj)))
                BuildNodeHierarchy(hiddenObj, hiddenObjects, processedObjects);

            ExpandAllNodes(_treeNodes);
        }

        private void ExpandAllNodes(List<TreeNode> nodes) {
            foreach (var node in nodes)
                if (node?.GameObject != null && node.Children.Count > 0) {
                    _expandedObjects.Add(node.GameObject);
                    ExpandAllNodes(node.Children);
                }
        }

        private static void CollectHiddenObjects(GameObject obj, HashSet<GameObject> collection) {
            if (obj == null) return;

            if ((obj.hideFlags & HideFlags.HideInHierarchy) != 0)
                if (!SettingSingleton.I.hiddenItemList.Contains(obj.name))
                    collection.Add(obj);

            var transform = obj.transform;
            for (var i = 0; i < transform.childCount; i++)
                CollectHiddenObjects(transform.GetChild(i).gameObject, collection);
        }

        private void BuildNodeHierarchy(GameObject hiddenObj, HashSet<GameObject> hiddenObjects,
            HashSet<GameObject> processedObjects) {
            if (hiddenObj == null || processedObjects.Contains(hiddenObj)) return;

            var hierarchy = new List<GameObject>();
            var current = hiddenObj;
            while (current != null) {
                hierarchy.Add(current);
                current = current.transform.parent?.gameObject;
            }

            hierarchy.Reverse();

            TreeNode parentNode = null;
            foreach (var obj in hierarchy) {
                var existingNode = FindNode(obj, parentNode);
                if (existingNode != null) {
                    parentNode = existingNode;
                    continue;
                }

                var isHidden = hiddenObjects.Contains(obj);
                var node = new TreeNode {
                    GameObject = obj,
                    IsHidden = isHidden,
                    Children = new List<TreeNode>()
                };

                if (parentNode == null)
                    _treeNodes.Add(node);
                else
                    parentNode.Children.Add(node);

                parentNode = node;
                processedObjects.Add(obj);
            }
        }

        private TreeNode FindNode(GameObject obj, TreeNode parent) {
            var searchList = parent == null ? _treeNodes : parent.Children;
            return searchList.FirstOrDefault(n => n.GameObject == obj);
        }

        private void RenderTree() {
            if (_treeContainer == null) return;

            _treeContainer.Clear();

            if (_treeNodes.Count == 0) {
                var emptyLabel = new Label(I18N.Get("UI.HierarchyExtension.NoHiddenObjectsFound")) {
                    style = {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        marginTop = 20,
                        marginBottom = 20,
                        opacity = 0.5f
                    }
                };
                _treeContainer.Add(emptyLabel);
                return;
            }

            foreach (var node in _treeNodes)
                RenderNode(node, 0);
        }

        private void RenderNode(TreeNode node, int depth) {
            if (node?.GameObject == null) return;

            var obj = node.GameObject;
            var isExpanded = _expandedObjects.Contains(obj);
            var hasChildren = node.Children.Count > 0;

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 18,
                    paddingLeft = depth * 14 + 2,
                    paddingRight = 2
                }
            };

            if (hasChildren) {
                var foldoutButton = new Button(() => ToggleExpand(obj)) {
                    text = isExpanded ? "▼" : "▶",
                    style = {
                        width = 12,
                        height = 12,
                        marginRight = 1,
                        paddingLeft = 0,
                        paddingRight = 0,
                        paddingTop = 0,
                        paddingBottom = 0,
                        fontSize = 8,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        backgroundColor = Color.clear,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        borderTopWidth = 0,
                        borderBottomWidth = 0
                    }
                };
                row.Add(foldoutButton);
            }
            else {
                var spacer = new VisualElement {
                    style = { width = 13, height = 12 }
                };
                row.Add(spacer);
            }

            Toggle checkbox = null;
            if (node.IsHidden) {
                checkbox = new Toggle {
                    value = _selectedObjects.Contains(obj),
                    style = {
                        width = 14,
                        height = 14,
                        marginRight = 4,
                        marginLeft = 2
                    }
                };
                checkbox.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                        _selectedObjects.Add(obj);
                    else
                        _selectedObjects.Remove(obj);
                });
                row.Add(checkbox);
            }
            else {
                var spacer = new VisualElement {
                    style = { width = 18, height = 14 }
                };
                row.Add(spacer);
            }

            var icon = new Image {
                image = AssetPreview.GetMiniThumbnail(obj),
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    width = 14,
                    height = 14,
                    marginRight = 3
                }
            };
            row.Add(icon);

            var nameLabel = new Label(obj.name) {
                style = {
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    fontSize = 11,
                    opacity = node.IsHidden ? 1.0f : 0.5f,
                    unityFontStyleAndWeight = node.IsHidden ? FontStyle.Normal : FontStyle.Italic,
                    paddingTop = 0,
                    paddingBottom = 0
                }
            };
            row.Add(nameLabel);

            if (node.IsHidden)
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (checkbox != null && evt.target != checkbox) {
                        var currentValue = _selectedObjects.Contains(obj);
                        if (currentValue)
                            _selectedObjects.Remove(obj);
                        else
                            _selectedObjects.Add(obj);
                        checkbox.value = !currentValue;
                    }

                    Selection.activeGameObject = obj;
                    EditorGUIUtility.PingObject(obj);
                });

            _treeContainer.Add(row);

            if (!isExpanded || !hasChildren) return;
            foreach (var child in node.Children)
                RenderNode(child, depth + 1);
        }

        private void ToggleExpand(GameObject obj) {
            if (!_expandedObjects.Add(obj))
                _expandedObjects.Remove(obj);

            RenderTree();
        }

        private void RestoreSelected() {
            if (_selectedObjects.Count == 0) return;

            var objectsToRestore = _selectedObjects.Where(obj => obj != null).ToList();
            if (objectsToRestore.Count == 0) return;

            Undo.RecordObjects(objectsToRestore.Select(obj => obj as Object).ToArray(),
                I18N.Get("UI.HierarchyExtension.RestoreSelectedHiddenGameObjects"));

            foreach (var obj in objectsToRestore) {
                obj.hideFlags &= ~HideFlags.HideInHierarchy;
                obj.SetActive(true);
                if (obj.CompareTag("EditorOnly")) obj.tag = "Untagged";
                EditorUtility.SetDirty(obj);
            }

            EditorApplication.RepaintHierarchyWindow();

            _selectedObjects.Clear();
            BuildTree();
            RenderTree();
        }

        private class TreeNode {
            public List<TreeNode> Children;
            public GameObject GameObject;
            public bool IsHidden;
        }
    }
}