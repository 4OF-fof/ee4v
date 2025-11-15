using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.HierarchyExtension.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.HierarchyExtension.UI.Window {
    public class ComponentInspector : BaseWindow {
        private const int KMaxAutoSizeAttempts = 6;
        private const float KHeaderHeight = 28f;
        private int _autoSizeAttempts;
        private bool _autoSizeCompleted;
        private Component _component;
        private Editor _editor;
        private GameObject _gameObject;
        private Material _material;

        private ScrollView _scrollView;

        protected override void OnDestroy() {
            base.OnDestroy();
            if (_editor == null) return;
            DestroyImmediate(_editor);
            _editor = null;
        }

        public static void Open(Component component, GameObject obj, Vector2 anchorScreen) {
            var window = OpenSetup<ComponentInspector>(anchorScreen, component);
            window.IsLocked = true;
            window._component = component;
            window._gameObject = obj;
            window._editor = Editor.CreateEditor(component);
            window.ShowPopup();
            window.ScheduleAutoSize();
        }

        public static void Open(Material material, GameObject obj, Vector2 anchorScreen) {
            var window = OpenSetup<ComponentInspector>(anchorScreen, material);
            window.position = new Rect(anchorScreen.x, anchorScreen.y, 380, 600);
            window.IsLocked = true;
            window._material = material;
            window._gameObject = obj;
            window._editor = Editor.CreateEditor(material);
            window.ShowPopup();
        }

        protected override bool CanReuseFor(object reuseKey) {
            return reuseKey switch {
                Component c => c == _component,
                Material m  => m == _material,
                _           => false
            };
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
            if (_component == null && _material == null) return root;

            var icon = AssetPreview.GetMiniThumbnail(_component == null ? _material : _component);
            var iconImage = new Image {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    width = 16, height = 16,
                    marginLeft = 4, marginRight = 4
                }
            };
            root.Add(iconImage);

            if (_component != null) {
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
                    activeToggle.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue == behaviour.enabled) return;
                        Undo.RecordObject(behaviour, "Toggle GameObject Active");
                        behaviour.enabled = evt.newValue;
                        EditorUtility.SetDirty(behaviour);
                    });
                    root.Add(activeToggle);
                }
            }

            var labelText = _component == null ? _material.name : $"{_component.GetType().Name} ({_gameObject.name})";
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
            if (_editor == null) return new VisualElement();
            var editorContainer = new IMGUIContainer(() =>
            {
                if (_editor == null) return;

                var usable = Mathf.Max(0f, position.width - 32f);
                var prevWide = EditorGUIUtility.wideMode;
                var prevLabel = EditorGUIUtility.labelWidth;
                try {
                    EditorGUIUtility.wideMode = usable > 330f;
                    EditorGUIUtility.labelWidth = Mathf.Clamp(usable * 0.45f, 120f, 220f);

                    if (_material != null) {
                        var materialEditor = _editor as MaterialEditor;
                        if (materialEditor != null) {
                            materialEditor.DrawHeader();
                            ReflectionWrapper.DrawMaterialInspector(materialEditor, _material);
                        }
                        else {
                            Debug.LogError(I18N.Get("Debug.HierarchyExtension.CouldNotCastToMaterialEditor"));
                        }
                    }
                    else {
                        _editor.OnInspectorGUI();
                    }
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
            var targetWidth = Mathf.Clamp(position.width, 260,
                Mathf.Max(260, contentWidth > 0 ? contentWidth + 32 : 420));
            const float padding = 8;
            var targetHeight = Mathf.Clamp(KHeaderHeight + contentHeight + padding, 10, 600);
            var p = position;
            var sizeChanged = Mathf.Abs(p.width - targetWidth) > 0.5 || Mathf.Abs(p.height - targetHeight) > 0.5;
            if (sizeChanged) position = new Rect(p.x, p.y, targetWidth, targetHeight);
            if (markCompleted) _autoSizeCompleted = true;
        }

        private void RetryAutoSize() {
            _autoSizeAttempts++;
            if (_autoSizeAttempts >= KMaxAutoSizeAttempts) return;
            rootVisualElement.schedule.Execute(TryAutoSize).ExecuteLater(40);
        }
    }
}