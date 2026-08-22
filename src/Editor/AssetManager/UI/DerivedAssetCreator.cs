using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ee4v.AssetManager.UI
{
    internal sealed class DerivedAssetCreationRequest
    {
        public string ParentItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public GameObject Prefab { get; set; }
    }

    internal sealed class DerivedAssetInfo
    {
        public string ParentItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string AssetPath { get; set; }
        public GameObject Prefab { get; set; }
    }

    internal static class DerivedAssetCreator
    {
        internal const string VariantRoot =
            "Assets/!ee4vAsset/Variant";
        private const string MetadataPrefix = "ee4v-derived-asset:v1:";

        private static readonly HashSet<string> SharedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".asmdef",
                ".asmref",
                ".cginc",
                ".compute",
                ".cs",
                ".dll",
                ".hlsl",
                ".shader",
                ".shadergraph"
            };

        private readonly struct AssetObjectKey : IEquatable<AssetObjectKey>
        {
            public AssetObjectKey(string guid, long localId)
            {
                Guid = guid ?? string.Empty;
                LocalId = localId;
            }

            public string Guid { get; }
            public long LocalId { get; }

            public bool Equals(AssetObjectKey other)
            {
                return LocalId == other.LocalId && string.Equals(
                    Guid,
                    other.Guid,
                    StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is AssetObjectKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Guid != null
                                ? StringComparer.Ordinal.GetHashCode(Guid)
                                : 0) * 397) ^ LocalId.GetHashCode();
                }
            }
        }

        [Serializable]
        private sealed class DerivedAssetMetadata
        {
            public string parentItemId;
            public string name;
            public string description;
            public string sourceGuid;
        }

        public static bool IsValidName(string value)
        {
            var name = (value ?? string.Empty).Trim();
            return name.Length > 0 &&
                   name != "." &&
                   name != ".." &&
                   name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   name.IndexOf('/') < 0 &&
                   name.IndexOf('\\') < 0;
        }

        public static string GetVariantFolder(string name)
        {
            return VariantRoot + "/" + (name ?? string.Empty).Trim();
        }

        public static IReadOnlyList<GameObject> FindPrefabCandidates(
            IEnumerable<string> importedAssetGuids)
        {
            return (importedAssetGuids ?? Array.Empty<string>())
                .Where(guid => !string.IsNullOrWhiteSpace(guid))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsPrefabPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null)
                .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(AssetDatabase.GetAssetPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static DerivedAssetInfo Create(
            DerivedAssetCreationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var name = (request.Name ?? string.Empty).Trim();
            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    "The derived asset name is invalid.",
                    nameof(request));
            }

            var sourcePath = AssetDatabase.GetAssetPath(request.Prefab);
            if (string.IsNullOrEmpty(sourcePath) ||
                !sourcePath.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "A Prefab asset is required.",
                    nameof(request));
            }

            var outputFolder = GetVariantFolder(name);
            if (AssetDatabase.IsValidFolder(outputFolder))
            {
                throw new InvalidOperationException(
                    "The output folder already exists.");
            }

            EnsureFolder(VariantRoot);
            var createdFolder = AssetDatabase.CreateFolder(
                VariantRoot,
                name);
            if (string.IsNullOrEmpty(createdFolder))
            {
                throw new InvalidOperationException(
                    "The output folder could not be created.");
            }

            try
            {
                var assetsFolder = outputFolder + "/Assets";
                AssetDatabase.CreateFolder(outputFolder, "Assets");

                var dependencies = AssetDatabase
                    .GetDependencies(sourcePath, true)
                    .Where(path => IsCopyableDependency(
                        path,
                        sourcePath))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var destinations = CreateDestinationPaths(
                    dependencies,
                    assetsFolder);
                var objectMap = new Dictionary<
                    AssetObjectKey,
                    AssetObjectKey>();

                CreateCopiedAssets(
                    dependencies,
                    destinations,
                    objectMap);
                RemapCopiedAssetReferences(
                    dependencies,
                    destinations,
                    objectMap);

                var prefabDestinations = dependencies
                    .Where(IsPrefabPath)
                    .ToDictionary(
                        path => path,
                        path => destinations[path],
                        StringComparer.OrdinalIgnoreCase);
                var createdPrefabs = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                var creatingPrefabs = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var dependency in prefabDestinations.Keys)
                {
                    CreatePrefabVariant(
                        dependency,
                        prefabDestinations[dependency],
                        prefabDestinations,
                        objectMap,
                        createdPrefabs,
                        creatingPrefabs);
                }

                var rootPath = outputFolder + "/" + name + ".prefab";
                CreatePrefabVariant(
                    sourcePath,
                    rootPath,
                    prefabDestinations,
                    objectMap,
                    createdPrefabs,
                    creatingPrefabs);

                var metadata = new DerivedAssetMetadata
                {
                    parentItemId = request.ParentItemId ?? string.Empty,
                    name = name,
                    description = request.Description ?? string.Empty,
                    sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath)
                };
                var importer = AssetImporter.GetAtPath(rootPath);
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        "The created Prefab importer is unavailable.");
                }

                importer.userData = MetadataPrefix +
                                    JsonUtility.ToJson(metadata);
                importer.SaveAndReimport();
                AssetDatabase.SaveAssets();

                return new DerivedAssetInfo
                {
                    ParentItemId = metadata.parentItemId,
                    Name = metadata.name,
                    Description = metadata.description,
                    AssetPath = rootPath,
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        rootPath)
                };
            }
            catch
            {
                AssetDatabase.DeleteAsset(outputFolder);
                throw;
            }
        }

        public static IReadOnlyList<DerivedAssetInfo> FindByParentItem(
            string parentItemId)
        {
            if (string.IsNullOrEmpty(parentItemId) ||
                !AssetDatabase.IsValidFolder(VariantRoot))
            {
                return Array.Empty<DerivedAssetInfo>();
            }

            return AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { VariantRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(ReadInfo)
                .Where(info => info != null && string.Equals(
                    info.ParentItemId,
                    parentItemId,
                    StringComparison.Ordinal))
                .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static DerivedAssetInfo ReadInfo(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            var data = importer?.userData ?? string.Empty;
            if (!data.StartsWith(
                    MetadataPrefix,
                    StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                var metadata = JsonUtility.FromJson<DerivedAssetMetadata>(
                    data.Substring(MetadataPrefix.Length));
                if (metadata == null)
                {
                    return null;
                }

                return new DerivedAssetInfo
                {
                    ParentItemId = metadata.parentItemId ?? string.Empty,
                    Name = metadata.name ?? Path.GetFileNameWithoutExtension(
                        path),
                    Description = metadata.description ?? string.Empty,
                    AssetPath = path,
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                };
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }

        private static bool IsCopyableDependency(
            string path,
            string sourcePrefabPath)
        {
            return !string.Equals(
                       path,
                       sourcePrefabPath,
                       StringComparison.OrdinalIgnoreCase) &&
                   path.StartsWith("Assets/", StringComparison.Ordinal) &&
                   !SharedExtensions.Contains(Path.GetExtension(path));
        }

        private static Dictionary<string, string> CreateDestinationPaths(
            IReadOnlyList<string> sourcePaths,
            string destinationFolder)
        {
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var usedNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var sourcePath in sourcePaths)
            {
                var fileName = Path.GetFileName(sourcePath);
                var stem = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var uniqueName = fileName;
                for (var suffix = 2; !usedNames.Add(uniqueName); suffix++)
                {
                    uniqueName = stem + "_" + suffix + extension;
                }
                result[sourcePath] = destinationFolder + "/" + uniqueName;
            }
            return result;
        }

        private static void CreateCopiedAssets(
            IReadOnlyList<string> dependencies,
            IReadOnlyDictionary<string, string> destinations,
            IDictionary<AssetObjectKey, AssetObjectKey> objectMap)
        {
            var copiedPaths = new List<KeyValuePair<string, string>>();
            foreach (var sourcePath in dependencies)
            {
                if (IsPrefabPath(sourcePath))
                {
                    continue;
                }

                var destinationPath = destinations[sourcePath];
                var sourceMaterial = AssetDatabase
                    .LoadAssetAtPath<Material>(sourcePath);
                if (sourceMaterial != null && sourcePath.EndsWith(
                        ".mat",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var materialVariant = new Material(sourceMaterial)
                    {
                        parent = sourceMaterial
                    };
                    AssetDatabase.CreateAsset(
                        materialVariant,
                        destinationPath);
                    MapAsset(sourceMaterial, materialVariant, objectMap);
                    continue;
                }

                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                {
                    throw new InvalidOperationException(
                        "A dependency could not be copied: " + sourcePath);
                }
                copiedPaths.Add(new KeyValuePair<string, string>(
                    sourcePath,
                    destinationPath));
            }

            AssetDatabase.SaveAssets();
            foreach (var copiedPath in copiedPaths)
            {
                AssetDatabase.ImportAsset(
                    copiedPath.Value,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                MapCopiedObjects(
                    copiedPath.Key,
                    copiedPath.Value,
                    objectMap);
            }
        }

        private static void RemapCopiedAssetReferences(
            IReadOnlyList<string> dependencies,
            IReadOnlyDictionary<string, string> destinations,
            IDictionary<AssetObjectKey, AssetObjectKey> objectMap)
        {
            foreach (var sourcePath in dependencies)
            {
                if (IsPrefabPath(sourcePath))
                {
                    continue;
                }

                var destinationPath = destinations[sourcePath];
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(
                             destinationPath))
                {
                    RemapObjectReferences(asset, objectMap);
                }

                var sourceMaterial = AssetDatabase
                    .LoadAssetAtPath<Material>(sourcePath);
                var destinationMaterial = AssetDatabase
                    .LoadAssetAtPath<Material>(destinationPath);
                if (sourceMaterial != null && destinationMaterial != null)
                {
                    destinationMaterial.parent = sourceMaterial;
                    EditorUtility.SetDirty(destinationMaterial);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void MapCopiedObjects(
            string sourcePath,
            string destinationPath,
            IDictionary<AssetObjectKey, AssetObjectKey> objectMap)
        {
            var destinationsById = AssetDatabase
                .LoadAllAssetsAtPath(destinationPath)
                .Where(asset => asset != null)
                .Select(asset => new
                {
                    Asset = asset,
                    Id = GetLocalId(asset)
                })
                .Where(entry => entry.Id.HasValue)
                .ToDictionary(entry => entry.Id.Value, entry => entry.Asset);
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(
                         sourcePath).Where(asset => asset != null))
            {
                var id = GetLocalId(source);
                if (id.HasValue && destinationsById.TryGetValue(
                        id.Value,
                        out var destination))
                {
                    MapAsset(source, destination, objectMap);
                }
            }
        }

        private static void MapAsset(
            Object source,
            Object destination,
            IDictionary<AssetObjectKey, AssetObjectKey> objectMap)
        {
            if (TryGetAssetKey(source, out var sourceKey) &&
                TryGetAssetKey(destination, out var destinationKey))
            {
                objectMap[sourceKey] = destinationKey;
            }
        }

        private static long? GetLocalId(Object asset)
        {
            return asset != null &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                asset,
                out _,
                out long localId)
                ? localId
                : (long?)null;
        }

        private static bool TryGetAssetKey(
            Object asset,
            out AssetObjectKey key)
        {
            if (asset != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out var guid,
                    out long localId))
            {
                key = new AssetObjectKey(guid, localId);
                return true;
            }

            key = default;
            return false;
        }

        private static Object ResolveAsset(AssetObjectKey key)
        {
            var path = AssetDatabase.GUIDToAssetPath(key.Guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .FirstOrDefault(asset => GetLocalId(asset) == key.LocalId);
        }

        private static void CreatePrefabVariant(
            string sourcePath,
            string destinationPath,
            IReadOnlyDictionary<string, string> prefabDestinations,
            IDictionary<AssetObjectKey, AssetObjectKey> objectMap,
            ISet<string> created,
            ISet<string> creating)
        {
            if (created.Contains(sourcePath))
            {
                return;
            }
            if (!creating.Add(sourcePath))
            {
                throw new InvalidOperationException(
                    "A cyclic Prefab dependency was found.");
            }

            foreach (var nestedPath in AssetDatabase
                         .GetDependencies(sourcePath, false)
                         .Where(path => IsPrefabPath(path) &&
                                        !string.Equals(
                                            path,
                                            sourcePath,
                                            StringComparison.OrdinalIgnoreCase)))
            {
                if (prefabDestinations.TryGetValue(
                        nestedPath,
                        out var nestedDestination))
                {
                    CreatePrefabVariant(
                        nestedPath,
                        nestedDestination,
                        prefabDestinations,
                        objectMap,
                        created,
                        creating);
                }
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = PrefabUtility.InstantiatePrefab(
                    source,
                    scene) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "The Prefab could not be instantiated: " + sourcePath);
                }

                ReplaceDirectNestedPrefabs(
                    instance,
                    prefabDestinations);
                foreach (var component in instance
                             .GetComponentsInChildren<Component>(true))
                {
                    if (component != null &&
                        RemapObjectReferences(component, objectMap))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                component);
                    }
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    destinationPath,
                    out var success);
                Object.DestroyImmediate(instance);
                if (!success || prefab == null)
                {
                    throw new InvalidOperationException(
                        "The Prefab Variant could not be saved: " +
                        destinationPath);
                }
                MapAsset(source, prefab, objectMap);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            creating.Remove(sourcePath);
            created.Add(sourcePath);
        }

        private static void ReplaceDirectNestedPrefabs(
            GameObject root,
            IReadOnlyDictionary<string, string> prefabDestinations)
        {
            var nestedRoots = root.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.gameObject)
                .Where(candidate => candidate != root &&
                                    PrefabUtility
                                        .IsAnyPrefabInstanceRoot(candidate))
                .Where(candidate => !HasNestedPrefabParent(
                    candidate.transform.parent,
                    root.transform))
                .ToArray();
            foreach (var nestedRoot in nestedRoots)
            {
                var source = PrefabUtility
                    .GetCorrespondingObjectFromSource(nestedRoot);
                var sourcePath = AssetDatabase.GetAssetPath(source);
                if (!prefabDestinations.TryGetValue(
                        sourcePath,
                        out var destinationPath))
                {
                    continue;
                }

                var destination = AssetDatabase
                    .LoadAssetAtPath<GameObject>(destinationPath);
                if (destination != null)
                {
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                        nestedRoot,
                        destination,
                        InteractionMode.AutomatedAction);
                }
            }
        }

        private static bool HasNestedPrefabParent(
            Transform parent,
            Transform root)
        {
            while (parent != null && parent != root)
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(parent.gameObject))
                {
                    return true;
                }
                parent = parent.parent;
            }
            return false;
        }

        private static bool RemapObjectReferences(
            Object target,
            IDictionary<AssetObjectKey, AssetObjectKey> objectMap)
        {
            if (target == null)
            {
                return false;
            }

            var serializedObject = new SerializedObject(target);
            var iterator = serializedObject.GetIterator();
            var changed = false;
            while (iterator.Next(true))
            {
                if (iterator.propertyType !=
                    SerializedPropertyType.ObjectReference)
                {
                    continue;
                }
                if (target is Material &&
                    string.Equals(
                        iterator.propertyPath,
                        "m_Parent",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var source = iterator.objectReferenceValue;
                if (TryGetAssetKey(source, out var sourceKey) &&
                    objectMap.TryGetValue(
                        sourceKey,
                        out var destinationKey))
                {
                    var destination = ResolveAsset(destinationKey);
                    if (destination != null)
                    {
                        iterator.objectReferenceValue = destination;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
            return changed;
        }

        private static bool IsPrefabPath(string path)
        {
            return path != null && path.EndsWith(
                ".prefab",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
