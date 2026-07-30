using System.IO;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerProtectionMenu
    {
        private const string CreateMaterialVariantPath =
            "Assets/AssetManager/Create Material Variant";
        private const string CreatePrefabVariantPath =
            "Assets/AssetManager/Create Prefab Variant";
        private const string CreateCopyPath =
            "Assets/AssetManager/Create Editable Copy";
        private const string ProtectPath =
            "Assets/AssetManager/Protect";
        private const string UnprotectPath =
            "Assets/AssetManager/Unprotect";

        [MenuItem(CreateMaterialVariantPath, false, 2110)]
        private static void CreateMaterialVariant()
        {
            if (!TryGetSelection(
                    out var actions,
                    out var guid,
                    out var sourcePath))
            {
                return;
            }

            CreateMaterialVariant(
                actions,
                guid,
                sourcePath);
        }

        internal static void CreateMaterialVariant(
            IAssetManagerProtectionActions actions,
            string guid,
            string sourcePath)
        {
            var destination = EditorUtility
                .SaveFilePanelInProject(
                    I18N.Get(
                        "assetManager.protection.variant.title"),
                    I18N.Get(
                        "assetManager.protection.variant.defaultName",
                        Path.GetFileNameWithoutExtension(
                            sourcePath)),
                    "mat",
                    I18N.Get(
                        "assetManager.protection.variant.message"));
            if (string.IsNullOrWhiteSpace(destination))
            {
                return;
            }

            ExecuteCreation(
                () => actions.CreateMaterialVariant(
                    guid,
                    destination),
                destination);
        }

        [MenuItem(CreateMaterialVariantPath, true)]
        private static bool CanCreateMaterialVariant()
        {
            return TryGetSelection(
                       out var actions,
                       out var guid,
                       out _) &&
                   actions.IsProtected(guid) &&
                   actions.CanCreateMaterialVariant(guid);
        }

        [MenuItem(CreatePrefabVariantPath, false, 2111)]
        private static void CreatePrefabVariant()
        {
            if (!TryGetSelection(
                    out var actions,
                    out var guid,
                    out var sourcePath))
            {
                return;
            }

            CreatePrefabVariant(
                actions,
                guid,
                sourcePath);
        }

        internal static void CreatePrefabVariant(
            IAssetManagerProtectionActions actions,
            string guid,
            string sourcePath)
        {
            var destination = EditorUtility
                .SaveFilePanelInProject(
                    I18N.Get(
                        "assetManager.protection.prefabVariant.title"),
                    I18N.Get(
                        "assetManager.protection.prefabVariant.defaultName",
                        Path.GetFileNameWithoutExtension(
                            sourcePath)),
                    "prefab",
                    I18N.Get(
                        "assetManager.protection.prefabVariant.message"));
            if (string.IsNullOrWhiteSpace(destination))
            {
                return;
            }

            ExecuteCreation(
                () => actions.CreatePrefabVariant(
                    guid,
                    destination),
                destination);
        }

        [MenuItem(CreatePrefabVariantPath, true)]
        private static bool CanCreatePrefabVariant()
        {
            return TryGetSelection(
                       out var actions,
                       out var guid,
                       out _) &&
                   actions.IsProtected(guid) &&
                   actions.CanCreatePrefabVariant(guid);
        }

        [MenuItem(CreateCopyPath, false, 2112)]
        private static void CreateEditableCopy()
        {
            if (!TryGetSelection(
                    out var actions,
                    out var guid,
                    out var sourcePath))
            {
                return;
            }

            CreateEditableCopy(
                actions,
                guid,
                sourcePath);
        }

        internal static void CreateEditableCopy(
            IAssetManagerProtectionActions actions,
            string guid,
            string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath)
                .TrimStart('.');
            var destination = EditorUtility
                .SaveFilePanelInProject(
                    I18N.Get(
                        "assetManager.protection.copy.title"),
                    I18N.Get(
                        "assetManager.protection.copy.defaultName",
                        Path.GetFileNameWithoutExtension(
                            sourcePath)),
                    extension,
                    I18N.Get(
                        "assetManager.protection.copy.message"));
            if (string.IsNullOrWhiteSpace(destination))
            {
                return;
            }

            ExecuteCreation(
                () => actions.CreateEditableCopy(
                    guid,
                    destination),
                destination);
        }

        [MenuItem(CreateCopyPath, true)]
        private static bool CanCreateEditableCopy()
        {
            return TryGetSelection(
                       out var actions,
                       out var guid,
                       out var sourcePath) &&
                   actions.IsProtected(guid) &&
                   !AssetDatabase.IsValidFolder(sourcePath);
        }

        [MenuItem(ProtectPath, false, 2120)]
        private static void Protect()
        {
            if (TryGetSelection(
                    out var actions,
                    out var guid,
                    out _))
            {
                actions.SetProtected(guid, true);
            }
        }

        [MenuItem(ProtectPath, true)]
        private static bool CanProtect()
        {
            return TryGetSelection(
                       out var actions,
                       out var guid,
                       out _) &&
                   actions.IsManaged(guid) &&
                   !actions.IsProtected(guid);
        }

        [MenuItem(UnprotectPath, false, 2121)]
        private static void Unprotect()
        {
            if (!TryGetSelection(
                    out var actions,
                    out var guid,
                    out var sourcePath))
            {
                return;
            }

            Unprotect(
                actions,
                guid,
                sourcePath);
        }

        internal static void Unprotect(
            IAssetManagerProtectionActions actions,
            string guid,
            string sourcePath)
        {
            if (!EditorUtility.DisplayDialog(
                    I18N.Get(
                        "assetManager.protection.unprotect.title"),
                    I18N.Get(
                        "assetManager.protection.unprotect.message",
                        Path.GetFileName(sourcePath)),
                    I18N.Get(
                        "assetManager.protection.unprotect.confirm"),
                    I18N.Get(
                        "assetManager.protection.cancel")))
            {
                return;
            }

            actions.SetProtected(guid, false);
        }

        [MenuItem(UnprotectPath, true)]
        private static bool CanUnprotect()
        {
            return TryGetSelection(
                       out var actions,
                       out var guid,
                       out _) &&
                   actions.IsProtected(guid);
        }

        private static void ExecuteCreation(
            System.Func<bool> operation,
            string destination)
        {
            try
            {
                if (!operation())
                {
                    ShowCreationError();
                    return;
                }

                AssetDatabase.SaveAssets();
                var created =
                    AssetDatabase.LoadMainAssetAtPath(
                        destination);
                if (created != null)
                {
                    Selection.activeObject = created;
                    EditorGUIUtility.PingObject(created);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                ShowCreationError();
            }
        }

        private static void ShowCreationError()
        {
            EditorUtility.DisplayDialog(
                I18N.Get(
                    "assetManager.protection.error.title"),
                I18N.Get(
                    "assetManager.protection.error.message"),
                I18N.Get(
                    "assetManager.protection.error.close"));
        }

        private static bool TryGetSelection(
            out IAssetManagerProtectionActions actions,
            out string guid,
            out string assetPath)
        {
            actions = null;
            guid = string.Empty;
            assetPath = string.Empty;
            if (!AssetManagerUiDependencies
                    .TryGetProtectionActions(
                        out actions) ||
                Selection.activeObject == null)
            {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(
                Selection.activeObject);
            guid = string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(
                    assetPath);
            return !string.IsNullOrWhiteSpace(guid);
        }
    }
}
