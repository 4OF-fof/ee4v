using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ThreePaneLayoutState
    {
        public ThreePaneLayoutState(
            float leftWidth = 240f,
            float rightWidth = 280f,
            float leftMinWidth = 180f,
            float leftMaxWidth = 0f,
            float mainMinWidth = 360f,
            float rightMinWidth = 220f,
            float rightMaxWidth = 0f,
            bool leftCollapsed = false,
            bool rightCollapsed = false)
        {
            LeftWidth = Mathf.Max(0f, leftWidth);
            RightWidth = Mathf.Max(0f, rightWidth);
            LeftMinWidth = Mathf.Max(0f, leftMinWidth);
            LeftMaxWidth = Mathf.Max(0f, leftMaxWidth);
            MainMinWidth = Mathf.Max(0f, mainMinWidth);
            RightMinWidth = Mathf.Max(0f, rightMinWidth);
            RightMaxWidth = Mathf.Max(0f, rightMaxWidth);
            LeftCollapsed = leftCollapsed;
            RightCollapsed = rightCollapsed;
        }

        public float LeftWidth { get; }

        public float RightWidth { get; }

        public float LeftMinWidth { get; }

        public float LeftMaxWidth { get; }

        public float MainMinWidth { get; }

        public float RightMinWidth { get; }

        public float RightMaxWidth { get; }

        public bool LeftCollapsed { get; }

        public bool RightCollapsed { get; }
    }

    internal sealed class ThreePaneLayout : VisualElement
    {
        private const string RootClassName = "ee4v-ui-three-pane-layout";
        private const string PaneClassName = "ee4v-ui-three-pane-layout__pane";
        private const string LeftPaneClassName = "ee4v-ui-three-pane-layout__pane--left";
        private const string MainPaneClassName = "ee4v-ui-three-pane-layout__pane--main";
        private const string RightPaneClassName = "ee4v-ui-three-pane-layout__pane--right";
        private const string PaneBodyClassName = "ee4v-ui-three-pane-layout__pane-body";
        private const string MainPaneBodyClassName = "ee4v-ui-three-pane-layout__main-pane-body";
        private const string ToolbarRowClassName = "ee4v-ui-three-pane-layout__toolbar-row";
        private const string BodyRowClassName = "ee4v-ui-three-pane-layout__body-row";
        private const string ToolbarClassName = "ee4v-ui-three-pane-layout__toolbar";
        private const string LeftToolbarClassName = "ee4v-ui-three-pane-layout__toolbar--left";
        private const string MainToolbarClassName = "ee4v-ui-three-pane-layout__toolbar--main";
        private const string MainToolbarHasLeftToggleClassName = "ee4v-ui-three-pane-layout__toolbar--main-has-left-toggle";
        private const string MainToolbarHasRightToggleClassName = "ee4v-ui-three-pane-layout__toolbar--main-has-right-toggle";
        private const string RightToolbarClassName = "ee4v-ui-three-pane-layout__toolbar--right";
        private const string ToolbarContentClassName = "ee4v-ui-three-pane-layout__toolbar-content";
        private const string ToolbarEdgeLineClassName = "ee4v-ui-three-pane-layout__toolbar-edge-line";
        private const string LeftToolbarEdgeLineClassName = "ee4v-ui-three-pane-layout__toolbar-edge-line--left";
        private const string RightToolbarEdgeLineClassName = "ee4v-ui-three-pane-layout__toolbar-edge-line--right";
        private const string ToolbarEdgeLineActiveClassName = "ee4v-ui-three-pane-layout__toolbar-edge-line--active";
        private const string PaneEdgeLineClassName = "ee4v-ui-three-pane-layout__pane-edge-line";
        private const string LeftPaneEdgeLineClassName = "ee4v-ui-three-pane-layout__pane-edge-line--left";
        private const string RightPaneEdgeLineClassName = "ee4v-ui-three-pane-layout__pane-edge-line--right";
        private const string PaneEdgeLineActiveClassName = "ee4v-ui-three-pane-layout__pane-edge-line--active";
        private const string SplitterClassName = "ee4v-ui-three-pane-layout__splitter";
        private const string SplitterGripClassName = "ee4v-ui-three-pane-layout__splitter-grip";
        private const string PaneToggleClassName = "ee4v-ui-three-pane-layout__pane-toggle";
        private const string LeftPaneToggleInSideClassName = "ee4v-ui-three-pane-layout__pane-toggle--left-in-side";
        private const string LeftPaneToggleInMainClassName = "ee4v-ui-three-pane-layout__pane-toggle--left-in-main";
        private const string RightPaneToggleInSideClassName = "ee4v-ui-three-pane-layout__pane-toggle--right-in-side";
        private const string RightPaneToggleInMainClassName = "ee4v-ui-three-pane-layout__pane-toggle--right-in-main";
        private const string PaneToggleHoverClassName = "ee4v-ui-three-pane-layout__pane-toggle--hover";
        private const string PaneToggleActiveClassName = "ee4v-ui-three-pane-layout__pane-toggle--active";
        private const float SplitterWidth = 9f;
        private readonly VisualElement _toolbarRow;
        private readonly VisualElement _bodyRow;
        private readonly VisualElement _leftToolbar;
        private readonly VisualElement _leftToolbarEdgeLine;
        private readonly VisualElement _mainToolbar;
        private readonly VisualElement _rightToolbar;
        private readonly VisualElement _rightToolbarEdgeLine;
        private readonly VisualElement _leftPane;
        private readonly VisualElement _leftPaneEdgeLine;
        private readonly VisualElement _mainPane;
        private readonly VisualElement _rightPane;
        private readonly VisualElement _rightPaneEdgeLine;
        private readonly IMGUIContainer _dragCursorOverlay;
        private readonly VisualElement _leftSplitter;
        private readonly VisualElement _leftToggleButton;
        private readonly Icon _leftToggleIcon;
        private readonly VisualElement _rightSplitter;
        private readonly VisualElement _rightToggleButton;
        private readonly Icon _rightToggleIcon;
        private SplitterKind? _draggingSplitter;
        private float _dragBoundaryOffset;
        private float _leftWidth;
        private float _rightWidth;
        private float _leftMinWidth;
        private float _leftMaxWidth;
        private float _mainMinWidth;
        private float _rightMinWidth;
        private float _rightMaxWidth;
        private bool _leftCollapsed;
        private bool _rightCollapsed;
        private bool _leftSplitterHovered;
        private bool _rightSplitterHovered;

        public ThreePaneLayout(ThreePaneLayoutState state = null)
        {
            AddToClassList(RootClassName);

            _toolbarRow = new VisualElement();
            _toolbarRow.AddToClassList(ToolbarRowClassName);
            _bodyRow = new VisualElement();
            _bodyRow.AddToClassList(BodyRowClassName);

            _leftPane = CreatePane(LeftPaneClassName, LeftPaneEdgeLineClassName, out var leftBody, out _leftPaneEdgeLine);
            _leftToggleButton = CreatePaneToggleButton(ToggleLeftCollapsed, out _leftToggleIcon);
            _rightToggleButton = CreatePaneToggleButton(ToggleRightCollapsed, out _rightToggleIcon);
            _leftToolbar = CreateToolbar(LeftToolbarClassName, LeftToolbarEdgeLineClassName, out var leftToolbarContent, out _leftToolbarEdgeLine);
            _mainToolbar = CreateMainToolbar(out var mainToolbarContent);
            _rightToolbar = CreateToolbar(RightToolbarClassName, RightToolbarEdgeLineClassName, out var rightToolbarContent, out _rightToolbarEdgeLine);
            _mainPane = CreateMainPane(out var mainBody);
            _rightPane = CreatePane(RightPaneClassName, RightPaneEdgeLineClassName, out var rightBody, out _rightPaneEdgeLine);

            MainOverlayContent = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            MainOverlayContent.style.position = Position.Absolute;
            MainOverlayContent.style.top = 0f;
            MainOverlayContent.style.bottom = 0f;

            LeftToolbarContent = leftToolbarContent;
            MainToolbarContent = mainToolbarContent;
            RightToolbarContent = rightToolbarContent;
            LeftPaneContent = leftBody;
            MainContent = mainBody;
            RightPaneContent = rightBody;

            _leftSplitter = CreateSplitter(SplitterKind.Left);
            _rightSplitter = CreateSplitter(SplitterKind.Right);
            _dragCursorOverlay = new IMGUIContainer(DrawDragCursorOverlay)
            {
                pickingMode = PickingMode.Ignore,
                focusable = false
            };
            _dragCursorOverlay.style.position = Position.Absolute;
            _dragCursorOverlay.style.left = 0f;
            _dragCursorOverlay.style.right = 0f;
            _dragCursorOverlay.style.top = 0f;
            _dragCursorOverlay.style.bottom = 0f;

            _toolbarRow.Add(_leftToolbar);
            _toolbarRow.Add(_mainToolbar);
            _toolbarRow.Add(_rightToolbar);

            _bodyRow.Add(_leftPane);
            _bodyRow.Add(_leftSplitter);
            _bodyRow.Add(_mainPane);
            _bodyRow.Add(_rightSplitter);
            _bodyRow.Add(_rightPane);

            Add(_toolbarRow);
            Add(_bodyRow);
            Add(MainOverlayContent);
            Add(_dragCursorOverlay);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            SetState(state ?? new ThreePaneLayoutState());
        }

        public VisualElement LeftToolbarContent { get; }

        public VisualElement MainToolbarContent { get; }

        public VisualElement RightToolbarContent { get; }

        public VisualElement LeftPaneContent { get; }

        public VisualElement MainContent { get; }

        public VisualElement MainOverlayContent { get; }

        public VisualElement RightPaneContent { get; }

        public event Action<float> LeftPaneWidthChanged;

        public event Action<float> RightPaneWidthChanged;

        public event Action<bool> LeftCollapsedChanged;

        public event Action<bool> RightCollapsedChanged;

        public ThreePaneLayoutState GetState()
        {
            return new ThreePaneLayoutState(
                _leftWidth,
                _rightWidth,
                _leftMinWidth,
                _leftMaxWidth,
                _mainMinWidth,
                _rightMinWidth,
                _rightMaxWidth,
                _leftCollapsed,
                _rightCollapsed);
        }

        public void SetState(ThreePaneLayoutState state)
        {
            var nextState = state ?? new ThreePaneLayoutState();
            _leftWidth = nextState.LeftWidth;
            _rightWidth = nextState.RightWidth;
            _leftMinWidth = nextState.LeftMinWidth;
            _leftMaxWidth = nextState.LeftMaxWidth;
            _mainMinWidth = nextState.MainMinWidth;
            _rightMinWidth = nextState.RightMinWidth;
            _rightMaxWidth = nextState.RightMaxWidth;
            _leftCollapsed = nextState.LeftCollapsed;
            _rightCollapsed = nextState.RightCollapsed;

            NormalizePaneWidths();
            RefreshLayout();
        }

        public void SetLeftCollapsed(bool collapsed, bool notify = true)
        {
            if (_leftCollapsed == collapsed)
            {
                return;
            }

            _leftCollapsed = collapsed;
            NormalizePaneWidths();
            RefreshLayout();

            if (notify)
            {
                LeftCollapsedChanged?.Invoke(collapsed);
            }
        }

        public void SetRightCollapsed(bool collapsed, bool notify = true)
        {
            if (_rightCollapsed == collapsed)
            {
                return;
            }

            _rightCollapsed = collapsed;
            NormalizePaneWidths();
            RefreshLayout();

            if (notify)
            {
                RightCollapsedChanged?.Invoke(collapsed);
            }
        }

        private static VisualElement CreatePane(
            string paneModifierClassName,
            string edgeLineModifierClassName,
            out VisualElement body,
            out VisualElement edgeLine)
        {
            var pane = new VisualElement();
            pane.AddToClassList(PaneClassName);
            pane.AddToClassList(paneModifierClassName);

            body = new VisualElement();
            body.AddToClassList(PaneBodyClassName);

            pane.Add(body);
            edgeLine = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            edgeLine.AddToClassList(PaneEdgeLineClassName);
            edgeLine.AddToClassList(edgeLineModifierClassName);
            pane.Add(edgeLine);
            return pane;
        }

        private static VisualElement CreateMainPane(out VisualElement mainContent)
        {
            var pane = new VisualElement();
            pane.AddToClassList(PaneClassName);
            pane.AddToClassList(MainPaneClassName);

            var body = new VisualElement();
            body.AddToClassList(PaneBodyClassName);
            body.AddToClassList(MainPaneBodyClassName);

            mainContent = body;
            pane.Add(body);
            return pane;
        }

        private static VisualElement CreateToolbar(
            string toolbarModifierClassName,
            string edgeLineModifierClassName,
            out VisualElement content,
            out VisualElement edgeLine)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList(ToolbarClassName);
            toolbar.AddToClassList(toolbarModifierClassName);

            content = new VisualElement();
            content.AddToClassList(ToolbarContentClassName);
            toolbar.Add(content);

            edgeLine = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            edgeLine.AddToClassList(ToolbarEdgeLineClassName);
            edgeLine.AddToClassList(edgeLineModifierClassName);
            toolbar.Add(edgeLine);
            return toolbar;
        }

        private static VisualElement CreateMainToolbar(out VisualElement content)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList(ToolbarClassName);
            toolbar.AddToClassList(MainToolbarClassName);

            content = new VisualElement();
            content.AddToClassList(ToolbarContentClassName);
            toolbar.Add(content);
            return toolbar;
        }

        private static VisualElement CreatePaneToggleButton(Action toggleAction, out Icon toggleIcon)
        {
            var button = new VisualElement
            {
                focusable = false
            };
            button.AddToClassList(PaneToggleClassName);
            button.AddManipulator(new Clickable(toggleAction));
            button.RegisterCallback<PointerEnterEvent>(_ => button.EnableInClassList(PaneToggleHoverClassName, true));
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                button.EnableInClassList(PaneToggleHoverClassName, false);
                button.EnableInClassList(PaneToggleActiveClassName, false);
            });
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == (int)MouseButton.LeftMouse)
                {
                    button.EnableInClassList(PaneToggleActiveClassName, true);
                }
            });
            button.RegisterCallback<PointerUpEvent>(_ => button.EnableInClassList(PaneToggleActiveClassName, false));
            button.RegisterCallback<PointerCaptureOutEvent>(_ => button.EnableInClassList(PaneToggleActiveClassName, false));

            toggleIcon = new Icon(IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: UiSizeTokens.Size10));
            button.Add(toggleIcon);
            return button;
        }

        private VisualElement CreateSplitter(SplitterKind kind)
        {
            var splitter = new VisualElement();
            splitter.AddToClassList(SplitterClassName);
            splitter.style.width = SplitterWidth;

            var grip = new VisualElement();
            grip.AddToClassList(SplitterGripClassName);
            grip.AddManipulator(new SplitterDragManipulator(this, kind));
            grip.RegisterCallback<PointerEnterEvent>(_ => SetSplitterHover(kind, true));
            grip.RegisterCallback<PointerLeaveEvent>(_ => SetSplitterHover(kind, false));

            var cursorRectHost = new IMGUIContainer(() =>
            {
                EditorGUIUtility.AddCursorRect(new Rect(0f, 0f, grip.contentRect.width, grip.contentRect.height), MouseCursor.ResizeHorizontal);
            })
            {
                pickingMode = PickingMode.Ignore,
                focusable = false
            };
            cursorRectHost.style.position = Position.Absolute;
            cursorRectHost.style.left = 0f;
            cursorRectHost.style.right = 0f;
            cursorRectHost.style.top = 0f;
            cursorRectHost.style.bottom = 0f;

            splitter.Add(grip);
            grip.Add(cursorRectHost);
            return splitter;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Approximately(evt.newRect.width, evt.oldRect.width))
            {
                return;
            }

            NormalizePaneWidths();
            RefreshLayout();
        }

        private void DrawDragCursorOverlay()
        {
            if (!_draggingSplitter.HasValue)
            {
                return;
            }

            EditorGUIUtility.AddCursorRect(new Rect(0f, 0f, contentRect.width, contentRect.height), MouseCursor.ResizeHorizontal);
        }

        private void RefreshLayout()
        {
            var leftBodyVisible = !_leftCollapsed;
            var rightBodyVisible = !_rightCollapsed;
            var leftDragActive = !_leftCollapsed && (_leftSplitterHovered || _draggingSplitter == SplitterKind.Left);
            var rightDragActive = !_rightCollapsed && (_rightSplitterHovered || _draggingSplitter == SplitterKind.Right);

            _leftPane.style.display = _leftCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _leftPane.style.width = _leftCollapsed ? 0f : _leftWidth;
            _leftPane.style.minWidth = 0f;
            LeftPaneContent.style.display = leftBodyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _leftPaneEdgeLine.EnableInClassList(PaneEdgeLineActiveClassName, leftDragActive);
            _leftToolbar.style.display = _leftCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _leftToolbar.style.width = _leftCollapsed ? 0f : _leftWidth;
            _leftToolbar.style.minWidth = 0f;
            _leftToolbarEdgeLine.EnableInClassList(ToolbarEdgeLineActiveClassName, leftDragActive);

            _mainPane.style.display = DisplayStyle.Flex;
            _mainPane.style.minWidth = 0f;
            _mainToolbar.style.display = DisplayStyle.Flex;
            _mainToolbar.style.minWidth = 0f;

            _rightPane.style.display = _rightCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _rightPane.style.width = _rightCollapsed ? 0f : _rightWidth;
            _rightPane.style.minWidth = 0f;
            RightPaneContent.style.display = rightBodyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _rightPaneEdgeLine.EnableInClassList(PaneEdgeLineActiveClassName, rightDragActive);
            _rightToolbar.style.display = _rightCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _rightToolbar.style.width = _rightCollapsed ? 0f : _rightWidth;
            _rightToolbar.style.minWidth = 0f;
            _rightToolbarEdgeLine.EnableInClassList(ToolbarEdgeLineActiveClassName, rightDragActive);

            _leftSplitter.style.display = _leftCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _rightSplitter.style.display = _rightCollapsed ? DisplayStyle.None : DisplayStyle.Flex;

            MainOverlayContent.style.left = _leftCollapsed
                ? 0f
                : _leftWidth + SplitterWidth;
            MainOverlayContent.style.right = _rightCollapsed
                ? 0f
                : _rightWidth + SplitterWidth;

            _mainToolbar.EnableInClassList(MainToolbarHasLeftToggleClassName, _leftCollapsed);
            _mainToolbar.EnableInClassList(MainToolbarHasRightToggleClassName, _rightCollapsed);
            RefreshToggleHost(_leftToggleButton, _leftCollapsed ? _mainToolbar : _leftToolbar, !_leftCollapsed, true);
            RefreshToggleHost(_rightToggleButton, _rightCollapsed ? _mainToolbar : _rightToolbar, !_rightCollapsed, false);
            UpdateToggleIcon(_leftToggleIcon, _leftCollapsed ? 0f : 180f);
            UpdateToggleIcon(_rightToggleIcon, _rightCollapsed ? 180f : 0f);
        }

        private static void RefreshToggleHost(VisualElement toggleButton, VisualElement host, bool inSideToolbar, bool leftToggle)
        {
            if (toggleButton.parent != host)
            {
                toggleButton.RemoveFromHierarchy();
                host.Add(toggleButton);
            }

            toggleButton.EnableInClassList(LeftPaneToggleInSideClassName, leftToggle && inSideToolbar);
            toggleButton.EnableInClassList(LeftPaneToggleInMainClassName, leftToggle && !inSideToolbar);
            toggleButton.EnableInClassList(RightPaneToggleInSideClassName, !leftToggle && inSideToolbar);
            toggleButton.EnableInClassList(RightPaneToggleInMainClassName, !leftToggle && !inSideToolbar);
        }

        private static void UpdateToggleIcon(Icon icon, float rotationDegrees)
        {
            if (icon == null)
            {
                return;
            }

            icon.SetState(IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: UiSizeTokens.Size10));
            icon.style.rotate = new Rotate(new Angle(rotationDegrees, AngleUnit.Degree));
        }

        private void NormalizePaneWidths()
        {
            _leftMinWidth = Mathf.Max(0f, _leftMinWidth);
            _leftMaxWidth = Mathf.Max(0f, _leftMaxWidth);
            _mainMinWidth = Mathf.Max(0f, _mainMinWidth);
            _rightMinWidth = Mathf.Max(0f, _rightMinWidth);
            _rightMaxWidth = Mathf.Max(0f, _rightMaxWidth);
            _leftWidth = Mathf.Max(0f, _leftWidth);
            _rightWidth = Mathf.Max(0f, _rightWidth);

            if (_leftCollapsed && _rightCollapsed)
            {
                return;
            }

            if (!_leftCollapsed)
            {
                _leftWidth = ClampPaneWidth(_leftWidth, _leftMinWidth, ResolveLeftMaxWidth(_rightCollapsed ? 0f : _rightWidth), true);
            }

            if (!_rightCollapsed)
            {
                _rightWidth = ClampPaneWidth(_rightWidth, _rightMinWidth, ResolveRightMaxWidth(_leftCollapsed ? 0f : _leftWidth), true);
            }

            if (!_leftCollapsed)
            {
                _leftWidth = ClampPaneWidth(_leftWidth, _leftMinWidth, ResolveLeftMaxWidth(_rightCollapsed ? 0f : _rightWidth), true);
            }
        }

        private float ResolveLeftMaxWidth(float rightWidth)
        {
            var layoutMaxWidth = GetLeftLayoutMaxWidth(rightWidth);
            if (_leftMaxWidth <= 0f)
            {
                return layoutMaxWidth;
            }

            return Mathf.Min(layoutMaxWidth, _leftMaxWidth);
        }

        private float ResolveRightMaxWidth(float leftWidth)
        {
            var layoutMaxWidth = GetRightLayoutMaxWidth(leftWidth);
            if (_rightMaxWidth <= 0f)
            {
                return layoutMaxWidth;
            }

            return Mathf.Min(layoutMaxWidth, _rightMaxWidth);
        }

        private float GetLeftLayoutMaxWidth(float rightWidth)
        {
            var totalWidth = resolvedStyle.width;
            if (float.IsNaN(totalWidth) || totalWidth <= 0f)
            {
                return _leftWidth;
            }

            return Mathf.Max(0f, totalWidth - (2f * SplitterWidth) - rightWidth - _mainMinWidth);
        }

        private float GetRightLayoutMaxWidth(float leftWidth)
        {
            var totalWidth = resolvedStyle.width;
            if (float.IsNaN(totalWidth) || totalWidth <= 0f)
            {
                return _rightWidth;
            }

            return Mathf.Max(0f, totalWidth - (2f * SplitterWidth) - leftWidth - _mainMinWidth);
        }

        private static float ClampPaneWidth(float requestedWidth, float minWidth, float maxWidth, bool enforceMin)
        {
            if (maxWidth <= 0f)
            {
                return 0f;
            }

            if (!enforceMin)
            {
                return Mathf.Clamp(requestedWidth, 0f, maxWidth);
            }

            if (maxWidth < minWidth)
            {
                return maxWidth;
            }

            return Mathf.Clamp(requestedWidth, minWidth, maxWidth);
        }

        private void ToggleLeftCollapsed()
        {
            SetLeftCollapsed(!_leftCollapsed);
        }

        private void ToggleRightCollapsed()
        {
            SetRightCollapsed(!_rightCollapsed);
        }

        private bool CanBeginDrag(SplitterKind kind)
        {
            switch (kind)
            {
                case SplitterKind.Left:
                    if (_leftCollapsed)
                    {
                        return false;
                    }

                    return ResolveLeftMaxWidth(_rightCollapsed ? 0f : _rightWidth) > 0f;
                case SplitterKind.Right:
                    if (_rightCollapsed)
                    {
                        return false;
                    }

                    return ResolveRightMaxWidth(_leftCollapsed ? 0f : _leftWidth) > 0f;
                default:
                    return false;
            }
        }

        private void BeginDrag(SplitterKind kind, float pointerX)
        {
            _draggingSplitter = kind;
            _dragBoundaryOffset = GetBoundaryPosition(kind) - pointerX;
            SetSplitterHover(kind, true);
            _dragCursorOverlay.MarkDirtyRepaint();
        }

        private void UpdateDrag(SplitterKind kind, float pointerX)
        {
            if (_draggingSplitter != kind)
            {
                return;
            }

            ApplyBoundaryPosition(kind, pointerX + _dragBoundaryOffset);
        }

        private void EndDrag()
        {
            switch (_draggingSplitter)
            {
                case SplitterKind.Left:
                    SetLeftWidth(_leftWidth, true, true);

                    break;
                case SplitterKind.Right:
                    SetRightWidth(_rightWidth, true, true);

                    break;
            }

            SetSplitterHover(SplitterKind.Left, false);
            SetSplitterHover(SplitterKind.Right, false);
            _draggingSplitter = null;
            _dragCursorOverlay.MarkDirtyRepaint();
            RefreshLayout();
        }

        private float GetBoundaryPosition(SplitterKind kind)
        {
            switch (kind)
            {
                case SplitterKind.Left:
                    return _leftCollapsed ? 0f : _leftWidth;
                case SplitterKind.Right:
                    return _rightCollapsed
                        ? resolvedStyle.width - SplitterWidth
                        : resolvedStyle.width - _rightWidth - SplitterWidth;
                default:
                    return 0f;
            }
        }

        private void ApplyBoundaryPosition(SplitterKind kind, float boundaryPosition)
        {
            switch (kind)
            {
                case SplitterKind.Left:
                    SetLeftWidth(boundaryPosition, true, true);
                    break;
                case SplitterKind.Right:
                    SetRightWidth(resolvedStyle.width - boundaryPosition - SplitterWidth, true, true);
                    break;
            }
        }

        private void SetLeftWidth(float width, bool notify, bool enforceMin)
        {
            var maxWidth = ResolveLeftMaxWidth(_rightCollapsed ? 0f : _rightWidth);
            var nextWidth = ClampPaneWidth(width, _leftMinWidth, maxWidth, enforceMin);
            var widthChanged = !Mathf.Approximately(nextWidth, _leftWidth);
            if (!widthChanged)
            {
                return;
            }

            _leftWidth = nextWidth;

            if (enforceMin)
            {
                NormalizePaneWidths();
            }

            RefreshLayout();

            if (notify && widthChanged)
            {
                LeftPaneWidthChanged?.Invoke(_leftWidth);
            }
        }

        private void SetRightWidth(float width, bool notify, bool enforceMin)
        {
            var maxWidth = ResolveRightMaxWidth(_leftCollapsed ? 0f : _leftWidth);
            var nextWidth = ClampPaneWidth(width, _rightMinWidth, maxWidth, enforceMin);
            var widthChanged = !Mathf.Approximately(nextWidth, _rightWidth);
            if (!widthChanged)
            {
                return;
            }

            _rightWidth = nextWidth;

            if (enforceMin)
            {
                NormalizePaneWidths();
            }

            RefreshLayout();

            if (notify && widthChanged)
            {
                RightPaneWidthChanged?.Invoke(_rightWidth);
            }
        }

        private void SetSplitterHover(SplitterKind kind, bool hovered)
        {
            switch (kind)
            {
                case SplitterKind.Left:
                    if (_leftSplitterHovered == hovered)
                    {
                        return;
                    }

                    _leftSplitterHovered = hovered;
                    break;
                case SplitterKind.Right:
                    if (_rightSplitterHovered == hovered)
                    {
                        return;
                    }

                    _rightSplitterHovered = hovered;
                    break;
                default:
                    return;
            }

            RefreshLayout();
        }

        private enum SplitterKind
        {
            Left,
            Right
        }

        private sealed class SplitterDragManipulator : PointerManipulator
        {
            private readonly ThreePaneLayout _owner;
            private readonly SplitterKind _kind;
            private bool _active;
            private int _pointerId = -1;

            public SplitterDragManipulator(ThreePaneLayout owner, SplitterKind kind)
            {
                _owner = owner;
                _kind = kind;
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (_owner == null || !_owner.CanBeginDrag(_kind) || !CanStartManipulation(evt))
                {
                    return;
                }

                _active = true;
                _pointerId = evt.pointerId;
                target.CapturePointer(_pointerId);
                _owner.BeginDrag(_kind, GetOwnerLocalPointerX(evt.localPosition));
                evt.StopPropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!_active || evt.pointerId != _pointerId || _owner == null)
                {
                    return;
                }

                _owner.UpdateDrag(_kind, GetOwnerLocalPointerX(evt.localPosition));
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!_active || evt.pointerId != _pointerId || !CanStopManipulation(evt))
                {
                    return;
                }

                target.ReleasePointer(_pointerId);
                _active = false;
                _pointerId = -1;
                _owner.EndDrag();
                evt.StopPropagation();
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (!_active)
                {
                    return;
                }

                _active = false;
                _pointerId = -1;
                _owner.EndDrag();
            }

            private float GetOwnerLocalPointerX(Vector2 localPosition)
            {
                return target.worldBound.x - _owner.worldBound.x + localPosition.x;
            }
        }
    }
}


