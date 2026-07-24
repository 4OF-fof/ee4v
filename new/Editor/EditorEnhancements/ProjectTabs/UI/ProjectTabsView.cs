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
            bool canClose)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            CanClose = canClose;
        }

        public string Id { get; }

        public string Title { get; }

        public string Tooltip { get; }

        public bool CanClose { get; }
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
        private const string CloseButtonClassName =
            "ee4v-project-tabs__close";
        private const string AddButtonClassName =
            "ee4v-project-tabs__add";

        private readonly Button _backButton;
        private readonly Button _forwardButton;
        private readonly VisualElement _strip;
        private readonly Button _addButton;
        private ProjectTabsViewState _state;

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

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList(ScrollClassName);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            _strip = scroll.contentContainer;
            _strip.AddToClassList(StripClassName);

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
            Add(scroll);
        }

        public event Action BackRequested;

        public event Action ForwardRequested;

        public event Action<int> BackHistoryRequested;

        public event Action<int> ForwardHistoryRequested;

        public event Action AddRequested;

        public event Action<string> TabSelected;

        public event Action<string> TabCloseRequested;

        public void SetState(ProjectTabsViewState state)
        {
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

                var tabElement = CreateTab(tab);
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

        private VisualElement CreateTab(ProjectTabViewState state)
        {
            var tab = new VisualElement
            {
                tooltip = state.Tooltip,
                focusable = true,
                tabIndex = 0
            };
            tab.AddToClassList(TabClassName);

            var title = UiTextFactory.Create(state.Title);
            title.AddToClassList(TabTitleClassName);
            title.SetWhiteSpace(WhiteSpace.NoWrap);
            title.pickingMode = PickingMode.Ignore;
            tab.Add(title);

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
                if (evt.target == closeButton)
                {
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
                if (evt.keyCode == UnityEngine.KeyCode.Return ||
                    evt.keyCode == UnityEngine.KeyCode.Space)
                {
                    TabSelected?.Invoke(state.Id);
                    evt.StopPropagation();
                }
            });
            tab.RegisterCallback<ContextClickEvent>(evt =>
            {
                var menu = new GenericMenu();
                if (state.CanClose)
                {
                    menu.AddItem(
                        new GUIContent(I18N.Get("toolbar.close.menu")),
                        false,
                        () => TabCloseRequested?.Invoke(state.Id));
                }
                else
                {
                    menu.AddDisabledItem(
                        new GUIContent(I18N.Get("toolbar.close.menu")));
                }

                menu.ShowAsContext();
                evt.StopPropagation();
            });

            return tab;
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
    }
}
