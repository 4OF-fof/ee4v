using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: InternalsVisibleTo("Ee4v.AssetManager.UI.Editor")]

namespace Ee4v.AssetProtection
{
    internal static class AssetProtectionModule
    {
        private static readonly HashSet<string> ProtectedGuids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<UnityEngine.Object>
            ManagedNotEditableObjects = new HashSet<UnityEngine.Object>();
        private static readonly HashSet<EditorWindow>
            ManagedDisabledWindows = new HashSet<EditorWindow>();
        private static readonly Type AnimatorWindowType = Type.GetType(
            "UnityEditor.Graphs.AnimatorControllerTool, UnityEditor.Graphs");
        private static readonly FieldInfo AnimatorControllerField =
            AnimatorWindowType?.GetField(
                "m_AnimatorController",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static IAssetManager _manager;

        static AssetProtectionModule()
        {
            AssemblyReloadEvents.beforeAssemblyReload +=
                ClearProtection;
            EditorApplication.quitting += ClearProtection;
            EditorApplication.update += ApplyEditorObjectProtection;
            EditorApplication.update +=
                AssetInspectorProtectionOverlay.Update;
            EditorApplication.update +=
                PrefabStageProtectionOverlay.Update;
        }

        internal static void Configure(IAssetManager manager)
        {
            if (ReferenceEquals(_manager, manager))
            {
                return;
            }

            if (_manager != null)
            {
                _manager.Changed -= OnManagerChanged;
            }

            _manager = manager;
            if (_manager != null)
            {
                _manager.Changed += OnManagerChanged;
            }

            Reload();
        }

        internal static bool IsProtected(string assetOrMetaPath)
        {
            if (string.IsNullOrEmpty(assetOrMetaPath))
            {
                return false;
            }

            var assetPath = assetOrMetaPath.EndsWith(
                ".meta",
                StringComparison.OrdinalIgnoreCase)
                ? assetOrMetaPath.Substring(
                    0,
                    assetOrMetaPath.Length - ".meta".Length)
                : assetOrMetaPath;
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return ProtectedGuids.Contains(guid);
        }

        private static void OnManagerChanged(AssetManagerChange change)
        {
            if (change != null &&
                change.Kind ==
                AssetManagerChangeKind.FileImportedAssetGuidsChanged)
            {
                Reload();
            }
        }

        private static void Reload()
        {
            ClearProtection();
            ProtectedGuids.Clear();
            if (_manager == null)
            {
                return;
            }

            foreach (var association in
                     _manager.GetImportedAssetAssociations())
            {
                if (!string.IsNullOrWhiteSpace(association.AssetGuid))
                {
                    ProtectedGuids.Add(association.AssetGuid);
                }
            }
        }

        private static void ApplyEditorObjectProtection()
        {
            foreach (var window in
                     Resources.FindObjectsOfTypeAll<AnimationWindow>())
            {
                ProtectObject(window.animationClip);
            }

            if (AnimatorWindowType == null ||
                AnimatorControllerField == null)
            {
                return;
            }

            foreach (EditorWindow window in
                     Resources.FindObjectsOfTypeAll(AnimatorWindowType))
            {
                var controller = AnimatorControllerField.GetValue(window)
                    as UnityEngine.Object;
                var blocked = IsProtected(
                    AssetDatabase.GetAssetPath(controller));
                if (blocked && window.rootVisualElement.enabledSelf)
                {
                    window.rootVisualElement.SetEnabled(false);
                    ManagedDisabledWindows.Add(window);
                }
                else if (!blocked &&
                         ManagedDisabledWindows.Remove(window))
                {
                    window.rootVisualElement.SetEnabled(true);
                }
            }
        }

        internal static bool ProtectObject(UnityEngine.Object asset)
        {
            if (asset == null ||
                !IsProtected(AssetDatabase.GetAssetPath(asset)))
            {
                return false;
            }

            if ((asset.hideFlags & HideFlags.NotEditable) == 0)
            {
                asset.hideFlags |= HideFlags.NotEditable;
                ManagedNotEditableObjects.Add(asset);
            }

            return true;
        }

        private static void ClearProtection()
        {
            foreach (var asset in ManagedNotEditableObjects)
            {
                if (asset != null)
                {
                    asset.hideFlags &= ~HideFlags.NotEditable;
                }
            }

            ManagedNotEditableObjects.Clear();
            foreach (var window in ManagedDisabledWindows)
            {
                if (window != null)
                {
                    window.rootVisualElement.SetEnabled(true);
                }
            }

            ManagedDisabledWindows.Clear();
            AssetInspectorProtectionOverlay.Clear();
            PrefabStageProtectionOverlay.Clear();
        }
    }

    internal static class AssetInspectorProtectionOverlay
    {
        private const string ElementName =
            "ee4v-asset-protection-asset-inspector-overlay";

        internal static void Update()
        {
            if (!InspectorApi.TryGetStates(out var states))
            {
                return;
            }

            for (var i = 0; i < states.Count; i++)
            {
                Sync(states[i]);
            }
        }

