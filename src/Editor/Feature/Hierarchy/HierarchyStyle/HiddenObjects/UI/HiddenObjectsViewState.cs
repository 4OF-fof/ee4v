using System;
using System.Collections.Generic;
using Ee4v.UI;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsViewText
    {
        public HiddenObjectsViewText(
            string searchPlaceholder,
            string searchTooltip,
            string clearSearchTooltip,
            string sceneTooltip,
            string refreshText,
            string refreshTooltip,
            string selectAllText,
            string clearSelectionText,
            string revealText)
        {
            SearchPlaceholder = searchPlaceholder ?? string.Empty;
            SearchTooltip = searchTooltip ?? string.Empty;
            ClearSearchTooltip = clearSearchTooltip ?? string.Empty;
            SceneTooltip = sceneTooltip ?? string.Empty;
            RefreshText = refreshText ?? string.Empty;
            RefreshTooltip = refreshTooltip ?? string.Empty;
            SelectAllText = selectAllText ?? string.Empty;
            ClearSelectionText = clearSelectionText ?? string.Empty;
            RevealText = revealText ?? string.Empty;
        }

        public string SearchPlaceholder { get; }

        public string SearchTooltip { get; }

        public string ClearSearchTooltip { get; }

        public string SceneTooltip { get; }

        public string RefreshText { get; }

        public string RefreshTooltip { get; }

        public string SelectAllText { get; }

        public string ClearSelectionText { get; }

        public string RevealText { get; }
    }

    internal sealed class HiddenObjectSceneOptionViewState
    {
        public HiddenObjectSceneOptionViewState(int sceneHandle, string label)
        {
            SceneHandle = sceneHandle;
            Label = label ?? string.Empty;
        }

        public int SceneHandle { get; }

        public string Label { get; }
    }

    internal sealed class HiddenObjectNodeViewState
    {
        public HiddenObjectNodeViewState(
            int instanceId,
            string name,
            bool isHidden,
            bool isSelected,
            IconState icon,
            IReadOnlyList<HiddenObjectNodeViewState> children)
        {
            InstanceId = instanceId;
            Name = name ?? string.Empty;
            IsHidden = isHidden;
            IsSelected = isSelected;
            Icon = icon ?? IconState.FromBuiltinIcon(
                UiBuiltinIcon.GenericFile,
                UiSizeTokens.Size16);
            Children = children ?? Array.Empty<HiddenObjectNodeViewState>();
        }

        public int InstanceId { get; }

        public string Name { get; }

        public bool IsHidden { get; }

        public bool IsSelected { get; }

        public IconState Icon { get; }

        public IReadOnlyList<HiddenObjectNodeViewState> Children { get; }
    }

    internal sealed class HiddenObjectSceneGroupViewState
    {
        public HiddenObjectSceneGroupViewState(
            int sceneHandle,
            string sceneName,
            string hiddenCountText,
            IReadOnlyList<HiddenObjectNodeViewState> roots)
        {
            SceneHandle = sceneHandle;
            SceneName = sceneName ?? string.Empty;
            HiddenCountText = hiddenCountText ?? string.Empty;
            Roots = roots ?? Array.Empty<HiddenObjectNodeViewState>();
        }

        public int SceneHandle { get; }

        public string SceneName { get; }

        public string HiddenCountText { get; }

        public IReadOnlyList<HiddenObjectNodeViewState> Roots { get; }
    }

    internal sealed class HiddenObjectsViewState
    {
        public HiddenObjectsViewState(
            IReadOnlyList<HiddenObjectSceneGroupViewState> sceneGroups,
            IReadOnlyList<HiddenObjectSceneOptionViewState> sceneOptions,
            int selectedSceneHandle,
            string query,
            string summaryText,
            string emptyTitle,
            string emptyMessage,
            int visibleHiddenCount,
            int selectedCount)
        {
            SceneGroups = sceneGroups ??
                Array.Empty<HiddenObjectSceneGroupViewState>();
            SceneOptions = sceneOptions ??
                Array.Empty<HiddenObjectSceneOptionViewState>();
            SelectedSceneHandle = selectedSceneHandle;
            Query = query ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            EmptyTitle = emptyTitle ?? string.Empty;
            EmptyMessage = emptyMessage ?? string.Empty;
            VisibleHiddenCount = Math.Max(0, visibleHiddenCount);
            SelectedCount = Math.Max(0, selectedCount);
        }

        public IReadOnlyList<HiddenObjectSceneGroupViewState> SceneGroups { get; }

        public IReadOnlyList<HiddenObjectSceneOptionViewState> SceneOptions { get; }

        public int SelectedSceneHandle { get; }

        public string Query { get; }

        public string SummaryText { get; }

        public string EmptyTitle { get; }

        public string EmptyMessage { get; }

        public int VisibleHiddenCount { get; }

        public int SelectedCount { get; }
    }
}
