using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ee4v.AvatarInfo;
using Ee4v.Core.AvatarEvaluation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Profiling;

namespace Ee4v.Mcp
{
    internal static class AvatarMcpTools
    {
        private const string DescriptorTypeName =
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
        private const string PhysBoneTypeName =
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
        private const string HeadChopTypeName =
            "VRC.SDK3.Avatars.Components.VRCHeadChop";

        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_find_avatars",
                "Finds VRChat avatar roots in loaded scenes and Prefab Mode. Use the general Unity MCP for project asset searches.",
                McpSchemas.Object(),
                arguments => Task.FromResult(McpToolResult.Success(
                    FindAvatars())),
                readOnly: true));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_inspect_avatar",
                "Returns factual VRChat avatar inventory and metrics without judging them: rig, meshes, materials, textures, animation, PhysBones, constraints, and expression parameter usage.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(
                        "Reference returned by ee4v_find_avatars.")
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(
                    InspectAvatar(UnityObjectReference.ResolveGameObject(
                        (string)arguments["avatarRef"])))),
                readOnly: true));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_audit_avatar",
                "Evaluates an avatar against VRChat-oriented rules and returns only actionable findings and their severity. Use inspect_avatar when raw inventory or metrics are needed.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(
                        "Reference returned by ee4v_find_avatars."),
                    ["platform"] = McpSchemas.Enum("pc", "android")
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(
                    AuditAvatar(
                        UnityObjectReference.ResolveGameObject(
                            (string)arguments["avatarRef"]),
                        (string)arguments["platform"] ?? "pc"))),
                readOnly: true));

        }

        private static JObject FindAvatars()
        {
            var descriptorType = FindComponentType(DescriptorTypeName);
            if (descriptorType == null)
            {
                return new JObject
                {
                    ["ok"] = true,
                    ["sdkAvailable"] = false,
                    ["avatars"] = new JArray(),
                    ["warnings"] = new JArray("VRChat SDK avatar descriptor type was not found.")
                };
            }

            var avatars = Resources.FindObjectsOfTypeAll(descriptorType)
                .Cast<Component>()
                .Select(component => component.gameObject)
                .Where(gameObject =>
                    !EditorUtility.IsPersistent(gameObject) &&
                    gameObject.scene.IsValid() && gameObject.scene.isLoaded &&
                    !EditorSceneManager.IsPreviewScene(gameObject.scene));

            var values = avatars
                .Distinct()
                .OrderBy(avatar => avatar.name, StringComparer.OrdinalIgnoreCase)
                .Select(AvatarSummary)
                .ToArray();
            return new JObject
            {
                ["ok"] = true,
                ["sdkAvailable"] = true,
                ["scope"] = "loaded",
                ["avatars"] = new JArray(values)
            };
        }

        private static JObject AvatarSummary(GameObject avatar)
        {
            var assetPath = AssetDatabase.GetAssetPath(avatar);
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var kind = !string.IsNullOrEmpty(assetPath)
                ? "prefabAsset"
                : prefabStage != null && avatar.scene == prefabStage.scene
                    ? "prefabStage"
                    : "scene";
            return new JObject
            {
                ["avatarRef"] = UnityObjectReference.Create(avatar),
                ["name"] = avatar.name,
                ["kind"] = kind,
                ["isPlaying"] = EditorApplication.isPlaying,
                ["hierarchyPath"] = UnityObjectReference.HierarchyPath(avatar.transform),
                ["assetPath"] = assetPath,
                ["scenePath"] = avatar.scene.path
            };
        }

        private static JObject InspectAvatar(GameObject avatar)
        {
            var skinned = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshRenderers = avatar.GetComponentsInChildren<MeshRenderer>(true);
            var meshes = skinned
                .Select(renderer => renderer.sharedMesh)
                .Concat(avatar.GetComponentsInChildren<MeshFilter>(true)
                    .Select(filter => filter.sharedMesh))
                .Where(mesh => mesh != null)
                .Distinct()
                .ToArray();
            var materials = avatar.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            var textures = materials
                .SelectMany(MaterialTextures)
                .Where(texture => texture != null)
                .Distinct()
                .ToArray();
            var animator = avatar.GetComponent<Animator>();
            var descriptor = GetComponent(avatar, DescriptorTypeName);
            var expression = ReadExpressionSummary(descriptor);

            return new JObject
            {
                ["ok"] = true,
                ["avatar"] = AvatarSummary(avatar),
                ["scope"] = "currentHierarchy",
                ["lastMeasurement"] = McpJson.From(AvatarPlayModePerformanceCache.Get(avatar)),
                ["rig"] = new JObject
                {
                    ["hasAnimator"] = animator != null,
                    ["isHuman"] = animator != null && animator.isHuman,
                    ["hasAvatar"] = animator != null && animator.avatar != null,
                    ["transformCount"] = avatar.GetComponentsInChildren<Transform>(true).Length
                },
                ["rendering"] = new JObject
                {
                    ["skinnedMeshRendererCount"] = skinned.Length,
                    ["meshRendererCount"] = meshRenderers.Length,
                    ["uniqueMeshCount"] = meshes.Length,
                    ["triangleCount"] = skinned.Select(renderer => renderer.sharedMesh)
                        .Concat(meshRenderers.Select(renderer => renderer.GetComponent<MeshFilter>()?.sharedMesh))
                        .Where(mesh => mesh != null).Sum(mesh => (long)mesh.triangles.Length / 3L),
                    ["blendShapeCount"] = meshes.Sum(mesh => mesh.blendShapeCount),
                    ["materialSlotCount"] = avatar.GetComponentsInChildren<Renderer>(true)
                        .Sum(renderer => renderer.sharedMaterials.Length),
                    ["uniqueMaterialCount"] = materials.Length,
                    ["uniqueTextureCount"] = textures.Length,
                    ["estimatedTextureMemoryBytes"] = textures.Sum(
                        texture => Math.Max(0L, Profiler.GetRuntimeMemorySizeLong(texture)))
                },
                ["avatarDynamics"] = new JObject
                {
                    ["physBoneCount"] = CountComponents(avatar, PhysBoneTypeName),
                    ["headChopCount"] = CountComponents(avatar, HeadChopTypeName),
                    ["unityConstraintCount"] = avatar
                        .GetComponentsInChildren<Component>(true)
                        .Count(component => component is IConstraint),
                    ["vrcConstraintCount"] = CountComponentsByNamespace(
                        avatar,
                        "VRC.SDK3.Dynamics.Constraint.Components")
                },
                ["animation"] = new JObject
                {
                    ["animationClipCount"] = CountReferencedAnimationClips(avatar),
                    ["animatorCount"] = avatar.GetComponentsInChildren<Animator>(true).Length
                },
                ["expressions"] = expression,
                ["topLevelObjects"] = new JArray(TopLevelObjects(avatar))
            };
        }

        private static JObject AuditAvatar(GameObject avatar, string platform)
        {
            var inventory = InspectAvatar(avatar);
            var findings = new JArray();
            var descriptor = GetComponent(avatar, DescriptorTypeName);
            var animator = avatar.GetComponent<Animator>();
            var triangleCount = (long)inventory["rendering"]["triangleCount"];
            var parameterBits = (int?)inventory["expressions"]["syncedParameterBits"];
            var parameterLimit = (int?)inventory["expressions"]["parameterLimit"];

            if (descriptor == null)
            {
                AddFinding(findings, "error", "descriptor_missing",
                    "VRC Avatar Descriptor is missing.", avatar);
            }

            if (animator == null || animator.avatar == null)
            {
                AddFinding(findings, "error", "animator_avatar_missing",
                    "The root Animator has no Avatar assigned.", avatar);
            }
            else if (!animator.isHuman)
            {
                AddFinding(findings, "info", "generic_rig",
                    "The avatar uses a Generic rig; humanoid IK checks do not apply.", avatar);
            }

            if (!avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Any(renderer => string.Equals(
                        renderer.name,
                        "Body",
                        StringComparison.OrdinalIgnoreCase)))
            {
                AddFinding(findings, "warning", "body_renderer_missing",
                    "No SkinnedMeshRenderer named Body was found; ee4v face tools require an explicit mesh selection.",
                    avatar);
            }

            var triangleGuide = string.Equals(
                platform,
                "android",
                StringComparison.OrdinalIgnoreCase)
                ? 20000L
                : 70000L;
            if (triangleCount > triangleGuide)
            {
                AddFinding(findings, "warning", "triangle_guide_exceeded",
                    "Triangle count exceeds the common " + platform +
                    " avatar guide of " + triangleGuide + ".", avatar);
            }

            if (parameterBits.HasValue && parameterLimit.HasValue && parameterBits > parameterLimit)
            {
                AddFinding(findings, "error", "expression_parameter_budget_exceeded",
                    "Synced Expression Parameters exceed the SDK limit (" + parameterLimit +
                    " bits); source: " + (string)inventory["expressions"]["parameterSource"] + ".", descriptor);
            }
            if (!parameterBits.HasValue)
                AddFinding(findings, "warning", "expression_parameter_usage_unavailable",
                    "Parameter usage could not be obtained; it is not treated as zero.", avatar);
            var measured = AvatarPlayModePerformanceCache.Get(avatar);
            if (measured?.CapturedAt == null)
                AddFinding(findings, "info", "performance_measurement_missing",
                    "Current hierarchy counts are not final NDMF/AAO performance. Use ee4v_measure_avatar_performance for a processed measurement.", avatar);
            else if (!string.IsNullOrEmpty(measured.Error))
                AddFinding(findings, "warning", "performance_measurement_failed", measured.Error, avatar);

            foreach (var duplicate in DuplicateExpressionParameters(descriptor))
            {
                AddFinding(findings, "warning", "duplicate_expression_parameter",
                    "Expression parameter is declared more than once: " + duplicate,
                    descriptor);
            }

            foreach (var menuIssue in AuditExpressionMenus(descriptor))
            {
                findings.Add(menuIssue);
            }

            foreach (var constraint in avatar.GetComponentsInChildren<Component>(true)
                         .Where(component => component is IConstraint))
            {
                AddFinding(findings, "warning", "unity_constraint",
                    "Use a VRChat constraint instead of a Unity constraint for predictable runtime behavior.",
                    constraint);
            }

            AuditPhysBoneHumanoidRoots(avatar, animator, findings);
            return new JObject
            {
                ["ok"] = true,
                ["avatar"] = AvatarSummary(avatar),
                ["platform"] = platform,
                ["summary"] = new JObject
                {
                    ["errorCount"] = findings.Count(value =>
                        string.Equals((string)value["severity"], "error", StringComparison.Ordinal)),
                    ["warningCount"] = findings.Count(value =>
                        string.Equals((string)value["severity"], "warning", StringComparison.Ordinal)),
                    ["infoCount"] = findings.Count(value =>
                        string.Equals((string)value["severity"], "info", StringComparison.Ordinal))
                },
                ["findings"] = findings
            };
        }

        private static IEnumerable<JObject> TopLevelObjects(GameObject avatar)
        {
            for (var index = 0; index < avatar.transform.childCount; index++)
            {
                var child = avatar.transform.GetChild(index);
                yield return new JObject
                {
                    ["objectRef"] = UnityObjectReference.Create(child.gameObject),
                    ["name"] = child.name,
                    ["active"] = child.gameObject.activeSelf,
                    ["skinnedMeshRendererCount"] = child
                        .GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    ["componentTypes"] = new JArray(child.GetComponents<Component>()
                        .Where(component => component != null)
                        .Select(component => component.GetType().FullName)
                        .OrderBy(name => name, StringComparer.Ordinal))
                };
            }
        }

        private static JObject ReadExpressionSummary(Component descriptor)
        {
            var result = new JObject
            {
                ["hasDescriptor"] = descriptor != null,
                ["hasExpressionParameters"] = false,
                ["hasExpressionsMenu"] = false,
                ["parameterCount"] = 0,
                ["syncedParameterBits"] = JValue.CreateNull(),
                ["parameterLimit"] = JValue.CreateNull(),
                ["parameterSource"] = "unavailable"
            };
            if (descriptor == null)
            {
                return result;
            }

            var parameters = GetMemberValue(descriptor, "expressionParameters") as UnityEngine.Object;
            var menu = GetMemberValue(descriptor, "expressionsMenu") as UnityEngine.Object;
            result["hasExpressionParameters"] = parameters != null;
            result["hasExpressionsMenu"] = menu != null;
            try
            {
                var memory = AvatarInfoSdk.Provider?.ReadParameterMemory(descriptor.gameObject);
                if (memory != null)
                {
                    var estimated = !EditorApplication.isPlaying && memory.ItemsEstimated && memory.Items != null;
                    result["syncedParameterBits"] = estimated ? memory.Items.Sum(item => item.Used) : memory.Used;
                    result["parameterLimit"] = memory.Limit;
                    result["parameterCount"] = memory.Parameters?.Length ?? 0;
                    result["parameterSource"] = estimated ? "authoringEstimate" : EditorApplication.isPlaying ? "runtimeDescriptor" : "descriptor";
                    result["parameterMemory"] = McpJson.From(memory);
                }
            }
            catch (Exception exception) { result["parameterError"] = exception.GetBaseException().Message; }
            try
            {
                var candidates = AvatarAuthoringParameters.Read(descriptor.gameObject);
                result["authoringParameters"] = McpJson.From(candidates.Select(value => new { value.Name, type = value.Type.ToString(), value.Expression }));
                var composed = Ee4v.ExpressionMenu.ExpressionMenuApi.Inspect(descriptor.gameObject);
                result["composedMenuPageCount"] = composed.Pages.Count;
                result["composedMenuEntryCount"] = composed.Entries.Count;
                result["menuSource"] = "maAuthoring";
            }
            catch (Exception exception)
            {
                result["authoringError"] = exception.GetBaseException().Message;
            }
            return result;
        }

        private static IReadOnlyList<string> DuplicateExpressionParameters(Component descriptor)
        {
            var parameters = descriptor == null
                ? null
                : GetMemberValue(descriptor, "expressionParameters") as UnityEngine.Object;
            var entries = parameters == null
                ? null
                : GetMemberValue(parameters, "parameters") as IEnumerable;
            if (entries == null)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            foreach (var entry in entries)
            {
                var name = GetMemberValue(entry, "name") as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names.GroupBy(name => name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<JObject> AuditExpressionMenus(Component descriptor)
        {
            if (descriptor == null)
            {
                return Array.Empty<JObject>();
            }

            var findings = new List<JObject>();
            var visited = new HashSet<int>();
            try
            {
                AvatarAuthoringMenu.Read(descriptor.gameObject, out var sources);
                foreach (var source in sources.Where(page => page.Asset != null)) AuditMenu(source.Asset, visited, findings);
            }
            catch (Exception exception)
            {
                findings.Add(new JObject { ["severity"] = "warning", ["code"] = "expression_menu_resolution_failed",
                    ["message"] = exception.GetBaseException().Message });
            }
            return findings;
        }

        private static void AuditMenu(
            UnityEngine.Object menu,
            ISet<int> visited,
            ICollection<JObject> findings)
        {
            if (menu == null || !visited.Add(menu.GetInstanceID()))
            {
                return;
            }

            var controls = GetMemberValue(menu, "controls") as IEnumerable;
            if (controls == null)
            {
                return;
            }

            var values = controls.Cast<object>().Where(value => value != null).ToArray();
            if (values.Length > 8)
            {
                findings.Add(new JObject
                {
                    ["severity"] = "error",
                    ["code"] = "expression_menu_overflow",
                    ["message"] = "An Expressions Menu contains more than 8 controls.",
                    ["objectRef"] = UnityObjectReference.Create(menu),
                    ["assetPath"] = AssetDatabase.GetAssetPath(menu)
                });
            }

            foreach (var control in values)
            {
                var submenu = GetMemberValue(control, "type")?.ToString() == "SubMenu" ? GetMemberValue(control, "subMenu") as UnityEngine.Object : null;
                if (submenu != null)
                {
                    AuditMenu(submenu, visited, findings);
                }
            }
        }

        private static void AuditPhysBoneHumanoidRoots(
            GameObject avatar,
            Animator animator,
            JArray findings)
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            var humanoid = new HashSet<Transform>();
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone)
                {
                    continue;
                }

                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    humanoid.Add(transform);
                }
            }

            var physBoneType = FindComponentType(PhysBoneTypeName);
            if (physBoneType == null)
            {
                return;
            }

            foreach (Component physBone in avatar.GetComponentsInChildren(physBoneType, true))
            {
                var root = GetMemberValue(physBone, "rootTransform") as Transform ??
                           GetMemberValue(physBone, "_rootTransform") as Transform ??
                           physBone.transform;
                if (humanoid.Contains(root))
                {
                    AddFinding(findings, "error", "physbone_humanoid_root",
                        "A PhysBone uses a Humanoid bone as its root: " + root.name,
                        physBone);
                }
            }
        }

        private static IEnumerable<Texture> MaterialTextures(Material material)
        {
            foreach (var propertyName in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    yield return texture;
                }
            }
        }

        private static int CountReferencedAnimationClips(GameObject avatar)
        {
            return avatar.GetComponentsInChildren<Animator>(true)
                .Select(animator => animator.runtimeAnimatorController)
                .Where(controller => controller != null)
                .SelectMany(controller => controller.animationClips)
                .Where(clip => clip != null)
                .Distinct()
                .Count();
        }

        private static int CountComponents(GameObject avatar, string fullName)
        {
            var type = FindComponentType(fullName);
            return type == null ? 0 : avatar.GetComponentsInChildren(type, true).Length;
        }

        private static int CountComponentsByNamespace(
            GameObject avatar,
            string namespacePrefix)
        {
            return avatar.GetComponentsInChildren<Component>(true)
                .Count(component => component != null &&
                                    (component.GetType().Namespace ?? string.Empty)
                                    .StartsWith(namespacePrefix, StringComparison.Ordinal));
        }

        private static Component GetComponent(GameObject avatar, string fullName)
        {
            var type = FindComponentType(fullName);
            return type == null ? null : avatar.GetComponent(type);
        }

        private static Type FindComponentType(string fullName)
        {
            return TypeCache.GetTypesDerivedFrom<Component>()
                .FirstOrDefault(type => string.Equals(
                    type.FullName,
                    fullName,
                    StringComparison.Ordinal));
        }

        private static object GetMemberValue(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance |
                                       BindingFlags.Public |
                                       BindingFlags.NonPublic;
            var type = target.GetType();
            return type.GetField(name, flags)?.GetValue(target) ??
                   type.GetProperty(name, flags)?.GetValue(target, null);
        }

        private static void AddFinding(
            JArray findings,
            string severity,
            string code,
            string message,
            UnityEngine.Object target)
        {
            findings.Add(new JObject
            {
                ["severity"] = severity,
                ["code"] = code,
                ["message"] = message,
                ["objectRef"] = UnityObjectReference.Create(target),
                ["assetPath"] = AssetDatabase.GetAssetPath(target)
            });
        }
    }
}
