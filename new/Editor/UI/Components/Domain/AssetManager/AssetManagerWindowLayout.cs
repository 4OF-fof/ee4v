using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class AssetManagerWindowLayoutState
    {
        public AssetManagerWindowLayoutState(
            float navigationWidth = 240f,
            float inspectorWidth = 280f,
            float navigationMinWidth = 180f,
            float navigationMaxWidth = 0f,
            float contentMinWidth = 360f,
            float inspectorMinWidth = 220f,
            float inspectorMaxWidth = 0f,
            bool navigationCollapsed = false,
            bool inspectorCollapsed = false)
        {
            NavigationWidth = Mathf.Max(0f, navigationWidth);
            InspectorWidth = Mathf.Max(0f, inspectorWidth);
            NavigationMinWidth = Mathf.Max(0f, navigationMinWidth);
            NavigationMaxWidth = Mathf.Max(0f, navigationMaxWidth);
            ContentMinWidth = Mathf.Max(0f, contentMinWidth);
            InspectorMinWidth = Mathf.Max(0f, inspectorMinWidth);
            InspectorMaxWidth = Mathf.Max(0f, inspectorMaxWidth);
            NavigationCollapsed = navigationCollapsed;
            InspectorCollapsed = inspectorCollapsed;
        }

        public float NavigationWidth { get; }

        public float InspectorWidth { get; }

        public float NavigationMinWidth { get; }

        public float NavigationMaxWidth { get; }

        public float ContentMinWidth { get; }

        public float InspectorMinWidth { get; }

        public float InspectorMaxWidth { get; }

        public bool NavigationCollapsed { get; }

        public bool InspectorCollapsed { get; }
    }

    internal sealed class AssetManagerWindowLayout : VisualElement
    {
        private const string RootClassName = "ee4v-ui-asset-manager-window-layout";
        private const string PaneClassName = "ee4v-ui-asset-manager-window-layout__pane";
        private const string NavigationPaneClassName = "ee4v-ui-asset-manager-window-layout__pane--navigation";
        private const string ContentPaneClassName = "ee4v-ui-asset-manager-window-layout__pane--content";
        private const string InspectorPaneClassName = "ee4v-ui-asset-manager-window-layout__pane--inspector";
        private const string PaneBodyClassName = "ee4v-ui-asset-manager-window-layout__pane-body";
        private const string ContentPaneBodyClassName = "ee4v-ui-asset-manager-window-layout__content-pane-body";
        private const string ContentToolbarClassName = "ee4v-ui-asset-manager-window-layout__content-toolbar";
        private const string ContentHostClassName = "ee4v-ui-asset-manager-window-layout__content-host";
        private const string SplitterClassName = "ee4v-ui-asset-manager-window-layout__splitter";
        private const string SplitterCollapsedClassName = "ee4v-ui-asset-manager-window-layout__splitter--collapsed";
        private const string SplitterToggleClassName = "ee4v-ui-asset-manager-window-layout__splitter-toggle";
        private const string SplitterGripClassName = "ee4v-ui-asset-manager-window-layout__splitter-grip";
        private const string SplitterHandleClassName = "ee4v-ui-asset-manager-window-layout__splitter-handle";
        private const float SplitterWidth = 18f;
        private readonly VisualElement _navigationPane;
        private readonly VisualElement _contentPane;
        private readonly VisualElement _inspectorPane;
        private readonly IMGUIContainer _dragCursorOverlay;
        private readonly VisualElement _navigationSplitter;
        private readonly Button _navigationToggleButton;
        private readonly Icon _navigationToggleIcon;
        private readonly VisualElement _inspectorSplitter;
        private readonly Button _inspectorToggleButton;
        private readonly Icon _inspectorToggleIcon;
        private SplitterKind? _draggingSplitter;
        private float _dragBoundaryOffset;
        private float _navigationWidth;
        private float _inspectorWidth;
        private float _navigationMinWidth;
        private float _navigationMaxWidth;
        private float _contentMinWidth;
        private float _inspectorMinWidth;
        private float _inspectorMaxWidth;
        private bool _navigationCollapsed;
        private bool _inspectorCollapsed;

        public AssetManagerWindowLayout(AssetManagerWindowLayoutState state = null)
        {
            AddToClassList(RootClassName);

            _navigationPane = CreatePane(NavigationPaneClassName, out var navigationBody);
            _contentPane = CreateContentPane(out var mainToolbar, out var contentBody);
            _inspectorPane = CreatePane(InspectorPaneClassName, out var inspectorBody);

            NavigationPaneContent = navigationBody;
            MainToolbar = mainToolbar;
            ContentPaneContent = contentBody;
            InspectorPaneContent = inspectorBody;

            _navigationSplitter = CreateSplitter(
                ToggleNavigationCollapsed,
                out _navigationToggleButton,
                out _navigationToggleIcon,
                SplitterKind.Navigation);
            _inspectorSplitter = CreateSplitter(
                ToggleInspectorCollapsed,
                out _inspectorToggleButton,
                out _inspectorToggleIcon,
                SplitterKind.Inspector);
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

            Add(_navigationPane);
            Add(_navigationSplitter);
            Add(_contentPane);
            Add(_inspectorSplitter);
            Add(_inspectorPane);
            Add(_dragCursorOverlay);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            SetState(state ?? new AssetManagerWindowLayoutState());
        }

        public VisualElement NavigationPaneContent { get; }

        public AssetManagerToolbar MainToolbar { get; }

        public VisualElement ContentPaneContent { get; }

        public VisualElement InspectorPaneContent { get; }

        public event Action<float> NavigationPaneWidthChanged;

        public event Action<float> InspectorPaneWidthChanged;

        public event Action<bool> NavigationCollapsedChanged;

        public event Action<bool> InspectorCollapsedChanged;

        public AssetManagerWindowLayoutState GetState()
        {
            return new AssetManagerWindowLayoutState(
                _navigationWidth,
                _inspectorWidth,
                _navigationMinWidth,
                _navigationMaxWidth,
                _contentMinWidth,
                _inspectorMinWidth,
                _inspectorMaxWidth,
                _navigationCollapsed,
                _inspectorCollapsed);
        }

        public void SetState(AssetManagerWindowLayoutState state)
        {
            var nextState = state ?? new AssetManagerWindowLayoutState();
            _navigationWidth = nextState.NavigationWidth;
            _inspectorWidth = nextState.InspectorWidth;
            _navigationMinWidth = nextState.NavigationMinWidth;
            _navigationMaxWidth = nextState.NavigationMaxWidth;
            _contentMinWidth = nextState.ContentMinWidth;
            _inspectorMinWidth = nextState.InspectorMinWidth;
            _inspectorMaxWidth = nextState.InspectorMaxWidth;
            _navigationCollapsed = nextState.NavigationCollapsed;
            _inspectorCollapsed = nextState.InspectorCollapsed;

            NormalizePaneWidths();
            RefreshLayout();
        }

        public void SetNavigationCollapsed(bool collapsed, bool notify = true)
        {
            if (_navigationCollapsed == collapsed)
            {
                return;
            }

            _navigationCollapsed = collapsed;
            NormalizePaneWidths();
            RefreshLayout();

            if (notify)
            {
                NavigationCollapsedChanged?.Invoke(collapsed);
            }
        }

        public void SetInspectorCollapsed(bool collapsed, bool notify = true)
        {
            if (_inspectorCollapsed == collapsed)
            {
                return;
            }

            _inspectorCollapsed = collapsed;
            NormalizePaneWidths();
            RefreshLayout();

            if (notify)
            {
                InspectorCollapsedChanged?.Invoke(collapsed);
            }
        }

        private static VisualElement CreatePane(string paneModifierClassName, out VisualElement body)
        {
            var pane = new VisualElement();
            pane.AddToClassList(PaneClassName);
            pane.AddToClassList(paneModifierClassName);

            body = new VisualElement();
            body.AddToClassList(PaneBodyClassName);

            pane.Add(body);
            return pane;
        }

        private static VisualElement CreateContentPane(out AssetManagerToolbar toolbar, out VisualElement contentHost)
        {
            var pane = new VisualElement();
            pane.AddToClassList(PaneClassName);
            pane.AddToClassList(ContentPaneClassName);

            var body = new VisualElement();
            body.AddToClassList(PaneBodyClassName);
            body.AddToClassList(ContentPaneBodyClassName);

            toolbar = new AssetManagerToolbar();
            toolbar.AddToClassList(ContentToolbarClassName);

            contentHost = new VisualElement();
            contentHost.AddToClassList(ContentHostClassName);

            body.Add(toolbar);
            body.Add(contentHost);
            pane.Add(body);
            return pane;
        }

        private VisualElement CreateSplitter(Action toggleAction, out Button toggleButton, out Icon toggleIcon, SplitterKind kind)
        {
            var splitter = new VisualElement();
            splitter.AddToClassList(SplitterClassName);
            splitter.style.width = SplitterWidth;

            toggleButton = new Button(toggleAction)
            {
                focusable = false
            };
            toggleButton.AddToClassList(SplitterToggleClassName);

            toggleIcon = new Icon(IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: 10f));
            toggleButton.Add(toggleIcon);

            var grip = new VisualElement();
            grip.AddToClassList(SplitterGripClassName);
            var handle = new VisualElement();
            handle.AddToClassList(SplitterHandleClassName);
            handle.pickingMode = PickingMode.Ignore;
            grip.Add(handle);
            grip.AddManipulator(new SplitterDragManipulator(this, kind));

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

            splitter.Add(toggleButton);
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
            var navigationBodyVisible = !_navigationCollapsed && _navigationWidth >= _navigationMinWidth;
            var inspectorBodyVisible = !_inspectorCollapsed && _inspectorWidth >= _inspectorMinWidth;

            _navigationPane.style.display = _navigationCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _navigationPane.style.width = _navigationCollapsed ? 0f : _navigationWidth;
            _navigationPane.style.minWidth = 0f;
            NavigationPaneContent.style.display = navigationBodyVisible ? DisplayStyle.Flex : DisplayStyle.None;

            _contentPane.style.display = DisplayStyle.Flex;
            _contentPane.style.minWidth = 0f;

            _inspectorPane.style.display = _inspectorCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _inspectorPane.style.width = _inspectorCollapsed ? 0f : _inspectorWidth;
            _inspectorPane.style.minWidth = 0f;
            InspectorPaneContent.style.display = inspectorBodyVisible ? DisplayStyle.Flex : DisplayStyle.None;

            _navigationSplitter.EnableInClassList(SplitterCollapsedClassName, _navigationCollapsed);
            _inspectorSplitter.EnableInClassList(SplitterCollapsedClassName, _inspectorCollapsed);

            UpdateToggleIcon(_navigationToggleIcon, _navigationCollapsed ? 0f : 180f);
            UpdateToggleIcon(_inspectorToggleIcon, _inspectorCollapsed ? 180f : 0f);
        }

        private static void UpdateToggleIcon(Icon icon, float rotationDegrees)
        {
            if (icon == null)
            {
                return;
            }

            icon.SetState(IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: 10f));
            icon.style.rotate = new Rotate(new Angle(rotationDegrees, AngleUnit.Degree));
        }

        private void NormalizePaneWidths()
        {
            _navigationMinWidth = Mathf.Max(0f, _navigationMinWidth);
            _navigationMaxWidth = Mathf.Max(0f, _navigationMaxWidth);
            _contentMinWidth = Mathf.Max(0f, _contentMinWidth);
            _inspectorMinWidth = Mathf.Max(0f, _inspectorMinWidth);
            _inspectorMaxWidth = Mathf.Max(0f, _inspectorMaxWidth);
            _navigationWidth = Mathf.Max(0f, _navigationWidth);
            _inspectorWidth = Mathf.Max(0f, _inspectorWidth);

            if (_navigationCollapsed && _inspectorCollapsed)
            {
                return;
            }

            if (!_navigationCollapsed)
            {
                _navigationWidth = ClampPaneWidth(_navigationWidth, _navigationMinWidth, ResolveNavigationMaxWidth(_inspectorCollapsed ? 0f : _inspectorWidth), true);
            }

            if (!_inspectorCollapsed)
            {
                _inspectorWidth = ClampPaneWidth(_inspectorWidth, _inspectorMinWidth, ResolveInspectorMaxWidth(_navigationCollapsed ? 0f : _navigationWidth), true);
            }

            if (!_navigationCollapsed)
            {
                _navigationWidth = ClampPaneWidth(_navigationWidth, _navigationMinWidth, ResolveNavigationMaxWidth(_inspectorCollapsed ? 0f : _inspectorWidth), true);
            }
        }

        private float ResolveNavigationMaxWidth(float inspectorWidth)
        {
            var layoutMaxWidth = GetNavigationLayoutMaxWidth(inspectorWidth);
            if (_navigationMaxWidth <= 0f)
            {
                return layoutMaxWidth;
            }

            return Mathf.Min(layoutMaxWidth, _navigationMaxWidth);
        }

        private float ResolveInspectorMaxWidth(float navigationWidth)
        {
            var layoutMaxWidth = GetInspectorLayoutMaxWidth(navigationWidth);
            if (_inspectorMaxWidth <= 0f)
            {
                return layoutMaxWidth;
            }

            return Mathf.Min(layoutMaxWidth, _inspectorMaxWidth);
        }

        private float GetNavigationLayoutMaxWidth(float inspectorWidth)
        {
            var totalWidth = resolvedStyle.width;
            if (float.IsNaN(totalWidth) || totalWidth <= 0f)
            {
                return _navigationWidth;
            }

            return Mathf.Max(0f, totalWidth - (2f * SplitterWidth) - inspectorWidth - _contentMinWidth);
        }

        private float GetInspectorLayoutMaxWidth(float navigationWidth)
        {
            var totalWidth = resolvedStyle.width;
            if (float.IsNaN(totalWidth) || totalWidth <= 0f)
            {
                return _inspectorWidth;
            }

            return Mathf.Max(0f, totalWidth - (2f * SplitterWidth) - navigationWidth - _contentMinWidth);
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

        private void ToggleNavigationCollapsed()
        {
            SetNavigationCollapsed(!_navigationCollapsed);
        }

        private void ToggleInspectorCollapsed()
        {
            SetInspectorCollapsed(!_inspectorCollapsed);
        }

        private bool CanBeginDrag(SplitterKind kind)
        {
            switch (kind)
            {
                case SplitterKind.Navigation:
                    if (_navigationCollapsed)
                    {
                        return false;
                    }

                    return ResolveNavigationMaxWidth(_inspectorCollapsed ? 0f : _inspectorWidth) > 0f;
                case SplitterKind.Inspector:
                    if (_inspectorCollapsed)
                    {
                        return false;
                    }

                    return ResolveInspectorMaxWidth(_navigationCollapsed ? 0f : _navigationWidth) > 0f;
                default:
                    return false;
            }
        }

        private void BeginDrag(SplitterKind kind, float pointerX)
        {
            _draggingSplitter = kind;
            _dragBoundaryOffset = GetBoundaryPosition(kind) - pointerX;
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
                case SplitterKind.Navigation:
                    SetNavigationWidth(_navigationWidth, true, true);

                    break;
                case SplitterKind.Inspector:
                    SetInspectorWidth(_inspectorWidth, true, true);

                    break;
            }

            _draggingSplitter = null;
            _dragCursorOverlay.MarkDirtyRepaint();
        }

        private float GetBoundaryPosition(SplitterKind kind)
        {
            switch (kind)
            {
                case SplitterKind.Navigation:
                    return _navigationCollapsed ? 0f : _navigationWidth;
                case SplitterKind.Inspector:
                    return _inspectorCollapsed
                        ? resolvedStyle.width - SplitterWidth
                        : resolvedStyle.width - _inspectorWidth - SplitterWidth;
                default:
                    return 0f;
            }
        }

        private void ApplyBoundaryPosition(SplitterKind kind, float boundaryPosition)
        {
            switch (kind)
            {
                case SplitterKind.Navigation:
                    SetNavigationWidth(boundaryPosition, true, true);
                    break;
                case SplitterKind.Inspector:
                    SetInspectorWidth(resolvedStyle.width - boundaryPosition - SplitterWidth, true, true);
                    break;
            }
        }

        private void SetNavigationWidth(float width, bool notify, bool enforceMin)
        {
            var maxWidth = ResolveNavigationMaxWidth(_inspectorCollapsed ? 0f : _inspectorWidth);
            var nextWidth = ClampPaneWidth(width, _navigationMinWidth, maxWidth, enforceMin);
            var widthChanged = !Mathf.Approximately(nextWidth, _navigationWidth);
            if (!widthChanged)
            {
                return;
            }

            _navigationWidth = nextWidth;

            if (enforceMin)
            {
                NormalizePaneWidths();
            }

            RefreshLayout();

            if (notify && widthChanged)
            {
                NavigationPaneWidthChanged?.Invoke(_navigationWidth);
            }
        }

        private void SetInspectorWidth(float width, bool notify, bool enforceMin)
        {
            var maxWidth = ResolveInspectorMaxWidth(_navigationCollapsed ? 0f : _navigationWidth);
            var nextWidth = ClampPaneWidth(width, _inspectorMinWidth, maxWidth, enforceMin);
            var widthChanged = !Mathf.Approximately(nextWidth, _inspectorWidth);
            if (!widthChanged)
            {
                return;
            }

            _inspectorWidth = nextWidth;

            if (enforceMin)
            {
                NormalizePaneWidths();
            }

            RefreshLayout();

            if (notify && widthChanged)
            {
                InspectorPaneWidthChanged?.Invoke(_inspectorWidth);
            }
        }

        private enum SplitterKind
        {
            Navigation,
            Inspector
        }

        private sealed class SplitterDragManipulator : PointerManipulator
        {
            private readonly AssetManagerWindowLayout _owner;
            private readonly SplitterKind _kind;
            private bool _active;
            private int _pointerId = -1;

            public SplitterDragManipulator(AssetManagerWindowLayout owner, SplitterKind kind)
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