        internal static void Clear()
        {
            if (!InspectorApi.TryGetStates(out var states))
            {
                return;
            }

            for (var i = 0; i < states.Count; i++)
            {
                states[i]?.Window?.rootVisualElement
                    ?.Q<VisualElement>(ElementName)
                    ?.RemoveFromHierarchy();
            }
        }

        private static void Sync(InspectorState state)
        {
            var root = state?.Window?.rootVisualElement;
            var overlay = root?.Q<VisualElement>(ElementName);
            var viewportRect = state?.EditorsViewportRect ?? Rect.zero;
            if (root == null ||
                !ContainsProtectedBlendTree(state) ||
                viewportRect.width <= 0f ||
                viewportRect.height <= 0f)
            {
                overlay?.RemoveFromHierarchy();
                return;
            }

            if (overlay == null)
            {
                overlay = Create();
                root.Add(overlay);
            }

            if (overlay.parent != null &&
                overlay.parent.IndexOf(overlay) <
                overlay.parent.childCount - 1)
            {
                overlay.BringToFront();
            }

            overlay.style.left = viewportRect.x;
            overlay.style.top = viewportRect.y;
            overlay.style.width = viewportRect.width;
            overlay.style.height = viewportRect.height;
        }

        private static bool ContainsProtectedBlendTree(
            InspectorState state)
        {
            for (var targetIndex = 0;
                 targetIndex < state.EditorTargets.Count;
                 targetIndex++)
            {
                var target = state.EditorTargets[targetIndex];
                if (target is BlendTree &&
                    EditorUtility.IsPersistent(target) &&
                    AssetProtectionModule.ProtectObject(target))
                {
                    return true;
                }
            }

            return false;
        }

        private static VisualElement Create()
        {
            var overlay = new VisualElement
            {
                name = ElementName,
                focusable = true
            };
            overlay.style.position = Position.Absolute;
            overlay.style.backgroundColor =
                new Color(0.08f, 0.08f, 0.08f, 0.82f);
            UiComposition.Prepare(overlay);
            overlay.Add(new EmptyState(new EmptyStateState(
                I18N.Get("asset.warning.title"))));
            overlay.RegisterCallback<PointerDownEvent>(
                _ => overlay.Focus());
            overlay.RegisterCallback<KeyDownEvent>(
                evt => evt.StopPropagation());
            return overlay;
        }
    }

    internal static class PrefabStageProtectionOverlay
    {
        private const string HierarchyElementName =
            "ee4v-asset-protection-hierarchy-overlay";
        private const string InspectorElementName =
            "ee4v-asset-protection-inspector-overlay";
        private static PrefabStage _continuedStage;

        internal static void Update()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                _continuedStage = null;
            }

            var shouldShow =
                stage != null &&
                stage != _continuedStage &&
                AssetProtectionModule.IsProtected(stage.assetPath);
            SyncHierarchyWindows(stage, shouldShow);
            SyncInspectorWindows(stage, shouldShow);
        }

        internal static void Clear()
        {
            _continuedStage = null;
            RemoveFromAllWindows();
        }

        private static void SyncHierarchyWindows(
            PrefabStage stage,
            bool shouldShow)
        {
            if (!HierarchyItemApi.TryGetOpenWindows(out var windows))
            {
                return;
            }

            for (var i = 0; i < windows.Count; i++)
            {
                SyncHierarchyWindow(
                    windows[i],
                    stage,
                    shouldShow);
            }
        }

        private static void SyncHierarchyWindow(
            EditorWindow window,
            PrefabStage stage,
            bool shouldShow)
        {
            var root = window?.rootVisualElement;
            var overlay = root?.Q<VisualElement>(
                HierarchyElementName);
            if (root == null ||
                !shouldShow ||
                !HierarchyItemApi.TryGetTreeViewRect(
                    window,
                    out var treeViewRect))
            {
                overlay?.RemoveFromHierarchy();
                return;
            }

            overlay = EnsureOverlay(
                root,
                overlay,
                HierarchyElementName,
                stage);
            SetRect(overlay, treeViewRect);
        }

        private static void SyncInspectorWindows(
            PrefabStage stage,
            bool shouldShow)
        {
            if (!InspectorApi.TryGetStates(out var states))
            {
                return;
            }

            for (var i = 0; i < states.Count; i++)
            {
                SyncInspectorWindow(
                    states[i],
                    stage,
                    shouldShow);
            }
        }

        private static void SyncInspectorWindow(
            InspectorState state,
            PrefabStage stage,
            bool shouldShow)
        {
            var root = state?.Window?.rootVisualElement;
            var overlay = root?.Q<VisualElement>(
                InspectorElementName);
            var viewportRect = state?.EditorsViewportRect ?? Rect.zero;
            if (root == null ||
                !shouldShow ||
                !IsInspectingStage(state, stage) ||
                viewportRect.width <= 0f ||
                viewportRect.height <= 0f)
            {
                overlay?.RemoveFromHierarchy();
                return;
            }

            overlay = EnsureOverlay(
                root,
                overlay,
                InspectorElementName,
                stage);
            SetRect(overlay, viewportRect);
        }

