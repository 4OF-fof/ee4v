using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ee4v.AssetManager.Contracts;
using UnityEditor;
using UnityEngine;

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

        private static void ProtectObject(UnityEngine.Object asset)
        {
            if (asset == null ||
                !IsProtected(AssetDatabase.GetAssetPath(asset)) ||
                (asset.hideFlags & HideFlags.NotEditable) != 0)
            {
                return;
            }

            asset.hideFlags |= HideFlags.NotEditable;
            ManagedNotEditableObjects.Add(asset);
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
