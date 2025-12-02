using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.ProjectExtension.API;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension {
    [InitializeOnLoad]
    internal static class ProjectExtensionManager {
        private static List<IProjectExtensionComponent> _components;

        static ProjectExtensionManager() {
            EditorApplication.projectWindowItemOnGUI -= OnGUI;
            EditorApplication.projectWindowItemOnGUI += OnGUI;
            Resolve();
        }

        private static void Resolve() {
            _components = new List<IProjectExtensionComponent>();

            var types = TypeCache.GetTypesDerivedFrom<IProjectExtensionComponent>()
                .Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in types) {
                if (type.GetConstructor(Type.EmptyTypes) != null &&
                    Activator.CreateInstance(type) is IProjectExtensionComponent component) {
                    _components.Add(component);
                }
            }

            _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private static void OnGUI(string guid, Rect selectionRect) {
            if (!SettingSingleton.I.enableProjectExtension) return;
            if (ProjectExtensionAPI.IsHighlighted(guid)) EditorGUI.DrawRect(selectionRect, ColorPreset.HighlightColor);

            var currentRect = selectionRect;
            foreach (var component in _components) component.OnGUI(ref currentRect, guid, selectionRect);
        }
    }
}