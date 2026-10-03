using System;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    /// <summary>Checks content rather than a Scene's sticky dirty flag.</summary>
    internal static class PrefabEditingChangesBackend
    {
        public static bool HasContentOverrides(GameObject instance)
        {
            if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance)) return false;
            if (PrefabUtility.GetAddedGameObjects(instance).Count > 0 ||
                PrefabUtility.GetRemovedGameObjects(instance).Count > 0 ||
                PrefabUtility.GetAddedComponents(instance).Count > 0 ||
                PrefabUtility.GetRemovedComponents(instance).Count > 0) return true;
            foreach (var change in PrefabUtility.GetObjectOverrides(instance, false))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(change.instanceObject);
                if (source == null || !SerializedValuesEqual(change.instanceObject, source,
                    property => property.prefabOverride && !property.isDefaultOverride)) return true;
            }
            return false;
        }

        public static bool SerializedValuesEqual(UnityEngine.Object current, UnityEngine.Object saved,
            Func<SerializedProperty, bool> include = null)
        {
            if (current == null || saved == null) return current == saved;
            using (var currentObject = new SerializedObject(current))
            using (var savedObject = new SerializedObject(saved))
            {
                var property = currentObject.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyPath == "m_ObjectHideFlags" ||
                        property.propertyType == SerializedPropertyType.Generic ||
                        include != null && !include(property)) continue;
                    var original = savedObject.FindProperty(property.propertyPath);
                    if (original == null || property.propertyType != original.propertyType) return false;
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var value = property.objectReferenceValue;
                        var expected = original.objectReferenceValue;
                        if (value == expected) continue;
                        if (value == null || EditorUtility.IsPersistent(value) ||
                            PrefabUtility.GetCorrespondingObjectFromSource(value) != expected) return false;
                    }
                    else if (!SerializedProperty.DataEquals(property, original)) return false;
                }
            }
            return true;
        }
    }
}