        private static VisualElement EnsureOverlay(
            VisualElement root,
            VisualElement overlay,
            string elementName,
            PrefabStage stage)
        {
            if (overlay == null ||
                !ReferenceEquals(overlay.userData, stage))
            {
                overlay?.RemoveFromHierarchy();
                overlay = Create(elementName, stage);
                root.Add(overlay);
            }

            if (overlay.parent != null &&
                overlay.parent.IndexOf(overlay) <
                overlay.parent.childCount - 1)
            {
                overlay.BringToFront();
            }

            return overlay;
        }

        private static VisualElement Create(
            string elementName,
            PrefabStage stage)
        {
            var overlay = new VisualElement
            {
                name = elementName,
                focusable = true,
                userData = stage
            };
            overlay.style.position = Position.Absolute;
            overlay.style.backgroundColor =
                new Color(0.08f, 0.08f, 0.08f, 0.82f);
            UiComposition.Prepare(
                overlay,
                "Editor/UI/Components/Inputs/ui-button.uss");

            var warning = new EmptyState(new EmptyStateState(
                I18N.Get("prefabStage.warning.title")));
            var continueButton = UiTextFactory.CreateButton(
                I18N.Get("prefabStage.warning.continue"),
                () => ContinueEditing(stage));
            continueButton.AddToClassList("ee4v-ui-button");
            continueButton.style.minWidth =
                UiSizeTokens.ActionButtonWidth;
            warning.Actions.Add(continueButton);
            overlay.Add(warning);

            overlay.RegisterCallback<PointerDownEvent>(
                _ => overlay.Focus());
            overlay.RegisterCallback<KeyDownEvent>(
                evt => evt.StopPropagation());
            return overlay;
        }

        private static void SetRect(
            VisualElement overlay,
            Rect rect)
        {
            overlay.style.left = rect.x;
            overlay.style.top = rect.y;
            overlay.style.width = rect.width;
            overlay.style.height = rect.height;
        }

        private static bool IsInspectingStage(
            InspectorState state,
            PrefabStage stage)
        {
            if (state == null || stage == null)
            {
                return false;
            }

            for (var i = 0; i < state.InspectedObjects.Count; i++)
            {
                var inspected = state.InspectedObjects[i];
                var gameObject = inspected as GameObject;
                if (gameObject == null && inspected is Component component)
                {
                    gameObject = component.gameObject;
                }

                if (gameObject != null &&
                    gameObject.scene == stage.scene)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ContinueEditing(PrefabStage stage)
        {
            if (stage == null ||
                PrefabStageUtility.GetCurrentPrefabStage() != stage)
            {
                return;
            }

            _continuedStage = stage;
            RemoveFromAllWindows();
        }

        private static void RemoveFromAllWindows()
        {
            RemoveFromHierarchyWindows();
            RemoveFromInspectorWindows();
        }

        private static void RemoveFromHierarchyWindows()
        {
            if (!HierarchyItemApi.TryGetOpenWindows(out var windows))
            {
                return;
            }

            for (var i = 0; i < windows.Count; i++)
            {
                windows[i]?.rootVisualElement
                    ?.Q<VisualElement>(HierarchyElementName)
                    ?.RemoveFromHierarchy();
            }
        }

        private static void RemoveFromInspectorWindows()
        {
            if (!InspectorApi.TryGetStates(out var states))
            {
                return;
            }

            for (var i = 0; i < states.Count; i++)
            {
                states[i]?.Window?.rootVisualElement
                    ?.Q<VisualElement>(InspectorElementName)
                    ?.RemoveFromHierarchy();
            }
        }
    }

    internal sealed class AssetProtectionModificationProcessor :
        AssetModificationProcessor
    {
        private static bool IsOpenForEdit(
            string assetOrMetaFilePath,
            out string message)
        {
            var blocked = AssetProtectionModule.IsProtected(
                assetOrMetaFilePath);
            message = string.Empty;
            return !blocked;
        }

        private static bool IsOpenForEdit(
            string[] assetOrMetaFilePaths,
            List<string> outNotEditablePaths,
            StatusQueryOptions statusQueryOptions)
        {
            for (var i = 0; i < assetOrMetaFilePaths.Length; i++)
            {
                var path = assetOrMetaFilePaths[i];
                if (AssetProtectionModule.IsProtected(path))
                {
                    outNotEditablePaths.Add(path);
                }
            }

            return outNotEditablePaths.Count == 0;
        }

        private static string[] OnWillSaveAssets(string[] paths)
        {
            var editablePaths = new List<string>(paths.Length);
            for (var i = 0; i < paths.Length; i++)
            {
                if (!AssetProtectionModule.IsProtected(paths[i]))
                {
                    editablePaths.Add(paths[i]);
                }
            }

            return editablePaths.ToArray();
        }
    }
}
