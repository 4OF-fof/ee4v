using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsWindow : EditorWindow
    {
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName =
            "ee4v-hidden-objects-window";
        private const float MinimumWidth = 420f;
        private const float MinimumHeight = 280f;

        private readonly HashSet<int> _collapsedInstanceIds =
            new HashSet<int>();
        private readonly Dictionary<string, int> _sceneHandleByLabel =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private HiddenObjectsController _controller;
        private SearchField _searchField;
        private PopupField<string> _scenePopup;
        private UiTextElement _summaryText;
        private VisualElement _treeHost;
        private Button _selectAllButton;
        private Button _clearSelectionButton;
        private Button _revealButton;

        internal static void OpenForScene(int sceneHandle)
        {
            var window = GetWindow<HiddenObjectsWindow>();
            window.EnsureController();
            window._controller.Refresh();
            window._controller.SetSceneFilter(sceneHandle);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(
                I18N.Get("window.title"));
            minSize = new Vector2(MinimumWidth, MinimumHeight);
            EnsureController();
            _controller.StateChanged += RenderState;
            EditorApplication.hierarchyChanged +=
                OnHierarchyChanged;
            Undo.undoRedoPerformed += OnHierarchyChanged;
            I18N.Reloaded += OnLocalizationReloaded;
            _controller.Refresh();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= RenderState;
            }

            EditorApplication.hierarchyChanged -=
                OnHierarchyChanged;
            Undo.undoRedoPerformed -= OnHierarchyChanged;
            I18N.Reloaded -= OnLocalizationReloaded;
        }

        private void OnFocus()
        {
            _controller?.Refresh();
        }

        private void CreateGUI()
        {
            EnsureController();
            BuildWindow();
            RenderState(_controller.State);
        }

        private void EnsureController()
        {
            if (_controller != null)
            {
                return;
            }

            _controller = new HiddenObjectsController(
                new UnityHiddenObjectRepository(),
                new UnityHiddenObjectNavigator());
        }

        private void BuildWindow()
        {
            titleContent = new GUIContent(
                I18N.Get("window.title"));

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/EditorEnhancements/HiddenObjects/UI/hidden-objects-window.uss");

            var header = new VisualElement();
            header.AddToClassList(
                "ee4v-hidden-objects-window__header");

            var title = UiTextFactory.Create(
                I18N.Get("window.heading"),
                "ee4v-hidden-objects-window__title",
                UiClassNames.WindowTitle);
            _summaryText = UiTextFactory.Create(
                string.Empty,
                "ee4v-hidden-objects-window__summary",
                UiClassNames.SecondaryText);
            header.Add(title);
            header.Add(_summaryText);

            var filters = new VisualElement();
            filters.AddToClassList(
                "ee4v-hidden-objects-window__filters");
            _searchField = new SearchField(
                new SearchFieldState(
                    _controller.State.Query,
                    I18N.Get("window.search.placeholder"),
                    I18N.Get("window.search.tooltip"),
                    I18N.Get("window.search.clearTooltip")));
            _searchField.AddToClassList(
                "ee4v-hidden-objects-window__search");
            _searchField.ValueChanged += _controller.SetQuery;

            _scenePopup = new PopupField<string>(
                new List<string>
                {
                    I18N.Get("window.scene.all")
                },
                0);
            _scenePopup.tooltip =
                I18N.Get("window.scene.tooltip");
            _scenePopup.AddToClassList(
                "ee4v-hidden-objects-window__scene-filter");
            _scenePopup.RegisterValueChangedCallback(
                OnSceneFilterChanged);

            var refreshButton = new Button(
                _controller.Refresh)
            {
                text = I18N.Get("window.action.refresh"),
                tooltip = I18N.Get(
                    "window.action.refreshTooltip")
            };
            refreshButton.AddToClassList(
                "ee4v-hidden-objects-window__refresh");

            filters.Add(_searchField);
            filters.Add(_scenePopup);
            filters.Add(refreshButton);

            var scrollView = new ScrollView();
            scrollView.AddToClassList(
                "ee4v-hidden-objects-window__scroll");
            _treeHost = new VisualElement();
            _treeHost.AddToClassList(
                "ee4v-hidden-objects-window__tree");
            scrollView.Add(_treeHost);

            var actions = new VisualElement();
            actions.AddToClassList(
                "ee4v-hidden-objects-window__actions");
            _selectAllButton = new Button(
                _controller.SelectAllVisible)
            {
                text = I18N.Get(
                    "window.action.selectAllVisible")
            };
            _clearSelectionButton = new Button(
                _controller.ClearSelection)
            {
                text = I18N.Get(
                    "window.action.clearSelection")
            };
            _revealButton = new Button(RevealSelected)
            {
                text = I18N.Get(
                    "window.action.revealSelected")
            };
            _revealButton.AddToClassList(
                "ee4v-hidden-objects-window__primary-action");

            actions.Add(_selectAllButton);
            actions.Add(_clearSelectionButton);
            actions.Add(_revealButton);

            root.Add(header);
            root.Add(filters);
            root.Add(scrollView);
            root.Add(actions);
        }

        private void RenderState(HiddenObjectsState state)
        {
            if (state == null || _treeHost == null)
            {
                return;
            }

            var selectedIds = new HashSet<int>(
                state.SelectedInstanceIds);
            _summaryText.SetText(I18N.Get(
                "window.summary",
                state.TotalHiddenCount,
                state.VisibleHiddenCount,
                selectedIds.Count));
            UpdateSceneChoices(state);

            _treeHost.Clear();
            if (state.SceneGroups.Count == 0)
            {
                var emptyText = state.TotalHiddenCount == 0
                    ? I18N.Get("window.empty.noHidden")
                    : I18N.Get("window.empty.noMatches");
                _treeHost.Add(UiTextFactory.Create(
                    emptyText,
                    "ee4v-hidden-objects-window__empty",
                    UiClassNames.SecondaryText));
            }
            else
            {
                for (var groupIndex = 0;
                     groupIndex < state.SceneGroups.Count;
                     groupIndex++)
                {
                    RenderSceneGroup(
                        state.SceneGroups[groupIndex],
                        selectedIds,
                        !string.IsNullOrWhiteSpace(state.Query));
                }
            }

            _selectAllButton.SetEnabled(
                state.VisibleHiddenCount > 0);
            _clearSelectionButton.SetEnabled(
                selectedIds.Count > 0);
            _revealButton.SetEnabled(selectedIds.Count > 0);
        }

        private void RenderSceneGroup(
            HiddenObjectSceneGroup group,
            ISet<int> selectedIds,
            bool expandForSearch)
        {
            var sceneContainer = new VisualElement();
            sceneContainer.AddToClassList(
                "ee4v-hidden-objects-window__scene");
            sceneContainer.Add(
                UiTextFactory.Create(
                    group.SceneName,
                    "ee4v-hidden-objects-window__scene-name",
                    UiClassNames.SectionTitle));

            for (var rootIndex = 0;
                 rootIndex < group.Roots.Count;
                 rootIndex++)
            {
                RenderNode(
                    sceneContainer,
                    group.Roots[rootIndex],
                    0,
                    selectedIds,
                    expandForSearch);
            }

            _treeHost.Add(sceneContainer);
        }

        private void RenderNode(
            VisualElement parent,
            HiddenObjectTreeNode node,
            int depth,
            ISet<int> selectedIds,
            bool expandForSearch)
        {
            var hasChildren = node.Children.Count > 0;
            var isExpanded = expandForSearch ||
                !_collapsedInstanceIds.Contains(node.InstanceId);
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-hidden-objects-window__row");
            if (!node.IsHidden)
            {
                row.AddToClassList(
                    "ee4v-hidden-objects-window__row--ancestor");
            }

            row.style.paddingLeft =
                UiSpacingTokens.Xs +
                depth * UiSizeTokens.Size14;

            var disclosure = new Button();
            disclosure.AddToClassList(
                "ee4v-hidden-objects-window__disclosure");
            if (hasChildren)
            {
                disclosure.Add(new Icon(
                    IconState.FromBuiltinIcon(
                        isExpanded
                            ? UiBuiltinIcon.DisclosureOpen
                            : UiBuiltinIcon.DisclosureClosed,
                        UiSizeTokens.Size10,
                        isExpanded
                            ? I18N.Get(
                                "window.tree.collapseTooltip")
                            : I18N.Get(
                                "window.tree.expandTooltip"))));
                disclosure.clicked += () =>
                {
                    if (!_collapsedInstanceIds.Add(
                            node.InstanceId))
                    {
                        _collapsedInstanceIds.Remove(
                            node.InstanceId);
                    }

                    RenderState(_controller.State);
                };
            }
            else
            {
                disclosure.SetEnabled(false);
            }

            row.Add(disclosure);

            if (node.IsHidden)
            {
                var toggle = new Toggle();
                toggle.AddToClassList(
                    "ee4v-hidden-objects-window__selection");
                toggle.SetValueWithoutNotify(
                    selectedIds.Contains(node.InstanceId));
                toggle.RegisterValueChangedCallback(
                    evt => _controller.SetSelected(
                        node.InstanceId,
                        evt.newValue));
                row.Add(toggle);
            }
            else
            {
                var selectionSpacer = new VisualElement();
                selectionSpacer.AddToClassList(
                    "ee4v-hidden-objects-window__selection-spacer");
                row.Add(selectionSpacer);
            }

            var target = EditorUtility.InstanceIDToObject(
                node.InstanceId);
            var texture = target != null
                ? AssetPreview.GetMiniThumbnail(target)
                : null;
            var icon = texture != null
                ? new Icon(IconState.FromTexture(
                    texture,
                    UiSizeTokens.Size16))
                : new Icon(IconState.FromBuiltinIcon(
                    UiBuiltinIcon.GenericFile,
                    UiSizeTokens.Size16));
            icon.AddToClassList(
                "ee4v-hidden-objects-window__object-icon");
            row.Add(icon);

            var name = UiTextFactory.Create(
                node.Name,
                "ee4v-hidden-objects-window__object-name");
            if (!node.IsHidden)
            {
                name.SetColor(UiColorTokens.TextMuted);
            }

            row.Add(name);
            row.RegisterCallback<ClickEvent>(
                evt =>
                {
                    if (evt.target != disclosure)
                    {
                        _controller.Focus(node.InstanceId);
                    }
                });
            parent.Add(row);

            if (!hasChildren || !isExpanded)
            {
                return;
            }

            for (var childIndex = 0;
                 childIndex < node.Children.Count;
                 childIndex++)
            {
                RenderNode(
                    parent,
                    node.Children[childIndex],
                    depth + 1,
                    selectedIds,
                    expandForSearch);
            }
        }

        private void UpdateSceneChoices(HiddenObjectsState state)
        {
            _sceneHandleByLabel.Clear();
            var allScenesLabel =
                I18N.Get("window.scene.all");
            _sceneHandleByLabel[allScenesLabel] = 0;

            var duplicateNames = new HashSet<string>(
                state.SceneOptions
                    .GroupBy(
                        option => option.Label,
                        StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
                StringComparer.Ordinal);
            var choices = new List<string>
            {
                allScenesLabel
            };
            var selectedLabel = allScenesLabel;

            for (var i = 0;
                 i < state.SceneOptions.Count;
                 i++)
            {
                var option = state.SceneOptions[i];
                var label = duplicateNames.Contains(option.Label)
                    ? I18N.Get(
                        "window.scene.duplicateFormat",
                        option.Label,
                        option.SceneHandle)
                    : option.Label;
                choices.Add(label);
                _sceneHandleByLabel[label] =
                    option.SceneHandle;
                if (option.SceneHandle ==
                    state.SelectedSceneHandle)
                {
                    selectedLabel = label;
                }
            }

            _scenePopup.choices = choices;
            _scenePopup.SetValueWithoutNotify(selectedLabel);
        }

        private void OnSceneFilterChanged(
            ChangeEvent<string> evt)
        {
            if (_sceneHandleByLabel.TryGetValue(
                    evt.newValue,
                    out var sceneHandle))
            {
                _controller.SetSceneFilter(sceneHandle);
            }
        }

        private void RevealSelected()
        {
            var count = _controller.RevealSelected(
                I18N.Get("undo.revealSelected"));
            if (count > 0)
            {
                ShowNotification(new GUIContent(
                    I18N.Get(
                        "window.notification.revealed",
                        count)));
            }
        }

        private void OnHierarchyChanged()
        {
            _controller?.Refresh();
        }

        private void OnLocalizationReloaded()
        {
            BuildWindow();
            RenderState(_controller.State);
        }
    }
}
