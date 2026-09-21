using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class ModularAvatarMcpTools
    {
        private const string MergeArmatureTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature";
        private const string BlendshapeSyncTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarBlendshapeSync";
        private const string MenuItemTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";
        private const string MenuInstallerTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string ObjectToggleTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarObjectToggle";
        private const string MaterialSetterTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMaterialSetter";
        private const string ControlsRootName = "ee4v MCP Controls";

        internal static void Register()
        {
            RegisterListControls();
            RegisterApplyOutfit();
            RegisterObjectToggle();
            RegisterMaterialToggle();
        }

        private static void RegisterListControls()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_list_avatar_controls",
                "Lists Modular Avatar menu controls and their reaction components on an avatar.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String()
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(
                    ListControls(ResolveAvatar(arguments)))),
                readOnly: true));
        }

        private static void RegisterApplyOutfit()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_apply_outfit_setup",
                "Adds or updates Modular Avatar Merge Armature and Blendshape Sync components for an outfit already parented below an avatar. Existing unrelated bindings are preserved.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["outfitRef"] = McpSchemas.String(),
                    ["prefix"] = McpSchemas.String(
                        "Optional bone-name prefix removed while merging."),
                    ["suffix"] = McpSchemas.String(
                        "Optional bone-name suffix removed while merging."),
                    ["syncBlendshapes"] = McpSchemas.Boolean(
                        "Add same-name body BlendShape bindings. Defaults to true."),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "avatarRef", "outfitRef"),
                arguments => Task.FromResult(McpToolResult.Success(ApplyOutfit(
                    ResolveAvatar(arguments),
                    UnityObjectReference.ResolveGameObject((string)arguments["outfitRef"]),
                    (string)arguments["prefix"] ?? string.Empty,
                    (string)arguments["suffix"] ?? string.Empty,
                    (bool?)arguments["syncBlendshapes"] ?? true,
                    (bool?)arguments["dryRun"] ?? false))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static void RegisterObjectToggle()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_upsert_object_toggle",
                "Creates or updates an ee4v-owned Modular Avatar toggle that changes one or more avatar objects between active and inactive states.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["controlName"] = McpSchemas.String(
                        "Stable unique name used for the generated control object."),
                    ["label"] = McpSchemas.String(),
                    ["parameter"] = McpSchemas.String(),
                    ["targetRefs"] = McpSchemas.Array(McpSchemas.String()),
                    ["activeWhenOn"] = McpSchemas.Boolean(),
                    ["defaultOn"] = McpSchemas.Boolean(),
                    ["saved"] = McpSchemas.Boolean(),
                    ["synced"] = McpSchemas.Boolean(),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "avatarRef", "controlName", "parameter", "targetRefs"),
                arguments => Task.FromResult(McpToolResult.Success(UpsertObjectToggle(
                    ResolveAvatar(arguments),
                    RequiredName(arguments, "controlName"),
                    (string)arguments["label"],
                    RequiredText(arguments, "parameter"),
                    ResolveTargets(arguments, "targetRefs"),
                    (bool?)arguments["activeWhenOn"] ?? true,
                    (bool?)arguments["defaultOn"] ?? true,
                    (bool?)arguments["saved"] ?? true,
                    (bool?)arguments["synced"] ?? true,
                    (bool?)arguments["dryRun"] ?? false))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static void RegisterMaterialToggle()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_upsert_material_toggle",
                "Creates or updates an ee4v-owned Modular Avatar toggle that replaces one material slot while enabled.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["controlName"] = McpSchemas.String(),
                    ["label"] = McpSchemas.String(),
                    ["parameter"] = McpSchemas.String(),
                    ["rendererRef"] = McpSchemas.String(),
                    ["materialPath"] = McpSchemas.String(
                        "Replacement Material asset path under Assets."),
                    ["materialIndex"] = McpSchemas.Integer(),
                    ["defaultOn"] = McpSchemas.Boolean(),
                    ["saved"] = McpSchemas.Boolean(),
                    ["synced"] = McpSchemas.Boolean(),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "avatarRef", "controlName", "parameter", "rendererRef",
                    "materialPath", "materialIndex"),
                arguments => Task.FromResult(McpToolResult.Success(UpsertMaterialToggle(
                    ResolveAvatar(arguments),
                    RequiredName(arguments, "controlName"),
                    (string)arguments["label"],
                    RequiredText(arguments, "parameter"),
                    UnityObjectReference.ResolveGameObject((string)arguments["rendererRef"]),
                    RequiredMaterial((string)arguments["materialPath"]),
                    (int)arguments["materialIndex"],
                    (bool?)arguments["defaultOn"] ?? false,
                    (bool?)arguments["saved"] ?? true,
                    (bool?)arguments["synced"] ?? true,
                    (bool?)arguments["dryRun"] ?? false))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static JObject ListControls(GameObject avatar)
        {
            var menuType = FindComponentType(MenuItemTypeName);
            if (menuType == null)
            {
                return Availability(false, "Modular Avatar was not found.");
            }

            var values = avatar.GetComponentsInChildren(menuType, true)
                .Cast<Component>()
                .OrderBy(component => UnityObjectReference.HierarchyPath(
                    component.transform), StringComparer.Ordinal)
                .Select(component =>
                {
                    var portable = GetMember(component, "PortableControl");
                    return new JObject
                    {
                        ["objectRef"] = UnityObjectReference.Create(component.gameObject),
                        ["path"] = RelativePath(avatar.transform, component.transform),
                        ["label"] = (string)GetMember(component, "label") ??
                                    component.gameObject.name,
                        ["controlType"] = GetMember(portable, "Type")?.ToString() ??
                                          string.Empty,
                        ["parameter"] = (string)GetMember(portable, "Parameter") ??
                                        string.Empty,
                        ["value"] = JToken.FromObject(
                            Convert.ToSingle(GetMember(portable, "Value") ?? 0f)),
                        ["saved"] = JToken.FromObject(
                            Convert.ToBoolean(GetMember(component, "isSaved") ?? false)),
                        ["synced"] = JToken.FromObject(
                            Convert.ToBoolean(GetMember(component, "isSynced") ?? false)),
                        ["defaultOn"] = JToken.FromObject(
                            Convert.ToBoolean(GetMember(component, "isDefault") ?? false)),
                        ["reactionTypes"] = new JArray(component.GetComponents<Component>()
                            .Where(value => value != null && value != component &&
                                            value.GetType().FullName != typeof(Transform).FullName)
                            .Select(value => value.GetType().FullName)
                            .OrderBy(name => name, StringComparer.Ordinal))
                    };
                });
            return new JObject
            {
                ["ok"] = true,
                ["modularAvatarAvailable"] = true,
                ["controls"] = new JArray(values)
            };
        }

        private static JObject ApplyOutfit(
            GameObject avatar,
            GameObject outfit,
            string prefix,
            string suffix,
            bool syncBlendshapes,
            bool dryRun)
        {
            EnsureEditableAvatarChild(avatar, outfit, "outfitRef");
            var mergeType = RequireComponentType(MergeArmatureTypeName);
            var syncType = RequireComponentType(BlendshapeSyncTypeName);
            var outfitArmature = DirectChild(outfit.transform, "Armature");
            var avatarArmature = ResolveAvatarArmature(avatar);
            if (outfitArmature == null || avatarArmature == null)
            {
                throw new McpToolException(
                    "armature_not_found",
                    "Both the outfit and avatar must have a resolvable Armature root.");
            }

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
            var suggestions = outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null)
                .Select(renderer => new
                {
                    Renderer = renderer,
                    Shapes = Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                        .Select(renderer.sharedMesh.GetBlendShapeName)
                        .Where(bodyShapes.Contains)
                        .ToArray()
                })
                .Where(value => value.Shapes.Length > 0)
                .ToArray();

            if (dryRun)
            {
                return new JObject
                {
                    ["ok"] = true,
                    ["dryRun"] = true,
                    ["mergeArmaturePath"] = RelativePath(avatar.transform, outfitArmature),
                    ["mergeTargetPath"] = RelativePath(avatar.transform, avatarArmature),
                    ["blendshapeSyncRendererCount"] = syncBlendshapes ? suggestions.Length : 0,
                    ["blendshapeBindingCount"] = syncBlendshapes
                        ? suggestions.Sum(value => value.Shapes.Length)
                        : 0
                };
            }

            return Mutate("Apply ee4v Outfit Setup", () =>
            {
                var merge = GetOrAdd(outfitArmature.gameObject, mergeType);
                Undo.RecordObject(merge, "Configure Merge Armature");
                SetAvatarObjectReference(merge, "mergeTarget", avatarArmature.gameObject);
                SetMember(merge, "prefix", prefix);
                SetMember(merge, "suffix", suffix);
                MarkChanged(merge);

                var addedBindings = 0;
                if (syncBlendshapes && body != null)
                {
                    foreach (var suggestion in suggestions)
                    {
                        var sync = GetOrAdd(suggestion.Renderer.gameObject, syncType);
                        Undo.RecordObject(sync, "Configure Blendshape Sync");
                        var bindings = GetListMember(sync, "Bindings");
                        foreach (var shape in suggestion.Shapes)
                        {
                            if (bindings.Cast<object>().Any(binding =>
                                    BindingMatches(binding, sync, body.gameObject, shape)))
                            {
                                continue;
                            }

                            var binding = Activator.CreateInstance(
                                bindings.GetType().GetGenericArguments()[0]);
                            SetAvatarObjectReference(binding, "ReferenceMesh", body.gameObject);
                            SetMember(binding, "Blendshape", shape);
                            SetMember(binding, "LocalBlendshape", shape);
                            bindings.Add(binding);
                            addedBindings++;
                        }

                        SetMember(sync, "Bindings", bindings);
                        MarkChanged(sync);
                    }
                }

                return new JObject
                {
                    ["ok"] = true,
                    ["dryRun"] = false,
                    ["mergeArmatureRef"] = UnityObjectReference.Create(merge),
                    ["mergeArmaturePath"] = RelativePath(avatar.transform, outfitArmature),
                    ["mergeTargetPath"] = RelativePath(avatar.transform, avatarArmature),
                    ["blendshapeSyncRendererCount"] = syncBlendshapes ? suggestions.Length : 0,
                    ["addedBlendshapeBindingCount"] = addedBindings
                };
            });
        }

        private static JObject UpsertObjectToggle(
            GameObject avatar,
            string controlName,
            string label,
            string parameter,
            IReadOnlyList<GameObject> targets,
            bool activeWhenOn,
            bool defaultOn,
            bool saved,
            bool synced,
            bool dryRun)
        {
            EnsureEditableAvatar(avatar);
            if (targets.Count == 0)
            {
                throw new McpToolException("targets_required", "targetRefs must not be empty.");
            }

            foreach (var target in targets)
            {
                EnsureEditableAvatarChild(avatar, target, "targetRefs");
                if (target == avatar)
                {
                    throw new McpToolException(
                        "invalid_toggle_target",
                        "The avatar root itself cannot be an object-toggle target.");
                }
            }

            var menuType = RequireComponentType(MenuItemTypeName);
            var installerType = RequireComponentType(MenuInstallerTypeName);
            var toggleType = RequireComponentType(ObjectToggleTypeName);
            if (dryRun)
            {
                return ControlPlan(controlName, label, parameter, "ObjectToggle", defaultOn,
                    saved, synced, targets.Select(value => UnityObjectReference.Create(value)));
            }

            return Mutate("Upsert ee4v Object Toggle", () =>
            {
                var control = GetOrCreateControl(avatar, controlName);
                RejectOtherReaction(control, toggleType);
                var menu = GetOrAdd(control, menuType);
                GetOrAdd(control, installerType);
                var toggle = GetOrAdd(control, toggleType);
                ConfigureMenuItem(menu, label, parameter, defaultOn, saved, synced);

                Undo.RecordObject(toggle, "Configure Object Toggle");
                var objects = NewListMember(toggle, "Objects");
                var entryType = objects.GetType().GetGenericArguments()[0];
                foreach (var target in targets.Distinct())
                {
                    var entry = Activator.CreateInstance(entryType);
                    SetAvatarObjectReference(entry, "Object", target);
                    SetMember(entry, "Active", activeWhenOn);
                    objects.Add(entry);

                    Undo.RecordObject(target, "Set Object Toggle Default");
                    target.SetActive(defaultOn ? activeWhenOn : !activeWhenOn);
                    MarkChanged(target);
                }

                SetMember(toggle, "Objects", objects);
                MarkChanged(toggle);
                return ControlResult(control, menu, "ObjectToggle", targets.Count);
            });
        }

        private static JObject UpsertMaterialToggle(
            GameObject avatar,
            string controlName,
            string label,
            string parameter,
            GameObject rendererObject,
            Material material,
            int materialIndex,
            bool defaultOn,
            bool saved,
            bool synced,
            bool dryRun)
        {
            EnsureEditableAvatarChild(avatar, rendererObject, "rendererRef");
            var renderer = rendererObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new McpToolException(
                    "renderer_required",
                    "rendererRef must resolve to a GameObject with a Renderer component.");
            }

            if (materialIndex < 0 || materialIndex >= renderer.sharedMaterials.Length)
            {
                throw new McpToolException(
                    "material_index_out_of_range",
                    "materialIndex is outside the renderer's material slots.");
            }

            var menuType = RequireComponentType(MenuItemTypeName);
            var installerType = RequireComponentType(MenuInstallerTypeName);
            var setterType = RequireComponentType(MaterialSetterTypeName);
            if (dryRun)
            {
                return ControlPlan(controlName, label, parameter, "MaterialSetter", defaultOn,
                    saved, synced, new[] { UnityObjectReference.Create(rendererObject) });
            }

            return Mutate("Upsert ee4v Material Toggle", () =>
            {
                var control = GetOrCreateControl(avatar, controlName);
                RejectOtherReaction(control, setterType);
                var menu = GetOrAdd(control, menuType);
                GetOrAdd(control, installerType);
                var setter = GetOrAdd(control, setterType);
                ConfigureMenuItem(menu, label, parameter, defaultOn, saved, synced);

                Undo.RecordObject(setter, "Configure Material Setter");
                var objects = NewListMember(setter, "Objects");
                var entry = Activator.CreateInstance(objects.GetType().GetGenericArguments()[0]);
                SetAvatarObjectReference(entry, "Object", rendererObject);
                SetMember(entry, "Material", material);
                SetMember(entry, "MaterialIndex", materialIndex);
                objects.Add(entry);
                SetMember(setter, "Objects", objects);
                MarkChanged(setter);
                return ControlResult(control, menu, "MaterialSetter", 1);
            });
        }

        private static JObject ControlPlan(
            string controlName,
            string label,
            string parameter,
            string reactionType,
            bool defaultOn,
            bool saved,
            bool synced,
            IEnumerable<string> targetRefs)
        {
            return new JObject
            {
                ["ok"] = true,
                ["dryRun"] = true,
                ["controlName"] = controlName,
                ["label"] = string.IsNullOrWhiteSpace(label) ? controlName : label,
                ["parameter"] = parameter,
                ["reactionType"] = reactionType,
                ["defaultOn"] = defaultOn,
                ["saved"] = saved,
                ["synced"] = synced,
                ["parameterCostBits"] = synced ? 1 : 0,
                ["targetRefs"] = new JArray(targetRefs)
            };
        }

        private static JObject ControlResult(
            GameObject control,
            Component menu,
            string reactionType,
            int targetCount)
        {
            var portable = GetMember(menu, "PortableControl");
            return new JObject
            {
                ["ok"] = true,
                ["dryRun"] = false,
                ["controlRef"] = UnityObjectReference.Create(control),
                ["controlPath"] = UnityObjectReference.HierarchyPath(control.transform),
                ["label"] = (string)GetMember(menu, "label") ?? control.name,
                ["parameter"] = (string)GetMember(portable, "Parameter") ?? string.Empty,
                ["reactionType"] = reactionType,
                ["targetCount"] = targetCount
            };
        }

        private static void ConfigureMenuItem(
            Component menu,
            string label,
            string parameter,
            bool defaultOn,
            bool saved,
            bool synced)
        {
            Undo.RecordObject(menu, "Configure Menu Item");
            var portable = GetMember(menu, "PortableControl");
            if (portable == null)
            {
                throw MissingMember(menu, "PortableControl");
            }

            SetEnumMember(portable, "Type", "Toggle");
            SetMember(portable, "Parameter", parameter);
            SetMember(portable, "Value", 1f);
            SetMember(menu, "label", string.IsNullOrWhiteSpace(label)
                ? menu.gameObject.name
                : label.Trim());
            SetMember(menu, "isDefault", defaultOn);
            SetMember(menu, "isSaved", saved);
            SetMember(menu, "isSynced", synced);
            SetMember(menu, "automaticValue", false);
            MarkChanged(menu);
        }

        private static bool BindingMatches(
            object binding,
            Component sync,
            GameObject referenceMesh,
            string shape)
        {
            if (!string.Equals(
                    (string)GetMember(binding, "Blendshape"),
                    shape,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    (string)GetMember(binding, "LocalBlendshape"),
                    shape,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var reference = GetMember(binding, "ReferenceMesh");
            var get = reference?.GetType().GetMethod(
                "Get",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Component) },
                null);
            return get?.Invoke(reference, new object[] { sync }) as GameObject ==
                   referenceMesh;
        }

        private static GameObject GetOrCreateControl(GameObject avatar, string controlName)
        {
            var root = DirectChild(avatar.transform, ControlsRootName)?.gameObject;
            if (root == null)
            {
                root = new GameObject(ControlsRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create ee4v MCP Controls");
                root.transform.SetParent(avatar.transform, false);
            }

            var existing = DirectChild(root.transform, controlName)?.gameObject;
            if (existing != null)
            {
                return existing;
            }

            var control = new GameObject(controlName);
            Undo.RegisterCreatedObjectUndo(control, "Create Avatar Control");
            control.transform.SetParent(root.transform, false);
            return control;
        }

        private static void RejectOtherReaction(GameObject control, Type expected)
        {
            var reactionNames = new[] { ObjectToggleTypeName, MaterialSetterTypeName };
            foreach (var name in reactionNames)
            {
                var type = FindComponentType(name);
                if (type != null && type != expected && control.GetComponent(type) != null)
                {
                    throw new McpToolException(
                        "control_type_conflict",
                        "controlName is already used by a different reaction type.");
                }
            }
        }

        private static Component GetOrAdd(GameObject target, Type type)
        {
            return target.GetComponent(type) ?? Undo.AddComponent(target, type);
        }

        private static IList GetListMember(object target, string name)
        {
            var value = GetMember(target, name) as IList;
            if (value == null)
            {
                throw MissingMember(target, name);
            }

            return value;
        }

        private static IList NewListMember(object target, string name)
        {
            var type = GetMemberType(target, name);
            if (type == null || !typeof(IList).IsAssignableFrom(type))
            {
                throw MissingMember(target, name);
            }

            return (IList)Activator.CreateInstance(type);
        }

        private static void SetAvatarObjectReference(
            object container,
            string memberName,
            GameObject target)
        {
            var referenceType = GetMemberType(container, memberName);
            if (referenceType == null)
            {
                throw MissingMember(container, memberName);
            }

            var reference = GetMember(container, memberName) ??
                            Activator.CreateInstance(referenceType);
            var set = referenceType.GetMethod(
                "Set",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(GameObject) },
                null);
            if (set == null)
            {
                throw MissingMember(reference, "Set(GameObject)");
            }

            set.Invoke(reference, new object[] { target });
            SetMember(container, memberName, reference);
        }

        private static JObject Mutate(string undoName, Func<JObject> action)
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            try
            {
                var result = action();
                Undo.CollapseUndoOperations(group);
                return result;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static void MarkChanged(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            if (target is Component component)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            else if (target is GameObject gameObject)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            }
        }

        private static void EnsureEditableAvatar(GameObject avatar)
        {
            if (avatar == null)
            {
                throw new McpToolException("avatar_not_found", "The avatar was not found.");
            }

            if (EditorUtility.IsPersistent(avatar))
            {
                throw new McpToolException(
                    "prefab_asset_not_editable",
                    "Open the avatar prefab in Prefab Mode or place it in a scene before applying changes.");
            }
        }

        private static void EnsureEditableAvatarChild(
            GameObject avatar,
            GameObject child,
            string argumentName)
        {
            EnsureEditableAvatar(avatar);
            if (child == null ||
                (child != avatar && !child.transform.IsChildOf(avatar.transform)))
            {
                throw new McpToolException(
                    "object_outside_avatar",
                    argumentName + " must resolve to the avatar root or one of its descendants.");
            }
        }

        private static Transform ResolveAvatarArmature(GameObject avatar)
        {
            var animator = avatar.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null && hips.parent != null &&
                    hips.parent.IsChildOf(avatar.transform))
                {
                    return hips.parent;
                }
            }

            return DirectChild(avatar.transform, "Armature");
        }

        private static Transform DirectChild(Transform root, string name)
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

        private static string RelativePath(Transform root, Transform target)
        {
            return target == root
                ? string.Empty
                : AnimationUtility.CalculateTransformPath(target, root);
        }

        private static GameObject ResolveAvatar(JObject arguments)
        {
            return UnityObjectReference.ResolveGameObject((string)arguments["avatarRef"]);
        }

        private static IReadOnlyList<GameObject> ResolveTargets(
            JObject arguments,
            string propertyName)
        {
            var values = arguments[propertyName] as JArray;
            return values == null
                ? Array.Empty<GameObject>()
                : values.Values<string>()
                    .Select(UnityObjectReference.ResolveGameObject)
                    .ToArray();
        }

        private static string RequiredName(JObject arguments, string name)
        {
            var value = ((string)arguments[name])?.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Contains("/") || value.Contains("\\"))
            {
                throw new McpToolException(
                    "invalid_" + name,
                    name + " must be non-empty and must not contain a path separator.");
            }

            return value;
        }

        private static string RequiredText(JObject arguments, string name)
        {
            var value = ((string)arguments[name])?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new McpToolException(
                    "invalid_" + name,
                    name + " must be non-empty.");
            }

            return value;
        }

        private static Material RequiredMaterial(string assetPath)
        {
            var path = assetPath?.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new McpToolException(
                    "invalid_material_path",
                    "materialPath must be an asset path under Assets.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new McpToolException(
                    "material_not_found",
                    "No Material asset was found at materialPath.");
            }

            return material;
        }

        private static Type RequireComponentType(string fullName)
        {
            var type = FindComponentType(fullName);
            if (type == null)
            {
                throw new McpToolException(
                    "modular_avatar_unavailable",
                    "Required Modular Avatar component was not found: " + fullName);
            }

            return type;
        }

        private static Type FindComponentType(string fullName)
        {
            return TypeCache.GetTypesDerivedFrom<Component>()
                .FirstOrDefault(type => string.Equals(
                    type.FullName,
                    fullName,
                    StringComparison.Ordinal));
        }

        private static object GetMember(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance |
                                       BindingFlags.Public |
                                       BindingFlags.NonPublic;
            var type = target.GetType();
            var field = type.GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(target);
            }

            return type.GetProperty(name, flags)?.GetValue(target, null);
        }

        private static Type GetMemberType(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance |
                                       BindingFlags.Public |
                                       BindingFlags.NonPublic;
            var type = target.GetType();
            return type.GetField(name, flags)?.FieldType ??
                   type.GetProperty(name, flags)?.PropertyType;
        }

        private static void SetMember(object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Instance |
                                       BindingFlags.Public |
                                       BindingFlags.NonPublic;
            var type = target.GetType();
            var field = type.GetField(name, flags);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            var property = type.GetProperty(name, flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return;
            }

            throw MissingMember(target, name);
        }

        private static void SetEnumMember(object target, string name, string value)
        {
            var enumType = GetMemberType(target, name);
            if (enumType == null || !enumType.IsEnum)
            {
                throw MissingMember(target, name);
            }

            SetMember(target, name, Enum.Parse(enumType, value, true));
        }

        private static McpToolException MissingMember(object target, string name)
        {
            return new McpToolException(
                "modular_avatar_api_mismatch",
                (target?.GetType().FullName ?? "Unknown type") +
                " does not expose the expected member " + name + ".");
        }

        private static JObject Availability(bool available, string warning)
        {
            return new JObject
            {
                ["ok"] = true,
                ["modularAvatarAvailable"] = available,
                ["controls"] = new JArray(),
                ["warnings"] = new JArray(warning)
            };
        }
    }
}
