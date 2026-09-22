using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Infrastructure;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ee4v.Mcp
{
    internal static class AssetManagerPrefabMcpTools
    {
        internal sealed class PrefabCandidateResolution
        {
            internal IReadOnlyList<object> Candidates { get; set; }
            internal IReadOnlyList<object> UnresolvedImportedAssets { get; set; }
            internal int ImportedAssetGuidCount { get; set; }
            internal int NonPrefabAssetCount { get; set; }
            internal string EmptyReason { get; set; }
        }

        internal sealed class ResolvedPrefab
        {
            internal string Guid { get; set; }
            internal string Path { get; set; }
            internal GameObject Asset { get; set; }
            internal PrefabAssetType AssetType { get; set; }
            internal string DependencyHash { get; set; }
        }

        private sealed class CandidateSeed
        {
            internal string Guid { get; set; }
            internal string Path { get; set; }
            internal GameObject Asset { get; set; }
            internal PrefabAssetType AssetType { get; set; }
            internal HashSet<string> Sources { get; } =
                new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class PreviewIssue
        {
            public string Code { get; set; }
            public string Message { get; set; }
            public string RendererPath { get; set; }
            public int? MaterialSlot { get; set; }
        }

        internal static void Register()
        {
            RegisterInspectPrefab();
            AssetManagerPrefabPreviewMcpTool.Register();
        }

        internal static PrefabCandidateResolution FindCandidates(
            string itemId,
            IEnumerable<string> importedAssetGuids)
        {
            var importedGuids = (importedAssetGuids ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var seeds = new Dictionary<string, CandidateSeed>(
                StringComparer.OrdinalIgnoreCase);
            var unresolved = new List<object>();
            var nonPrefabCount = 0;

            foreach (var guid in importedGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    unresolved.Add(new
                    {
                        AssetGuid = guid,
                        AssetPath = string.Empty,
                        Reason = "guid_not_resolved_in_project"
                    });
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var type = prefab == null
                    ? PrefabAssetType.NotAPrefab
                    : PrefabUtility.GetPrefabAssetType(prefab);
                if (prefab == null || type == PrefabAssetType.NotAPrefab)
                {
                    nonPrefabCount++;
                    continue;
                }

                AddSeed(seeds, guid, path, prefab, type, "importedAsset");
            }

            foreach (var record in DerivedAssetCatalog.FindByParentItem(itemId))
            {
                var path = record.AssetPath;
                var guid = AssetDatabase.AssetPathToGUID(path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var type = prefab == null
                    ? PrefabAssetType.NotAPrefab
                    : PrefabUtility.GetPrefabAssetType(prefab);
                if (string.IsNullOrEmpty(guid) ||
                    prefab == null ||
                    type == PrefabAssetType.NotAPrefab)
                {
                    unresolved.Add(new
                    {
                        AssetGuid = guid ?? string.Empty,
                        AssetPath = path ?? string.Empty,
                        Reason = "derived_prefab_not_resolved_in_project"
                    });
                    continue;
                }

                AddSeed(seeds, guid, path, prefab, type, "derivedPrefab");
            }

            var candidates = seeds.Values
                .OrderBy(seed => seed.Asset.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(seed => seed.Path, StringComparer.OrdinalIgnoreCase)
                .Select(seed => DescribeCandidate(itemId, seed))
                .ToArray();
            return new PrefabCandidateResolution
            {
                Candidates = candidates,
                UnresolvedImportedAssets = unresolved,
                ImportedAssetGuidCount = importedGuids.Length,
                NonPrefabAssetCount = nonPrefabCount,
                EmptyReason = candidates.Length == 0
                    ? importedGuids.Length == 0
                        ? "item_has_no_imported_assets_or_derived_prefabs"
                        : "no_imported_prefab_exists_in_project"
                    : string.Empty
            };
        }

        internal static ResolvedPrefab Resolve(
            string prefabGuid,
            string prefabPath)
        {
            var guid = (prefabGuid ?? string.Empty).Trim();
            var path = NormalizeProjectPath(prefabPath);
            if (guid.Length == 0 && path.Length == 0)
            {
                throw new McpToolException(
                    "prefab_reference_required",
                    "prefabGuid or prefabPath is required.");
            }

            if (guid.Length > 0)
            {
                var guidPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(guidPath))
                {
                    throw new McpToolException(
                        "prefab_not_found",
                        "prefabGuid does not resolve to an imported Project asset.",
                        new JObject { ["prefabGuid"] = guid });
                }

                if (path.Length > 0 && !string.Equals(
                        path,
                        guidPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new McpToolException(
                        "prefab_reference_mismatch",
                        "prefabGuid and prefabPath resolve to different assets.",
                        new JObject
                        {
                            ["prefabGuid"] = guid,
                            ["guidAssetPath"] = guidPath,
                            ["prefabPath"] = path
                        });
                }

                path = guidPath;
            }
            else
            {
                guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new McpToolException(
                        "prefab_not_found",
                        "prefabPath does not resolve to an imported Project asset.",
                        new JObject { ["prefabPath"] = path });
                }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var type = prefab == null
                ? PrefabAssetType.NotAPrefab
                : PrefabUtility.GetPrefabAssetType(prefab);
            if (prefab == null || type == PrefabAssetType.NotAPrefab)
            {
                throw new McpToolException(
                    "asset_is_not_prefab",
                    "The resolved Project asset is not a Prefab.",
                    new JObject
                    {
                        ["prefabGuid"] = guid,
                        ["prefabPath"] = path
                    });
            }

            return new ResolvedPrefab
            {
                Guid = guid,
                Path = path,
                Asset = prefab,
                AssetType = type,
                DependencyHash = AssetDatabase
                    .GetAssetDependencyHash(path)
                    .ToString()
            };
        }

        internal static IReadOnlyList<object> GetPreviewIssues(
            GameObject prefab)
        {
            return FindPreviewIssues(prefab).Cast<object>().ToArray();
        }

        internal static void EnsurePreviewable(ResolvedPrefab resolved)
        {
            var issues = FindPreviewIssues(resolved?.Asset);
            if (issues.Count == 0)
            {
                return;
            }

            var code = issues.Any(issue => issue.Code == "missing_shader")
                ? "prefab_missing_shader"
                : "prefab_preview_unavailable";
            throw new McpToolException(
                code,
                "The Prefab cannot be rendered by the fixed preview profile.",
                new JObject
                {
                    ["prefabGuid"] = resolved?.Guid ?? string.Empty,
                    ["prefabPath"] = resolved?.Path ?? string.Empty,
                    ["reasons"] = McpJson.From(issues)
                });
        }

        private static void RegisterInspectPrefab()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_inspect_prefab",
                "Inspects an imported Project Prefab without modifying the Prefab, Scene, or AssetManager state. Returns hierarchy, renderers, meshes, materials, shaders, textures, BlendShapes, missing references, and Unity MCP handoff paths.",
                PrefabReferenceSchema(),
                arguments => Task.FromResult(McpToolResult.Success(
                    McpJson.From(Inspect(Resolve(
                        (string)arguments["prefabGuid"],
                        (string)arguments["prefabPath"]))))),
                readOnly: true));
        }

        private static object Inspect(ResolvedPrefab resolved)
        {
            var root = resolved.Asset;
            var gameObjects = root
                .GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.gameObject)
                .ToArray();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var missingScripts = gameObjects.Sum(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            var brokenMaterials = new List<object>();
            var brokenMeshes = new List<object>();
            var materialHandoffs = new List<object>();
            var rendererHandoffs = new List<string>();
            var rendererValues = new List<object>();
            var totalTriangles = 0L;
            Bounds? totalBounds = null;

            foreach (var renderer in renderers)
            {
                var relativePath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    root.transform);
                var rendererPath = HierarchyPath(root, relativePath);
                rendererHandoffs.Add(rendererPath);
                var mesh = RendererMesh(renderer);
                var meshReferenceState = MeshReferenceState(renderer);
                if (IsMeshRenderer(renderer) && mesh == null)
                {
                    brokenMeshes.Add(new
                    {
                        RendererPath = rendererPath,
                        RendererType = renderer.GetType().FullName,
                        ReferenceState = meshReferenceState
                    });
                }

                var triangleCount = TriangleCount(mesh);
                totalTriangles += triangleCount;
                var meshBounds = RendererBoundsInRootSpace(
                    renderer,
                    mesh,
                    root.transform);
                if (meshBounds.HasValue)
                {
                    totalBounds = Encapsulate(totalBounds, meshBounds.Value);
                }

                var blendShapes = mesh == null
                    ? Array.Empty<string>()
                    : Enumerable.Range(0, mesh.blendShapeCount)
                        .Select(mesh.GetBlendShapeName)
                        .ToArray();
                var materials = renderer.sharedMaterials ??
                                Array.Empty<Material>();
                var materialValues = new List<object>();
                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    var referenceState = MaterialReferenceState(renderer, slot);
                    if (material == null)
                    {
                        brokenMaterials.Add(new
                        {
                            RendererPath = rendererPath,
                            MaterialSlot = slot,
                            ReferenceState = referenceState
                        });
                    }

                    var materialPath = AssetDatabase.GetAssetPath(material);
                    var materialGuid = string.IsNullOrEmpty(materialPath)
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(materialPath);
                    var textures = MaterialTextures(material).ToArray();
                    materialValues.Add(new
                    {
                        Slot = slot,
                        ReferenceState = referenceState,
                        MaterialName = material == null
                            ? string.Empty
                            : material.name,
                        MaterialGuid = materialGuid,
                        MaterialPath = materialPath,
                        ShaderName = material?.shader == null
                            ? string.Empty
                            : material.shader.name,
                        Textures = textures
                    });
                    materialHandoffs.Add(new
                    {
                        RendererPath = rendererPath,
                        RelativeRendererPath = relativePath,
                        MaterialSlot = slot,
                        MaterialGuid = materialGuid,
                        MaterialPath = materialPath
                    });
                }

                rendererValues.Add(new
                {
                    RendererPath = rendererPath,
                    RelativeRendererPath = relativePath,
                    RendererType = renderer.GetType().FullName,
                    renderer.enabled,
                    Mesh = AssetReference(mesh),
                    MeshReferenceState = meshReferenceState,
                    Bounds = meshBounds.HasValue
                        ? BoundsValue(meshBounds.Value)
                        : null,
                    TriangleCount = triangleCount,
                    BlendShapes = blendShapes,
                    Materials = materialValues
                });
            }

            var issues = FindPreviewIssues(root);
            return new
            {
                Ok = true,
                PrefabGuid = resolved.Guid,
                AssetPath = resolved.Path,
                PrefabName = root.name,
                PrefabType = PrefabTypeName(resolved.AssetType),
                ParentPrefab = ParentPrefab(root, resolved.AssetType),
                resolved.DependencyHash,
                Hierarchy = new
                {
                    GameObjectCount = gameObjects.Length,
                    ActiveGameObjectCount = gameObjects.Count(IsActiveInPrefab),
                    MaxDepth = gameObjects.Max(gameObject => DepthFrom(
                        gameObject.transform,
                        root.transform)),
                    Nodes = gameObjects.Select(gameObject =>
                    {
                        var relativePath = AnimationUtility
                            .CalculateTransformPath(
                                gameObject.transform,
                                root.transform);
                        return new
                        {
                            Path = HierarchyPath(root, relativePath),
                            RelativePath = relativePath,
                            gameObject.activeSelf,
                            ChildCount = gameObject.transform.childCount,
                            Components = gameObject
                                .GetComponents<Component>()
                                .Select(component => component == null
                                    ? "Missing Script"
                                    : component.GetType().FullName)
                                .ToArray()
                        };
                    }).ToArray()
                },
                Bounds = totalBounds.HasValue
                    ? BoundsValue(totalBounds.Value)
                    : null,
                TriangleCount = totalTriangles,
                Renderers = rendererValues,
                MissingScriptCount = missingScripts,
                BrokenMaterialReferences = brokenMaterials,
                BrokenMeshReferences = brokenMeshes,
                UnityMcpHandoff = new
                {
                    PrefabPath = resolved.Path,
                    Renderers = rendererHandoffs,
                    Materials = materialHandoffs
                },
                PreviewAvailable = issues.Count == 0,
                PreviewUnavailableReasons = issues
            };
        }

        private static object DescribeCandidate(
            string itemId,
            CandidateSeed seed)
        {
            var issues = FindPreviewIssues(seed.Asset);
            return new
            {
                ItemId = itemId,
                PrefabGuid = seed.Guid,
                AssetPath = seed.Path,
                PrefabName = seed.Asset.name,
                PrefabType = PrefabTypeName(seed.AssetType),
                IsVariant = seed.AssetType == PrefabAssetType.Variant,
                ParentPrefab = ParentPrefab(seed.Asset, seed.AssetType),
                DependencyHash = AssetDatabase
                    .GetAssetDependencyHash(seed.Path)
                    .ToString(),
                PreviewAvailable = issues.Count == 0,
                PreviewUnavailableReason = issues.Count == 0
                    ? string.Empty
                    : issues[0].Code,
                PreviewUnavailableReasons = issues,
                Sources = seed.Sources.OrderBy(
                    value => value,
                    StringComparer.Ordinal).ToArray()
            };
        }

        private static void AddSeed(
            IDictionary<string, CandidateSeed> seeds,
            string guid,
            string path,
            GameObject prefab,
            PrefabAssetType type,
            string source)
        {
            var key = string.IsNullOrEmpty(guid) ? path : guid;
            if (!seeds.TryGetValue(key, out var seed))
            {
                seed = new CandidateSeed
                {
                    Guid = guid,
                    Path = path,
                    Asset = prefab,
                    AssetType = type
                };
                seeds.Add(key, seed);
            }

            seed.Sources.Add(source);
        }

        private static List<PreviewIssue> FindPreviewIssues(GameObject prefab)
        {
            var issues = new List<PreviewIssue>();
            if (prefab == null)
            {
                issues.Add(new PreviewIssue
                {
                    Code = "prefab_not_loaded",
                    Message = "The Prefab asset could not be loaded."
                });
                return issues;
            }

            var renderers = prefab.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled &&
                                   IsActiveInPrefab(renderer.gameObject))
                .ToArray();
            if (renderers.Length == 0)
            {
                issues.Add(new PreviewIssue
                {
                    Code = "no_enabled_renderer",
                    Message = "The Prefab has no enabled Renderer in its default hierarchy."
                });
                return issues;
            }

            foreach (var renderer in renderers)
            {
                var relativePath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    prefab.transform);
                var rendererPath = HierarchyPath(prefab, relativePath);
                if (IsMeshRenderer(renderer) && RendererMesh(renderer) == null)
                {
                    issues.Add(new PreviewIssue
                    {
                        Code = "missing_mesh",
                        Message = "A mesh Renderer has no resolvable Mesh.",
                        RendererPath = rendererPath
                    });
                }

                var materials = renderer.sharedMaterials ??
                                Array.Empty<Material>();
                if (materials.Length == 0)
                {
                    issues.Add(new PreviewIssue
                    {
                        Code = "missing_material",
                        Message = "An enabled Renderer has no Material slots.",
                        RendererPath = rendererPath
                    });
                }

                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (material == null)
                    {
                        issues.Add(new PreviewIssue
                        {
                            Code = "missing_material",
                            Message = "A Material slot is empty or has a broken reference.",
                            RendererPath = rendererPath,
                            MaterialSlot = slot
                        });
                    }
                    else if (material.shader == null || string.Equals(
                                 material.shader.name,
                                 "Hidden/InternalErrorShader",
                                 StringComparison.Ordinal))
                    {
                        issues.Add(new PreviewIssue
                        {
                            Code = "missing_shader",
                            Message = "A Material uses a missing or error Shader.",
                            RendererPath = rendererPath,
                            MaterialSlot = slot
                        });
                    }
                }
            }

            return issues;
        }

        private static JObject PrefabReferenceSchema()
        {
            return McpSchemas.Object(new JObject
            {
                ["prefabGuid"] = McpSchemas.String(
                    "GUID of an already imported Project Prefab."),
                ["prefabPath"] = McpSchemas.String(
                    "Optional Assets/ or Packages/ path. Paths outside the Project are rejected.")
            });
        }

        private static string NormalizeProjectPath(string value)
        {
            var path = (value ?? string.Empty).Trim().Replace('\\', '/');
            if (path.Length == 0)
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path) ||
                (!path.StartsWith("Assets/", StringComparison.Ordinal) &&
                 !string.Equals(path, "Assets", StringComparison.Ordinal) &&
                 !path.StartsWith("Packages/", StringComparison.Ordinal) &&
                 !string.Equals(path, "Packages", StringComparison.Ordinal)) ||
                path.Split('/').Any(part => part == ".."))
            {
                throw new McpToolException(
                    "prefab_path_outside_project",
                    "prefabPath must be a Project-relative Assets/ or Packages/ path.",
                    new JObject { ["prefabPath"] = value ?? string.Empty });
            }

            return path;
        }

        private static object ParentPrefab(
            GameObject prefab,
            PrefabAssetType type)
        {
            if (prefab == null || type != PrefabAssetType.Variant)
            {
                return null;
            }

            var parent = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            var path = AssetDatabase.GetAssetPath(parent);
            return parent == null || string.IsNullOrEmpty(path)
                ? null
                : new
                {
                    PrefabGuid = AssetDatabase.AssetPathToGUID(path),
                    AssetPath = path,
                    PrefabName = parent.name
                };
        }

        private static object AssetReference(Object value)
        {
            if (value == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(value);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                value,
                out string guid,
                out long localId);
            return new
            {
                Name = value.name,
                Guid = guid ?? string.Empty,
                Path = path ?? string.Empty,
                LocalId = localId
            };
        }

        private static IEnumerable<object> MaterialTextures(Material material)
        {
            if (material == null)
            {
                yield break;
            }

            foreach (var propertyName in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(propertyName);
                if (texture == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(texture);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    texture,
                    out string guid,
                    out long localId);
                yield return new
                {
                    PropertyName = propertyName,
                    TextureName = texture.name,
                    TextureGuid = guid ?? string.Empty,
                    TexturePath = path ?? string.Empty,
                    LocalId = localId
                };
            }
        }

        private static Mesh RendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            if (renderer is MeshRenderer)
            {
                return renderer.GetComponent<MeshFilter>()?.sharedMesh;
            }

            return null;
        }

        private static bool IsMeshRenderer(Renderer renderer)
        {
            return renderer is SkinnedMeshRenderer || renderer is MeshRenderer;
        }

        private static long TriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0L;
            }

            var triangles = 0L;
            for (var index = 0; index < mesh.subMeshCount; index++)
            {
                if (mesh.GetTopology(index) == MeshTopology.Triangles)
                {
                    triangles += mesh.GetIndexCount(index) / 3L;
                }
            }

            return triangles;
        }

        private static Bounds? RendererBoundsInRootSpace(
            Renderer renderer,
            Mesh mesh,
            Transform root)
        {
            if (renderer == null || root == null)
            {
                return null;
            }

            if (renderer is SkinnedMeshRenderer skinned && mesh != null)
            {
                var matrix = root.worldToLocalMatrix *
                             renderer.transform.localToWorldMatrix;
                return TransformBounds(skinned.localBounds, matrix);
            }

            if (mesh != null)
            {
                var matrix = root.worldToLocalMatrix *
                             renderer.transform.localToWorldMatrix;
                return TransformBounds(mesh.bounds, matrix);
            }

            return TransformBounds(
                renderer.bounds,
                root.worldToLocalMatrix);
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(bounds.center);
            var extents = bounds.extents;
            var axisX = matrix.MultiplyVector(
                new Vector3(extents.x, 0f, 0f));
            var axisY = matrix.MultiplyVector(
                new Vector3(0f, extents.y, 0f));
            var axisZ = matrix.MultiplyVector(
                new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static Bounds? Encapsulate(Bounds? current, Bounds value)
        {
            if (!current.HasValue)
            {
                return value;
            }

            var bounds = current.Value;
            bounds.Encapsulate(value);
            return bounds;
        }

        private static object BoundsValue(Bounds bounds)
        {
            return new
            {
                Center = VectorValue(bounds.center),
                Size = VectorValue(bounds.size),
                Min = VectorValue(bounds.min),
                Max = VectorValue(bounds.max)
            };
        }

        private static object VectorValue(Vector3 value)
        {
            return new { value.x, value.y, value.z };
        }

        private static string MaterialReferenceState(
            Renderer renderer,
            int slot)
        {
            var serialized = new SerializedObject(renderer);
            var materials = serialized.FindProperty("m_Materials");
            if (materials == null ||
                !materials.isArray ||
                slot < 0 ||
                slot >= materials.arraySize)
            {
                return "empty";
            }

            return ObjectReferenceState(materials.GetArrayElementAtIndex(slot));
        }

        private static string MeshReferenceState(Renderer renderer)
        {
            Object owner;
            if (renderer is SkinnedMeshRenderer)
            {
                owner = renderer;
            }
            else if (renderer is MeshRenderer)
            {
                owner = renderer.GetComponent<MeshFilter>();
            }
            else
            {
                return "not_applicable";
            }

            if (owner == null)
            {
                return "component_missing";
            }

            var mesh = new SerializedObject(owner).FindProperty("m_Mesh");
            return mesh == null ? "empty" : ObjectReferenceState(mesh);
        }

        private static string ObjectReferenceState(SerializedProperty property)
        {
            if (property.objectReferenceValue != null)
            {
                return "assigned";
            }

            return property.objectReferenceInstanceIDValue == 0
                ? "empty"
                : "missing";
        }

        private static bool IsActiveInPrefab(GameObject gameObject)
        {
            for (var current = gameObject?.transform;
                 current != null;
                 current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return gameObject != null;
        }

        private static int DepthFrom(Transform transform, Transform root)
        {
            var depth = 0;
            while (transform != null && transform != root)
            {
                transform = transform.parent;
                depth++;
            }

            return depth;
        }

        private static string HierarchyPath(
            GameObject root,
            string relativePath)
        {
            return string.IsNullOrEmpty(relativePath)
                ? root.name
                : root.name + "/" + relativePath;
        }

        private static string PrefabTypeName(PrefabAssetType type)
        {
            switch (type)
            {
                case PrefabAssetType.Regular:
                    return "regular";
                case PrefabAssetType.Variant:
                    return "variant";
                case PrefabAssetType.Model:
                    return "model";
                case PrefabAssetType.MissingAsset:
                    return "missingAsset";
                default:
                    return "notPrefab";
            }
        }
    }
}
