using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Interfaces;
using UnityEditor;

namespace _4OF.ee4v.Core.Manager {
    public class AssetManagerComponentManager : IDisposable {
        private readonly List<IAssetManagerComponent> _components = new();
        private AssetManagerContext _context;

        public void Dispose() {
            foreach (var component in _components)
                try {
                    component.Dispose();
                }
                catch {
                    // ignore
                }

            _components.Clear();
        }

        public void Initialize(AssetManagerContext context) {
            _context = context;
            _components.Clear();

            var types = TypeCache.GetTypesDerivedFrom<IAssetManagerComponent>()
                .Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in types) {
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                try {
                    if (Activator.CreateInstance(type) is IAssetManagerComponent component) _components.Add(component);
                }
                catch {
                    // ignore
                }
            }

            _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            foreach (var component in _components)
                try {
                    component.Initialize(_context);
                }
                catch {
                    // ignore
                }
        }

        public IEnumerable<IAssetManagerComponent> GetComponents(AssetManagerComponentLocation location) {
            return _components.Where(c => c.Location == location);
        }
    }
}