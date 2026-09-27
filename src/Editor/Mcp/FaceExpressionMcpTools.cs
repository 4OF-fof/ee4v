using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.FaceExpression;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class FaceExpressionMcpTools
    {
        internal static void Register()
        {
            RegisterListBlendShapePresets();
            RegisterGetBlendShapePreset();
            RegisterUpdateBlendShapePreset();
            RegisterInspectFace();
            RegisterWriteClip();
            RegisterInspectClip();
            RegisterEditAnimation();
            RegisterRemoveAnimationPose();
            RegisterRenderPreview();
            RegisterRemapClip();
            RegisterGetConfiguration();
            RegisterPlanApply();
            RegisterApply();
        }

        private static void RegisterListBlendShapePresets()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_list_blendshape_presets",
                "Lists saved FBX BlendShape presets with their asset GUIDs, paths, and mapping counts.",
                McpSchemas.Object(),
                _ => Task.FromResult(McpToolResult.Success(McpJson.From(new
                {
                    ok = true,
                    presets = BlendShapePresetApi.ListPresets()
                }))),
                readOnly: true));
        }

        private static void RegisterGetBlendShapePreset()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_get_blendshape_preset",
                "Reads one saved FBX BlendShape preset, including every mapping and its revision. Use meshLocalId and shapeName to identify mappings when updating.",
                McpSchemas.Object(new JObject
                {
                    ["assetGuid"] = McpSchemas.String(
                        "FBX asset GUID returned by ee4v_list_blendshape_presets.")
                }, "assetGuid"),
                arguments =>
                {
                    var preset = BlendShapePresetApi.GetPreset(
                        (string)arguments["assetGuid"]);
                    if (preset == null)
                    {
                        throw new McpToolException("preset_not_found",
                            "The saved FBX BlendShape preset was not found.");
                    }

                    return Task.FromResult(McpToolResult.Success(McpJson.From(new
                    {
                        ok = true,
                        preset
                    })));
                },
                readOnly: true));
        }

        private static void RegisterUpdateBlendShapePreset()
        {
            var changeSchema = McpSchemas.Object(new JObject
            {
                ["meshLocalId"] = McpSchemas.String(
                    "Mesh local ID as a string from ee4v_get_blendshape_preset."),
                ["shapeName"] = McpSchemas.String(),
                ["role"] = McpSchemas.String("Set to an empty string to clear."),
                ["side"] = McpSchemas.Enum(string.Empty, "L", "R"),
                ["mouthMorph"] = McpSchemas.Boolean(),
                ["appearancePart"] = McpSchemas.Enum(
                    string.Empty, "expression", "head", "chest", "waist",
                    "shoulders", "arms", "hands", "legs", "feet", "other"),
                ["appearanceGroup"] = McpSchemas.String(
                    "Set to an empty string to clear.")
            }, "meshLocalId", "shapeName");

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_update_blendshape_preset",
                "Partially updates named mappings in an existing FBX BlendShape preset. Unspecified fields and mappings are preserved. Requires the revision from get_blendshape_preset; dryRun validates without saving.",
                McpSchemas.Object(new JObject
                {
                    ["assetGuid"] = McpSchemas.String(),
                    ["expectedRevision"] = McpSchemas.String(),
                    ["changes"] = McpSchemas.Array(changeSchema),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "assetGuid", "expectedRevision", "changes"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    BlendShapePresetApi.UpdatePreset(
                        (string)arguments["assetGuid"],
                        McpJson.To<List<BlendShapePresetMappingChange>>(
                            arguments["changes"]),
                        (string)arguments["expectedRevision"],
                        (bool?)arguments["dryRun"] ?? false)))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static void RegisterInspectFace()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_inspect_face",
                "Lists editable BlendShape channels and their preset classification. When clipPath is provided, also overlays the clip and returns its revision and validation findings.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["clipPath"] = McpSchemas.String(
                        "Optional AnimationClip whose stored values should be overlaid."),
                    ["rendererPaths"] = McpSchemas.Array(McpSchemas.String())
                }, "avatarRef"),
                arguments =>
                {
                    var avatar = Avatar(arguments);
                    var clip = OptionalClip(arguments);
                    return Task.FromResult(McpToolResult.Success(McpJson.From(new
                    {
                        ok = true,
                        avatarRef = (string)arguments["avatarRef"],
                        clipPath = clip == null
                            ? string.Empty
                            : AssetDatabase.GetAssetPath(clip),
                        revision = clip == null
                            ? string.Empty
                            : FaceExpressionApi.Revision(clip),
                        channels = FaceExpressionApi.Inspect(
                            avatar,
                            clip,
                            McpJson.To<List<string>>(arguments["rendererPaths"])),
                        findings = clip == null
                            ? Array.Empty<FaceExpressionValidationFinding>()
                            : FaceExpressionApi.ValidateClip(avatar, clip)
                    })));
                },
                readOnly: true));
        }

        private static void RegisterWriteClip()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_upsert_expression_clip",
                "Creates or atomically updates a static, single-pose face AnimationClip. Use create to reject overwrites, patch to preserve unspecified channels, or replace to clear unspecified BlendShape channels. Use the expression-animation tools for clips with multiple poses.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["assetPath"] = McpSchemas.String(
                        "Destination .anim path under Assets."),
                    ["mode"] = McpSchemas.Enum("create", "patch", "replace"),
                    ["channels"] = McpSchemas.Array(StaticChannelSchema()),
                    ["dryRun"] = McpSchemas.Boolean(),
                    ["expectedRevision"] = McpSchemas.String(
                        "Optional revision returned by inspect_face when clipPath is provided.")
                }, "avatarRef", "assetPath", "mode", "channels"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.WriteClip(
                        Avatar(arguments),
                        (string)arguments["assetPath"],
                        ParseWriteMode((string)arguments["mode"]),
                        McpJson.To<List<FaceExpressionClipChange>>(arguments["channels"]),
                        (bool?)arguments["dryRun"] ?? false,
                        (string)arguments["expectedRevision"])))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static void RegisterInspectClip()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_inspect_expression_clip",
                "Inspects and validates a static or animated expression clip. Returns revision, validation findings and counts, and ordered poses with times, transitions, names, source clips, and loop state. Set includeAnimation=false for validation only; includeChannels=true includes pose BlendShape values.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["clipPath"] = McpSchemas.String(),
                    ["includeAnimation"] = McpSchemas.Boolean(),
                    ["includeChannels"] = McpSchemas.Boolean()
                }, "avatarRef", "clipPath"),
                arguments =>
                {
                    var clip = RequiredClip(arguments);
                    var findings = FaceExpressionApi.ValidateClip(Avatar(arguments), clip);
                    return Task.FromResult(McpToolResult.Success(McpJson.From(new
                    {
                        ok = true,
                        revision = FaceExpressionApi.Revision(clip),
                        animation = (bool?)arguments["includeAnimation"] == false ? null :
                            FaceExpressionApi.InspectAnimation(clip, (bool?)arguments["includeChannels"] ?? false),
                        errorCount = findings.Count(finding => finding.Severity == "error"),
                        warningCount = findings.Count(finding => finding.Severity == "warning"),
                        findings
                    })));
                }, readOnly: true));
        }

        private static void RegisterEditAnimation()
        {
            var properties = new JObject
            {
                ["action"] = McpSchemas.Enum("add", "update", "move", "setLoop"),
                ["avatarRef"] = McpSchemas.String(),
                ["clipPath"] = McpSchemas.String(),
                ["afterPoseIndex"] = McpSchemas.Integer(),
                ["poseIndex"] = McpSchemas.Integer(),
                ["targetIndex"] = McpSchemas.Integer("Adjacent destination index."),
                ["transitionDuration"] = McpSchemas.Number("Seconds to the following or inserted pose; must be positive."),
                ["name"] = McpSchemas.String("Empty clears the custom pose name."),
                ["sourceClipPath"] = McpSchemas.String("Read-only pose source. Empty clears the source."),
                ["channels"] = McpSchemas.Array(PoseChannelSchema()),
                ["looping"] = McpSchemas.Boolean(),
                ["dryRun"] = McpSchemas.Boolean(),
                ["expectedRevision"] = RevisionSchema()
            };
            var schema = McpSchemas.Object(properties, "action", "clipPath");
            schema["oneOf"] = new JArray(
                AnimationEditSchema(properties, "add", new[] { "avatarRef", "afterPoseIndex", "transitionDuration", "name", "sourceClipPath", "channels" }, new[] { "avatarRef", "afterPoseIndex", "transitionDuration" }),
                AnimationEditSchema(properties, "update", new[] { "avatarRef", "poseIndex", "name", "sourceClipPath", "channels", "transitionDuration" }, new[] { "avatarRef", "poseIndex" }),
                AnimationEditSchema(properties, "move", new[] { "poseIndex", "targetIndex" }, new[] { "poseIndex", "targetIndex" }),
                AnimationEditSchema(properties, "setLoop", new[] { "looping" }, new[] { "looping" }));
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_edit_expression_animation",
                "Edits one expression animation using action add, update, move, or setLoop. add duplicates a pose after afterPoseIndex; update patches supplied pose fields; move swaps adjacent poses; setLoop changes looping only. dryRun and expectedRevision apply to every action. Pose deletion is a separate destructive tool.",
                schema,
                arguments => Task.FromResult(EditAnimation(arguments)),
                readOnly: false, destructive: false, idempotent: false));
        }

        private static JObject AnimationEditSchema(JObject properties, string action, string[] fields, string[] required)
        {
            var selected = new JObject
            {
                ["action"] = McpSchemas.Enum(action)
            };
            foreach (var field in new[] { "clipPath", "dryRun", "expectedRevision" }.Concat(fields))
            {
                selected[field] = properties[field].DeepClone();
            }
            var schema = McpSchemas.Object(selected, new[] { "action", "clipPath" }.Concat(required).ToArray());
            if (action == "update")
            {
                schema["anyOf"] = new JArray(new[] { "name", "sourceClipPath", "channels", "transitionDuration" }
                    .Select(field => new JObject { ["required"] = new JArray(field) }));
            }
            return schema;
        }

        private static McpToolResult EditAnimation(JObject arguments)
        {
            var dryRun = (bool?)arguments["dryRun"] ?? false;
            var revision = (string)arguments["expectedRevision"];
            switch ((string)arguments["action"])
            {
                case "add":
                    if (arguments["transitionDuration"] == null ||
                        (arguments["transitionDuration"].Type != JTokenType.Integer && arguments["transitionDuration"].Type != JTokenType.Float))
                    {
                        throw new McpToolException("invalid_request", "transitionDuration must be a number.");
                    }
                    return McpToolResult.Success(McpJson.From(FaceExpressionApi.AddAnimationPose(
                        Avatar(arguments), RequiredClip(arguments), RequiredIndex(arguments, "afterPoseIndex"),
                        (float)arguments["transitionDuration"], (string)arguments["name"],
                        (string)arguments["sourceClipPath"], PoseChannels(arguments), dryRun, revision)));
                case "update":
                    RequiredIndex(arguments, "poseIndex");
                    return UpdateAnimationPose(arguments);
                case "move":
                    return McpToolResult.Success(McpJson.From(FaceExpressionApi.MoveAnimationPose(
                        RequiredClip(arguments), RequiredIndex(arguments, "poseIndex"), RequiredIndex(arguments, "targetIndex"), dryRun, revision)));
                case "setLoop":
                    if (arguments["looping"]?.Type != JTokenType.Boolean)
                    {
                        throw new McpToolException("invalid_request", "looping must be a boolean.");
                    }
                    return McpToolResult.Success(McpJson.From(FaceExpressionApi.SetAnimationLooping(
                        RequiredClip(arguments), (bool)arguments["looping"], dryRun, revision)));
                default:
                    throw new McpToolException("invalid_request", "action must be add, update, move, or setLoop.");
            }
        }

        private static int RequiredIndex(JObject arguments, string field)
        {
            if (arguments[field]?.Type != JTokenType.Integer)
            {
                throw new McpToolException("invalid_request", field + " must be an integer.");
            }
            return (int)arguments[field];
        }

        private static void RegisterRemoveAnimationPose()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_remove_expression_pose",
                "Removes one expression pose and closes the resulting gap. An expression clip must retain at least one pose.",
                McpSchemas.Object(new JObject
                {
                    ["clipPath"] = McpSchemas.String(),
                    ["poseIndex"] = McpSchemas.Integer(),
                    ["dryRun"] = McpSchemas.Boolean(),
                    ["expectedRevision"] = RevisionSchema()
                }, "clipPath", "poseIndex"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.RemoveAnimationPose(
                        RequiredClip(arguments),
                        (int)arguments["poseIndex"],
                        (bool?)arguments["dryRun"] ?? false,
                        (string)arguments["expectedRevision"])))),
                readOnly: false,
                destructive: true,
                idempotent: false));
        }

        private static void RegisterRenderPreview()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_render_expression_preview",
                "Renders a face-focused PNG preview of an avatar with an optional expression clip applied.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["clipPath"] = McpSchemas.String(),
                    ["width"] = McpSchemas.Integer(),
                    ["height"] = McpSchemas.Integer(),
                    ["time"] = McpSchemas.Number(
                        "Optional animation time in seconds. Defaults to 0.")
                }, "avatarRef"),
                arguments =>
                {
                    var avatar = Avatar(arguments);
                    var clip = OptionalClip(arguments);
                    var bytes = FaceExpressionApi.RenderPreview(
                        avatar,
                        clip,
                        (int?)arguments["width"] ?? 512,
                        (int?)arguments["height"] ?? 512,
                        (float?)arguments["time"] ?? 0f);
                    if (bytes.Length == 0)
                    {
                        return Task.FromResult(McpToolResult.Error(
                            "expression_preview_unavailable",
                            "The avatar has no active SkinnedMeshRenderer with an assigned mesh for a face preview."));
                    }

                    return Task.FromResult(McpToolResult.Image(
                        new JObject
                        {
                            ["ok"] = true,
                            ["byteLength"] = bytes.Length,
                            ["clipPath"] = clip == null
                                ? string.Empty
                                : AssetDatabase.GetAssetPath(clip),
                            ["time"] = (float?)arguments["time"] ?? 0f
                        },
                        bytes));
                },
                readOnly: true));
        }

        private static void RegisterRemapClip()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_remap_expression_clip",
                "Copies a face clip between avatars. Exact renderer-path and BlendShape-name matches are preferred, then ee4v preset role and side are used. Ambiguous or unresolved channels are reported instead of guessed.",
                McpSchemas.Object(new JObject
                {
                    ["sourceAvatarRef"] = McpSchemas.String(),
                    ["sourceClipPath"] = McpSchemas.String(),
                    ["targetAvatarRef"] = McpSchemas.String(),
                    ["targetAssetPath"] = McpSchemas.String(),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "sourceAvatarRef", "sourceClipPath", "targetAvatarRef", "targetAssetPath"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.RemapClip(
                        UnityObjectReference.ResolveGameObject(
                            (string)arguments["sourceAvatarRef"]),
                        LoadClip((string)arguments["sourceClipPath"], true),
                        UnityObjectReference.ResolveGameObject(
                            (string)arguments["targetAvatarRef"]),
                        (string)arguments["targetAssetPath"],
                        (bool?)arguments["dryRun"] ?? false)))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static void RegisterGetConfiguration()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_get_facial_configuration",
                "Reads ee4v gesture-matrix and menu-only face assignments from an avatar's generated FacialSet or FX controller.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String()
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(new
                {
                    ok = true,
                    configuration = FaceExpressionApi.ReadConfiguration(Avatar(arguments))
                }))),
                readOnly: true));
        }

        private static void RegisterPlanApply()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_plan_facial_set_apply",
                "Checks FacialSet prerequisites and reports the ee4v-owned prefab, controller, and menu assets that apply would create or update.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String()
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.PlanApply(Avatar(arguments))))),
                readOnly: true));
        }

        private static void RegisterApply()
        {
            var assignment = McpSchemas.Object(new JObject
            {
                ["left"] = GestureSchema(),
                ["right"] = GestureSchema(),
                ["clipPath"] = McpSchemas.String(),
                ["enableBlink"] = McpSchemas.Boolean(),
                ["fixMouth"] = McpSchemas.Boolean(),
                ["menuName"] = McpSchemas.String()
            }, "left", "right");
            var menuEntry = McpSchemas.Object(new JObject
            {
                ["name"] = McpSchemas.String(),
                ["clipPath"] = McpSchemas.String(),
                ["enableBlink"] = McpSchemas.Boolean(),
                ["fixMouth"] = McpSchemas.Boolean()
            }, "name", "clipPath");
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_apply_facial_set",
                "Generates or updates the ee4v-owned FX controller, Expressions Menu, icons, and Modular Avatar FacialSet prefab. mode=patch (default) merges supplied gesture combinations and menu names into current assignments; mode=replace uses the complete configuration. A one-entry patch edits one gesture without touching others. dryRun validates without writing assets. Run ee4v_plan_facial_set_apply first.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["mode"] = McpSchemas.Enum("patch", "replace"),
                    ["configuration"] = McpSchemas.Object(new JObject
                    {
                        ["assignments"] = McpSchemas.Array(assignment),
                        ["menuEntries"] = McpSchemas.Array(menuEntry)
                    }),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "avatarRef", "configuration"),
                arguments => Task.FromResult(ApplyFacialSet(arguments)),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static McpToolResult UpdateAnimationPose(JObject arguments)
        {
            var updateName = arguments.Property("name") != null;
            var updateSource = arguments.Property("sourceClipPath") != null;
            var updateChannels = arguments.Property("channels") != null;
            var updateTransition = arguments.Property("transitionDuration") != null;
            if (!updateName &&
                !updateSource &&
                !updateChannels &&
                !updateTransition)
            {
                throw new McpToolException(
                    "pose_update_empty",
                    "Provide name, sourceClipPath, channels, or transitionDuration.");
            }

            return McpToolResult.Success(McpJson.From(
                FaceExpressionApi.UpdateAnimationPose(
                    Avatar(arguments),
                    RequiredClip(arguments),
                    (int)arguments["poseIndex"],
                    updateName,
                    (string)arguments["name"],
                    updateSource,
                    (string)arguments["sourceClipPath"],
                    PoseChannels(arguments),
                    updateTransition
                        ? (float?)arguments["transitionDuration"]
                        : null,
                    (bool?)arguments["dryRun"] ?? false,
                    (string)arguments["expectedRevision"])));
        }

        private static IReadOnlyList<FaceExpressionClipChange> PoseChannels(
            JObject arguments)
        {
            return arguments.Property("channels") == null
                ? null
                : McpJson.To<List<FaceExpressionClipChange>>(
                    arguments["channels"]);
        }

        private static JObject StaticChannelSchema()
        {
            return McpSchemas.Object(new JObject
            {
                ["rendererPath"] = McpSchemas.String(),
                ["shapeName"] = McpSchemas.String(),
                ["value"] = McpSchemas.Number(),
                ["animated"] = McpSchemas.Boolean(
                    "Set false to remove the channel from an existing static clip.")
            }, "rendererPath", "shapeName", "value");
        }

        private static JObject PoseChannelSchema()
        {
            return McpSchemas.Object(new JObject
            {
                ["rendererPath"] = McpSchemas.String(),
                ["shapeName"] = McpSchemas.String(),
                ["value"] = McpSchemas.Number()
            }, "rendererPath", "shapeName", "value");
        }

        private static JObject RevisionSchema()
        {
            return McpSchemas.String(
                "Optional revision returned by ee4v_inspect_expression_clip. The write is rejected if the clip changed.");
        }

        private static GameObject Avatar(JObject arguments)
        {
            return UnityObjectReference.ResolveGameObject(
                (string)arguments["avatarRef"]);
        }

        private static McpToolResult ApplyFacialSet(JObject arguments)
        {
            if (!(arguments["configuration"] is JObject))
            {
                throw new McpToolException("invalid_request", "configuration must be an object.");
            }
            var avatar = Avatar(arguments);
            var configuration = McpJson.To<FaceExpressionConfigurationData>(arguments["configuration"]);
            var mode = (string)arguments["mode"] ?? "patch";
            if (mode == "patch")
            {
                var current = FaceExpressionApi.ReadConfiguration(avatar);
                var assignments = (current.Assignments ?? Array.Empty<FaceExpressionGestureAssignmentData>()).ToList();
                var gestures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var assignment in configuration.Assignments ?? Array.Empty<FaceExpressionGestureAssignmentData>())
                {
                    if (assignment == null || !gestures.Add(assignment.Left + ":" + assignment.Right))
                    {
                        throw new McpToolException("invalid_request", "Gesture patches must be non-null and unique.");
                    }
                    var index = assignments.FindIndex(existing =>
                        string.Equals(existing.Left, assignment.Left, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(existing.Right, assignment.Right, StringComparison.OrdinalIgnoreCase));
                    if (index < 0) assignments.Add(assignment);
                    else assignments[index] = assignment;
                }
                var menus = (current.MenuEntries ?? Array.Empty<FaceExpressionMenuEntryData>()).ToList();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in configuration.MenuEntries ?? Array.Empty<FaceExpressionMenuEntryData>())
                {
                    if (entry == null || !names.Add(entry.Name))
                    {
                        throw new McpToolException("invalid_request", "Menu patches must be non-null and unique.");
                    }
                    var index = menus.FindIndex(existing => string.Equals(existing.Name, entry.Name, StringComparison.Ordinal));
                    if (index < 0) menus.Add(entry);
                    else menus[index] = entry;
                }
                configuration = new FaceExpressionConfigurationData { Assignments = assignments, MenuEntries = menus };
            }
            else if (mode != "replace")
            {
                throw new McpToolException("invalid_request", "mode must be patch or replace.");
            }
            return McpToolResult.Success(McpJson.From(FaceExpressionApi.ApplyConfiguration(
                avatar, configuration, (bool?)arguments["dryRun"] ?? false)));
        }

        private static AnimationClip OptionalClip(JObject arguments)
        {
            return LoadClip((string)arguments["clipPath"], false);
        }

        private static AnimationClip RequiredClip(JObject arguments)
        {
            return LoadClip((string)arguments["clipPath"], true);
        }

        private static AnimationClip LoadClip(string path, bool required)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                if (required)
                {
                    throw new McpToolException(
                        "clip_path_required",
                        "clipPath is required.");
                }

                return null;
            }

            var normalized = path.Trim().Replace('\\', '/');
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(normalized);
            if (clip == null)
            {
                throw new McpToolException(
                    "clip_not_found",
                    "AnimationClip was not found: " + normalized);
            }

            return clip;
        }

        private static FaceExpressionClipWriteMode ParseWriteMode(string value)
        {
            if (Enum.TryParse(value, true, out FaceExpressionClipWriteMode mode))
            {
                return mode;
            }

            throw new McpToolException(
                "invalid_write_mode",
                "mode must be create, patch, or replace.");
        }

        private static JObject GestureSchema()
        {
            return McpSchemas.Enum(
                "Neutral",
                "Fist",
                "Open",
                "Point",
                "Victory",
                "RockNRoll",
                "HandGun",
                "ThumbsUp");
        }
    }
}
