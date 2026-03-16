using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ee4v.Core.Internal.EditorAPI;

namespace Ee4v.Core.Injector
{
    public enum HierarchyItemKind
    {
        Unknown,
        SceneHeader,
        GameObject
    }

    public enum ProjectItemViewMode
    {
        Unknown,
        OneColumn,
        TwoColumns
    }

    public enum ProjectItemOrientation
    {
        Unknown,
        Horizontal,
        Vertical
    }

    public sealed class ItemInjectionContext
    {
        internal ItemInjectionContext(InjectionChannel channel, int instanceId, string guid, Rect selectionRect)
        {
            Channel = channel;
            InstanceId = instanceId;
            Guid = guid;
            SelectionRect = selectionRect;
            CurrentRect = selectionRect;
            Target = ResolveTarget(instanceId, guid);
            HierarchyItemKind = ResolveHierarchyItemKind(channel, instanceId, Target, out var scene);
            HierarchyScene = scene;
            ProjectViewMode = ResolveProjectViewMode(channel);
            ProjectOrientation = ResolveProjectOrientation(channel, selectionRect, ProjectViewMode);
        }

        public InjectionChannel Channel { get; }

        public int InstanceId { get; }

        public string Guid { get; }

        public Rect SelectionRect { get; }

        public Rect CurrentRect { get; set; }

        public Object Target { get; }

        public HierarchyItemKind HierarchyItemKind { get; }

        public Scene HierarchyScene { get; }

        public ProjectItemViewMode ProjectViewMode { get; }

        public ProjectItemOrientation ProjectOrientation { get; }

        public bool IsHierarchySceneHeader
        {
            get { return HierarchyItemKind == HierarchyItemKind.SceneHeader; }
        }

        public bool IsHierarchyGameObject
        {
            get { return HierarchyItemKind == HierarchyItemKind.GameObject; }
        }

        private static Object ResolveTarget(int instanceId, string guid)
        {
            if (instanceId != 0)
            {
                return EditorUtility.InstanceIDToObject(instanceId);
            }

            if (!string.IsNullOrEmpty(guid))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    return AssetDatabase.LoadMainAssetAtPath(assetPath);
                }
            }

            return null;
        }

        private static HierarchyItemKind ResolveHierarchyItemKind(InjectionChannel channel, int instanceId, Object target, out Scene scene)
        {
            scene = default(Scene);

            if (channel != InjectionChannel.HierarchyItem)
            {
                return HierarchyItemKind.Unknown;
            }

            if (target is GameObject)
            {
                return HierarchyItemKind.GameObject;
            }

            scene = ResolveSceneByHandle(instanceId);
            return scene.IsValid() ? HierarchyItemKind.SceneHeader : HierarchyItemKind.Unknown;
        }

        private static Scene ResolveSceneByHandle(int handle)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.handle == handle)
                {
                    return scene;
                }
            }

            return default(Scene);
        }

        private static ProjectItemViewMode ResolveProjectViewMode(InjectionChannel channel)
        {
            if (channel != InjectionChannel.ProjectItem)
            {
                return ProjectItemViewMode.Unknown;
            }

            ProjectBrowserSnapshot snapshot;
            return ProjectBrowser.TryGetSnapshot(out snapshot)
                ? ToProjectItemViewMode(snapshot.ViewMode)
                : ProjectItemViewMode.Unknown;
        }

        private static ProjectItemOrientation ResolveProjectOrientation(
            InjectionChannel channel,
            Rect selectionRect,
            ProjectItemViewMode viewMode)
        {
            if (channel != InjectionChannel.ProjectItem)
            {
                return ProjectItemOrientation.Unknown;
            }

            ProjectBrowserSnapshot snapshot;
            if (ProjectBrowser.TryGetSnapshot(selectionRect, out snapshot))
            {
                return ToProjectItemOrientation(snapshot.Orientation);
            }

            if (viewMode == ProjectItemViewMode.OneColumn)
            {
                return ProjectItemOrientation.Horizontal;
            }

            return selectionRect.height > EditorGUIUtility.singleLineHeight * 1.5f
                ? ProjectItemOrientation.Vertical
                : ProjectItemOrientation.Horizontal;
        }

        private static ProjectItemViewMode ToProjectItemViewMode(ProjectBrowserViewMode viewMode)
        {
            switch (viewMode)
            {
                case ProjectBrowserViewMode.OneColumn:
                    return ProjectItemViewMode.OneColumn;
                case ProjectBrowserViewMode.TwoColumns:
                    return ProjectItemViewMode.TwoColumns;
                default:
                    return ProjectItemViewMode.Unknown;
            }
        }

        private static ProjectItemOrientation ToProjectItemOrientation(ProjectBrowserOrientation orientation)
        {
            switch (orientation)
            {
                case ProjectBrowserOrientation.Horizontal:
                    return ProjectItemOrientation.Horizontal;
                case ProjectBrowserOrientation.Vertical:
                    return ProjectItemOrientation.Vertical;
                default:
                    return ProjectItemOrientation.Unknown;
            }
        }
    }
}
