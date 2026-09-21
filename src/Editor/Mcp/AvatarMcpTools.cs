using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private const string PhysBoneColliderTypeName =
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider";
        private const string ContactSenderTypeName =
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender";
        private const string ContactReceiverTypeName =
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver";
        private const string HeadChopTypeName =
            "VRC.SDK3.Avatars.Components.VRCHeadChop";

        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_find_avatars",
                "Finds VRChat avatar roots in loaded scenes, Prefab Mode, or prefab assets. Returned avatarRef values are stable inputs for other ee4v tools.",
                McpSchemas.Object(new JObject
                {
                    ["scope"] = McpSchemas.Enum("loaded", "project"),
                    ["searchFolder"] = McpSchemas.String(
                        "Optional Assets folder used when scope is project.")
                }),
                arguments => Task.FromResult(McpToolResult.Success(
                    FindAvatars(
                        (string)arguments["scope"] ?? "loaded",
                        (string)arguments["searchFolder"]))),
                readOnly: true));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_inspect_avatar",
                "Returns a VRChat-oriented inventory of one avatar: rig, meshes, materials, textures, animation, PhysBones, contacts, constraints, Modular Avatar components, and expression parameter usage.",
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
                "Audits an avatar for common VRChat modification problems and returns machine-readable findings without changing it.",
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

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_plan_outfit_setup",
                "Analyzes an outfit against a target avatar and proposes armature, blendshape-sync, material, and clipping setup. This tool does not modify prefabs.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["outfitRef"] = McpSchemas.String(
                        "GlobalObjectId, hierarchy path, or prefab asset path for the outfit root.")
                }, "avatarRef", "outfitRef"),
                arguments => Task.FromResult(McpToolResult.Success(
                    PlanOutfit(
                        UnityObjectReference.ResolveGameObject(
                            (string)arguments["avatarRef"]),
                        UnityObjectReference.ResolveGameObject(
                            (string)arguments["outfitRef"])))),
                readOnly: true));
        }

        private static JObject FindAvatars(string scope, string searchFolder)
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

            IEnumerable<GameObject> avatars;
            if (string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase))
            {
                var folder = string.IsNullOrWhiteSpace(searchFolder)
                    ? "Assets"
                    : searchFolder.Trim().Replace('\\', '/');
                if (!folder.StartsWith("Assets", StringComparison.Ordinal) ||
                    !AssetDatabase.IsValidFolder(folder))
                {
                    throw new McpToolException(
                        "invalid_search_folder",
                        "searchFolder must be an existing folder under Assets.");
                }

                avatars = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .Where(prefab => prefab != null &&
                                     prefab.GetComponent(descriptorType) != null);
            }
            else
            {
                avatars = Resources.FindObjectsOfTypeAll(descriptorType)
                    .Cast<Component>()
                    .Select(component => component.gameObject)
                    .Where(gameObject =>
                        !EditorUtility.IsPersistent(gameObject) &&
                        gameObject.scene.IsValid());
            }

            var values = avatars
                .Distinct()
                .OrderBy(avatar => avatar.name, StringComparer.OrdinalIgnoreCase)
                .Select(AvatarSummary)
                .ToArray();
            return new JObject
            {
                ["ok"] = true,
                ["sdkAvailable"] = true,
                ["scope"] = scope,
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
                    ["triangleCount"] = meshes.Sum(mesh => (long)mesh.triangles.Length / 3L),
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
                    ["physBoneColliderCount"] = CountComponents(avatar, PhysBoneColliderTypeName),
                    ["contactSenderCount"] = CountComponents(avatar, ContactSenderTypeName),
                    ["contactReceiverCount"] = CountComponents(avatar, ContactReceiverTypeName),
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
                ["modularAvatar"] = new JObject
                {
                    ["componentCount"] = CountComponentsByNamespace(
                        avatar,
                        "nadena.dev.modular_avatar")
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
            var parameterBits = (int)inventory["expressions"]["syncedParameterBits"];

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

            if (parameterBits > 256)
            {
                AddFinding(findings, "error", "expression_parameter_budget_exceeded",
                    "Synced Expression Parameters use more than 256 bits.", descriptor);
            }

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
                ["findings"] = findings,
                ["inventory"] = inventory
            };
        }

        private static JObject PlanOutfit(GameObject avatar, GameObject outfit)
        {
            if (avatar == outfit)
            {
                throw new McpToolException(
                    "invalid_outfit",
                    "outfitRef must identify an outfit root, not the avatar root.");
            }

            var avatarBones = avatar.GetComponentsInChildren<Transform>(true)
                .GroupBy(transform => transform.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var outfitRenderers = outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var avatarRenderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => !renderer.transform.IsChildOf(outfit.transform))
                .ToArray();
            var body = avatarRenderers.FirstOrDefault(renderer =>
                           string.Equals(renderer.name, "Body", StringComparison.OrdinalIgnoreCase)) ??
                       avatarRenderers.FirstOrDefault(renderer =>
                           renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0);
            var bodyShapes = body?.sharedMesh == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(Enumerable.Range(0, body.sharedMesh.blendShapeCount)
                    .Select(body.sharedMesh.GetBlendShapeName), StringComparer.Ordinal);
            var usedBones = outfitRenderers
                .SelectMany(renderer => renderer.bones ?? Array.Empty<Transform>())
                .Where(bone => bone != null)
                .Distinct()
                .ToArray();
            var missingBones = usedBones
                .Where(bone => !avatarBones.ContainsKey(bone.name))
                .Select(bone => bone.name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var ambiguousBones = usedBones
                .Select(bone => bone.name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => avatarBones.TryGetValue(name, out var matches) && matches.Length > 1)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var sync = new JArray(outfitRenderers.SelectMany(renderer =>
            {
                var mesh = renderer.sharedMesh;
                return mesh == null
                    ? Enumerable.Empty<JObject>()
                    : Enumerable.Range(0, mesh.blendShapeCount)
                        .Select(mesh.GetBlendShapeName)
                        .Where(bodyShapes.Contains)
                        .Select(shape => new JObject
                        {
                            ["targetRendererPath"] = AnimationUtility.CalculateTransformPath(
                                renderer.transform,
                                outfit.transform),
                            ["targetShape"] = shape,
                            ["sourceRendererPath"] = body == null
                                ? string.Empty
                                : AnimationUtility.CalculateTransformPath(
                                    body.transform,
                                    avatar.transform),
                            ["sourceShape"] = shape
                        });
            }));

            return new JObject
            {
                ["ok"] = true,
                ["avatar"] = AvatarSummary(avatar),
                ["outfit"] = new JObject
                {
                    ["objectRef"] = UnityObjectReference.Create(outfit),
                    ["name"] = outfit.name,
                    ["rendererCount"] = outfitRenderers.Length,
                    ["materialCount"] = outfitRenderers
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Where(material => material != null)
                        .Distinct()
                        .Count()
                },
                ["armature"] = new JObject
                {
                    ["suggestedRoot"] = FindDirectChild(outfit.transform, "Armature") == null
                        ? string.Empty
                        : "Armature",
                    ["usedBoneCount"] = usedBones.Length,
                    ["missingBoneNames"] = new JArray(missingBones),
                    ["ambiguousBoneNames"] = new JArray(ambiguousBones),
                    ["canMergeByName"] = missingBones.Length == 0 && ambiguousBones.Length == 0
                },
                ["blendshapeSyncSuggestions"] = sync,
                ["recommendations"] = new JArray(
                    "Use Modular Avatar Merge Armature for the outfit armature.",
                    "Use Blendshape Sync for body-shape keys listed in blendshapeSyncSuggestions.",
                    "Use Shape Changer or Mesh Cutter only for body areas fully covered by the outfit.",
                    "Add an Object Toggle or menu item when the outfit must be removable in VRChat.")
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
                ["syncedParameterBits"] = 0
            };
            if (descriptor == null)
            {
                return result;
            }

            var parameters = GetMemberValue(descriptor, "expressionParameters") as UnityEngine.Object;
            var menu = GetMemberValue(descriptor, "expressionsMenu") as UnityEngine.Object;
            result["hasExpressionParameters"] = parameters != null;
            result["hasExpressionsMenu"] = menu != null;
            if (parameters == null)
            {
                return result;
            }

            var entries = GetMemberValue(parameters, "parameters") as IEnumerable;
            var count = 0;
            var bits = 0;
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    count++;
                    var synced = GetMemberValue(entry, "networkSynced");
                    if (synced is bool isSynced && !isSynced)
                    {
                        continue;
                    }

                    var type = GetMemberValue(entry, "valueType")?.ToString() ?? string.Empty;
                    bits += type.IndexOf("Bool", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 8;
                }
            }

            result["parameterCount"] = count;
            result["syncedParameterBits"] = bits;
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
            var root = descriptor == null
                ? null
                : GetMemberValue(descriptor, "expressionsMenu") as UnityEngine.Object;
            if (root == null)
            {
                return Array.Empty<JObject>();
            }

            var findings = new List<JObject>();
            var visited = new HashSet<int>();
            AuditMenu(root, visited, findings);
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
                var submenu = GetMemberValue(control, "subMenu") as UnityEngine.Object;
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

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
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
