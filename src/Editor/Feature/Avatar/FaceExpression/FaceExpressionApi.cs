using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    public enum FaceExpressionClipWriteMode
    {
        Create,
        Patch,
        Replace
    }

    public sealed class FaceExpressionChannelData
    {
        public string RendererPath { get; set; }
        public string Name { get; set; }
        public float Value { get; set; }
        public float InitialValue { get; set; }
        public bool Animated { get; set; }
        public bool IsHeader { get; set; }
        public string HeaderText { get; set; }
        public string Role { get; set; }
        public string Side { get; set; }
        public bool MouthMorph { get; set; }
        public string SourceAssetGuid { get; set; }
        public long SourceMeshLocalId { get; set; }
    }

    public sealed class FaceExpressionClipChange
    {
        public string RendererPath { get; set; }
        public string ShapeName { get; set; }
        public float Value { get; set; }
        public bool Animated { get; set; } = true;
    }

    public sealed class FaceExpressionClipWriteResult
    {
        public bool Changed { get; set; }
        public bool Created { get; set; }
        public bool DryRun { get; set; }
        public string AssetPath { get; set; }
        public string AssetGuid { get; set; }
        public string Revision { get; set; }
        public IReadOnlyList<string> UpdatedChannels { get; set; }
        public IReadOnlyList<string> RemovedChannels { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }
    }

    public sealed class FaceExpressionValidationFinding
    {
        public string Severity { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string RendererPath { get; set; }
        public string ShapeName { get; set; }
    }

    public sealed class FaceExpressionGestureAssignmentData
    {
        public string Left { get; set; }
        public string Right { get; set; }
        public string ClipPath { get; set; }
        public bool EnableBlink { get; set; } = true;
        public bool FixMouth { get; set; }
        public string MenuName { get; set; }
    }

    public sealed class FaceExpressionMenuEntryData
    {
        public string Name { get; set; }
        public string ClipPath { get; set; }
        public bool EnableBlink { get; set; } = true;
        public bool FixMouth { get; set; }
    }

    public sealed class FaceExpressionConfigurationData
    {
        public IReadOnlyList<FaceExpressionGestureAssignmentData> Assignments { get; set; }
        public IReadOnlyList<FaceExpressionMenuEntryData> MenuEntries { get; set; }
    }

    public sealed class FaceExpressionApplyPlan
    {
        public bool CanApply { get; set; }
        public bool HasAvatarDescriptor { get; set; }
        public bool HasModularAvatar { get; set; }
        public string RootName { get; set; }
        public string PrefabPath { get; set; }
        public string ControllerPath { get; set; }
        public string MenuPath { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }
    }

    public sealed class FaceExpressionApplyResult
    {
        public bool Succeeded { get; set; }
        public string ErrorCode { get; set; }
        public string ControllerPath { get; set; }
        public FaceExpressionApplyPlan Plan { get; set; }
    }

    public sealed class FaceExpressionRemapResult
    {
        public FaceExpressionClipWriteResult Write { get; set; }
        public IReadOnlyList<string> ResolvedRoles { get; set; }
        public IReadOnlyList<string> UnresolvedRoles { get; set; }
        public IReadOnlyList<string> AmbiguousRoles { get; set; }
    }

    public static class FaceExpressionApi
    {
        public static IReadOnlyList<FaceExpressionChannelData> Inspect(
            GameObject avatar,
            AnimationClip clip = null,
            IReadOnlyList<string> rendererPaths = null)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            rendererPaths = rendererPaths ?? FaceExpressionClipEditor.GetRendererPaths(avatar);
            var channels = FaceExpressionClipEditor.Read(
                avatar,
                clip,
                FaceExpressionSettings.GetSeparators(),
                rendererPaths);
            var rule = FaceExpressionSettings.GetNameRule(
                BlendShapePresetStorage.Shared);
            return channels.Select(channel => ToData(channel, rule)).ToArray();
        }

        public static IReadOnlyList<string> ListClips(string folder = null)
        {
            folder = string.IsNullOrWhiteSpace(folder)
                ? ProjectAssetSettings.GetAssetFolder("Animation/Facial")
                : NormalizeAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return Array.Empty<string>();
            }

            return AssetDatabase.FindAssets("t:AnimationClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static FaceExpressionClipWriteResult WriteClip(
            GameObject avatar,
            string assetPath,
            FaceExpressionClipWriteMode mode,
            IReadOnlyList<FaceExpressionClipChange> changes,
            bool dryRun = false,
            string expectedRevision = null)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            assetPath = ValidateClipPath(assetPath);
            changes = changes ?? Array.Empty<FaceExpressionClipChange>();
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var occupied = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (occupied != null && existing == null)
            {
                throw new InvalidOperationException(
                    "The destination is occupied by a non-AnimationClip asset.");
            }

            if (mode == FaceExpressionClipWriteMode.Create && existing != null)
            {
                throw new InvalidOperationException(
                    "The destination AnimationClip already exists.");
            }

            if (mode != FaceExpressionClipWriteMode.Create && existing == null)
            {
                throw new InvalidOperationException(
                    "The AnimationClip to update was not found.");
            }

            var revision = existing == null ? string.Empty : Revision(assetPath);
            if (!string.IsNullOrWhiteSpace(expectedRevision) &&
                !string.Equals(expectedRevision, revision, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The AnimationClip changed after it was inspected.");
            }

            var available = FaceExpressionClipEditor.Read(
                    avatar,
                    existing,
                    FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(avatar))
                .Where(channel => !channel.IsHeader)
                .ToDictionary(
                    channel => ChannelKey(channel.RendererPath, channel.Name),
                    channel => channel,
                    StringComparer.Ordinal);
            var requested = new Dictionary<string, FaceExpressionClipChange>(
                StringComparer.Ordinal);
            var warnings = new List<string>();
            foreach (var change in changes)
            {
                if (change == null || string.IsNullOrWhiteSpace(change.ShapeName))
                {
                    throw new ArgumentException(
                        "Each channel change requires shapeName.",
                        nameof(changes));
                }

                var key = ChannelKey(change.RendererPath, change.ShapeName);
                if (!available.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "BlendShape channel was not found on the avatar: " + key);
                }

                if (requested.ContainsKey(key))
                {
                    warnings.Add("The last duplicate channel value was used: " + key);
                }

                requested[key] = change;
            }

            var removed = new List<string>();
            if (mode == FaceExpressionClipWriteMode.Replace && existing != null)
            {
                removed.AddRange(AnimationUtility.GetCurveBindings(existing)
                    .Where(IsBlendShapeBinding)
                    .Select(binding => ChannelKey(
                        binding.path,
                        binding.propertyName.Substring("blendShape.".Length))));
            }

            if (dryRun)
            {
                return new FaceExpressionClipWriteResult
                {
                    Changed = requested.Count > 0 || removed.Count > 0,
                    Created = existing == null,
                    DryRun = true,
                    AssetPath = assetPath,
                    AssetGuid = existing == null
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(assetPath),
                    Revision = revision,
                    UpdatedChannels = requested.Keys.ToArray(),
                    RemovedChannels = removed.Except(requested.Keys).ToArray(),
                    Warnings = warnings
                };
            }

            EnsureParentFolder(assetPath);
            var clip = existing ?? FaceExpressionClipEditor.Create(assetPath);
            if (mode == FaceExpressionClipWriteMode.Replace)
            {
                Undo.RecordObject(clip, "Replace Face Expression");
                foreach (var binding in AnimationUtility.GetCurveBindings(clip)
                             .Where(IsBlendShapeBinding))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }

            foreach (var pair in requested)
            {
                var channel = available[pair.Key];
                channel.Value = pair.Value.Value;
                channel.Animated = pair.Value.Animated;
                FaceExpressionClipEditor.Write(clip, channel);
            }

            clip.frameRate = 60f;
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            return new FaceExpressionClipWriteResult
            {
                Changed = requested.Count > 0 || removed.Count > 0 || existing == null,
                Created = existing == null,
                DryRun = false,
                AssetPath = assetPath,
                AssetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                Revision = Revision(assetPath),
                UpdatedChannels = requested.Keys.ToArray(),
                RemovedChannels = removed.Except(requested.Keys).ToArray(),
                Warnings = warnings
            };
        }

        public static IReadOnlyList<FaceExpressionValidationFinding> ValidateClip(
            GameObject avatar,
            AnimationClip clip)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            var findings = new List<FaceExpressionValidationFinding>();
            var available = new HashSet<string>(FaceExpressionClipEditor.Read(
                    avatar,
                    null,
                    FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(avatar))
                .Where(channel => !channel.IsHeader)
                .Select(channel => ChannelKey(channel.RendererPath, channel.Name)),
                StringComparer.Ordinal);
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!IsBlendShapeBinding(binding))
                {
                    findings.Add(Finding(
                        "warning",
                        "non_blendshape_curve",
                        "The clip contains a curve outside the ee4v face-expression scope.",
                        binding.path,
                        binding.propertyName));
                    continue;
                }

                var shapeName = binding.propertyName.Substring("blendShape.".Length);
                if (!available.Contains(ChannelKey(binding.path, shapeName)))
                {
                    findings.Add(Finding(
                        "error",
                        "binding_not_found",
                        "The target Renderer or BlendShape does not exist on this avatar.",
                        binding.path,
                        shapeName));
                }

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length != 1 ||
                    !Mathf.Approximately(curve.keys[0].time, 0f))
                {
                    findings.Add(Finding(
                        "warning",
                        "not_single_frame",
                        "Face expression curves should contain one key at time 0.",
                        binding.path,
                        shapeName));
                }

                if (curve != null && curve.keys.Any(key => key.value < 0f || key.value > 100f))
                {
                    findings.Add(Finding(
                        "warning",
                        "value_out_of_range",
                        "BlendShape values outside 0-100 are clamped by the ee4v editor.",
                        binding.path,
                        shapeName));
                }
            }

            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0)
            {
                findings.Add(Finding(
                    "warning",
                    "object_reference_curves",
                    "The clip contains object-reference curves.",
                    string.Empty,
                    string.Empty));
            }

            if (AnimationUtility.GetAnimationEvents(clip).Length > 0)
            {
                findings.Add(Finding(
                    "warning",
                    "animation_events",
                    "The clip contains Animation Events.",
                    string.Empty,
                    string.Empty));
            }

            return findings;
        }

        public static byte[] RenderPreview(
            GameObject avatar,
            AnimationClip clip,
            int width = 512,
            int height = 512)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            width = Mathf.Clamp(width, 64, 1024);
            height = Mathf.Clamp(height, 64, 1024);
            using (var preview = new FaceExpressionPreview(null))
            {
                preview.SetAvatar(avatar);
                var channels = FaceExpressionClipEditor.Read(
                    avatar,
                    clip,
                    FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(avatar));
                var texture = preview.RenderThumbnail(channels, width, height);
                if (texture == null)
                {
                    return Array.Empty<byte>();
                }

                try
                {
                    return texture.EncodeToPNG();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        public static FaceExpressionRemapResult RemapClip(
            GameObject sourceAvatar,
            AnimationClip sourceClip,
            GameObject targetAvatar,
            string targetPath,
            bool dryRun = false)
        {
            if (sourceAvatar == null || targetAvatar == null || sourceClip == null)
            {
                throw new ArgumentNullException(
                    sourceAvatar == null
                        ? nameof(sourceAvatar)
                        : targetAvatar == null
                            ? nameof(targetAvatar)
                            : nameof(sourceClip));
            }

            var rule = FaceExpressionSettings.GetNameRule(
                BlendShapePresetStorage.Shared);
            var source = FaceExpressionClipEditor.Read(
                    sourceAvatar,
                    sourceClip,
                    FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(sourceAvatar))
                .Where(channel => channel.Animated && !channel.IsHeader)
                .Select(channel => new
                {
                    Channel = channel,
                    Name = rule.TryParse(channel, out var parsed) ? parsed : null
                })
                .ToArray();
            var target = FaceExpressionClipEditor.Read(
                    targetAvatar,
                    null,
                    FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(targetAvatar))
                .Where(channel => !channel.IsHeader)
                .Select(channel => new
                {
                    Channel = channel,
                    Name = rule.TryParse(channel, out var parsed) ? parsed : null
                })
                .Where(item => item.Name != null)
                .GroupBy(item => RoleKey(item.Name.Role, item.Name.Side))
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var changes = new List<FaceExpressionClipChange>();
            var resolved = new List<string>();
            var unresolved = new List<string>();
            var ambiguous = new List<string>();
            foreach (var item in source)
            {
                if (item.Name == null)
                {
                    unresolved.Add(item.Channel.DisplayName);
                    continue;
                }

                var key = RoleKey(item.Name.Role, item.Name.Side);
                if (!target.TryGetValue(key, out var matches))
                {
                    unresolved.Add(key);
                    continue;
                }

                if (matches.Length != 1)
                {
                    ambiguous.Add(key);
                    continue;
                }

                changes.Add(new FaceExpressionClipChange
                {
                    RendererPath = matches[0].Channel.RendererPath,
                    ShapeName = matches[0].Channel.Name,
                    Value = item.Channel.Value,
                    Animated = true
                });
                resolved.Add(key);
            }

            return new FaceExpressionRemapResult
            {
                Write = WriteClip(
                    targetAvatar,
                    targetPath,
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath) == null
                        ? FaceExpressionClipWriteMode.Create
                        : FaceExpressionClipWriteMode.Replace,
                    changes,
                    dryRun),
                ResolvedRoles = resolved.Distinct().OrderBy(value => value).ToArray(),
                UnresolvedRoles = unresolved.Distinct().OrderBy(value => value).ToArray(),
                AmbiguousRoles = ambiguous.Distinct().OrderBy(value => value).ToArray()
            };
        }

        public static FaceExpressionConfigurationData ReadConfiguration(
            GameObject avatar)
        {
            var gateway = new VrchatFaceExpressionGateway();
            if (!gateway.TryRead(avatar, out var configuration))
            {
                throw new InvalidOperationException(
                    "VRC Avatar Descriptor was not found.");
            }

            return new FaceExpressionConfigurationData
            {
                Assignments = configuration.Assignments
                    .OrderBy(pair => (int)pair.Key.Left)
                    .ThenBy(pair => (int)pair.Key.Right)
                    .Select(pair => new FaceExpressionGestureAssignmentData
                    {
                        Left = pair.Key.Left.ToString(),
                        Right = pair.Key.Right.ToString(),
                        ClipPath = AssetDatabase.GetAssetPath(pair.Value.Clip),
                        EnableBlink = pair.Value.EnableBlink,
                        FixMouth = pair.Value.FixMouth,
                        MenuName = pair.Value.MenuName
                    })
                    .ToArray(),
                MenuEntries = configuration.MenuEntries
                    .Select(entry => new FaceExpressionMenuEntryData
                    {
                        Name = entry.Name,
                        ClipPath = AssetDatabase.GetAssetPath(entry.Assignment.Clip),
                        EnableBlink = entry.Assignment.EnableBlink,
                        FixMouth = entry.Assignment.FixMouth
                    })
                    .ToArray()
            };
        }

        public static FaceExpressionApplyPlan PlanApply(GameObject avatar)
        {
            var gateway = new VrchatFaceExpressionGateway();
            var hasDescriptor = gateway.TryRead(avatar, out _);
            var paths = FaceExpressionGenerationPaths.Create(avatar);
            var hasModularAvatar = ModularAvatarFaceExpressionInstaller.IsAvailable &&
                                   VrchatExpressionMenuWriter.IsAvailable;
            var warnings = new List<string>();
            if (!hasDescriptor)
            {
                warnings.Add("VRC Avatar Descriptor was not found.");
            }

            if (!hasModularAvatar)
            {
                warnings.Add("Modular Avatar or the VRChat Expressions Menu type was not found.");
            }

            return new FaceExpressionApplyPlan
            {
                CanApply = hasDescriptor && hasModularAvatar,
                HasAvatarDescriptor = hasDescriptor,
                HasModularAvatar = hasModularAvatar,
                RootName = paths.RootName,
                PrefabPath = paths.PrefabPath,
                ControllerPath = paths.ControllerPath,
                MenuPath = paths.MenuPath,
                Warnings = warnings
            };
        }

        public static FaceExpressionApplyResult ApplyConfiguration(
            GameObject avatar,
            FaceExpressionConfigurationData data)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            data = data ?? new FaceExpressionConfigurationData();
            var assignments = new Dictionary<GestureCombination, FaceExpressionAssignment>();
            foreach (var item in data.Assignments ??
                         Array.Empty<FaceExpressionGestureAssignmentData>())
            {
                if (!Enum.TryParse(item.Left, true, out FaceGesture left) ||
                    !Enum.TryParse(item.Right, true, out FaceGesture right))
                {
                    throw new ArgumentException(
                        "Gesture names must be Neutral, Fist, Open, Point, Victory, RockNRoll, HandGun, or ThumbsUp.");
                }

                assignments[new GestureCombination(left, right)] =
                    new FaceExpressionAssignment(
                        LoadClip(item.ClipPath),
                        item.EnableBlink,
                        item.FixMouth,
                        item.MenuName);
            }

            var entries = (data.MenuEntries ?? Array.Empty<FaceExpressionMenuEntryData>())
                .Select(item => new FaceExpressionMenuEntry(
                    item.Name,
                    new FaceExpressionAssignment(
                        LoadClip(item.ClipPath),
                        item.EnableBlink,
                        item.FixMouth,
                        item.Name)))
                .ToArray();
            var configuration = new FaceExpressionConfiguration(assignments, entries);
            var plan = PlanApply(avatar);
            var gateway = new VrchatFaceExpressionGateway();
            var succeeded = gateway.TryApply(
                avatar,
                configuration,
                out var controller,
                out var error);
            return new FaceExpressionApplyResult
            {
                Succeeded = succeeded,
                ErrorCode = error ?? string.Empty,
                ControllerPath = AssetDatabase.GetAssetPath(controller),
                Plan = plan
            };
        }

        public static string Revision(AnimationClip clip)
        {
            return clip == null ? string.Empty : Revision(AssetDatabase.GetAssetPath(clip));
        }

        private static FaceExpressionChannelData ToData(
            BlendShapeChannel channel,
            BlendShapeNamingRule rule)
        {
            var parsed = rule.TryParse(channel, out var name) ? name : null;
            return new FaceExpressionChannelData
            {
                RendererPath = channel.RendererPath,
                Name = channel.Name,
                Value = channel.Value,
                InitialValue = channel.InitialValue,
                Animated = channel.Animated,
                IsHeader = channel.IsHeader,
                HeaderText = channel.HeaderText ?? string.Empty,
                Role = parsed?.Role ?? string.Empty,
                Side = parsed?.Side ?? string.Empty,
                MouthMorph = rule.IsMouthMorph(
                    channel.SourceAssetGuid,
                    channel.SourceMeshLocalId,
                    channel.Name),
                SourceAssetGuid = channel.SourceAssetGuid,
                SourceMeshLocalId = channel.SourceMeshLocalId
            };
        }

        private static AnimationClip LoadClip(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                NormalizeAssetPath(path));
            if (clip == null)
            {
                throw new InvalidOperationException(
                    "AnimationClip was not found: " + path);
            }

            return clip;
        }

        private static FaceExpressionValidationFinding Finding(
            string severity,
            string code,
            string message,
            string rendererPath,
            string shapeName)
        {
            return new FaceExpressionValidationFinding
            {
                Severity = severity,
                Code = code,
                Message = message,
                RendererPath = rendererPath,
                ShapeName = shapeName
            };
        }

        private static string ValidateClipPath(string value)
        {
            var path = NormalizeAssetPath(value);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(path), ".anim", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException(
                    "AnimationClip path must be an .anim path under Assets.",
                    nameof(value));
            }

            return path;
        }

        private static string NormalizeAssetPath(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\\', '/');
        }

        private static void EnsureParentFolder(string assetPath)
        {
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent) || !parent.StartsWith("Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("AnimationClip parent folder is invalid.");
            }

            var current = "Assets";
            foreach (var segment in parent.Substring("Assets".Length)
                         .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, segment);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new InvalidOperationException(
                            "Could not create asset folder: " + next);
                    }
                }

                current = next;
            }
        }

        private static bool IsBlendShapeBinding(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer) &&
                   binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal);
        }

        private static string ChannelKey(string rendererPath, string shapeName)
        {
            return (rendererPath ?? string.Empty) + "::" + (shapeName ?? string.Empty);
        }

        private static string RoleKey(string role, string side)
        {
            return (role ?? string.Empty).Trim() + "::" + (side ?? string.Empty).Trim();
        }

        private static string Revision(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
        }
    }
}
