using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.Manager {
    [InitializeOnLoad]
    internal static class HierarchyExtensionManager {
        private static List<IHierarchyExtensionComponent> _components;

        static HierarchyExtensionManager() {
            EditorApplication.hierarchyWindowItemOnGUI -= OnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
            Resolve();
        }

        private static void Resolve() {
            _components = new List<IHierarchyExtensionComponent>();

            var types = TypeCache.GetTypesDerivedFrom<IHierarchyExtensionComponent>()
                .Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in types)
                if (type.GetConstructor(Type.EmptyTypes) != null &&
                    Activator.CreateInstance(type) is IHierarchyExtensionComponent component)
                    _components.Add(component);

            _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private static void OnGUI(int instanceID, Rect selectionRect) {
            if (!SettingSingleton.I.enableHierarchyExtension) return;

            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

            if (gameObject != null && gameObject.CompareTag("EditorOnly") && !gameObject.activeSelf)
                gameObject.hideFlags |= HideFlags.HideInHierarchy;

            var currentRect = selectionRect;

            foreach (var component in _components)
                component.OnGUI(ref currentRect, gameObject, instanceID, selectionRect);
        }
    }
}