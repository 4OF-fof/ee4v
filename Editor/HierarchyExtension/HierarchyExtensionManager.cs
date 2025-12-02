using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Attributes;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension {
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

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetCustomAttributes(typeof(ExportsHierarchyExtensionComponent), false))
                .SelectMany(attr => ((ExportsHierarchyExtensionComponent)attr).Types)
                .Distinct();

            foreach (var type in types)
                if (Activator.CreateInstance(type) is IHierarchyExtensionComponent component)
                    _components.Add(component);

            _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private static void OnGUI(int instanceID, Rect selectionRect) {
            if (!Settings.I.enableHierarchyExtension) return;

            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

            if (gameObject != null && gameObject.CompareTag("EditorOnly") && !gameObject.activeSelf)
                gameObject.hideFlags |= HideFlags.HideInHierarchy;

            var currentRect = selectionRect;

            foreach (var component in _components)
                component.OnGUI(ref currentRect, gameObject, instanceID, selectionRect);
        }
    }
}