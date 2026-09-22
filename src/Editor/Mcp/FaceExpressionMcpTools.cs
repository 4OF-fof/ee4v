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
            var channelSchema = McpSchemas.Object(new JObject
            {
                ["rendererPath"] = McpSchemas.String(),
                ["shapeName"] = McpSchemas.String(),
                ["value"] = McpSchemas.Number(),
                ["animated"] = McpSchemas.Boolean(
                    "Set false to remove the channel from an existing clip.")
            }, "rendererPath", "shapeName", "value");
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_upsert_expression_clip",
                "Creates or atomically updates a single-frame face AnimationClip. Use create to reject overwrites, patch to preserve unspecified channels, or replace to clear unspecified BlendShape channels.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["assetPath"] = McpSchemas.String(
                        "Destination .anim path under Assets."),
                    ["mode"] = McpSchemas.Enum("create", "patch", "replace"),
                    ["channels"] = McpSchemas.Array(channelSchema),
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
                    ["height"] = McpSchemas.Integer()
                }, "avatarRef"),
                arguments =>
                {
                    var bytes = FaceExpressionApi.RenderPreview(
                        Avatar(arguments),
                        OptionalClip(arguments),
                        (int?)arguments["width"] ?? 512,
                        (int?)arguments["height"] ?? 512);
                    return Task.FromResult(McpToolResult.Image(
                        new JObject
                        {
                            ["ok"] = bytes.Length > 0,
                            ["byteLength"] = bytes.Length,
                            ["clipPath"] = (string)arguments["clipPath"] ?? string.Empty
                        },
                        bytes));
                },
                readOnly: true));
        }

        private static void RegisterRemapClip()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_remap_expression_clip",
                "Copies a face clip between avatars by ee4v BlendShape preset role and side. Ambiguous or unresolved roles are reported instead of guessed.",
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
                "Generates or updates the ee4v-owned FX controller, Expressions Menu, icons, and Modular Avatar FacialSet prefab from a complete configuration. Run ee4v_plan_facial_set_apply first.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["configuration"] = McpSchemas.Object(new JObject
                    {
                        ["assignments"] = McpSchemas.Array(assignment),
                        ["menuEntries"] = McpSchemas.Array(menuEntry)
                    })
                }, "avatarRef", "configuration"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    FaceExpressionApi.ApplyConfiguration(
                        Avatar(arguments),
                        McpJson.To<FaceExpressionConfigurationData>(
                            arguments["configuration"]))))),
                readOnly: false,
                destructive: false,
                idempotent: true));
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
