using System;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.Core.UI.Window {
    public abstract class BaseWindow : EditorWindow {
        private const float MinWidth = 340;
        private const float MinHeight = 100;

        private static bool _isDragging;
        private static Vector2 _dragStartMouseScreen;
        private static Rect _dragStartWindowPosition;
        private VisualElement _headerElement;
        private bool _isLocked;
        private bool _isResizing;
        private Image _lockIcon;
        private int _resizeControlID;
        private ResizeEdgeEnum _resizeEdge = ResizeEdgeEnum.None;
        private Vector2 _resizeStartMouseScreen;
        private Rect _resizeStartWindowPosition;
        protected Color? HeaderBackgroundColor { get; set; }

        protected bool IsLocked {
            get => _isLocked;
            set {
                if (_isLocked == value) return;
                _isLocked = value;
                if (_lockIcon == null) return;
                _lockIcon.image = EditorGUIUtility.IconContent(_isLocked ? "IN LockButton on" : "IN LockButton").image;
                _lockIcon.tintColor = ColorPreset.TextColor;
            }
        }

        protected virtual void OnEnable() {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        protected virtual void OnDestroy() {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            if (GUIUtility.hotControl == _resizeControlID) GUIUtility.hotControl = 0;
            _resizeControlID = 0;
        }

        private void OnGUI() {
            HandleResize(Event.current);
        }

        public void CreateGUI() {
            rootVisualElement.style.borderRightWidth = 2;
            rootVisualElement.style.borderLeftWidth = 2;
            rootVisualElement.style.borderTopWidth = 2;
            rootVisualElement.style.borderBottomWidth = 2;

            rootVisualElement.style.borderRightColor = ColorPreset.WindowBorder;
            rootVisualElement.style.borderLeftColor = ColorPreset.WindowBorder;
            rootVisualElement.style.borderTopColor = ColorPreset.WindowBorder;
            rootVisualElement.style.borderBottomColor = ColorPreset.WindowBorder;

            var header = Header();
            if (header != null) rootVisualElement.Add(header);

            var content = Content();
            if (content != null) rootVisualElement.Add(content);
        }

        protected virtual void OnLostFocus() {
            if (!IsLocked) Close();
        }

        private void OnBeforeAssemblyReload() {
            Close();
        }

        protected static T OpenSetup<T>(Vector2 anchorScreen, object reuseKey = null) where T : BaseWindow {
            var existing = Resources.FindObjectsOfTypeAll<T>();
            if (existing is { Length: > 0 })
                foreach (var win in existing) {
                    if (win == null || !win.CanReuseFor(reuseKey)) continue;
                    win.Focus();
                    return win;
                }

            var window = CreateInstance<T>();
            window.position = new Rect(anchorScreen.x, anchorScreen.y, 340, 250);
            return window;
        }

        protected virtual bool CanReuseFor(object reuseKey) {
            return true;
        }

        private VisualElement Header() {
            var closeButton = CloseButton();
            var header = new VisualElement {
                name = "ee4v-baseWindow-header",
                style = {
                    flexDirection = FlexDirection.Row,
                    height = 24,
                    flexShrink = 0,
                    backgroundColor = HeaderBackgroundColor == Color.clear || HeaderBackgroundColor == null
                        ? ColorPreset.WindowHeader
                        : HeaderBackgroundColor.Value,
                    justifyContent = Justify.Center
                }
            };
            _headerElement = header;
            var headerContent = HeaderContent();
            headerContent.style.flexGrow = 1;
            header.Add(headerContent);
            header.Add(closeButton);

            WindowMover(header);
            return header;
        }

        protected void UpdateHeaderBackground(Color? color) {
            HeaderBackgroundColor = color;
            if (_headerElement == null) return;
            _headerElement.style.backgroundColor = HeaderBackgroundColor == Color.clear || HeaderBackgroundColor == null
                ? ColorPreset.WindowHeader
                : HeaderBackgroundColor.Value;
            _headerElement.MarkDirtyRepaint();
        }

        protected virtual VisualElement Content() {
            return new VisualElement {
                name = "ee4v-baseWindow-content",
                style = {
                    marginRight = 4, marginLeft = 4, marginTop = 4, marginBottom = 4
                }
            };
        }

        protected abstract VisualElement HeaderContent();

        private Button CloseButton() {
            var button = new Button(Close) {
                style = {
                    width = 24, height = 24,
                    paddingRight = 0, paddingLeft = 0, paddingTop = 0, paddingBottom = 0,
                    backgroundColor = Color.clear,
                    borderRightWidth = 0, borderLeftWidth = 0, borderTopWidth = 0, borderBottomWidth = 0,
                    marginRight = 0, marginLeft = 0, marginTop = 0, marginBottom = 0,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var hoverColor = ColorPreset.MouseOverBackground;
            hoverColor.a = 0.3f;
            button.RegisterCallback<MouseEnterEvent>(_ => { button.style.backgroundColor = hoverColor; });
            button.RegisterCallback<MouseLeaveEvent>(_ => { button.style.backgroundColor = Color.clear; });

            var icon = new Image {
                image = EditorGUIUtility.IconContent("winbtn_win_close").image,
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    width = 16, height = 16
                }
            };
            button.Add(icon);

            return button;
        }

        private void WindowMover(VisualElement header) {
            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _isDragging = true;
                header.CaptureMouse();
                _dragStartMouseScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                _dragStartWindowPosition = position;
                evt.StopPropagation();
            });

            header.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!_isDragging) return;
                var mouseScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                var delta = mouseScreen - _dragStartMouseScreen;
                position = new Rect(_dragStartWindowPosition.x + delta.x, _dragStartWindowPosition.y + delta.y,
                    position.width, position.height);
            });

            header.RegisterCallback<MouseUpEvent>(evt =>
            {
                _isDragging = false;
                header.ReleaseMouse();
                evt.StopPropagation();
            });
        }

        private void HandleResize(Event e) {
            const float margin = 6;
            const float cornerSize = 6;

            var leftRect = new Rect(0, 24, margin, Mathf.Max(0, position.height - 24 - cornerSize));
            var rightRect = new Rect(position.width - margin, 24, margin,
                Mathf.Max(0, position.height - 24 - cornerSize));

            var innerBottomWidth = Mathf.Max(0, position.width - cornerSize * 2);
            var bottomRect = new Rect(cornerSize, Mathf.Max(0, position.height - margin), innerBottomWidth, margin);

            var bottomLeftRect = new Rect(0, Mathf.Max(0, position.height - cornerSize), cornerSize, cornerSize);
            var bottomRightRect = new Rect(Mathf.Max(0, position.width - cornerSize),
                Mathf.Max(0, position.height - cornerSize), cornerSize, cornerSize);

            EditorGUIUtility.AddCursorRect(leftRect, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(rightRect, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(bottomRect, MouseCursor.ResizeVertical);
            EditorGUIUtility.AddCursorRect(bottomLeftRect, MouseCursor.ResizeUpRight);
            EditorGUIUtility.AddCursorRect(bottomRightRect, MouseCursor.ResizeUpLeft);

            if (e.type == EventType.MouseDown && e.button == 0 && !_isDragging) {
                var detected = ResizeEdgeEnum.None;
                if (bottomLeftRect.Contains(e.mousePosition)) detected = ResizeEdgeEnum.Left | ResizeEdgeEnum.Bottom;
                else if (bottomRightRect.Contains(e.mousePosition))
                    detected = ResizeEdgeEnum.Right | ResizeEdgeEnum.Bottom;
                else if (leftRect.Contains(e.mousePosition)) detected = ResizeEdgeEnum.Left;
                else if (rightRect.Contains(e.mousePosition)) detected = ResizeEdgeEnum.Right;
                else if (bottomRect.Contains(e.mousePosition)) detected = ResizeEdgeEnum.Bottom;

                if (detected != ResizeEdgeEnum.None) {
                    _isResizing = true;
                    _resizeEdge = detected;
                    _resizeStartMouseScreen = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    _resizeStartWindowPosition = position;
                    _resizeControlID = GUIUtility.GetControlID(FocusType.Passive);
                    GUIUtility.hotControl = _resizeControlID;
                    e.Use();
                }
            }

            if (_isResizing && e.type == EventType.MouseDrag) {
                var mouseScreen = GUIUtility.GUIToScreenPoint(e.mousePosition);
                var delta = mouseScreen - _resizeStartMouseScreen;
                var newPos = _resizeStartWindowPosition;

                if ((_resizeEdge & ResizeEdgeEnum.Left) != 0) {
                    var newWidth = _resizeStartWindowPosition.width - delta.x;
                    var newX = _resizeStartWindowPosition.x + delta.x;
                    if (newWidth < MinWidth) {
                        newWidth = MinWidth;
                        newX = _resizeStartWindowPosition.x + (_resizeStartWindowPosition.width - newWidth);
                    }

                    newPos.x = newX;
                    newPos.width = newWidth;
                }

                if ((_resizeEdge & ResizeEdgeEnum.Right) != 0) {
                    var newWidth = _resizeStartWindowPosition.width + delta.x;
                    if (newWidth < MinWidth) newWidth = MinWidth;
                    newPos.width = newWidth;
                }

                if ((_resizeEdge & ResizeEdgeEnum.Bottom) != 0) {
                    var newHeight = _resizeStartWindowPosition.height + delta.y;
                    if (newHeight < MinHeight) newHeight = MinHeight;
                    newPos.height = newHeight;
                }

                position = newPos;
                e.Use();
            }

            if (!_isResizing || e.type != EventType.MouseUp) return;
            _isResizing = false;
            _resizeEdge = ResizeEdgeEnum.None;
            if (GUIUtility.hotControl == _resizeControlID) GUIUtility.hotControl = 0;
            _resizeControlID = 0;
            e.Use();
        }

        protected static VisualElement Spacer(int height = 0, int width = 0) {
            return new VisualElement { style = { height = height, width = width } };
        }

        [Flags]
        private enum ResizeEdgeEnum {
            None = 0,
            Left = 1,
            Right = 2,
            Bottom = 4
        }
    }
}