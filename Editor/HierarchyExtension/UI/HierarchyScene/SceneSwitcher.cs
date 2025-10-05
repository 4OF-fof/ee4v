﻿using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

using System.IO;
using System.Linq;
using System.Collections.Generic;

using _4OF.ee4v.Core.UI;
using _4OF.ee4v.HierarchyExtension.Data;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyScene {
    public class SceneSwitcher: EditorWindow {
        private readonly Dictionary<VisualElement, ItemState> _itemStates = new();
        
        private class ItemState {
            public Vector2 Start;
            public bool IsDragging;
        }
        
        public static void Open(Rect sceneRect) {
            var window = CreateInstance<SceneSwitcher>();
            window.ShowAsDropDown(sceneRect, new Vector2(200, 300));
        }

        public void CreateGUI() {
            var borderColor = ColorPreset.WindowBorder;
            rootVisualElement.style.borderRightWidth = 2;
            rootVisualElement.style.borderLeftWidth = 2;
            rootVisualElement.style.borderTopWidth = 2;
            rootVisualElement.style.borderBottomWidth = 2;

            rootVisualElement.style.borderRightColor = borderColor;
            rootVisualElement.style.borderLeftColor = borderColor;
            rootVisualElement.style.borderTopColor = borderColor;
            rootVisualElement.style.borderBottomColor = borderColor;

            var searchBar = new ToolbarSearchField {
                style = { width = 188, height = 16 }
            };
            rootVisualElement.Add(searchBar);

            var scenePaths = SceneListController.ScenePathList;
            var allScenePaths = scenePaths.ToList();

            var displayedPaths = allScenePaths;

            var sceneListView = new ListView {
                style = { flexGrow = 1 },
                itemsSource = displayedPaths,
                selectionType = SelectionType.Single,
                reorderable = true,
                makeItem = () => {
                    var container = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    var icon = new Image { style = { width = 16, height = 16, marginRight = 6, marginLeft = 6 } };
                    var label = new Label();
                    container.Add(icon);
                    container.Add(label);

                    container.RegisterCallback<PointerDownEvent>(evt => {
                        _itemStates[container] = new ItemState { Start = evt.position, IsDragging = false };
                    });
                    container.RegisterCallback<PointerMoveEvent>(evt => {
                        if (!_itemStates.TryGetValue(container, out var st) || st.IsDragging) return;
                        if (Vector2.Distance(st.Start, evt.position) > 4f) st.IsDragging = true;
                    });
                    container.RegisterCallback<PointerUpEvent>(evt => {
                        if (!_itemStates.TryGetValue(container, out var st)) return;
                        var pos = evt.position;
                        if (float.IsNegativeInfinity(pos.x)) return;
                        if (!st.IsDragging) {
                            var path = container.userData as string;
                            if (!string.IsNullOrEmpty(path)) {
                                var openScenePathsNow = GetOpenScenePaths();
                                if (!openScenePathsNow.Contains(path)) {
                                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                        EditorSceneManager.OpenScene(path);
                                        Close();
                                    }
                                }
                            }
                        }
                        _itemStates.Remove(container);
                    });

                    return container;
                }
            };

            searchBar.RegisterValueChangedCallback(evt => {
                var query = evt.newValue ?? string.Empty;
                if (string.IsNullOrEmpty(query)) {
                    displayedPaths = allScenePaths;
                } else {
                    var lower = query.ToLowerInvariant();
                    displayedPaths = allScenePaths.Where(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(lower)).ToList();
                }
                sceneListView.itemsSource = displayedPaths;
                sceneListView.RefreshItems();
            });

            sceneListView.bindItem = (element, i) => {
                var icon = element.Q<Image>();
                var label = element.Q<Label>();
                var path = (string)sceneListView.itemsSource[i];
                label.text = Path.GetFileNameWithoutExtension(path);

                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset != null) {
                    var content = EditorGUIUtility.ObjectContent(asset, asset.GetType());
                    icon.image = content.image;
                } else {
                    icon.image = null;
                }
                var openScenePathsNow = GetOpenScenePaths();
                element.SetEnabled(!openScenePathsNow.Contains(path));
                element.userData = path;
            };

            sceneListView.RegisterCallback<DragPerformEvent>(evt => {
                var dev = evt.GetType().Name;
                if (string.IsNullOrEmpty(dev)) return;
                ApplyReordered(displayedPaths);
            });
            sceneListView.RegisterCallback<DragExitedEvent>(evt => {
                var dev = evt.GetType().Name;
                if (string.IsNullOrEmpty(dev)) return;
                ApplyReordered(displayedPaths);
            });

            rootVisualElement.Add(sceneListView);
        }

        private static void ApplyReordered(List<string> newOrder) {
            var current = SceneListController.ScenePathList;
            if (current == null || newOrder == null) return;
            var working = new List<string>(current);
            for (var to = 0; to < newOrder.Count; to++) {
                var path = newOrder[to];
                if (to < 0 || to >= working.Count) continue;
                if (working[to] == path) continue;
                var from = working.IndexOf(path);
                if (from < 0) continue;
                SceneListController.Move(from, to);
                var item = working[from];
                working.RemoveAt(from);
                working.Insert(to, item);
            }
        }
        
        private static HashSet<string> GetOpenScenePaths() {
            var set = new HashSet<string>();
            for (var i = 0; i < SceneManager.sceneCount; ++i) {
                var s = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(s.path)) set.Add(s.path);
            }
            return set;
        }
    }
}