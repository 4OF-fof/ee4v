using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabsViewState
    {
        public ProjectTabsViewState(
            IReadOnlyList<ProjectTabViewState> tabs,
            string selectedTabId,
            bool canGoBack,
            bool canGoForward,
            IReadOnlyList<ProjectHistoryEntryViewState> backHistory = null,
            IReadOnlyList<ProjectHistoryEntryViewState> forwardHistory = null)
        {
            Tabs = tabs ?? Array.Empty<ProjectTabViewState>();
            SelectedTabId = selectedTabId ?? string.Empty;
            CanGoBack = canGoBack;
            CanGoForward = canGoForward;
            BackHistory = backHistory ??
                Array.Empty<ProjectHistoryEntryViewState>();
            ForwardHistory = forwardHistory ??
                Array.Empty<ProjectHistoryEntryViewState>();
        }

        public IReadOnlyList<ProjectTabViewState> Tabs { get; }

        public string SelectedTabId { get; }

        public bool CanGoBack { get; }

        public bool CanGoForward { get; }

        public IReadOnlyList<ProjectHistoryEntryViewState> BackHistory { get; }

        public IReadOnlyList<ProjectHistoryEntryViewState> ForwardHistory { get; }
    }

    internal sealed class ProjectHistoryEntryViewState
    {
        public ProjectHistoryEntryViewState(string label, int steps)
        {
            Label = label ?? string.Empty;
            Steps = steps;
        }

        public string Label { get; }

        public int Steps { get; }
    }

    internal sealed class ProjectTabViewState
    {
        public ProjectTabViewState(
            string id,
            string title,
            string tooltip,
            bool canClose,
            bool isPinned = false,
            bool isHome = false)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            CanClose = canClose;
            IsPinned = isPinned || isHome;
            IsHome = isHome;
        }

        public string Id { get; }

        public string Title { get; }

        public string Tooltip { get; }

        public bool CanClose { get; }

        public bool IsPinned { get; }

        public bool IsHome { get; }
    }

    internal sealed class ProjectTabsView : VisualElement
    {
        private const string RootClassName = "ee4v-project-tabs";
        private const string NavigationClassName =
            "ee4v-project-tabs__navigation";
        private const string NavigationButtonClassName =
            "ee4v-project-tabs__navigation-button";
        private const string NavigationLabelClassName =
            "ee4v-project-tabs__navigation-label";
        private const string ScrollClassName =
            "ee4v-project-tabs__scroll";
        private const string StripClassName =
            "ee4v-project-tabs__strip";
        private const string TabClassName =
            "ee4v-project-tabs__tab";
        private const string SelectedTabClassName =
            "ee4v-project-tabs__tab--selected";
        private const string TabTitleClassName =
            "ee4v-project-tabs__tab-title";
        private const string PinnedTabClassName =
            "ee4v-project-tabs__tab--pinned";
        private const string HomeTabClassName =
            "ee4v-project-tabs__tab--home";
        private const string PinIconClassName =
            "ee4v-project-tabs__pin-icon";
        private const string HomeIconClassName =
            "ee4v-project-tabs__home-icon";
        private const string DraggingTabClassName =
            "ee4v-project-tabs__tab--dragging";
        private const string DropIndicatorClassName =
            "ee4v-project-tabs__drop-indicator";
        private const string FolderDropTargetClassName =
            "ee4v-project-tabs__scroll--folder-drop-target";
        private const string CloseButtonClassName =
            "ee4v-project-tabs__close";
        private const string AddButtonClassName =
            "ee4v-project-tabs__add";
        private const float DragThreshold = 6f;

        private readonly Button _backButton;
        private readonly Button _forwardButton;
        private readonly ScrollView _scroll;
        private readonly VisualElement _strip;
        private readonly Button _addButton;
        private ProjectTabsViewState _state;
        private VisualElement _potentialDragTab;
        private VisualElement _draggingTab;
        private VisualElement _dropIndicator;
        private int _dragPointerId = -1;
        private int _dragOriginalIndex = -1;
        private int _dragTargetIndex = -1;
        private Vector2 _dragStartPosition;
        private string _suppressedClickTabId;

        public ProjectTabsView()
        {
            AddToClassList("ee4v-ui");
            AddToClassList(RootClassName);
            UiStyleUtility.AddPackageStyleSheet(
                this,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                this,
                "Editor/EditorEnhancements/ProjectTabs/UI/project-tabs.uss");

            var navigation = new VisualElement();
            navigation.AddToClassList(NavigationClassName);

            _backButton = CreateNavigationButton(
                "\u2190",
                () => BackRequested?.Invoke());
            _forwardButton = CreateNavigationButton(
                "\u2192",
                () => ForwardRequested?.Invoke());
            RegisterHistoryButton(
                _backButton,
                () => _state.BackHistory,
                true);
            RegisterHistoryButton(
                _forwardButton,
                () => _state.ForwardHistory,
                false);
            navigation.Add(_backButton);
            navigation.Add(_forwardButton);

            _scroll = new ScrollView(ScrollViewMode.Horizontal);
            _scroll.AddToClassList(ScrollClassName);
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            _strip = _scroll.contentContainer;
            _strip.AddToClassList(StripClassName);
            RegisterTabDragEvents();
            RegisterFolderDropEvents();

            _addButton = new Button(() => AddRequested?.Invoke())
            {
                text = "+"
            };
            _addButton.tooltip = I18N.Get("toolbar.add.tooltip");
            _addButton.AddToClassList(AddButtonClassName);

            _state = new ProjectTabsViewState(
                null,
                string.Empty,
                false,
                false);

            Add(navigation);
            Add(_scroll);
        }

        public event Action BackRequested;

        public event Action ForwardRequested;

        public event Action<int> BackHistoryRequested;

        public event Action<int> ForwardHistoryRequested;

        public event Action AddRequested;

        public event Action<string> TabSelected;

        public event Action<string> TabCloseRequested;

        public event Action<string, int> TabMoveRequested;

        public event Action<string> TabPinToggleRequested;

        public event Func<IReadOnlyList<string>, bool>
            FolderDropAcceptanceRequested;

        public event Action<IReadOnlyList<string>> FolderDropRequested;

        public void SetState(ProjectTabsViewState state)
        {
            CancelTabDrag();
            _state = state ??
                new ProjectTabsViewState(null, string.Empty, false, false);
            _backButton.SetEnabled(_state.CanGoBack);
            _forwardButton.SetEnabled(_state.CanGoForward);
            _strip.Clear();
            for (var i = 0; i < _state.Tabs.Count; i++)
            {
                var tab = _state.Tabs[i];
                if (tab == null)
                {
                    continue;
                }

                var minimumMoveIndex = tab.IsHome
                    ? 0
                    : 1;
                var maximumMoveIndex = tab.IsHome
                    ? 0
                    : _state.Tabs.Count - 1;
                var tabElement = CreateTab(
                    tab,
                    i,
                    minimumMoveIndex,
                    maximumMoveIndex);
                tabElement.EnableInClassList(
                    SelectedTabClassName,
                    string.Equals(
                        tab.Id,
                        _state.SelectedTabId,
                        StringComparison.Ordinal));
                _strip.Add(tabElement);
            }

            _strip.Add(_addButton);
        }

        private void RegisterHistoryButton(
            Button button,
            Func<IReadOnlyList<ProjectHistoryEntryViewState>> getEntries,
            bool back)
        {
            button.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowHistoryMenu(button, getEntries(), back);
                evt.StopPropagation();
            });
        }

        private void ShowHistoryMenu(
            VisualElement anchor,
            IReadOnlyList<ProjectHistoryEntryViewState> entries,
            bool back)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var rows = new HistoryNavigationOverlayRowState[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var steps = entry != null ? entry.Steps : 0;
                rows[i] = new HistoryNavigationOverlayRowState(
                    entry != null ? entry.Label : string.Empty,
                    steps > 0
                        ? (Action)(() =>
                        {
                            if (back)
                            {
                                BackHistoryRequested?.Invoke(steps);
                            }
                            else
                            {
                                ForwardHistoryRequested?.Invoke(steps);
                            }
                        })
                        : null);
            }

            HistoryNavigationMenu.Show(anchor, rows);
        }

        internal static int FindInsertionIndex(
            IReadOnlyList<float> itemCenters,
            float pointerX)
        {
            if (itemCenters == null ||
                float.IsNaN(pointerX) ||
                float.IsInfinity(pointerX))
            {
                return -1;
            }

            for (var i = 0; i < itemCenters.Count; i++)
            {
                var center = itemCenters[i];
                if (float.IsNaN(center) ||
                    float.IsInfinity(center))
                {
                    return -1;
                }

                if (pointerX < center)
                {
                    return i;
                }
            }

            return itemCenters.Count;
        }

        private VisualElement CreateTab(
            ProjectTabViewState state,
            int index,
            int minimumMoveIndex,
            int maximumMoveIndex)
        {
            var data = new TabElementData(
                state.Id,
                minimumMoveIndex,
                maximumMoveIndex,
                !state.IsHome &&
                maximumMoveIndex > minimumMoveIndex);
            var tab = new VisualElement
            {
                tooltip = state.Tooltip,
                focusable = true,
                tabIndex = 0,
                userData = data
            };
            tab.AddToClassList(TabClassName);
            tab.EnableInClassList(
                PinnedTabClassName,
                state.IsPinned && !state.IsHome);
            tab.EnableInClassList(
                HomeTabClassName,
                state.IsHome);

            if (state.IsHome)
            {
                var homeIcon = new Icon(
                    IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Home,
                        UiSizeTokens.Size12,
                        I18N.Get("toolbar.home.tooltip")));
                homeIcon.AddToClassList(HomeIconClassName);
                tab.Add(homeIcon);
            }
            else
            {
                if (state.IsPinned)
                {
                    var pinIcon = new Icon(
                        IconState.FromBuiltinIcon(
                            UiBuiltinIcon.Pin,
                            UiSizeTokens.Size10,
                            I18N.Get("toolbar.pin.tooltip")));
                    pinIcon.AddToClassList(PinIconClassName);
                    tab.Add(pinIcon);
                }

                var title = UiTextFactory.Create(state.Title);
                title.AddToClassList(TabTitleClassName);
                title.SetWhiteSpace(WhiteSpace.NoWrap);
                title.pickingMode = PickingMode.Ignore;
                tab.Add(title);
            }

            var closeButton = new Button(
                () => TabCloseRequested?.Invoke(state.Id));
            closeButton.tooltip = I18N.Get("toolbar.close.tooltip");
            closeButton.AddToClassList(CloseButtonClassName);
            closeButton.style.display = state.CanClose
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            closeButton.Add(new Icon(
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Close,
                    UiSizeTokens.Size12)));
            tab.Add(closeButton);

            tab.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.LeftMouse ||
                    evt.target == closeButton)
                {
                    return;
                }

                if (string.Equals(
                        _suppressedClickTabId,
                        state.Id,
                        StringComparison.Ordinal))
                {
                    _suppressedClickTabId = null;
                    evt.StopPropagation();
                    return;
                }

                TabSelected?.Invoke(state.Id);
            });
            tab.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 2 && state.CanClose)
                {
                    TabCloseRequested?.Invoke(state.Id);
                    evt.StopPropagation();
                }
            });
            tab.RegisterCallback<KeyDownEvent>(evt =>
            {
                var reorderModifier =
                    evt.shiftKey &&
                    (evt.ctrlKey || evt.commandKey);
                if (reorderModifier &&
                    evt.keyCode == UnityEngine.KeyCode.LeftArrow &&
                    index > minimumMoveIndex)
                {
                    TabMoveRequested?.Invoke(state.Id, index - 1);
                    evt.StopPropagation();
                    return;
                }

                if (reorderModifier &&
                    evt.keyCode == UnityEngine.KeyCode.RightArrow &&
                    index < maximumMoveIndex)
                {
                    TabMoveRequested?.Invoke(state.Id, index + 1);
                    evt.StopPropagation();
                    return;
                }

                if (evt.keyCode == UnityEngine.KeyCode.Return ||
                    evt.keyCode == UnityEngine.KeyCode.Space)
                {
                    TabSelected?.Invoke(state.Id);
                    evt.StopPropagation();
                }
            });
            tab.RegisterCallback<ContextClickEvent>(evt =>
            {
                if (evt.target != closeButton &&
                    !state.IsHome)
                {
                    TabPinToggleRequested?.Invoke(state.Id);
                }

                evt.PreventDefault();
                evt.StopImmediatePropagation();
            });

            return tab;
        }

        private void RegisterTabDragEvents()
        {
            _strip.RegisterCallback<PointerDownEvent>(
                OnTabPointerDown,
                TrickleDown.TrickleDown);
            _strip.RegisterCallback<PointerMoveEvent>(
                OnTabPointerMove,
                TrickleDown.TrickleDown);
            _strip.RegisterCallback<PointerUpEvent>(
                OnTabPointerUp,
                TrickleDown.TrickleDown);
            _strip.RegisterCallback<PointerCaptureOutEvent>(
                _ => CancelTabDrag());
        }

        private void OnTabPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse ||
                IsWithinClass(evt.target, CloseButtonClassName))
            {
                return;
            }

            var tab = FindTabElement(evt.target);
            var data = tab?.userData as TabElementData;
            if (tab == null || data == null || !data.CanMove)
            {
                return;
            }

            CancelTabDrag();
            _potentialDragTab = tab;
            _dragPointerId = evt.pointerId;
            _dragStartPosition = new Vector2(
                evt.position.x,
                evt.position.y);
            _strip.CapturePointer(evt.pointerId);
        }

        private void OnTabPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _dragPointerId ||
                (_potentialDragTab == null && _draggingTab == null))
            {
                return;
            }

            var pointerPosition = new Vector2(
                evt.position.x,
                evt.position.y);
            if (_draggingTab == null)
            {
                if (Vector2.Distance(
                        _dragStartPosition,
                        pointerPosition) < DragThreshold)
                {
                    return;
                }

                BeginTabDrag();
            }

            UpdateTabDrag(evt.position.x);
            evt.StopPropagation();
        }

        private void OnTabPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _dragPointerId)
            {
                return;
            }

            var clickedTabId =
                (_potentialDragTab?.userData as TabElementData)?.Id;
            var draggedTab = _draggingTab;
            var draggedTabId =
                (draggedTab?.userData as TabElementData)?.Id;
            var targetIndex = _dragTargetIndex;
            var originalIndex = _dragOriginalIndex;
            var wasDragging = draggedTab != null;
            CancelTabDrag();

            if (!wasDragging)
            {
                if (!string.IsNullOrEmpty(clickedTabId))
                {
                    SuppressNextClick(clickedTabId);
                    TabSelected?.Invoke(clickedTabId);
                }

                evt.StopPropagation();
                return;
            }

            SuppressNextClick(draggedTabId);
            if (!string.IsNullOrEmpty(draggedTabId) &&
                targetIndex >= 0 &&
                targetIndex != originalIndex)
            {
                TabMoveRequested?.Invoke(
                    draggedTabId,
                    targetIndex);
            }

            evt.StopPropagation();
        }

        private void BeginTabDrag()
        {
            _draggingTab = _potentialDragTab;
            _potentialDragTab = null;
            _dragOriginalIndex = GetTabElements().IndexOf(
                _draggingTab);
            _dragTargetIndex = _dragOriginalIndex;
            _draggingTab?.AddToClassList(
                DraggingTabClassName);
            _dropIndicator = new VisualElement();
            _dropIndicator.AddToClassList(
                DropIndicatorClassName);
        }

        private void UpdateTabDrag(float pointerX)
        {
            if (_draggingTab == null || _dropIndicator == null)
            {
                return;
            }

            var otherTabs = GetTabElements();
            otherTabs.Remove(_draggingTab);
            var centers = new float[otherTabs.Count];
            for (var i = 0; i < otherTabs.Count; i++)
            {
                centers[i] = otherTabs[i].worldBound.center.x;
            }

            var targetIndex = FindInsertionIndex(
                centers,
                pointerX);
            if (targetIndex < 0)
            {
                return;
            }

            var data = _draggingTab.userData as TabElementData;
            if (data == null)
            {
                return;
            }

            targetIndex = Mathf.Clamp(
                targetIndex,
                data.MinimumMoveIndex,
                data.MaximumMoveIndex);
            _dragTargetIndex = targetIndex;
            _dropIndicator.RemoveFromHierarchy();
            var reference = targetIndex < otherTabs.Count
                ? otherTabs[targetIndex]
                : _addButton;
            var insertIndex = _strip.IndexOf(reference);
            if (insertIndex < 0)
            {
                insertIndex = _strip.childCount;
            }

            _strip.Insert(insertIndex, _dropIndicator);
        }

        private void CancelTabDrag()
        {
            var pointerId = _dragPointerId;
            if (_draggingTab != null)
            {
                _draggingTab.RemoveFromClassList(
                    DraggingTabClassName);
            }

            _dropIndicator?.RemoveFromHierarchy();
            _potentialDragTab = null;
            _draggingTab = null;
            _dropIndicator = null;
            _dragPointerId = -1;
            _dragOriginalIndex = -1;
            _dragTargetIndex = -1;

            if (pointerId >= 0 &&
                _strip.HasPointerCapture(pointerId))
            {
                _strip.ReleasePointer(pointerId);
            }
        }

        private void SuppressNextClick(string tabId)
        {
            if (string.IsNullOrEmpty(tabId))
            {
                return;
            }

            _suppressedClickTabId = tabId;
            schedule.Execute(() =>
            {
                if (string.Equals(
                        _suppressedClickTabId,
                        tabId,
                        StringComparison.Ordinal))
                {
                    _suppressedClickTabId = null;
                }
            });
        }

        private List<VisualElement> GetTabElements()
        {
            var tabs = new List<VisualElement>();
            for (var i = 0; i < _strip.childCount; i++)
            {
                var child = _strip[i];
                if (child.ClassListContains(TabClassName))
                {
                    tabs.Add(child);
                }
            }

            return tabs;
        }

        private VisualElement FindTabElement(IEventHandler target)
        {
            var element = target as VisualElement;
            while (element != null && element != _strip)
            {
                if (element.parent == _strip &&
                    element.ClassListContains(TabClassName))
                {
                    return element;
                }

                element = element.parent;
            }

            return null;
        }

        private bool IsWithinClass(
            IEventHandler target,
            string className)
        {
            var element = target as VisualElement;
            while (element != null && element != _strip)
            {
                if (element.ClassListContains(className))
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private void RegisterFolderDropEvents()
        {
            _scroll.RegisterCallback<DragEnterEvent>(
                OnFolderDragEnter);
            _scroll.RegisterCallback<DragLeaveEvent>(
                OnFolderDragLeave);
            _scroll.RegisterCallback<DragUpdatedEvent>(
                OnFolderDragUpdated);
            _scroll.RegisterCallback<DragPerformEvent>(
                OnFolderDragPerform);
        }

        private void OnFolderDragEnter(DragEnterEvent evt)
        {
            SetFolderDropHighlight(
                CanAcceptFolderDrop(GetDragPaths()));
        }

        private void OnFolderDragLeave(DragLeaveEvent evt)
        {
            SetFolderDropHighlight(false);
        }

        private void OnFolderDragUpdated(DragUpdatedEvent evt)
        {
            var accepted = CanAcceptFolderDrop(GetDragPaths());
            DragAndDrop.visualMode = accepted
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;
            SetFolderDropHighlight(accepted);
            if (accepted)
            {
                evt.StopPropagation();
            }
        }

        private void OnFolderDragPerform(DragPerformEvent evt)
        {
            var paths = GetDragPaths();
            if (!CanAcceptFolderDrop(paths))
            {
                SetFolderDropHighlight(false);
                return;
            }

            DragAndDrop.AcceptDrag();
            FolderDropRequested?.Invoke(paths);
            SetFolderDropHighlight(false);
            evt.StopPropagation();
        }

        private bool CanAcceptFolderDrop(
            IReadOnlyList<string> paths)
        {
            return FolderDropAcceptanceRequested?.Invoke(paths) ==
                true;
        }

        private void SetFolderDropHighlight(bool enabled)
        {
            _scroll.EnableInClassList(
                FolderDropTargetClassName,
                enabled);
        }

        private static IReadOnlyList<string> GetDragPaths()
        {
            var paths = DragAndDrop.paths;
            return paths == null || paths.Length == 0
                ? Array.Empty<string>()
                : (IReadOnlyList<string>)paths;
        }

        private static Button CreateNavigationButton(
            string text,
            Action clicked)
        {
            var button = new Button(clicked);
            button.AddToClassList(NavigationButtonClassName);

            var label = UiTextFactory.Create(text);
            label.AddToClassList(NavigationLabelClassName);
            label.SetWhiteSpace(WhiteSpace.NoWrap);
            label.pickingMode = PickingMode.Ignore;
            label.style.alignItems = Align.Center;
            label.style.justifyContent = Justify.Center;
            button.Add(label);
            return button;
        }

        private sealed class TabElementData
        {
            public TabElementData(
                string id,
                int minimumMoveIndex,
                int maximumMoveIndex,
                bool canMove)
            {
                Id = id ?? string.Empty;
                MinimumMoveIndex = minimumMoveIndex;
                MaximumMoveIndex = maximumMoveIndex;
                CanMove = canMove;
            }

            public string Id { get; }

            public int MinimumMoveIndex { get; }

            public int MaximumMoveIndex { get; }

            public bool CanMove { get; }
        }
    }
}
