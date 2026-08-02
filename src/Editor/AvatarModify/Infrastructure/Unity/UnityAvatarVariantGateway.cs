using System;
using System.Collections.Generic;
using System.IO;
using Ee4v.AssetManager.Contracts;
using Ee4v.AvatarModify.Application;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarModify.Infrastructure.Unity
{
    internal sealed class UnityAvatarVariantGateway : IAvatarVariantGateway
    {
        private readonly IAssetManagerAssetDerivationService _derivation;

        internal UnityAvatarVariantGateway(
            IAssetManagerAssetDerivationService derivation)
        {
            _derivation = derivation ??
                throw new ArgumentNullException(nameof(derivation));
        }

        public VariantAssetResult Create(VariantAssetRequest request)
        {
            if (request == null)
            {
                return Failure("Variant request is required.");
            }

            var sourcePath =
                AssetDatabase.GUIDToAssetPath(
                    request.SourcePrefabGuid);
            var source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourcePath);
            if (source == null ||
                !_derivation.CanCreatePrefabVariant(
                    request.SourcePrefabGuid))
            {
                return Failure(
                    "Source prefab cannot create a variant.");
            }

            var variantName = SanitizeName(
                request.VariantName);
            if (string.IsNullOrWhiteSpace(variantName))
            {
                variantName = SanitizeName(source.name);
            }

            var destinationRoot = NormalizeAssetPath(
                request.DestinationRoot);
            if (string.IsNullOrWhiteSpace(destinationRoot))
            {
                destinationRoot = "Assets/AvatarVariants";
            }

            if (!destinationRoot.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    destinationRoot,
                    "Assets",
                    StringComparison.Ordinal))
            {
                return Failure(
                    "Variant destination must be inside Assets.");
            }

            var variantRoot = destinationRoot + "/" +
                              variantName;
            var createdAssets = new List<string>();
            var createdFolders = new List<string>();
            try
            {
                EnsureFolder(variantRoot, createdFolders);
                var prefabPath =
                    AssetDatabase.GenerateUniqueAssetPath(
                        variantRoot + "/" +
                        variantName + ".prefab");
                if (!_derivation.CreatePrefabVariant(
                        request.SourcePrefabGuid,
                        prefabPath))
                {
                    throw new InvalidOperationException(
                        "Could not create the prefab variant.");
                }

                createdAssets.Add(prefabPath);
                var materialMap = CreateMaterialVariants(
                    sourcePath,
                    variantRoot,
                    createdAssets,
                    createdFolders);
                if (materialMap.Count > 0)
                {
                    ReplaceMaterials(
                        prefabPath,
                        materialMap);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new VariantAssetResult
                {
                    Succeeded = true,
                    VariantPath = variantRoot,
                    VariantPrefabGuid =
                        AssetDatabase.AssetPathToGUID(
                            prefabPath)
                };
            }
            catch (Exception exception)
            {
                Rollback(createdAssets, createdFolders);
                return Failure(exception.Message);
            }
        }

        private Dictionary<string, Material>
            CreateMaterialVariants(
                string sourcePrefabPath,
                string variantRoot,
                ICollection<string> createdAssets,
                ICollection<string> createdFolders)
        {
            var result = new Dictionary<string, Material>(
                StringComparer.OrdinalIgnoreCase);
            var materialFolder = variantRoot + "/Materials";
            var dependencies = AssetDatabase.GetDependencies(
                sourcePrefabPath,
                true);
            for (var i = 0; i < dependencies.Length; i++)
            {
                var path = dependencies[i];
                var material =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        path);
                if (material == null)
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid) ||
                    result.ContainsKey(guid) ||
                    !_derivation.CanCreateMaterialVariant(guid))
                {
                    continue;
                }

                EnsureFolder(materialFolder, createdFolders);
                var destination =
                    AssetDatabase.GenerateUniqueAssetPath(
                        materialFolder + "/" +
                        SanitizeName(material.name) + ".mat");
                if (!_derivation.CreateMaterialVariant(
                        guid,
                        destination))
                {
                    throw new InvalidOperationException(
                        "Could not create material variant: " +
                        path);
                }

                createdAssets.Add(destination);
                var variant =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        destination);
                if (variant == null)
                {
                    throw new InvalidOperationException(
                        "Created material variant could not be loaded: " +
                        destination);
                }

                result[guid] = variant;
            }

            return result;
        }

        private static void ReplaceMaterials(
            string prefabPath,
            IReadOnlyDictionary<string, Material> replacements)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var renderers =
                    root.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var materials = renderers[i].sharedMaterials;
                    var changed = false;
                    for (var materialIndex = 0;
                         materialIndex < materials.Length;
                         materialIndex++)
                    {
                        var material = materials[materialIndex];
                        var path = AssetDatabase.GetAssetPath(
                            material);
                        var guid = AssetDatabase.AssetPathToGUID(
                            path);
                        if (!string.IsNullOrWhiteSpace(guid) &&
                            replacements.TryGetValue(
                                guid,
                                out var replacement))
                        {
                            materials[materialIndex] = replacement;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        renderers[i].sharedMaterials = materials;
                    }
                }

                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        prefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save material variants to the prefab variant.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(
            string assetPath,
            ICollection<string> createdFolders)
        {
            var parts = NormalizeAssetPath(assetPath).Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    createdFolders.Add(next);
                }

                current = next;
            }
        }

        private static void Rollback(
            IList<string> assets,
            IList<string> folders)
        {
            for (var i = assets.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(assets[i]);
            }

            for (var i = folders.Count - 1;
                 i >= 0;
                 i--)
            {
                if (AssetDatabase.IsValidFolder(folders[i]) &&
                    AssetDatabase.FindAssets(
                        string.Empty,
                        new[] { folders[i] }).Length == 0)
                {
                    AssetDatabase.DeleteAsset(folders[i]);
                }
            }
        }

        private static string SanitizeName(string value)
        {
            var result = value ?? string.Empty;
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result.Trim().Trim('.');
        }

        private static string NormalizeAssetPath(string value)
        {
            return (value ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .TrimEnd('/');
        }

        private static VariantAssetResult Failure(string error)
        {
            return new VariantAssetResult
            {
                Error = error ?? string.Empty
            };
        }
    }
}
