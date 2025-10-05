using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using System;

using _4OF.ee4v.Core.UI.Window;

namespace _4OF.ee4v.HierarchyExtension.UI.HierarchyItem.Window {
    public class ComponentInspector: BaseWindow {
        private Component _component;
        private GameObject _gameObject;
        private Editor _componentEditor;
        
        private ScrollView _scrollView;
        private bool _autoSizeCompleted;
        private int _autoSizeAttempts;
        private const int KMaxAutoSizeAttempts = 6;
        private const float KHeaderHeight = 28f;
        private const float KDynamicResizeThreshold = 40f;

        public static void Open(Component component, GameObject obj, Vector2 anchorScreen) {
            var window = OpenSetup<ComponentInspector>(anchorScreen, component);
            window.IsLocked = true;
            window._component = component;
            window._gameObject = obj;
            if (component) {
                window._componentEditor = Editor.CreateEditor(component);
            }
            window.ShowPopup();
            window.ScheduleAutoSize();
        }
        
        protected override bool CanReuseFor(object reuseKey) {
            if (reuseKey is Component c) return c == _component;
            return false;
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            if (_componentEditor == null) return;
            DestroyImmediate(_componentEditor);
            _componentEditor = null;
        }

        protected override VisualElement HeaderContent() {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 24,
                    flexGrow = 1,
                }
            };
            if (_component == null) return root;
            
            var icon = AssetPreview.GetMiniThumbnail(_component);
            var iconImage = new Image {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    width = 16, height = 16,
                    marginLeft = 4, marginRight = 4
                }
            };
            root.Add(iconImage);
            
            var behaviour = _component as Behaviour;
            if (behaviour != null) {
                var activeToggle = new Toggle {
                    value = behaviour.enabled,
                    style = {
                        width = 16,
                        height = 16,
                        marginRight = 4,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                activeToggle.RegisterValueChangedCallback(evt => {
                    if (evt.newValue == behaviour.enabled) return;
                    Undo.RecordObject(behaviour, "Toggle GameObject Active");
                    behaviour.enabled = evt.newValue;
                    EditorUtility.SetDirty(behaviour);
                });
                root.Add(activeToggle);
            }

            var labelText = $"{_component.GetType().Name} ({_gameObject.name})";
            var titleLabel = new Label(labelText) {
                tooltip = labelText,
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1, flexShrink = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginRight = 4,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            root.Add(titleLabel);
            
            return root;
        }

        protected override VisualElement Content() {
            var root = base.Content();
            _scrollView = new ScrollView();
            if (_componentEditor == null) return new VisualElement();
            var editorContainer = new IMGUIContainer(() => {
                if (_componentEditor == null) return;
                var usable = Mathf.Max(0f, position.width - 32f);

                var prevWide = EditorGUIUtility.wideMode;
                var prevLabel = EditorGUIUtility.labelWidth;
                try {
                    EditorGUIUtility.wideMode = usable > 330f;
                    EditorGUIUtility.labelWidth = Mathf.Clamp(usable * 0.45f, 120f, 220f);
                    _componentEditor.OnInspectorGUI();
                }
                finally {
                    EditorGUIUtility.wideMode = prevWide;
                    EditorGUIUtility.labelWidth = prevLabel;
                }
            }) {
                style = { flexGrow = 0 }
            };
            editorContainer.style.width = new StyleLength(Length.Percent(100));
            _scrollView.Add(editorContainer);
            _scrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(OnContentGeometryChanged);
            root.Add(_scrollView);
            return root;
        }
        
        private void ScheduleAutoSize() {
            if (_autoSizeCompleted) return;
            if (rootVisualElement == null) {
                EditorApplication.delayCall += ScheduleAutoSize;
                return;
            }
            rootVisualElement.schedule.Execute(TryAutoSize).ExecuteLater(20);
        }

        private void TryAutoSize() {
            if (_autoSizeCompleted) return;
            if (_scrollView == null) {
                RetryAutoSize();
                return;
            }
            var contentHeight = _scrollView.contentContainer.layout.height;
            var contentWidth = _scrollView.contentContainer.layout.width;
            if (contentHeight is <= 0f or float.NaN) {
                RetryAutoSize();
                return;
            }
            ApplyResize(contentWidth, contentHeight, true);
        }

        private void ApplyResize(float contentWidth, float contentHeight, bool markCompleted) {
            var targetWidth = Mathf.Clamp(position.width, 260, Mathf.Max(260, contentWidth > 0 ? contentWidth + 32 : 420));
            const float padding = 8;
            var targetHeight = Mathf.Clamp(KHeaderHeight + contentHeight + padding, 10, 600);
            var p = position;
            var sizeChanged = Mathf.Abs(p.width - targetWidth) > 0.5 || Mathf.Abs(p.height - targetHeight) > 0.5;
            if (sizeChanged) {
                position = new Rect(p.x, p.y, targetWidth, targetHeight);
            }
            if (markCompleted) _autoSizeCompleted = true;
        }

        private void RetryAutoSize() {
            _autoSizeAttempts++;
            if (_autoSizeAttempts >= KMaxAutoSizeAttempts) return;
            rootVisualElement.schedule.Execute(TryAutoSize).ExecuteLater(40);
        }

        private void OnContentGeometryChanged(GeometryChangedEvent evt) {
            if (!_autoSizeCompleted) return;
            if (_scrollView == null) return;
            var newHeight = _scrollView.contentContainer.layout.height;
            if (newHeight is <= 0f or Single.NaN) return;
            var desired = Mathf.Clamp(KHeaderHeight + newHeight + 8, 10, 600);
            if (Mathf.Abs(position.height - desired) < KDynamicResizeThreshold) return;
            var contentWidth = _scrollView.contentContainer.layout.width;
            ApplyResize(contentWidth, newHeight, false);
        }
    }
}