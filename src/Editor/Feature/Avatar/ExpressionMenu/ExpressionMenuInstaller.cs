using System;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.ExpressionMenu
{
    public static class ExpressionMenuInstaller
    {
        private const string RootName = "ee4v Expression Menu";
        private const string PrefabName = "ExpressionMenu.prefab";

        public static bool IsGeneratedPrefab(GameObject target)
        {
            if (target == null) return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target) ?? string.Empty;
            return path.EndsWith(".ExpressionMenu/" + PrefabName, StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf(".ExpressionMenu/Gimmicks/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/Gimmick.prefab", StringComparison.OrdinalIgnoreCase);
        }

        internal static VRCExpressionsMenu EnsureMenu(GameObject avatar, GameObject prefab,
            VRCExpressionsMenu target, GameObject childRoot = null)
        {
            if (avatar == null || prefab == null || EditorUtility.IsPersistent(avatar) ||
                !ExpressionMenuModel.CanWrite(prefab))
                throw new InvalidOperationException("An editable avatar Prefab instance is required.");
            var existingRoot = avatar.transform.Cast<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go =>
                    (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) ?? string.Empty)
                    .EndsWith(".ExpressionMenu/" + PrefabName, StringComparison.OrdinalIgnoreCase));
            if (existingRoot != null && !ExpressionMenuModel.CanWrite(
                    PrefabUtility.GetCorrespondingObjectFromSource(existingRoot)))
                throw new InvalidOperationException("The generated Expression Menu Prefab is read-only.");
            var targetType = typeof(ModularAvatarMenuInstaller).Assembly.GetType(
                "nadena.dev.modular_avatar.core.ModularAvatarMenuInstallTarget");
            if (childRoot != null && (targetType == null || !childRoot.transform.IsChildOf(avatar.transform)))
                throw new InvalidOperationException("The Modular Avatar child submenu target is unavailable.");
            var installTargets = targetType == null ? Array.Empty<Component>() :
                avatar.GetComponentsInChildren(targetType, true).Cast<Component>().ToArray();
            bool MatchesTarget(ModularAvatarMenuInstaller installer)
            {
                var refs = installTargets.Where(component =>
                    targetType.GetField("installer").GetValue(component) as Component == installer).ToArray();
                return childRoot == null ? refs.Length == 0 && installer.installTargetMenu == target :
                    refs.Any(component => component.transform.parent == childRoot.transform);
            }
            var existing = existingRoot == null ? null : existingRoot
                .GetComponentsInChildren<ModularAvatarMenuInstaller>(true)
                .FirstOrDefault(i => MatchesTarget(i) &&
                    i.menuToAppend != null && i.menuToAppend.controls.Count < 8);
            if (existing != null && existing.menuToAppend != null)
            {
                if (!ExpressionMenuModel.CanWrite(existing.menuToAppend))
                    throw new InvalidOperationException("The generated Expression Menu asset is read-only.");
                return existing.menuToAppend;
            }
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            var directory = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            var folderName = Path.GetFileNameWithoutExtension(prefabPath) + ".ExpressionMenu";
            var folder = directory + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(directory, folderName);
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = target == null ? "ee4v Expression Menu" : target.name + " additions";
            var menuPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Menu.asset");
            AssetDatabase.CreateAsset(menu, menuPath);
            Undo.RegisterCreatedObjectUndo(menu, "Create Expression Menu");
            var root = existingRoot;
            GameObject child = null;
            GameObject installPoint = null;
            try
            {
                if (root == null)
                {
                    root = new GameObject(RootName);
                    Undo.RegisterCreatedObjectUndo(root, "Create Expression Menu Installer");
                    Undo.SetTransformParent(root.transform, avatar.transform, "Install Expression Menu");
                    root.transform.localPosition = Vector3.zero;
                    root.transform.localRotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one;
                }
                child = new GameObject(menu.name);
                Undo.RegisterCreatedObjectUndo(child, "Create Expression Menu Installer");
                Undo.SetTransformParent(child.transform, root.transform, "Install Expression Menu");
                var installer = Undo.AddComponent<ModularAvatarMenuInstaller>(child);
                installer.menuToAppend = menu;
                installer.installTargetMenu = target;
                EditorUtility.SetDirty(installer);
                if (childRoot != null)
                {
                    installPoint = new GameObject("ee4v Expression Menu Target");
                    Undo.RegisterCreatedObjectUndo(installPoint, "Create Expression Menu Target");
                    Undo.SetTransformParent(installPoint.transform, childRoot.transform, "Install Expression Submenu");
                    var component = Undo.AddComponent(installPoint, targetType);
                    targetType.GetField("installer").SetValue(component, installer);
                    EditorUtility.SetDirty(component);
                }
                GameObject saved;
                if (existingRoot != null)
                {
                    PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
                    saved = PrefabUtility.GetCorrespondingObjectFromSource(root);
                }
                else saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root,
                    folder + "/" + PrefabName, InteractionMode.AutomatedAction);
                if (saved == null) throw new InvalidOperationException("Could not save the Expression Menu installer Prefab.");
                return menu;
            }
            catch
            {
                if (existingRoot == null && root != null) Undo.DestroyObjectImmediate(root);
                else if (child != null) Undo.DestroyObjectImmediate(child);
                if (installPoint != null) Undo.DestroyObjectImmediate(installPoint);
                AssetDatabase.DeleteAsset(menuPath);
                throw;
            }
        }
    }
}
