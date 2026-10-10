using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarInfo
{
    internal sealed class AvatarPerformanceOrigins
    {
        private readonly GameObject _avatar;
        private readonly Dictionary<UnityEngine.Object, AvatarInfoAnalysis.PerformanceOrigin> _objects =
            new Dictionary<UnityEngine.Object, AvatarInfoAnalysis.PerformanceOrigin>();
        private readonly Dictionary<ObjectReference, AvatarInfoAnalysis.PerformanceOrigin> _references =
            new Dictionary<ObjectReference, AvatarInfoAnalysis.PerformanceOrigin>();
        private IObjectRegistry _registry;

        internal AvatarPerformanceOrigins(GameObject source, GameObject copy)
        {
            _avatar = copy;
            var baseAsset = PrefabUtility.GetCorrespondingObjectFromSource(source);
            while (baseAsset != null)
            {
                var parentAsset = PrefabUtility.GetCorrespondingObjectFromSource(baseAsset);
                if (parentAsset == null || PrefabUtility.GetPrefabAssetType(parentAsset) == PrefabAssetType.Model) { break; }
                baseAsset = parentAsset;
            }
            var basePath = AssetDatabase.GetAssetPath(baseAsset);
            var body = new AvatarInfoAnalysis.PerformanceOrigin
                { Name = source.name, Path = string.Empty, AssetPath = basePath };
            void Remember(Transform original, Transform cloned, AvatarInfoAnalysis.PerformanceOrigin item)
            {
                if (original != source.transform && item == body)
                {
                    var belongsToBody = !string.IsNullOrEmpty(basePath) &&
                        PrefabUtility.GetCorrespondingObjectFromSourceAtPath(original.gameObject, basePath) != null;
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(original.gameObject) ||
                        !belongsToBody && original.parent == source.transform && !string.IsNullOrEmpty(basePath))
                        item = new AvatarInfoAnalysis.PerformanceOrigin
                        {
                            Name = original.name, Path = GetPath(source.transform, original),
                            AssetPath = PrefabUtility.IsAnyPrefabInstanceRoot(original.gameObject)
                                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original.gameObject) : string.Empty
                        };
                }
                _objects[cloned.gameObject] = item;
                foreach (var component in cloned.GetComponents<Component>())
                    if (component != null) { _objects[component] = item; }
                for (var index = 0; index < original.childCount; index++)
                    Remember(original.GetChild(index), cloned.GetChild(index), item);
            }
            Remember(source.transform, copy.transform, body);
        }

        internal void Track(IObjectRegistry registry)
        {
            _registry = registry;
            foreach (var item in _objects)
                if (item.Key != null) { _references[registry.GetReference(item.Key)] = item.Value; }
        }

        internal AvatarInfoAnalysis.PerformanceOrigin Resolve(UnityEngine.Object obj)
        {
            if (obj == null) { return null; }
            if (_objects.TryGetValue(obj, out var item)) { return item; }
            var reference = _registry?.GetReference(obj, false);
            if (reference != null && _references.TryGetValue(reference, out item)) { return item; }
            if (obj is Texture)
            {
                var asset = reference?.Object != null ? reference.Object : obj;
                var path = AssetDatabase.GetAssetPath(asset);
                return new AvatarInfoAnalysis.PerformanceOrigin
                {
                    Name = asset.name,
                    AssetPath = path.StartsWith("Assets/ee4v_Performance_", StringComparison.Ordinal)
                        ? string.Empty : path
                };
            }
            var transform = (obj as Component)?.transform ?? (obj as GameObject)?.transform;
            while (transform != null && transform != _avatar.transform)
            {
                if (_objects.TryGetValue(transform.gameObject, out item)) { return item; }
                var parentReference = _registry?.GetReference(transform.gameObject, false);
                if (parentReference != null && _references.TryGetValue(parentReference, out item)) { return item; }
                transform = transform.parent;
            }
            return null;
        }

        private static string GetPath(Transform root, Transform target)
        {
            var path = new List<string>();
            for (var current = target; current != null && current != root; current = current.parent)
                path.Add(current.name + "[" + current.GetSiblingIndex() + "]");
            path.Reverse();
            return string.Join("/", path);
        }
    }
}
