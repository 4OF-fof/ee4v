using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.UI;
using Ee4v.Core.Settings;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.Mcp
{
    internal static class AssetManagerVariantMcpTools
    {
        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_list_variants",
                "Lists Project Variants with their working Scene, source Prefab, root path, and editable Material Variants.",
                McpSchemas.Object(new JObject { ["itemId"] = McpSchemas.String() }, "itemId"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    ListVariants(Required(arguments, "itemId"))))),
                readOnly: true));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_create_variant",
                "Creates an ee4v Prefab Variant and opens a working Scene containing its selected instance. Uses the same creator as AssetManager UI, including Material Variants and Item metadata. Does not save a history revision.",
                McpSchemas.Object(new JObject
                {
                    ["itemId"] = McpSchemas.String(),
                    ["sourcePrefabGuid"] = McpSchemas.String(),
                    ["name"] = McpSchemas.String(),
                    ["description"] = McpSchemas.String()
                }, "itemId", "sourcePrefabGuid", "name"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    CreateVariant(arguments)))),
                readOnly: false, idempotent: false));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_add_prefab_to_variant",
                "Adds an AssetManager-imported Prefab under the Variant instance in its working Scene. Keeps the source Prefab linked and unmodified; rejects dependency cycles. Apply it to the Variant Prefab with ee4v_asset_save_variant.",
                McpSchemas.Object(new JObject
                {
                    ["variantGuid"] = McpSchemas.String(),
                    ["sourcePrefabGuid"] = McpSchemas.String()
                }, "variantGuid", "sourcePrefabGuid"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    AddPrefab(arguments)))),
                readOnly: false, idempotent: false));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_create_material_variant",
                "Creates an editable Material Variant and replaces references on the Variant instance in its working Scene. Does not modify the imported source Material or Variant Prefab until ee4v_asset_save_variant.",
                McpSchemas.Object(new JObject
                {
                    ["variantGuid"] = McpSchemas.String(),
                    ["sourceMaterialPath"] = McpSchemas.String(),
                    ["sourceMaterialLocalId"] = McpSchemas.String(
                        "Required when the path contains multiple Material sub-assets; use materialLocalId from Prefab inspection.")
                }, "variantGuid", "sourceMaterialPath"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    CreateMaterialVariant(arguments)))),
                readOnly: false, idempotent: false));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_save_variant",
                "Applies the working Scene instance to the Variant Prefab, saves the Scene, then saves an AssetManager history revision.",
                McpSchemas.Object(new JObject
                {
                    ["variantGuid"] = McpSchemas.String(),
                    ["memo"] = McpSchemas.String()
                }, "variantGuid"),
                SaveVariant,
                readOnly: false, idempotent: false));
        }

        private static object ListVariants(string itemId)
        {
            AssetManagerMcpTools.Manager().GetItem(itemId);
            return new
            {
                ItemId = itemId,
                Variants = DerivedAssetCatalog.FindByParentItem(itemId)
                    .Select(Describe).ToArray()
            };
        }

        private static object CreateVariant(JObject arguments)
        {
            var itemId = Required(arguments, "itemId");
            var sourceGuid = Required(arguments, "sourcePrefabGuid");
            var name = Required(arguments, "name");
            var manager = AssetManagerMcpTools.Manager();
            manager.GetItem(itemId);
            if (!manager.GetItemImportedAssetGuids(itemId)
                    .Contains(sourceGuid, StringComparer.OrdinalIgnoreCase))
            {
                throw new McpToolException("source_not_imported_for_item",
                    "sourcePrefabGuid must belong to an imported Prefab of itemId.");
            }
            var source = AssetManagerPrefabMcpTools.Resolve(sourceGuid, null);
            if (!DerivedAssetCreator.IsValidName(name))
            {
                throw new McpToolException("invalid_variant_name", "name is not a valid Variant folder name.");
            }
            if (AssetDatabase.IsValidFolder(DerivedAssetCreator.GetVariantFolder(name)))
            {
                throw new McpToolException("variant_name_exists", "A Variant folder with this name already exists.");
            }
            var result = DerivedAssetCreator.Create(new DerivedAssetCreationRequest
            {
                ParentItemId = itemId,
                Prefab = source.Asset,
                Name = name,
                Description = (string)arguments["description"] ?? string.Empty
            });
            var root = DerivedAssetCreator.OpenWorkingScene(result.AssetPath);
            SceneManager.SetActiveScene(root.scene);
            Selection.activeGameObject = root;
            return Describe(DerivedAssetCatalog.Read(result.AssetPath));
        }

        private static object AddPrefab(JObject arguments)
        {
            var variant = ResolveVariant(Required(arguments, "variantGuid"));
            var sourceGuid = Required(arguments, "sourcePrefabGuid");
            var source = AssetManagerPrefabMcpTools.Resolve(sourceGuid, null);
            if (!AssetManagerMcpTools.Manager()
                    .GetImportedAssetAssociations(new[] { sourceGuid }).Any())
            {
                throw new McpToolException("source_not_imported",
                    "sourcePrefabGuid must be imported through AssetManager.");
            }
            if (string.Equals(source.Path, variant.AssetPath, StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.GetDependencies(source.Path, true)
                    .Contains(variant.AssetPath, StringComparer.OrdinalIgnoreCase))
            {
                throw new McpToolException("prefab_dependency_cycle",
                    "The source Prefab would create a dependency cycle.");
            }

            var root = DerivedAssetCreator.OpenWorkingScene(variant.AssetPath);
            var added = PrefabUtility.InstantiatePrefab(
                source.Asset, root.scene) as GameObject;
            if (added == null)
            {
                throw new McpToolException("prefab_instantiate_failed",
                    "The source Prefab could not be instantiated.");
            }
            added.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(added, "Add Variant Prefab");
            EditorSceneManager.MarkSceneDirty(root.scene);
            return new
            {
                VariantGuid = AssetDatabase.AssetPathToGUID(variant.AssetPath),
                VariantPath = variant.AssetPath,
                WorkingScenePath = root.scene.path,
                SourcePrefabGuid = source.Guid,
                SourcePrefabPath = source.Path,
                ChildName = added.name,
                SiblingIndex = added.transform.GetSiblingIndex()
            };
        }

        private static object CreateMaterialVariant(JObject arguments)
        {
            var variant = ResolveVariant(Required(arguments, "variantGuid"));
            var sourcePath = Required(arguments, "sourceMaterialPath").Replace('\\', '/');
            if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                sourcePath.Split('/').Any(part => part == ".."))
            {
                throw new McpToolException("invalid_material_path",
                    "sourceMaterialPath must be a Project-relative Assets/ path.");
            }
            var materials = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<Material>().ToArray();
            if (materials.Length == 0)
            {
                throw new McpToolException("material_not_found",
                    "sourceMaterialPath does not resolve to a Material.");
            }
            var localIdText = ((string)arguments["sourceMaterialLocalId"] ?? string.Empty).Trim();
            if (localIdText.Length == 0 && materials.Length > 1)
            {
                throw new McpToolException("material_local_id_required",
                    "The path contains multiple Materials. Supply sourceMaterialLocalId from Prefab inspection.");
            }
            Material material;
            if (localIdText.Length == 0)
            {
                material = materials[0];
            }
            else
            {
                if (!long.TryParse(localIdText, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var localId))
                {
                    throw new McpToolException("invalid_material_local_id",
                        "sourceMaterialLocalId must be a decimal local file ID.");
                }
                material = materials.FirstOrDefault(candidate =>
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        candidate, out _, out long candidateLocalId) &&
                    candidateLocalId == localId);
                if (material == null)
                {
                    throw new McpToolException("material_not_found",
                        "No Material at sourceMaterialPath matches sourceMaterialLocalId.");
                }
            }
            var materialPath = DerivedAssetCreator.CreateMaterialVariant(variant.AssetPath, material);
            return new
            {
                VariantGuid = AssetDatabase.AssetPathToGUID(variant.AssetPath),
                VariantPath = variant.AssetPath,
                WorkingScenePath = DerivedAssetCreator.GetWorkingScenePath(
                    variant.AssetPath),
                SourceMaterialPath = sourcePath,
                SourceMaterialLocalId = localIdText,
                MaterialVariantPath = materialPath,
                MaterialVariantGuid = AssetDatabase.AssetPathToGUID(materialPath)
            };
        }

        private static async Task<McpToolResult> SaveVariant(JObject arguments)
        {
            var variant = ResolveVariant(Required(arguments, "variantGuid"));
            DerivedAssetCreator.ApplyWorkingScene(variant.AssetPath);
            var databasePath = Path.Combine(GlobalDataSettings.RootDirectory, "asset-manager-v1.db");
            var manager = AssetManagerFactory.OpenVariantSession(
                databasePath, AssetManagerMcpTools.Manager());
            var revision = await manager.Save(new AssetVariantSaveRequest
            {
                RootAssetPath = variant.AssetPath,
                Memo = (string)arguments["memo"] ?? string.Empty
            });
            return McpToolResult.Success(McpJson.From(new
            {
                VariantGuid = AssetDatabase.AssetPathToGUID(variant.AssetPath),
                VariantPath = variant.AssetPath,
                WorkingScenePath = DerivedAssetCreator.GetWorkingScenePath(
                    variant.AssetPath),
                Revision = revision
            }));
        }

        private static DerivedAssetRecord ResolveVariant(string variantGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(variantGuid);
            var record = DerivedAssetCatalog.Read(path);
            if (record == null || !path.StartsWith(
                    DerivedAssetCatalog.VariantRoot + "/", StringComparison.Ordinal) ||
                PrefabUtility.GetPrefabAssetType(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path)) != PrefabAssetType.Variant)
            {
                throw new McpToolException("variant_not_found",
                    "variantGuid must resolve to an ee4v Prefab Variant in the current Project.");
            }
            AssetManagerMcpTools.Manager().GetItem(record.ParentItemId);
            return record;
        }

        private static object Describe(DerivedAssetRecord record)
        {
            var folder = Path.GetDirectoryName(record.AssetPath)?.Replace('\\', '/');
            var materialsFolder = folder + "/Assets/Materials";
            return new
            {
                VariantGuid = AssetDatabase.AssetPathToGUID(record.AssetPath),
                VariantPath = record.AssetPath,
                WorkingScenePath = DerivedAssetCreator.GetWorkingScenePath(
                    record.AssetPath),
                record.ParentItemId,
                record.Name,
                record.Description,
                SourcePrefabGuid = record.SourceGuid,
                SourcePrefabPath = AssetDatabase.GUIDToAssetPath(record.SourceGuid),
                MaterialVariants = AssetDatabase.IsValidFolder(materialsFolder)
                    ? AssetDatabase.FindAssets("t:Material", new[] { materialsFolder })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
                    : Array.Empty<string>()
            };
        }

        private static string Required(JObject arguments, string name)
        {
            var value = ((string)arguments[name] ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                throw new McpToolException("invalid_request", name + " is required.");
            }
            return value;
        }
    }
}
