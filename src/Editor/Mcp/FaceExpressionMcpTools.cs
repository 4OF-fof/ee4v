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
            RegisterInspectFace();
            RegisterWriteClip();
            RegisterInspectAnimation();
            RegisterAddAnimationPose();
            RegisterUpdateAnimationPose();
            RegisterMoveAnimationPose();
            RegisterRemoveAnimationPose();
            RegisterSetAnimationLooping();
            RegisterValidateClip();
            RegisterRenderPreview();
            RegisterRemapClip();
            RegisterGetConfiguration();
            RegisterSetGestureExpression();
            RegisterPlanApply();
            RegisterApply();
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

        private static void RegisterInspectAnimation()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_inspect_expression_animation",
                "Inspects an expression clip as an ordered pose sequence, including pose times, transition durations, custom names, source clips, loop state, revision, and optional per-pose BlendShape values.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["clipPath"] = McpSchemas.String(),
                    ["includeChannels"] = McpSchemas.Boolean(
                        "Include every animated BlendShape value at every pose. Defaults to false to keep the response compact.")
                }, "avatarRef", "clipPath"),
                arguments =>
                {
                    var clip = RequiredClip(arguments);
                    return Task.FromResult(McpToolResult.Success(McpJson.From(new
                    {
                        ok = true,
                        animation = FaceExpressionApi.InspectAnimation(
                            clip,
                            (bool?)arguments["includeChannels"] ?? false),
                        findings = FaceExpressionApi.ValidateClip(
                            Avatar(arguments),
                            clip)
                    })));
                },
                readOnly: true));
        }

        private static void RegisterAddAnimationPose()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_add_expression_pose",
                "Duplicates one pose after its current position, then optionally assigns a custom name, a read-only source clip, or explicit BlendShape values. Source clips and explicit channels are mutually exclusive.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["clipPath"] = McpSchemas.String(),
                    ["afterPoseIndex"] = McpSchemas.Integer(),
                    ["transitionDuration"] = McpSchemas.Number(
                        "Seconds from the selected pose to the inserted pose. Must be greater than zero."),
                    ["name"] = McpSchemas.String(),
                    ["sourceClipPath"] = McpSchemas.String(
                        "Optional existing expression clip used as a read-only pose source."),
                    ["channels"] = McpSchemas.Array(PoseChannelSchema()),
                    ["dryRun"] = McpSchemas.Boolean(),
                    ["expectedRevision"] = RevisionSchema()
                },
                    "avatarRef",
                    "clipPath",
                    "afterPoseIndex",
                    "transitionDuration"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.AddAnimationPose(
                        Avatar(arguments),
                        RequiredClip(arguments),
                        (int)arguments["afterPoseIndex"],
                        (float)arguments["transitionDuration"],
                        (string)arguments["name"],
                        (string)arguments["sourceClipPath"],
                        PoseChannels(arguments),
                        (bool?)arguments["dryRun"] ?? false,
                        (string)arguments["expectedRevision"])))),
                readOnly: false,
                destructive: false,
                idempotent: false));
        }

        private static void RegisterUpdateAnimationPose()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_update_expression_pose",
                "Updates one existing pose. It can rename the pose, assign or clear its read-only source clip, set explicit BlendShape values, and change the duration to the following pose. Pass an empty sourceClipPath to make a sourced pose locally editable before setting channels.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["clipPath"] = McpSchemas.String(),
                    ["poseIndex"] = McpSchemas.Integer(),
                    ["name"] = McpSchemas.String(
                        "Optional custom name. Pass an empty string to restore the default Pose N label."),
                    ["sourceClipPath"] = McpSchemas.String(
                        "Optional source update. Pass an empty string to clear the source."),
                    ["channels"] = McpSchemas.Array(PoseChannelSchema()),
                    ["transitionDuration"] = McpSchemas.Number(
                        "Optional seconds to the following pose. Invalid for the last pose."),
                    ["dryRun"] = McpSchemas.Boolean(),
                    ["expectedRevision"] = RevisionSchema()
                }, "avatarRef", "clipPath", "poseIndex"),
                arguments => Task.FromResult(UpdateAnimationPose(arguments)),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static void RegisterMoveAnimationPose()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_move_expression_pose",
                "Swaps one expression pose with an adjacent pose while preserving the timeline positions and moving its custom name and source clip with its facial values.",
                McpSchemas.Object(new JObject
                {
                    ["clipPath"] = McpSchemas.String(),
                    ["poseIndex"] = McpSchemas.Integer(),
                    ["targetIndex"] = McpSchemas.Integer(
                        "Adjacent destination index."),
                    ["dryRun"] = McpSchemas.Boolean(),
                    ["expectedRevision"] = RevisionSchema()
                }, "clipPath", "poseIndex", "targetIndex"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.MoveAnimationPose(
                        RequiredClip(arguments),
                        (int)arguments["poseIndex"],
                        (int)arguments["targetIndex"],
                        (bool?)arguments["dryRun"] ?? false,
                        (string)arguments["expectedRevision"])))),
                readOnly: false,
                destructive: false,
                idempotent: false));
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

        private static void RegisterSetAnimationLooping()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_set_expression_animation_loop",
                "Enables or disables looping on an expression animation clip without changing its poses.",
                McpSchemas.Object(new JObject
                {
                    ["clipPath"] = McpSchemas.String(),
                    ["looping"] = McpSchemas.Boolean(),
                    ["dryRun"] = McpSchemas.Boolean(),
                    ["expectedRevision"] = RevisionSchema()
                }, "clipPath", "looping"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.SetAnimationLooping(
                        RequiredClip(arguments),
                        (bool)arguments["looping"],
                        (bool?)arguments["dryRun"] ?? false,
                        (string)arguments["expectedRevision"])))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static void RegisterValidateClip()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_validate_expression_clip",
                "Validates face-clip bindings, keyframes, values, object curves, and Animation Events against an avatar.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["clipPath"] = McpSchemas.String()
                }, "avatarRef", "clipPath"),
                arguments =>
                {
                    var findings = FaceExpressionApi.ValidateClip(
                        Avatar(arguments),
                        RequiredClip(arguments));
                    return Task.FromResult(McpToolResult.Success(McpJson.From(new
                    {
                        ok = true,
                        errorCount = findings.Count(finding => finding.Severity == "error"),
                        warningCount = findings.Count(finding => finding.Severity == "warning"),
                        findings
                    })));
                },
                readOnly: true));
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

        private static void RegisterSetGestureExpression()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_set_gesture_expression",
                "Assigns one expression clip to one left/right hand gesture combination while preserving every other gesture and menu-only assignment.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["left"] = GestureSchema(),
                    ["right"] = GestureSchema(),
                    ["clipPath"] = McpSchemas.String(),
                    ["enableBlink"] = McpSchemas.Boolean(),
                    ["fixMouth"] = McpSchemas.Boolean(),
                    ["menuName"] = McpSchemas.String(),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "avatarRef", "left", "right", "clipPath"),
                arguments => Task.FromResult(SetGestureExpression(arguments)),
                readOnly: false,
                destructive: false,
                idempotent: true));
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
                "Generates or updates the ee4v-owned FX controller, Expressions Menu, icons, and Modular Avatar FacialSet prefab from a complete configuration. Set dryRun to validate the configuration and prerequisites without writing assets. Run ee4v_plan_facial_set_apply first.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["configuration"] = McpSchemas.Object(new JObject
                    {
                        ["assignments"] = McpSchemas.Array(assignment),
                        ["menuEntries"] = McpSchemas.Array(menuEntry)
                    }),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "avatarRef", "configuration"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.ApplyConfiguration(
                        Avatar(arguments),
                        McpJson.To<FaceExpressionConfigurationData>(
                            arguments["configuration"]),
                        (bool?)arguments["dryRun"] ?? false)))),
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
                "Optional revision returned by ee4v_inspect_expression_animation. The write is rejected if the clip changed.");
        }

        private static GameObject Avatar(JObject arguments)
        {
            return UnityObjectReference.ResolveGameObject(
                (string)arguments["avatarRef"]);
        }

        private static McpToolResult SetGestureExpression(JObject arguments)
        {
            var avatar = Avatar(arguments);
            var left = (string)arguments["left"];
            var right = (string)arguments["right"];
            var clip = LoadClip((string)arguments["clipPath"], true);
            var assignment = new FaceExpressionGestureAssignmentData
            {
                Left = left,
                Right = right,
                ClipPath = AssetDatabase.GetAssetPath(clip),
                EnableBlink = (bool?)arguments["enableBlink"] ?? true,
                FixMouth = (bool?)arguments["fixMouth"] ?? false,
                MenuName = (string)arguments["menuName"] ?? string.Empty
            };
            var current = FaceExpressionApi.ReadConfiguration(avatar);
            var assignments = (current.Assignments ??
                               Array.Empty<FaceExpressionGestureAssignmentData>())
                .ToList();
            var index = assignments.FindIndex(value =>
                string.Equals(value.Left, left, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value.Right, right, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                assignments[index] = assignment;
            }
            else
            {
                assignments.Add(assignment);
            }

            var configuration = new FaceExpressionConfigurationData
            {
                Assignments = assignments,
                MenuEntries = current.MenuEntries ??
                              Array.Empty<FaceExpressionMenuEntryData>()
            };
            var dryRun = (bool?)arguments["dryRun"] ?? false;
            if (dryRun)
            {
                return McpToolResult.Success(McpJson.From(new
                {
                    ok = true,
                    dryRun = true,
                    assignment,
                    plan = FaceExpressionApi.PlanApply(avatar)
                }));
            }

            var apply = FaceExpressionApi.ApplyConfiguration(avatar, configuration);
            return McpToolResult.Success(McpJson.From(new
            {
                ok = apply.Succeeded,
                dryRun = false,
                assignment,
                apply
            }));
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
