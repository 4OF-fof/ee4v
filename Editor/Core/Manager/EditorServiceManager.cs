using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.Manager {
    [InitializeOnLoad]
    internal static class EditorServiceManager {
        private static readonly List<IEditorService> Services = new();

        static EditorServiceManager() {
            ResolveAndInitialize();
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
        }

        private static void ResolveAndInitialize() {
            if (Services.Count > 0) return;

            var types = TypeCache.GetTypesDerivedFrom<IEditorService>()
                .Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in types) {
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                try {
                    if (Activator.CreateInstance(type) is IEditorService service) {
                        service.Initialize();
                        Services.Add(service);
                    }
                }
                catch (Exception e) {
                    Debug.LogError(I18N.Get("Debug.Core.EditorServiceManager.InitializeFailedFmt", type.Name, e.Message));
                }
            }
        }

        private static void DisposeAll() {
            foreach (var service in Services)
                try {
                    service.Dispose();
                }
                catch (Exception e) {
                    Debug.LogError(I18N.Get("Debug.Core.EditorServiceManager.DisposeFailedFmt", service.Name ?? service.GetType().Name, e.Message));
                }

            Services.Clear();
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeAll;
        }
    }
}