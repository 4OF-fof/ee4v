using System;
using Ee4v.AvatarInfo;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.Simulation;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ee4v.AssetManager.UI
{
    public sealed class DerivedAssetCreationRequest
    {
        public string ParentItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public GameObject Prefab { get; set; }
    }

    public sealed class DerivedAssetInfo
    {
        public string VariantId { get; set; }
        public string ParentItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string AssetPath { get; set; }
        public string ScenePath { get; set; }
        public DateTime UpdatedAt { get; set; }
        public GameObject Prefab { get; set; }
    }

    public static class DerivedAssetCreator
    {
        internal const string VariantRoot = DerivedAssetCatalog.VariantRoot;
        private const string MaterialsFolderName = "Materials";

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
                var dependencies = AssetDatabase
                    .GetDependencies(sourcePath, true)
                    .Where(IsMaterialDependency)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var materialsFolder = assetsFolder + "/" +
                                      MaterialsFolderName;
                var destinations = CreateDestinationPaths(
                    dependencies,
                    materialsFolder);
                if (dependencies.Length > 0)
                {
                    EnsureFolder(materialsFolder);
                }
                var objectMap = new Dictionary<
                    AssetObjectKey,
                    Object>();

                CreateMaterialVariants(
                    dependencies,
                    destinations,
                    objectMap);

                var rootPath = outputFolder + "/" + name + ".prefab";
                CreateRootPrefabVariant(
                    sourcePath,
                    rootPath,
                    objectMap);

                var importer = AssetImporter.GetAtPath(rootPath);
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        "The created Prefab importer is unavailable.");
                }

                importer.userData = DerivedAssetCatalog.Serialize(
                    request.ParentItemId,
                    name,
                    request.Description,
                    AssetDatabase.AssetPathToGUID(sourcePath));
                importer.SaveAndReimport();
                var scenePath = EnsureWorkingScene(rootPath);
                AssetDatabase.SaveAssets();

                return new DerivedAssetInfo
                {
                    VariantId = AssetDatabase.AssetPathToGUID(rootPath),
                    ParentItemId = request.ParentItemId ?? string.Empty,
                    Name = name,
                    Description = request.Description ?? string.Empty,
                    AssetPath = rootPath,
                    ScenePath = scenePath,
                    UpdatedAt = File.GetLastWriteTimeUtc(rootPath),
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

        public static string CreateMaterialVariant(
            string variantPath,
            Material sourceMaterial)
        {
            var record = DerivedAssetCatalog.Read(variantPath);
            if (record == null || sourceMaterial == null ||
                !variantPath.StartsWith(VariantRoot + "/", StringComparison.Ordinal))
            {
                throw new ArgumentException("An ee4v Variant and a source Material are required.");
            }

            var sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            var variantFolder = Path.GetDirectoryName(variantPath)?.Replace('\\', '/');
            var root = OpenWorkingScene(variantPath);
            if (string.IsNullOrEmpty(sourcePath) ||
                sourcePath.StartsWith(variantFolder + "/", StringComparison.OrdinalIgnoreCase) ||
                !root.GetComponentsInChildren<Renderer>(true)
                    .Any(renderer => renderer.sharedMaterials
                        .Contains(sourceMaterial)))
            {
                throw new ArgumentException("The Material must be an external dependency of the Variant.");
            }

            var materialsFolder = variantFolder + "/Assets/Materials";
            EnsureFolder(materialsFolder);
            var safeName = new string(sourceMaterial.name
                .Select(character => char.IsLetterOrDigit(character) ||
                    character == ' ' || character == '_' || character == '-'
                    ? character : '_')
                .Take(80).ToArray());
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Material";
            }
            var destinationPath = AssetDatabase.GenerateUniqueAssetPath(
                materialsFolder + "/" + safeName + ".mat");
            var materialVariant = new Material(sourceMaterial)
            {
                name = sourceMaterial.name,
                parent = sourceMaterial,
                hideFlags = HideFlags.None
            };
            AssetDatabase.CreateAsset(materialVariant, destinationPath);
            try
            {
                var objectMap = new Dictionary<AssetObjectKey, Object>();
                MapAsset(sourceMaterial, materialVariant, objectMap);
                var replaced = false;
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && RemapObjectReferences(component, objectMap))
                    {
                        replaced = true;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(
                            component);
                    }
                }
                if (!replaced)
                {
                    throw new InvalidOperationException(
                        "The source Material is not assigned in the working Scene.");
                }
                EditorSceneManager.MarkSceneDirty(root.scene);
                AssetDatabase.SaveAssets();
                return destinationPath;
            }
            catch
            {
                AssetDatabase.DeleteAsset(destinationPath);
                throw;
            }
        }

        public static IReadOnlyList<DerivedAssetInfo> FindByParentItem(
            string parentItemId)
        {
            return DerivedAssetCatalog.FindByParentItem(parentItemId)
                .Select(ToInfo)
                .ToArray();
        }

        public static IReadOnlyList<DerivedAssetInfo> FindAll()
        {
            return DerivedAssetCatalog.FindAll()
                .Select(ToInfo)
                .Where(info => info?.Prefab != null)
                .ToArray();
        }

        internal static DerivedAssetInfo Read(string assetPath)
        {
            var info = ToInfo(DerivedAssetCatalog.Read(assetPath));
            return info?.Prefab != null ? info : null;
        }

        private static DerivedAssetInfo ToInfo(DerivedAssetRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new DerivedAssetInfo
            {
                VariantId = AssetDatabase.AssetPathToGUID(record.AssetPath),
                ParentItemId = record.ParentItemId,
                Name = record.Name,
                Description = record.Description,
                AssetPath = record.AssetPath,
                ScenePath = GetWorkingScenePath(record.AssetPath),
                UpdatedAt = File.GetLastWriteTimeUtc(record.AssetPath),
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    record.AssetPath)
            };
        }

        public static string GetWorkingScenePath(string prefabPath)
        {
            return string.IsNullOrEmpty(prefabPath)
                ? string.Empty
                : Path.ChangeExtension(prefabPath, ".unity")
                    .Replace('\\', '/');
        }

        public static string EnsureWorkingScene(string prefabPath)
        {
            var scenePath = GetWorkingScenePath(prefabPath);
            if (string.IsNullOrEmpty(scenePath) ||
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                throw new InvalidOperationException(
                    "The Variant Prefab is unavailable.");
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                return scenePath;
            }

            // Create a named asset first: NewScene(Additive) rejects an open
            // untitled Scene, even when that Scene has no unsaved changes.
            if (!EditorSceneApi.TryCreateEmptySceneAsset(scenePath))
            {
                throw new InvalidOperationException(
                    "The Variant working Scene asset could not be created.");
            }
            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = default(Scene);
            var saved = false;
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!(PrefabUtility.InstantiatePrefab(prefab, scene)
                      is GameObject avatar))
                {
                    throw new InvalidOperationException(
                        "The Variant Prefab could not be placed in the Scene.");
                }
                GestureManagerIntegration.PlaceInScene(scene, avatar);
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                {
                    throw new InvalidOperationException(
                        "The Variant working Scene could not be saved.");
                }
                saved = true;
                return scenePath;
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
                if (!saved)
                {
                    AssetDatabase.DeleteAsset(scenePath);
                }
            }
        }

        public static GameObject OpenWorkingScene(string prefabPath)
        {
            var scenePath = EnsureWorkingScene(prefabPath);
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath, OpenSceneMode.Additive);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var root = scene.GetRootGameObjects().FirstOrDefault(candidate =>
                PrefabUtility.GetCorrespondingObjectFromSource(candidate) ==
                prefab);
            if (root == null && EditorApplication.isPlaying)
            {
                root = AvatarPlayModePerformanceCache.FindPlayModeAvatar(GetWorkingScenePath(prefabPath), prefabPath);
            }
            if (root == null)
            {
                throw new InvalidOperationException(
                    "The Variant working Scene has no Variant Prefab instance: " +
                    scenePath);
            }
            return root;
        }

        public static GameObject ApplyWorkingScene(string prefabPath)
        {
            var root = OpenWorkingScene(prefabPath);
            if (PrefabUtility.HasPrefabInstanceAnyOverrides(root, false))
            {
                PrefabUtility.ApplyPrefabInstance(
                    root, InteractionMode.AutomatedAction);
            }
            if (!EditorSceneManager.SaveScene(root.scene))
            {
                throw new InvalidOperationException(
                    "The Variant working Scene could not be saved.");
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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

        private static bool IsMaterialDependency(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            return typeof(Material).IsAssignableFrom(assetType);
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

        private static void CreateMaterialVariants(
            IReadOnlyList<string> dependencies,
            IReadOnlyDictionary<string, string> destinations,
            IDictionary<AssetObjectKey, Object> objectMap)
        {
            foreach (var sourcePath in dependencies)
            {
                var destinationPath = destinations[sourcePath];
                var sourceMaterial = AssetDatabase
                    .LoadAssetAtPath<Material>(sourcePath);
                if (sourceMaterial == null)
                {
                    throw new InvalidOperationException(
                        "A Material dependency could not be loaded: " +
                        sourcePath);
                }

                var materialVariant = new Material(sourceMaterial)
                {
                    parent = sourceMaterial
                };
                AssetDatabase.CreateAsset(
                    materialVariant,
                    destinationPath);
                MapAsset(sourceMaterial, materialVariant, objectMap);
            }

            AssetDatabase.SaveAssets();
        }

        private static void MapAsset(
            Object source,
            Object destination,
            IDictionary<AssetObjectKey, Object> objectMap)
        {
            if (destination != null &&
                TryGetAssetKey(source, out var sourceKey))
            {
                objectMap[sourceKey] = destination;
            }
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

        private static void CreateRootPrefabVariant(
            string sourcePath,
            string destinationPath,
            IDictionary<AssetObjectKey, Object> objectMap)
        {
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

                AddAvatarOptimizer(instance);

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
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AddAvatarOptimizer(GameObject instance)
        {
            const string descriptorTypeName =
                "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
            if (!instance.GetComponents<Component>().Any(component =>
                    component != null &&
                    component.GetType().FullName == descriptorTypeName))
            {
                return;
            }

            var optimizerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "Anatawa12.AvatarOptimizer.TraceAndOptimize"))
                .FirstOrDefault(type => type != null);
            if (optimizerType != null &&
                instance.GetComponent(optimizerType) == null)
            {
                instance.AddComponent(optimizerType);
            }
        }

        private static bool RemapObjectReferences(
            Object target,
            IDictionary<AssetObjectKey, Object> objectMap)
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
                        out var destination))
                {
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
