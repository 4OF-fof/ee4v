using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsWindow : EditorWindow
    {
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName =
            "ee4v-hidden-objects-window";
        private const float MinimumWidth = 420f;
        private const float MinimumHeight = 280f;

        private HiddenObjectsController _controller;
        private UnityHiddenObjectIconProvider _iconProvider;
        private HiddenObjectsView _view;

        internal static void OpenForScene(int sceneHandle)
        {
            var window = GetWindow<HiddenObjectsWindow>();
            window.EnsureDependencies();
            window._controller.Refresh();
            window._controller.SetSceneFilter(sceneHandle);
            window.Show();
            window.Focus();
        }

        internal static void RefreshAll()
        {
            var windows =
                Resources.FindObjectsOfTypeAll<HiddenObjectsWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i]._controller?.Refresh();
                windows[i].Repaint();
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(
                I18N.Get("window.title"));
            minSize = new Vector2(MinimumWidth, MinimumHeight);
            EnsureDependencies();
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
            EnsureDependencies();
            BuildWindow();
            RenderState(_controller.State);
        }

        private void EnsureDependencies()
        {
            if (_controller == null)
            {
                _controller =
                    HiddenObjectsBootstrap.CreateController();
            }

            if (_iconProvider == null)
            {
                _iconProvider =
                    HiddenObjectsBootstrap.CreateIconProvider();
            }
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

            _view = new HiddenObjectsView(CreateViewText());
            _view.QueryChanged += _controller.SetQuery;
            _view.SceneChanged += _controller.SetSceneFilter;
            _view.RefreshRequested += _controller.Refresh;
            _view.SelectionChanged += _controller.SetSelected;
            _view.FocusRequested += _controller.Focus;
            _view.SelectAllRequested +=
                _controller.SelectAllVisible;
            _view.ClearSelectionRequested +=
                _controller.ClearSelection;
            _view.RevealRequested += RevealSelected;
            root.Add(_view);
        }

        private void RenderState(HiddenObjectsState state)
        {
            if (state == null || _view == null)
            {
                return;
            }

            _view.SetState(CreateViewState(state));
        }

        private HiddenObjectsViewText CreateViewText()
        {
            var text = new HiddenObjectsViewText(
                I18N.Get("window.search.placeholder"),
                I18N.Get("window.search.tooltip"),
                I18N.Get("window.search.clearTooltip"),
                I18N.Get("window.scene.tooltip"),
                I18N.Get("window.action.refresh"),
                I18N.Get("window.action.refreshTooltip"),
                I18N.Get("window.action.selectAllVisible"),
                I18N.Get("window.action.clearSelection"),
                I18N.Get("window.action.revealSelected"));
            return text;
        }

        private HiddenObjectsViewState CreateViewState(
            HiddenObjectsState state)
        {
            var selectedIds = new HashSet<int>(
                state.SelectedInstanceIds);
            var groups = state.SceneGroups
                .Select(group => CreateSceneGroupState(
                    group,
                    selectedIds))
                .ToArray();
            var sceneOptions = CreateSceneOptions(state);
            var hasHiddenObjects = state.TotalHiddenCount > 0;
            var emptyTitle = hasHiddenObjects
                ? I18N.Get("window.empty.noMatchesTitle")
                : I18N.Get("window.empty.noHiddenTitle");
            var emptyMessage = hasHiddenObjects
                ? I18N.Get("window.empty.noMatches")
                : I18N.Get("window.empty.noHidden");

            return new HiddenObjectsViewState(
                groups,
                sceneOptions,
                state.SelectedSceneHandle,
                state.Query,
                I18N.Get(
                    "window.summary",
                    state.TotalHiddenCount,
                    state.VisibleHiddenCount,
                    selectedIds.Count),
                emptyTitle,
                emptyMessage,
                state.VisibleHiddenCount,
                selectedIds.Count);
        }

        private HiddenObjectSceneGroupViewState CreateSceneGroupState(
            HiddenObjectSceneGroup group,
            ISet<int> selectedIds)
        {
            var roots = group.Roots
                .Select(node => CreateNodeState(node, selectedIds))
                .ToArray();
            var hiddenCount = CountHiddenNodes(group.Roots);
            return new HiddenObjectSceneGroupViewState(
                group.SceneHandle,
                group.SceneName,
                I18N.Get(
                    "window.scene.hiddenCount",
                    hiddenCount),
                roots);
        }

        private HiddenObjectNodeViewState CreateNodeState(
            HiddenObjectTreeNode node,
            ISet<int> selectedIds)
        {
            var texture = _iconProvider.Load(node.InstanceId);
            var icon = texture != null
                ? IconState.FromTexture(
                    texture,
                    UiSizeTokens.Size16)
                : IconState.FromBuiltinIcon(
                    UiBuiltinIcon.GenericFile,
                    UiSizeTokens.Size16);
            var children = node.Children
                .Select(child => CreateNodeState(
                    child,
                    selectedIds))
                .ToArray();

            return new HiddenObjectNodeViewState(
                node.InstanceId,
                node.Name,
                node.IsHidden,
                selectedIds.Contains(node.InstanceId),
                icon,
                children);
        }

        private static IReadOnlyList<HiddenObjectSceneOptionViewState>
            CreateSceneOptions(HiddenObjectsState state)
        {
            var allScenesLabel =
                I18N.Get("window.scene.all");
            var options =
                new List<HiddenObjectSceneOptionViewState>
                {
                    new HiddenObjectSceneOptionViewState(
                        0,
                        allScenesLabel)
                };
            var duplicateNames = new HashSet<string>(
                state.SceneOptions
                    .GroupBy(
                        option => option.Label,
                        StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
                StringComparer.Ordinal);

            for (var i = 0; i < state.SceneOptions.Count; i++)
            {
                var option = state.SceneOptions[i];
                var label = duplicateNames.Contains(option.Label)
                    ? I18N.Get(
                        "window.scene.duplicateFormat",
                        option.Label,
                        option.SceneHandle)
                    : option.Label;
                options.Add(
                    new HiddenObjectSceneOptionViewState(
                        option.SceneHandle,
                        label));
            }

            return options;
        }

        private static int CountHiddenNodes(
            IReadOnlyList<HiddenObjectTreeNode> nodes)
        {
            var count = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].IsHidden)
                {
                    count++;
                }

                count += CountHiddenNodes(nodes[i].Children);
            }

            return count;
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
