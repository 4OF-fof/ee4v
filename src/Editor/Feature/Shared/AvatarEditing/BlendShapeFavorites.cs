using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarEditing
{
    public static class BlendShapeFavorites
    {
        public static event Action Changed;
        public static event Action<string> KeyChanged;
        private static readonly Dictionary<string, bool> States = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly ConditionalWeakTable<Mesh, MeshIdentity> MeshIdentities = new ConditionalWeakTable<Mesh, MeshIdentity>();

        public static string Key(string guid, long meshId, string shapeName)
        {
            return "ee4v.blendShapeFavorite." + guid + "." + meshId + "." + Uri.EscapeDataString(shapeName ?? string.Empty);
        }

        public static string Key(Mesh mesh, string shapeName)
        {
            if (mesh == null) { return null; }
            var identity = MeshIdentities.GetValue(mesh, target =>
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long meshId);
                return new MeshIdentity(string.IsNullOrEmpty(guid) ? "session-" + target.GetInstanceID() : guid, meshId);
            });
            return Key(identity.Guid, identity.LocalId, shapeName);
        }

        public static bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key)) { return false; }
            if (!States.TryGetValue(key, out var selected))
            {
                selected = EditorPrefs.GetBool(key, false);
                States.Add(key, selected);
            }
            return selected;
        }

        public static void Toggle(string key)
        {
            if (string.IsNullOrEmpty(key)) { return; }
            var selected = !Contains(key);
            if (selected) { EditorPrefs.SetBool(key, true); }
            else { EditorPrefs.DeleteKey(key); }
            States[key] = selected;
            KeyChanged?.Invoke(key);
            Changed?.Invoke();
        }

        private sealed class MeshIdentity
        {
            internal MeshIdentity(string guid, long localId) { Guid = guid; LocalId = localId; }
            internal string Guid { get; }
            internal long LocalId { get; }
        }
    }
}
