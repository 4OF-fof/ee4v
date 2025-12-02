using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.HierarchyExtension.SceneSwitcher {
    public class SceneSwitcherWindow : EditorWindow {
        private readonly Dictionary<VisualElement, ItemState> _itemStates = new();

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

            var allScenePaths = SceneListService.SortedSceneList.ToList();
            var displayedPaths = allScenePaths;

            EventCallback<GeometryChangedEvent> focusCallback = null;
            focusCallback = _ =>
            {
                searchBar.Q<TextField>()?.Focus();
                searchBar.UnregisterCallback(focusCallback);
            };
            searchBar.RegisterCallback(focusCallback);

            var sceneListView = new ListView {
                style = { flexGrow = 1 },
                itemsSource = displayedPaths,
                selectionType = SelectionType.Single,
                reorderable = true,
                makeItem = () =>
                {
                    var container = new VisualElement
                        { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    var icon = new Image { style = { width = 16, height = 16, marginRight = 6, marginLeft = 6 } };
                    var label = new Label { style = { flexGrow = 1 } };
                    var starButton = new Image {
                        style = {
                            width = 16,
                            height = 16,
                            marginRight = 4
                        },
                        name = "star-button"
                    };
                    container.Add(icon);
                    container.Add(label);
                    container.Add(starButton);

                    starButton.RegisterCallback<PointerDownEvent>(evt => { evt.StopPropagation(); });
                    starButton.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        evt.StopPropagation();
                        var path = container.userData as string;
                        if (string.IsNullOrEmpty(path) || path.StartsWith("EE4V_CREATE_NEW:")) return;

                        var idx = SceneListService.IndexOfPath(path);
                        if (idx < 0) return;
                        var isFavorite = SceneList.instance.Contents[idx].isFavorite;
                        SceneList.instance.UpdateScene(idx, isFavorite: !isFavorite);

                        var isFav = SceneList.instance.Contents.FirstOrDefault(s => s.path == path)?.isFavorite ??
                            false;
                        starButton.tintColor = isFav ? ColorPreset.FavoriteStar : ColorPreset.NonFavorite;

                        allScenePaths = SceneListService.SortedSceneList.ToList();
                        var current = searchBar.value;
                        searchBar.value = null;
                        searchBar.value = current;
                    });

                    container.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        _itemStates[container] = new ItemState { Start = evt.position, IsDragging = false };
                    });
                    container.RegisterCallback<PointerMoveEvent>(evt =>
                    {
                        if (!_itemStates.TryGetValue(container, out var st) || st.IsDragging) return;
                        if (Vector2.Distance(st.Start, evt.position) > 4f) st.IsDragging = true;
                    });
                    container.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (!_itemStates.TryGetValue(container, out var st)) return;
                        var pos = evt.position;
                        if (float.IsNegativeInfinity(pos.x)) return;
                        if (!st.IsDragging) {
                            var path = container.userData as string;
                            if (!string.IsNullOrEmpty(path)) {
                                if (path.StartsWith("EE4V_CREATE_NEW:")) {
                                    var sceneName = path["EE4V_CREATE_NEW:".Length..];
                                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                        CreateAndOpenNewScene(sceneName);
                                        Close();
                                    }
                                }
                                else {
                                    var openScenePathsNow = GetOpenScenePaths();
                                    if (!openScenePathsNow.Contains(path))
                                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                            EditorSceneManager.OpenScene(path);
                                            SceneListService.MoveToTop(path);
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

            searchBar.RegisterValueChangedCallback(evt =>
            {
                var query = evt.newValue ?? string.Empty;
                if (string.IsNullOrEmpty(query)) {
                    displayedPaths = allScenePaths;
                }
                else {
                    var lower = query.ToLowerInvariant();
                    var filtered = allScenePaths
                        .Where(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(lower)).ToList();

                    var favorites = filtered
                        .Where(p => SceneList.instance.Contents.Any(s => s.path == p && s.isFavorite)).ToList();
                    var others = filtered.Where(p => !SceneList.instance.Contents.Any(s => s.path == p && s.isFavorite))
                        .ToList();
                    displayedPaths = favorites.Concat(others).ToList();

                    var exactMatch = allScenePaths.Any(p =>
                        Path.GetFileNameWithoutExtension(p).Equals(query, StringComparison.OrdinalIgnoreCase));
                    if (!exactMatch) displayedPaths.Add($"EE4V_CREATE_NEW:{query}");
                }

                sceneListView.itemsSource = displayedPaths;
                sceneListView.RefreshItems();
            });

            sceneListView.bindItem = (element, i) =>
            {
                var icon = element.Q<Image>();
                var label = element.Q<Label>();
                var starButton = element.Q<Image>("star-button");
                var path = (string)sceneListView.itemsSource[i];

                if (path.StartsWith("EE4V_CREATE_NEW:")) {
                    var sceneName = path["EE4V_CREATE_NEW:".Length..];
                    label.text = I18N.Get("UI.HierarchyScene.CreateSceneItem", sceneName);
                    icon.image = null;
                    starButton.image = null;
                    element.SetEnabled(true);
                    element.style.opacity = 1f;
                    element.userData = path;
                    return;
                }

                label.text = Path.GetFileNameWithoutExtension(path);

                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset != null) {
                    var content = EditorGUIUtility.ObjectContent(asset, asset.GetType());
                    icon.image = content.image;
                }
                else {
                    icon.image = null;
                }

                var isFavorite = SceneList.instance.Contents.Any(s => s.path == path && s.isFavorite);
                starButton.image = EditorGUIUtility.IconContent("d_Favorite Icon").image;
                starButton.tintColor = isFavorite ? ColorPreset.FavoriteStar : ColorPreset.NonFavorite;

                var openScenePathsNow = GetOpenScenePaths();
                var isOpen = openScenePathsNow.Contains(path);
                element.style.opacity = isOpen ? 0.5f : 1f;
                starButton.style.opacity = 1f;
                element.userData = path;
            };

            sceneListView.RegisterCallback<DragPerformEvent>(evt =>
            {
                var dev = evt.GetType().Name;
                if (string.IsNullOrEmpty(dev)) return;
                ApplyReordered(displayedPaths);
            });
            sceneListView.RegisterCallback<DragExitedEvent>(evt =>
            {
                var dev = evt.GetType().Name;
                if (string.IsNullOrEmpty(dev)) return;
                ApplyReordered(displayedPaths);
            });

            rootVisualElement.Add(sceneListView);
        }

        public static void Open(Rect sceneRect) {
            var window = CreateInstance<SceneSwitcherWindow>();
            window.ShowAsDropDown(sceneRect, new Vector2(200, 300));
        }

        private static void ApplyReordered(List<string> newOrder) {
            var current = SceneListService.SortedSceneList;
            if (current == null || newOrder == null) return;
            var working = new List<string>(current);
            for (var to = 0; to < newOrder.Count; to++) {
                var path = newOrder[to];
                if (to < 0 || to >= working.Count) continue;
                if (working[to] == path) continue;
                var from = working.IndexOf(path);
                if (from < 0) continue;
                SceneList.instance.MoveScene(from, to);
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

        private static void CreateAndOpenNewScene(string sceneName) {
            var baseFolder = Settings.I.sceneCreateFolderPath;
            baseFolder = baseFolder.Replace('\\', '/').Trim();
            if (!baseFolder.EndsWith("/")) baseFolder += "/";

            if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

            var scenePath = $"{baseFolder}{sceneName}.unity";
            var templatePath = $"{baseFolder}TEMPLATE.unity";

            if (File.Exists(scenePath)) {
                Debug.LogError(I18N.Get("Debug.HierarchyExtension.SceneAlreadyExists", scenePath));
                return;
            }

            if (File.Exists(templatePath)) {
                if (AssetDatabase.CopyAsset(templatePath, scenePath)) {
                    AssetDatabase.Refresh();
                    EditorSceneManager.OpenScene(scenePath);
                }
            }
            else {
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(newScene, scenePath);
            }

            SceneListService.MoveToTop(scenePath);
        }

        private class ItemState {
            public bool IsDragging;
            public Vector2 Start;
        }
    }
}