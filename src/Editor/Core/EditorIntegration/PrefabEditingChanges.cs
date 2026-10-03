using System;
using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.EditorIntegration
{
    /// <summary>Checks content rather than a Scene's sticky dirty flag.</summary>
    public static class PrefabEditingChanges
    {
        public static bool HasContentOverrides(GameObject instance) =>
            PrefabEditingChangesBackend.HasContentOverrides(instance);

        public static bool SerializedValuesEqual(UnityEngine.Object current, UnityEngine.Object saved,
            Func<SerializedProperty, bool> include = null) =>
            PrefabEditingChangesBackend.SerializedValuesEqual(current, saved, include);
    }
}
