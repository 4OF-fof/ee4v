using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class UnityObjectReference
    {
        private static readonly string Session = SessionState.GetString("Ee4v.Mcp.ObjectSession", Guid.NewGuid().ToString("N"));
        private static readonly Dictionary<string, UnityEngine.Object> Instances = new Dictionary<string, UnityEngine.Object>();
        private static readonly Dictionary<string, GameObject> Avatars = new Dictionary<string, GameObject>();

        static UnityObjectReference() => SessionState.SetString("Ee4v.Mcp.ObjectSession", Session);

        internal static string CreateAvatar(GameObject avatar)
        {
            if (avatar == null) return string.Empty;
            ValidateAvatar(avatar);
            foreach (var key in Avatars.Where(pair => pair.Value == null).Select(pair => pair.Key).ToArray()) Avatars.Remove(key);
            var reference = Create(avatar);
            Avatars[reference] = avatar;
            return reference;
        }

        internal static bool IsLoadedAvatarScene(GameObject avatar)
        {
            if (avatar == null || EditorUtility.IsPersistent(avatar) ||
                !avatar.scene.IsValid() || !avatar.scene.isLoaded) return false;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return !EditorSceneManager.IsPreviewScene(avatar.scene) || stage != null && avatar.scene == stage.scene;
        }

        private static void ValidateAvatar(GameObject avatar)
        {
            if (!IsLoadedAvatarScene(avatar) || !avatar.GetComponents<Component>().Any(component => component != null &&
                    component.GetType().FullName == "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor"))
                throw new McpToolException("avatar_not_found", "The reference must identify a loaded VRChat avatar root. Find avatars again.");
        }

        internal static GameObject ResolveWritableAvatar(string reference)
        {
            var avatar = ResolveGameObject(reference);
            if (EditorSceneManager.IsPreviewScene(avatar.scene))
                throw new McpToolException("scene_avatar_required", "Use an avatar instance in a user Scene for configuration writes; Prefab Mode is read-only.");
            return avatar;
        }
        internal static string Create(UnityEngine.Object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (!EditorUtility.IsPersistent(value) && EditorApplication.isPlaying)
                return CreateInstanceReference(value);

            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(value);
            var serialized = globalId.ToString();
            if (!string.IsNullOrEmpty(serialized) &&
                !serialized.EndsWith("-0-0", StringComparison.Ordinal))
            {
                return serialized;
            }

            var path = AssetDatabase.GetAssetPath(value);
            return string.IsNullOrEmpty(path) ? CreateInstanceReference(value) : path;
        }

        private static string CreateInstanceReference(UnityEngine.Object value)
        {
            foreach (var pair in Instances) if (pair.Value != null && pair.Value == value) return pair.Key;
            foreach (var key in Instances.Where(pair => pair.Value == null).Select(pair => pair.Key).ToArray()) Instances.Remove(key);
            var reference = "instance:" + Session + ":" + Guid.NewGuid().ToString("N");
            Instances[reference] = value;
            return reference;
        }

        internal static GameObject ResolveGameObject(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new McpToolException(
                    "avatar_ref_required",
                    "avatarRef is required. Use ee4v_find_avatars to obtain one.");
            }

            var value = reference.Trim();
            if (!Avatars.TryGetValue(value, out var target) || target == null)
                throw new McpToolException("avatar_not_found", "avatarRef must be returned by ee4v_find_avatars or the runtime tools in this domain session. Find avatars again.");
            if (GlobalObjectId.TryParse(value, out var globalId) &&
                GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) != target)
                throw new McpToolException("avatar_not_found", "The avatar reference is stale. Find avatars again.");
            ValidateAvatar(target);
            return target;
        }

        internal static string HierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
