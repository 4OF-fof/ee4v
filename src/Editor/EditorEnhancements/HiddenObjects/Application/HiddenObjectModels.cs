using System;
using System.Collections.Generic;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectSnapshotItem
    {
        public HiddenObjectSnapshotItem(
            int instanceId,
            int parentInstanceId,
            int sceneHandle,
            string sceneName,
            string name,
            bool isHidden,
            int order)
        {
            InstanceId = instanceId;
            ParentInstanceId = parentInstanceId;
            SceneHandle = sceneHandle;
            SceneName = sceneName ?? string.Empty;
            Name = name ?? string.Empty;
            IsHidden = isHidden;
            Order = order;
        }

        public int InstanceId { get; }

        public int ParentInstanceId { get; }

        public int SceneHandle { get; }

        public string SceneName { get; }

        public string Name { get; }

        public bool IsHidden { get; }

        public int Order { get; }
    }

    internal sealed class HiddenObjectTreeNode
    {
        public HiddenObjectTreeNode(
            int instanceId,
            string name,
            bool isHidden,
            IReadOnlyList<HiddenObjectTreeNode> children)
        {
            InstanceId = instanceId;
            Name = name ?? string.Empty;
            IsHidden = isHidden;
            Children = children ?? Array.Empty<HiddenObjectTreeNode>();
        }

        public int InstanceId { get; }

        public string Name { get; }

        public bool IsHidden { get; }

        public IReadOnlyList<HiddenObjectTreeNode> Children { get; }
    }

    internal sealed class HiddenObjectSceneGroup
    {
        public HiddenObjectSceneGroup(
            int sceneHandle,
            string sceneName,
            IReadOnlyList<HiddenObjectTreeNode> roots)
        {
            SceneHandle = sceneHandle;
            SceneName = sceneName ?? string.Empty;
            Roots = roots ?? Array.Empty<HiddenObjectTreeNode>();
        }

        public int SceneHandle { get; }

        public string SceneName { get; }

        public IReadOnlyList<HiddenObjectTreeNode> Roots { get; }
    }

    internal sealed class HiddenObjectSceneOption
    {
        public HiddenObjectSceneOption(int sceneHandle, string label)
        {
            SceneHandle = sceneHandle;
            Label = label ?? string.Empty;
        }

        public int SceneHandle { get; }

        public string Label { get; }
    }

    internal sealed class HiddenObjectsState
    {
        public HiddenObjectsState(
            IReadOnlyList<HiddenObjectSceneGroup> sceneGroups,
            IReadOnlyList<HiddenObjectSceneOption> sceneOptions,
            IReadOnlyCollection<int> selectedInstanceIds,
            int selectedSceneHandle,
            string query,
            int totalHiddenCount,
            int visibleHiddenCount)
        {
            SceneGroups = sceneGroups ??
                Array.Empty<HiddenObjectSceneGroup>();
            SceneOptions = sceneOptions ??
                Array.Empty<HiddenObjectSceneOption>();
            SelectedInstanceIds = selectedInstanceIds ??
                Array.Empty<int>();
            SelectedSceneHandle = selectedSceneHandle;
            Query = query ?? string.Empty;
            TotalHiddenCount = totalHiddenCount;
            VisibleHiddenCount = visibleHiddenCount;
        }

        public IReadOnlyList<HiddenObjectSceneGroup> SceneGroups { get; }

        public IReadOnlyList<HiddenObjectSceneOption> SceneOptions { get; }

        public IReadOnlyCollection<int> SelectedInstanceIds { get; }

        public int SelectedSceneHandle { get; }

        public string Query { get; }

        public int TotalHiddenCount { get; }

        public int VisibleHiddenCount { get; }
    }
}
