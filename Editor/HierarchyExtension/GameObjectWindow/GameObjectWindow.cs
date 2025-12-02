using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Component;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.HierarchyExtension.GameObjectWindow.Component;
using _4OF.ee4v.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.HierarchyExtension.GameObjectWindow {
    public class GameObjectWindow : BaseWindow {
        private readonly List<ObjectStyleComponent> _objectStylComponentList = new();

        private List<GameObject> _gameObjectList = new();
        private Color? _headerColor;
        private Image _headerIconImage;
        private bool _isSubscribed;

        protected override void OnDestroy() {
            base.OnDestroy();
            if (!_isSubscribed) return;
            IconSelector.OnIconChanged -= OnIconChangedHandler;
            ColorSelector.OnColorChangedComponent -= OnColorChangedHandler;
            _isSubscribed = false;
        }

        public static void Open(GameObject obj, Vector2 screenPosition) {
            var window = OpenSetup<GameObjectWindow>(screenPosition, obj);
            window._gameObjectList.Add(obj);
            window._objectStylComponentList.Add(obj.GetComponent<ObjectStyleComponent>());
            if (window._objectStylComponentList[0] == null)
                window._objectStylComponentList[0] = obj.AddComponent<ObjectStyleComponent>();
            if (window._gameObjectList.Count == 1) {
                var comp = window._objectStylComponentList[0];
                if (comp != null) window._headerColor = comp.color;
                window.HeaderBackgroundColor = window._headerColor;
                window.UpdateHeaderBackground(window._headerColor);
            }

            window.ShowPopup();
        }

        public static void Open(GameObject[] objList, Vector2 screenPosition) {
            var window = OpenSetup<GameObjectWindow>(screenPosition, objList);
            window._gameObjectList = objList.ToList();
            foreach (var obj in objList) {
                window._objectStylComponentList.Add(obj.GetComponent<ObjectStyleComponent>());
                if (window._objectStylComponentList.Last() == null)
                    window._objectStylComponentList[^1] = obj.AddComponent<ObjectStyleComponent>();
            }

            if (window._gameObjectList.Count == 1) {
                var comp = window._objectStylComponentList[0];
                if (comp != null) window._headerColor = comp.color;
                window.HeaderBackgroundColor = window._headerColor;
                window.UpdateHeaderBackground(window._headerColor);
            }

            window.ShowPopup();
        }

        protected override bool CanReuseFor(object reuseKey) {
            if (reuseKey is GameObject go) return _gameObjectList.Contains(go);
            return false;
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
            if (_gameObjectList == null) return root;

            var styleIcon = _objectStylComponentList?[0].icon;
            var icon = styleIcon != null ? styleIcon : AssetPreview.GetMiniThumbnail(_gameObjectList[0]);
            if (_gameObjectList.Count != 1) icon = EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image;
            var iconImage = new Image {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    width = 16, height = 16,
                    marginLeft = 4, marginRight = 4
                }
            };
            _headerIconImage = iconImage;
            if (!_isSubscribed) {
                IconSelector.OnIconChanged += OnIconChangedHandler;
                ColorSelector.OnColorChangedComponent += OnColorChangedHandler;
                _isSubscribed = true;
            }

            root.Add(iconImage);

            var firstState = _gameObjectList[0].activeSelf;
            var isMixed = false;
            for (var i = 1; i < _gameObjectList.Count; i++) {
                if (_gameObjectList[i].activeSelf == firstState) continue;
                isMixed = true;
                break;
            }

            var activeToggle = new Toggle {
                style = {
                    width = 16,
                    height = 16,
                    marginRight = 4,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            if (isMixed) activeToggle.showMixedValue = true;
            else activeToggle.value = firstState;

            activeToggle.RegisterValueChangedCallback(evt =>
            {
                var objectsToChange = _gameObjectList.Where(go => go != null && go.activeSelf != evt.newValue).ToList();

                if (objectsToChange.Count <= 0) return;
                Undo.RecordObjects(objectsToChange.Select(go => go as Object).ToArray(),
                    I18N.Get("UI.HierarchyExtension.ToggleGameObjectsActive"));
                foreach (var go in objectsToChange) {
                    go.SetActive(evt.newValue);
                    EditorUtility.SetDirty(go);
                }
            });
            root.Add(activeToggle);

            var titleText = _gameObjectList.Count == 1
                ? _gameObjectList[0].name
                : I18N.Get("UI.HierarchyExtension.SelectedObjectsFmt", _gameObjectList.Count);
            var titleLabel = new Label(titleText) {
                style = {
                    flexShrink = 1,
                    marginRight = 4,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            root.Add(titleLabel);

            return root;
        }

        private void OnIconChangedHandler(Texture newIcon, List<ObjectStyleComponent> components) {
            if (_gameObjectList == null || _gameObjectList.Count == 0) return;
            if (components == null) return;
            var intersects = components.Any(c => _objectStylComponentList.Contains(c));
            if (!intersects) return;

            if (_gameObjectList.Count == 1) {
                var icon = newIcon != null ? newIcon : AssetPreview.GetMiniThumbnail(_gameObjectList[0]);
                if (_headerIconImage == null) return;
                _headerIconImage.image = icon;
            }
            else {
                if (_headerIconImage == null) return;
                _headerIconImage.image = EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image;
            }

            _headerIconImage.MarkDirtyRepaint();
        }

        private void OnColorChangedHandler(Color newColor, List<ObjectStyleComponent> components) {
            if (_gameObjectList == null || _gameObjectList.Count == 0) return;
            if (components == null) return;
            var intersects = components.Any(c => _objectStylComponentList.Contains(c));
            if (!intersects) return;

            if (_gameObjectList.Count == 1)
                _headerColor = newColor;
            else
                _headerColor = null;
            UpdateHeaderBackground(_headerColor);
        }

        protected override VisualElement Content() {
            var root = base.Content();
            var scrollArea = new ScrollView();
            var colorSelector = new ColorSelector(_objectStylComponentList);
            scrollArea.Add(colorSelector);
            scrollArea.Add(Spacer(16));
            var iconSelector = new IconSelector(_gameObjectList, _objectStylComponentList);
            scrollArea.Add(iconSelector);
            if (_gameObjectList.Count == 1) {
                scrollArea.Add(Spacer(16));
                var componentList = new ComponentList(_gameObjectList[0], locked => IsLocked = locked);
                scrollArea.Add(componentList);
            }

            var hideObjectButton = new Button(() =>
            {
                foreach (var obj in _gameObjectList.Where(obj => obj != null)) {
                    Undo.RecordObject(obj, I18N.Get("UI.HierarchyExtension.HideGameObject"));
                    obj.hideFlags |= HideFlags.HideInHierarchy;
                    obj.SetActive(false);
                    obj.tag = "EditorOnly";
                    EditorUtility.SetDirty(obj);
                    Close();
                }

                EditorApplication.RepaintHierarchyWindow();
            }) {
                text = I18N.Get("UI.HierarchyExtension.HideInHierarchy"),
                style = {
                    backgroundColor = ColorPreset.WarningButton,
                    marginTop = 8, marginBottom = 4,
                    height = 24,
                    borderTopRightRadius = 10, borderTopLeftRadius = 10,
                    borderBottomRightRadius = 10, borderBottomLeftRadius = 10
                }
            };
            scrollArea.Add(hideObjectButton);
            root.Add(scrollArea);
            return root;
        }
    }
}