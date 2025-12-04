using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AvatarModifyUtility {
    public class MaterialListWindow : EditorWindow, IEditorUtility {
        private readonly List<MaterialData> _materialList = new();
        private GameObject _currentRoot;
        private bool _isManualSelection;
        private ScrollView _scrollView;
        private Label _statusLabel;

        private ObjectField _targetObjectField;

        private void OnEnable() {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable() {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        public void CreateGUI() {
            var root = rootVisualElement;
            root.style.backgroundColor = ColorPreset.DefaultBackground;

            var toolbar = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 42,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = ColorPreset.WindowHeader,
                    borderBottomWidth = 1,
                    borderBottomColor = ColorPreset.WindowBorder,
                    flexShrink = 0
                }
            };

            var label = new Label(I18N.Get("UI.AvatarModifyUtility.MaterialList.Target")) {
                style = {
                    marginRight = 8,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = ColorPreset.TextColor
                }
            };
            toolbar.Add(label);

            _targetObjectField = new ObjectField {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                style = {
                    flexGrow = 1,
                    maxWidth = 250,
                    height = 20
                }
            };

            _targetObjectField.SetEnabled(true);
            _targetObjectField.RegisterValueChangedCallback(evt =>
            {
                var newObj = evt.newValue as GameObject;
                if (newObj == _currentRoot) return;

                _isManualSelection = true;
                _currentRoot = newObj;
                if (_currentRoot != null) AnalyzeMaterialUsage();
                RebuildUI();
                _isManualSelection = false;
            });

            toolbar.Add(_targetObjectField);

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            toolbar.Add(spacer);

            var refreshBtn = new Button(OnRefreshClicked) {
                text = I18N.Get("UI.AvatarModifyUtility.MaterialList.Refresh"),
                style = {
                    height = 24,
                    width = 60,
                    backgroundColor = ColorPreset.DefaultBackground
                }
            };
            toolbar.Add(refreshBtn);

            root.Add(toolbar);

            _scrollView = new ScrollView {
                style = {
                    flexGrow = 1,
                    paddingTop = 10,
                    paddingBottom = 10
                }
            };
            root.Add(_scrollView);

            _statusLabel = new Label {
                style = {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 40,
                    color = ColorPreset.InActiveItem,
                    fontSize = 14,
                    whiteSpace = WhiteSpace.Normal
                }
            };

            RebuildUI();
        }

        public string Name => "Material List";
        public string Description => I18N.Get("_System.AvatarModifyUtility.MaterialList.Description");
        public string Trigger => I18N.Get("_System.AvatarModifyUtility.MaterialList.Trigger");

        [MenuItem("ee4v/Material List", false, 1)]
        public static void ShowWindow() {
            var window = GetWindow<MaterialListWindow>();
            window.titleContent = new GUIContent(I18N.Get("UI.AvatarModifyUtility.MaterialList.Title"));
            window.minSize = new Vector2(320, 300);
            window.Show();
        }

        private void OnSelectionChanged() {
            if (_isManualSelection) return;

            var activeGo = Selection.activeGameObject;
            GameObject newRoot = null;
            if (activeGo != null) newRoot = PrefabUtility.GetNearestPrefabInstanceRoot(activeGo);

            if (_currentRoot != null && activeGo != null &&
                activeGo.transform.IsChildOf(_currentRoot.transform)) return;

            _currentRoot = newRoot;
            if (_currentRoot != null) AnalyzeMaterialUsage();
            RebuildUI();
        }

        private void OnRefreshClicked() {
            if (_currentRoot != null) AnalyzeMaterialUsage();
            RebuildUI();
        }

        private void AnalyzeMaterialUsage() {
            _materialList.Clear();
            if (!_currentRoot) return;

            var renderers = _currentRoot.GetComponentsInChildren<Renderer>(true);
            var map = new Dictionary<Material, List<GameObject>>();

            foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials) {
                if (!mat) continue;

                if (!map.ContainsKey(mat)) map[mat] = new List<GameObject>();
                if (!map[mat].Contains(r.gameObject)) map[mat].Add(r.gameObject);
            }

            foreach (var kvp in map)
                _materialList.Add(new MaterialData {
                    Material = kvp.Key,
                    UsedBy = kvp.Value,
                    IsExpanded = false
                });

            _materialList.Sort((a, b) => string.CompareOrdinal(a.Material.name, b.Material.name));
        }

        private void RebuildUI() {
            if (_targetObjectField == null) return;

            _targetObjectField.SetValueWithoutNotify(_currentRoot);
            _scrollView.Clear();

            if (_currentRoot == null) {
                _statusLabel.text = I18N.Get("UI.AvatarModifyUtility.MaterialList.SelectPrefabHint");
                _scrollView.Add(_statusLabel);
                return;
            }

            if (_materialList.Count == 0) {
                _statusLabel.text = I18N.Get("UI.AvatarModifyUtility.MaterialList.NoMaterialsFound");
                _scrollView.Add(_statusLabel);
                return;
            }

            foreach (var data in _materialList) _scrollView.Add(CreateMaterialCard(data));
        }

        private VisualElement CreateMaterialCard(MaterialData data) {
            var card = new VisualElement {
                style = {
                    marginLeft = 10, marginRight = 10, marginBottom = 6,
                    borderTopWidth = 1, borderBottomWidth = 1,
                    borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = ColorPreset.WindowBorder,
                    borderBottomColor = ColorPreset.WindowBorder,
                    borderLeftColor = ColorPreset.WindowBorder,
                    borderRightColor = ColorPreset.WindowBorder,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    backgroundColor = ColorPreset.DefaultBackground,
                    overflow = Overflow.Hidden
                }
            };

            var header = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 6, paddingBottom = 6,
                    backgroundColor = ColorPreset.TransparentBlack10Style,
                    borderBottomWidth = 1,
                    borderBottomColor = ColorPreset.TransparentBlack10Style
                }
            };

            var chevron = new Label(data.IsExpanded ? "▼" : "▶") {
                style = {
                    width = 20, fontSize = 10,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ColorPreset.InActiveItem
                }
            };
            header.Add(chevron);

            var iconTex = AssetPreview.GetMiniThumbnail(data.Material);
            var icon = new Image {
                image = iconTex,
                style = {
                    width = 28, height = 28,
                    marginRight = 10,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    backgroundColor = ColorPreset.TransparentBlack20Style
                }
            };
            header.Add(icon);

            var infoContainer = new VisualElement { style = { justifyContent = Justify.Center } };
            var nameLabel = new Label(data.Material.name) {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    color = ColorPreset.TextColor,
                    marginBottom = 2
                }
            };
            var countLabel =
                new Label(I18N.Get("UI.AvatarModifyUtility.MaterialList.ObjectsUsedCountFmt", data.UsedBy.Count)) {
                    style = { fontSize = 10, color = ColorPreset.InActiveItem }
                };
            infoContainer.Add(nameLabel);
            infoContainer.Add(countLabel);
            header.Add(infoContainer);

            var headerSpacer = new VisualElement { style = { flexGrow = 1 } };
            header.Add(headerSpacer);

            var inspectBtn = new Button(() =>
            {
                var mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                var contextObj = data.UsedBy.FirstOrDefault() ?? _currentRoot;

                ComponentInspectorWindow.Open(data.Material, contextObj, mousePos);
            }) {
                text = I18N.Get("UI.AvatarModifyUtility.MaterialList.Inspect"),
                style = {
                    height = 24,
                    paddingLeft = 6, paddingRight = 6,
                    backgroundColor = ColorPreset.DefaultBackground
                }
            };

            inspectBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            header.Add(inspectBtn);


            var content = new VisualElement {
                style = {
                    display = data.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None,
                    paddingBottom = 4,
                    backgroundColor = ColorPreset.DefaultBackground
                }
            };

            header.RegisterCallback<PointerDownEvent>(evt =>
            {
                switch (evt.button) {
                    case 0:
                        data.IsExpanded = !data.IsExpanded;
                        chevron.text = data.IsExpanded ? "▼" : "▶";
                        content.style.display = data.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                        SelectObjects(data.UsedBy);
                        break;
                }

                evt.StopPropagation();
            });

            foreach (var go in data.UsedBy) {
                if (go == null) continue;

                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        height = 22,
                        paddingLeft = 34,
                        paddingRight = 10
                    }
                };

                var goIcon = new Image {
                    image = EditorGUIUtility.IconContent("GameObject Icon").image,
                    style = { width = 14, height = 14, marginRight = 4, opacity = 0.7f }
                };
                var goLabel = new Label(go.name) {
                    style = { fontSize = 11, color = ColorPreset.TextColor }
                };

                row.Add(goIcon);
                row.Add(goLabel);

                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    SelectObject(go);
                    evt.StopPropagation();
                });

                row.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    row.style.backgroundColor = ColorPreset.MouseOverBackground;
                    goIcon.style.opacity = 1f;
                });
                row.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    row.style.backgroundColor = StyleKeyword.Null;
                    goIcon.style.opacity = 0.7f;
                });

                content.Add(row);
            }

            card.Add(header);
            card.Add(content);
            return card;
        }

        private static void SelectObjects(List<GameObject> objects) {
            if (objects == null || objects.Count == 0) return;
            Selection.objects = objects.Cast<Object>().ToArray();
        }

        private static void SelectObject(GameObject go) {
            if (!go) return;
            Selection.activeGameObject = go;
        }

        private class MaterialData {
            public bool IsExpanded;
            public Material Material;
            public List<GameObject> UsedBy = new();
        }
    }
}