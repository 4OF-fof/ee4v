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

    public sealed class FaceExpressionAnimationPoseChannelData
    {
        public string RendererPath { get; set; }
        public string ShapeName { get; set; }
        public float Value { get; set; }
    }

    public sealed class FaceExpressionAnimationPoseData
    {
        public int Index { get; set; }
        public float Time { get; set; }
        public float TransitionDuration { get; set; }
        public string Name { get; set; }
        public string SourceClipPath { get; set; }
        public IReadOnlyList<FaceExpressionAnimationPoseChannelData> Channels
        {
            get;
            set;
        }
    }

    public sealed class FaceExpressionAnimationData
    {
        public string AssetPath { get; set; }
        public string Revision { get; set; }
        public float FrameRate { get; set; }
        public float Duration { get; set; }
        public bool Looping { get; set; }
        public int PoseCount { get; set; }
        public IReadOnlyList<FaceExpressionAnimationPoseData> Poses { get; set; }
    }

    public sealed class FaceExpressionAnimationEditResult
    {
        public bool Changed { get; set; }
        public bool DryRun { get; set; }
        public string AssetPath { get; set; }
        public string Revision { get; set; }
        public int PoseCount { get; set; }
        public int PoseIndex { get; set; }
        public float PoseTime { get; set; }
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
        public bool DryRun { get; set; }
        public string ErrorCode { get; set; }
        public string ControllerPath { get; set; }
        public FaceExpressionApplyPlan Plan { get; set; }
    }

    public sealed class FaceExpressionRemapResult
    {
        public FaceExpressionClipWriteResult Write { get; set; }
        public IReadOnlyList<string> ResolvedChannels { get; set; }
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

        public static FaceExpressionAnimationData InspectAnimation(
            AnimationClip clip,
            bool includeChannels = false)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            var poseTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            var poseSources = FaceExpressionClipEditor.GetPoseSources(
                clip,
                poseTimes);
            var poseNames = FaceExpressionClipEditor.GetPoseNames(
                clip,
                poseTimes);
            var bindings = includeChannels
                ? AnimationUtility.GetCurveBindings(clip)
                    .Where(IsBlendShapeBinding)
                    .OrderBy(binding => binding.path, StringComparer.Ordinal)
                    .ThenBy(binding => binding.propertyName, StringComparer.Ordinal)
                    .Select(binding => new
                    {
                        Binding = binding,
                        Curve = AnimationUtility.GetEditorCurve(clip, binding)
                    })
                    .Where(item => item.Curve != null)
                    .ToArray()
                : null;
            var poses = new FaceExpressionAnimationPoseData[poseTimes.Count];
            for (var poseIndex = 0; poseIndex < poseTimes.Count; poseIndex++)
            {
                var poseTime = poseTimes[poseIndex];
                poses[poseIndex] = new FaceExpressionAnimationPoseData
                {
                    Index = poseIndex,
                    Time = poseTime,
                    TransitionDuration = poseIndex < poseTimes.Count - 1
                        ? poseTimes[poseIndex + 1] - poseTime
                        : 0f,
                    Name = poseIndex < poseNames.Count
                        ? poseNames[poseIndex] ?? string.Empty
                        : string.Empty,
                    SourceClipPath = poseIndex < poseSources.Count
                        ? AssetDatabase.GetAssetPath(poseSources[poseIndex])
                        : string.Empty,
                    Channels = bindings == null
                        ? Array.Empty<FaceExpressionAnimationPoseChannelData>()
                        : bindings.Select(item =>
                                new FaceExpressionAnimationPoseChannelData
                                {
                                    RendererPath = item.Binding.path,
                                    ShapeName = item.Binding.propertyName.Substring(
                                        "blendShape.".Length),
                                    Value = item.Curve.Evaluate(poseTime)
                                })
                            .ToArray()
                };
            }

            return new FaceExpressionAnimationData
            {
                AssetPath = AssetDatabase.GetAssetPath(clip),
                Revision = Revision(clip),
                FrameRate = clip.frameRate,
                Duration = poseTimes.Count == 0
                    ? 0f
                    : poseTimes[poseTimes.Count - 1],
                Looping = FaceExpressionClipEditor.IsLooping(clip),
                PoseCount = poseTimes.Count,
                Poses = poses
            };
        }

        public static FaceExpressionAnimationEditResult AddAnimationPose(
            GameObject avatar,
            AnimationClip clip,
            int afterPoseIndex,
            float transitionDuration,
            string name = null,
            string sourceClipPath = null,
            IReadOnlyList<FaceExpressionClipChange> channels = null,
            bool dryRun = false,
            string expectedRevision = null)
        {
            ValidateAnimationEditTarget(avatar, clip, expectedRevision);
            ValidateTransitionDuration(transitionDuration);
            var poseTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            if (afterPoseIndex < 0 || afterPoseIndex >= poseTimes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(afterPoseIndex));
            }

            if (!FaceExpressionClipEditor.CanAddPose(clip))
            {
                throw new InvalidOperationException(
                    "The clip needs at least one BlendShape curve before a pose can be added.");
            }

            var source = LoadClip(sourceClipPath);
            ValidatePoseInputs(
                avatar,
                clip,
                poseTimes[afterPoseIndex],
                source,
                channels);
            var duration = NormalizeTransitionDuration(clip, transitionDuration);
            var newTime = FaceExpressionClipEditor.SnapTime(
                clip,
                poseTimes[afterPoseIndex] + duration);
            if (dryRun)
            {
                return AnimationEditResult(
                    clip,
                    true,
                    true,
                    poseTimes.Count + 1,
                    afterPoseIndex + 1,
                    newTime);
            }

            newTime = FaceExpressionClipEditor.AddPose(
                clip,
                poseTimes[afterPoseIndex],
                duration);
            if (newTime < 0f)
            {
                throw new InvalidOperationException("The pose could not be added.");
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                FaceExpressionClipEditor.SetPoseName(clip, newTime, name);
            }

            if (source != null && !FaceExpressionClipEditor.SetPoseSource(
                    clip,
                    newTime,
                    source,
                    ReadEditableChannels(avatar, clip, newTime)))
            {
                throw new InvalidOperationException(
                    "The source clip has no matching BlendShape curves.");
            }

            ApplyPoseChannels(avatar, clip, newTime, channels);
            AssetDatabase.SaveAssets();
            var updatedTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            return AnimationEditResult(
                clip,
                true,
                false,
                updatedTimes.Count,
                FindPoseIndex(updatedTimes, newTime),
                newTime);
        }

        public static FaceExpressionAnimationEditResult UpdateAnimationPose(
            GameObject avatar,
            AnimationClip clip,
            int poseIndex,
            bool updateName,
            string name,
            bool updateSource,
            string sourceClipPath,
            IReadOnlyList<FaceExpressionClipChange> channels = null,
            float? transitionDuration = null,
            bool dryRun = false,
            string expectedRevision = null)
        {
            ValidateAnimationEditTarget(avatar, clip, expectedRevision);
            var poseTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            if (poseIndex < 0 || poseIndex >= poseTimes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(poseIndex));
            }

            if (transitionDuration.HasValue)
            {
                ValidateTransitionDuration(transitionDuration.Value);
                if (poseIndex >= poseTimes.Count - 1)
                {
                    throw new InvalidOperationException(
                        "The last pose has no following transition.");
                }
            }

            var poseTime = poseTimes[poseIndex];
            var poseSources = FaceExpressionClipEditor.GetPoseSources(
                clip,
                poseTimes);
            var currentSource = poseSources[poseIndex];
            var source = updateSource ? LoadClip(sourceClipPath) : currentSource;
            ValidatePoseInputs(
                avatar,
                clip,
                poseTime,
                updateSource || (channels != null && channels.Count > 0)
                    ? source
                    : null,
                channels);

            var changed = false;
            if (updateName)
            {
                var names = FaceExpressionClipEditor.GetPoseNames(clip, poseTimes);
                changed |= !string.Equals(
                    names[poseIndex] ?? string.Empty,
                    (name ?? string.Empty).Trim(),
                    StringComparison.Ordinal);
            }

            if (updateSource)
            {
                changed |= currentSource != source;
            }

            changed |= PoseChannelsNeedWrite(avatar, clip, poseTime, channels);
            if (transitionDuration.HasValue)
            {
                changed |= !Mathf.Approximately(
                    poseTimes[poseIndex + 1] - poseTime,
                    NormalizeTransitionDuration(
                        clip,
                        transitionDuration.Value));
            }

            if (dryRun || !changed)
            {
                return AnimationEditResult(
                    clip,
                    changed,
                    dryRun,
                    poseTimes.Count,
                    poseIndex,
                    poseTime);
            }

            if (updateSource && !FaceExpressionClipEditor.SetPoseSource(
                    clip,
                    poseTime,
                    source,
                    ReadEditableChannels(avatar, clip, poseTime)))
            {
                throw new InvalidOperationException(
                    source == null
                        ? "The pose source could not be cleared."
                        : "The source clip has no matching BlendShape curves.");
            }

            ApplyPoseChannels(avatar, clip, poseTime, channels);
            if (updateName)
            {
                FaceExpressionClipEditor.SetPoseName(clip, poseTime, name);
            }

            if (transitionDuration.HasValue)
            {
                FaceExpressionClipEditor.SetTransitionDuration(
                    clip,
                    poseTime,
                    transitionDuration.Value);
            }

            AssetDatabase.SaveAssets();
            var updatedTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            return AnimationEditResult(
                clip,
                true,
                false,
                updatedTimes.Count,
                poseIndex,
                updatedTimes[poseIndex]);
        }

        public static FaceExpressionAnimationEditResult MoveAnimationPose(
            AnimationClip clip,
            int poseIndex,
            int targetIndex,
            bool dryRun = false,
            string expectedRevision = null)
        {
            ValidateAnimationClip(clip, expectedRevision);
            var poseTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            if (poseIndex < 0 ||
                targetIndex < 0 ||
                poseIndex >= poseTimes.Count ||
                targetIndex >= poseTimes.Count ||
                Mathf.Abs(targetIndex - poseIndex) != 1)
            {
                throw new ArgumentException(
                    "poseIndex and targetIndex must identify adjacent poses.");
            }

            if (!dryRun && !FaceExpressionClipEditor.MovePose(
                    clip,
                    poseIndex,
                    targetIndex))
            {
                throw new InvalidOperationException("The pose could not be moved.");
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
            }

            return AnimationEditResult(
                clip,
                true,
                dryRun,
                poseTimes.Count,
                targetIndex,
                poseTimes[targetIndex]);
        }

        public static FaceExpressionAnimationEditResult RemoveAnimationPose(
            AnimationClip clip,
            int poseIndex,
            bool dryRun = false,
            string expectedRevision = null)
        {
            ValidateAnimationClip(clip, expectedRevision);
            var poseTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            if (poseIndex < 0 || poseIndex >= poseTimes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(poseIndex));
            }

            if (poseTimes.Count <= 1)
            {
                throw new InvalidOperationException(
                    "An expression clip must keep at least one pose.");
            }

            var selectedIndex = Mathf.Max(0, poseIndex - 1);
            if (!dryRun && !FaceExpressionClipEditor.RemovePose(
                    clip,
                    poseTimes[poseIndex]))
            {
                throw new InvalidOperationException("The pose could not be removed.");
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
            }

            IReadOnlyList<float> updatedTimes;
            if (dryRun)
            {
                var removedTime = poseTimes[poseIndex];
                var shift = poseIndex == 0
                    ? -poseTimes[1]
                    : -(removedTime - poseTimes[poseIndex - 1]);
                updatedTimes = poseTimes
                    .Where((_, index) => index != poseIndex)
                    .Select(time => time > removedTime
                        ? FaceExpressionClipEditor.SnapTime(clip, time + shift)
                        : time)
                    .ToArray();
            }
            else
            {
                updatedTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            }
            return AnimationEditResult(
                clip,
                true,
                dryRun,
                updatedTimes.Count,
                selectedIndex,
                updatedTimes[selectedIndex]);
        }

        public static FaceExpressionAnimationEditResult SetAnimationLooping(
            AnimationClip clip,
            bool looping,
            bool dryRun = false,
            string expectedRevision = null)
        {
            ValidateAnimationClip(clip, expectedRevision);
            var poseTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            var changed = FaceExpressionClipEditor.IsLooping(clip) != looping;
            if (!dryRun && changed)
            {
                FaceExpressionClipEditor.SetLooping(clip, looping);
                AssetDatabase.SaveAssets();
            }

            return AnimationEditResult(
                clip,
                changed,
                dryRun,
                poseTimes.Count,
                0,
                poseTimes.Count == 0 ? 0f : poseTimes[0]);
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

            if (existing != null &&
                FaceExpressionClipEditor.GetPoseTimes(existing).Count > 1)
            {
                throw new InvalidOperationException(
                    "Animated expression clips must be edited with the animation pose API.");
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

            var existingBindings = existing == null
                ? Array.Empty<EditorCurveBinding>()
                : AnimationUtility.GetCurveBindings(existing)
                    .Where(IsBlendShapeBinding)
                    .ToArray();
            var removedBindings = Array.Empty<EditorCurveBinding>();
            if (mode == FaceExpressionClipWriteMode.Replace && existing != null)
            {
                removedBindings = existingBindings
                    .Where(binding => !requested.ContainsKey(ChannelKey(
                        binding.path,
                        binding.propertyName.Substring("blendShape.".Length))))
                    .ToArray();
            }

            var updated = requested
                .Where(pair => ChannelNeedsWrite(
                    existing,
                    available[pair.Key],
                    pair.Value))
                .ToArray();
            var removed = removedBindings
                .Select(binding => ChannelKey(
                    binding.path,
                    binding.propertyName.Substring("blendShape.".Length)))
                .ToArray();
            var frameRateChanged = existing != null &&
                                   !Mathf.Approximately(existing.frameRate, 60f);
            var changed = existing == null ||
                          updated.Length > 0 ||
                          removedBindings.Length > 0 ||
                          frameRateChanged;
            if (dryRun)
            {
                return new FaceExpressionClipWriteResult
                {
                    Changed = changed,
                    Created = existing == null,
                    DryRun = true,
                    AssetPath = assetPath,
                    AssetGuid = existing == null
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(assetPath),
                    Revision = revision,
                    UpdatedChannels = updated.Select(pair => pair.Key).ToArray(),
                    RemovedChannels = removed,
                    Warnings = warnings
                };
            }

            if (!changed)
            {
                return new FaceExpressionClipWriteResult
                {
                    Changed = false,
                    Created = false,
                    DryRun = false,
                    AssetPath = assetPath,
                    AssetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                    Revision = revision,
                    UpdatedChannels = Array.Empty<string>(),
                    RemovedChannels = Array.Empty<string>(),
                    Warnings = warnings
                };
            }

            EnsureParentFolder(assetPath);
            var clip = existing ?? FaceExpressionClipEditor.Create(assetPath);
            if (removedBindings.Length > 0 || frameRateChanged)
            {
                Undo.RecordObject(clip, "Replace Face Expression");
                foreach (var binding in removedBindings)
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }

            foreach (var pair in updated)
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
                Changed = true,
                Created = existing == null,
                DryRun = false,
                AssetPath = assetPath,
                AssetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                Revision = Revision(assetPath),
                UpdatedChannels = updated.Select(pair => pair.Key).ToArray(),
                RemovedChannels = removed,
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
            int height = 512,
            float sampleTime = 0f)
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
                    FaceExpressionClipEditor.GetRendererPaths(avatar),
                    sampleTime);
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
            var targetChannels = FaceExpressionClipEditor.Read(
                    targetAvatar,
                    null,
                    FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(targetAvatar))
                .Where(channel => !channel.IsHeader)
                .ToArray();
            var exactTargets = targetChannels.ToDictionary(
                channel => ChannelKey(channel.RendererPath, channel.Name),
                channel => channel,
                StringComparer.Ordinal);
            var target = targetChannels
                .Select(channel => new
                {
                    Channel = channel,
                    Name = rule.TryParse(channel, out var parsed) ? parsed : null
                })
                .Where(item => item.Name != null)
                .GroupBy(item => RoleKey(item.Name.Role, item.Name.Side))
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var changes = new List<FaceExpressionClipChange>();
            var resolvedChannels = new List<string>();
            var resolved = new List<string>();
            var unresolved = new List<string>();
            var ambiguous = new List<string>();
            foreach (var item in source)
            {
                var channelKey = ChannelKey(
                    item.Channel.RendererPath,
                    item.Channel.Name);
                if (exactTargets.TryGetValue(channelKey, out var exactTarget))
                {
                    changes.Add(new FaceExpressionClipChange
                    {
                        RendererPath = exactTarget.RendererPath,
                        ShapeName = exactTarget.Name,
                        Value = item.Channel.Value,
                        Animated = true
                    });
                    resolvedChannels.Add(channelKey);
                    continue;
                }

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
                ResolvedChannels = resolvedChannels.Distinct()
                    .OrderBy(value => value)
                    .ToArray(),
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
            FaceExpressionConfigurationData data,
            bool dryRun = false)
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
            if (dryRun)
            {
                return new FaceExpressionApplyResult
                {
                    Succeeded = plan.CanApply,
                    DryRun = true,
                    ErrorCode = !plan.HasAvatarDescriptor
                        ? "descriptorMissing"
                        : !plan.HasModularAvatar
                            ? "modularAvatarMissing"
                            : string.Empty,
                    ControllerPath = plan.ControllerPath,
                    Plan = plan
                };
            }

            var gateway = new VrchatFaceExpressionGateway();
            var succeeded = gateway.TryApply(
                avatar,
                configuration,
                out var controller,
                out var error);
            return new FaceExpressionApplyResult
            {
                Succeeded = succeeded,
                DryRun = false,
                ErrorCode = error ?? string.Empty,
                ControllerPath = AssetDatabase.GetAssetPath(controller),
                Plan = plan
            };
        }

        public static string Revision(AnimationClip clip)
        {
            return clip == null ? string.Empty : Revision(AssetDatabase.GetAssetPath(clip));
        }

        private static void ValidateAnimationEditTarget(
            GameObject avatar,
            AnimationClip clip,
            string expectedRevision)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            ValidateAnimationClip(clip, expectedRevision);
        }

        private static void ValidateAnimationClip(
            AnimationClip clip,
            string expectedRevision)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            var assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException(
                    "The AnimationClip must be a project asset.");
            }

            var revision = Revision(clip);
            if (!string.IsNullOrWhiteSpace(expectedRevision) &&
                !string.Equals(expectedRevision, revision, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The AnimationClip changed after it was inspected.");
            }
        }

        private static void ValidateTransitionDuration(float duration)
        {
            if (float.IsNaN(duration) ||
                float.IsInfinity(duration) ||
                duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    "Transition duration must be greater than zero.");
            }
        }

        private static float NormalizeTransitionDuration(
            AnimationClip clip,
            float duration)
        {
            var frameRate = clip == null || clip.frameRate <= 0f
                ? 60f
                : clip.frameRate;
            return Mathf.Max(
                1f / frameRate,
                FaceExpressionClipEditor.SnapTime(clip, duration));
        }

        private static void ValidatePoseInputs(
            GameObject avatar,
            AnimationClip clip,
            float poseTime,
            AnimationClip source,
            IReadOnlyList<FaceExpressionClipChange> changes)
        {
            changes = changes ?? Array.Empty<FaceExpressionClipChange>();
            if (source != null && changes.Count > 0)
            {
                throw new ArgumentException(
                    "A source clip and explicit channel values cannot be assigned to the same pose.");
            }

            if (ReferenceEquals(clip, source))
            {
                throw new ArgumentException(
                    "An animation cannot use itself as a pose source.");
            }

            var channels = ReadEditableChannels(avatar, clip, poseTime);
            var available = channels.ToDictionary(
                channel => ChannelKey(channel.RendererPath, channel.Name),
                channel => channel,
                StringComparer.Ordinal);
            var requested = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];
                if (change == null || string.IsNullOrWhiteSpace(change.ShapeName))
                {
                    throw new ArgumentException(
                        "Each pose channel requires shapeName.",
                        nameof(changes));
                }

                if (!change.Animated)
                {
                    throw new ArgumentException(
                        "Pose channel values cannot remove an entire curve.",
                        nameof(changes));
                }

                if (float.IsNaN(change.Value) ||
                    float.IsInfinity(change.Value))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(changes),
                        "Pose channel values must be finite numbers.");
                }

                var key = ChannelKey(change.RendererPath, change.ShapeName);
                if (!available.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "BlendShape channel was not found on the avatar: " + key);
                }

                if (!requested.Add(key))
                {
                    throw new ArgumentException(
                        "A pose channel was specified more than once: " + key,
                        nameof(changes));
                }
            }

            if (source == null)
            {
                return;
            }

            var sourceKeys = new HashSet<string>(
                AnimationUtility.GetCurveBindings(source)
                    .Where(IsBlendShapeBinding)
                    .Select(binding => ChannelKey(
                        binding.path,
                        binding.propertyName.Substring("blendShape.".Length))),
                StringComparer.Ordinal);
            if (!available.Keys.Any(sourceKeys.Contains))
            {
                throw new InvalidOperationException(
                    "The source clip has no matching BlendShape curves.");
            }
        }

        private static IReadOnlyList<BlendShapeChannel> ReadEditableChannels(
            GameObject avatar,
            AnimationClip clip,
            float poseTime)
        {
            return FaceExpressionClipEditor.Read(
                    avatar,
                    clip,
                    FaceExpressionSettings.GetSeparators(),
                    FaceExpressionClipEditor.GetRendererPaths(avatar),
                    poseTime)
                .Where(channel => !channel.IsHeader)
                .ToArray();
        }

        private static bool PoseChannelsNeedWrite(
            GameObject avatar,
            AnimationClip clip,
            float poseTime,
            IReadOnlyList<FaceExpressionClipChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return false;
            }

            var channels = ReadEditableChannels(avatar, clip, poseTime)
                .ToDictionary(
                    channel => ChannelKey(channel.RendererPath, channel.Name),
                    channel => channel,
                    StringComparer.Ordinal);
            return changes.Any(change =>
            {
                var channel = channels[ChannelKey(
                    change.RendererPath,
                    change.ShapeName)];
                return !channel.Animated ||
                       !Mathf.Approximately(
                           channel.Value,
                           Mathf.Clamp(change.Value, 0f, 100f));
            });
        }

        private static void ApplyPoseChannels(
            GameObject avatar,
            AnimationClip clip,
            float poseTime,
            IReadOnlyList<FaceExpressionClipChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return;
            }

            var channels = ReadEditableChannels(avatar, clip, poseTime)
                .ToDictionary(
                    channel => ChannelKey(channel.RendererPath, channel.Name),
                    channel => channel,
                    StringComparer.Ordinal);
            var poseTimes = FaceExpressionClipEditor.GetPoseTimes(clip);
            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];
                var channel = channels[ChannelKey(
                    change.RendererPath,
                    change.ShapeName)];
                var value = Mathf.Clamp(change.Value, 0f, 100f);
                if (channel.Animated &&
                    Mathf.Approximately(channel.Value, value))
                {
                    continue;
                }

                channel.Value = value;
                channel.Animated = true;
                FaceExpressionClipEditor.WritePose(
                    clip,
                    channel,
                    poseTime,
                    poseTimes);
            }
        }

        private static FaceExpressionAnimationEditResult AnimationEditResult(
            AnimationClip clip,
            bool changed,
            bool dryRun,
            int poseCount,
            int poseIndex,
            float poseTime)
        {
            return new FaceExpressionAnimationEditResult
            {
                Changed = changed,
                DryRun = dryRun,
                AssetPath = AssetDatabase.GetAssetPath(clip),
                Revision = Revision(clip),
                PoseCount = poseCount,
                PoseIndex = poseIndex,
                PoseTime = poseTime
            };
        }

        private static int FindPoseIndex(
            IReadOnlyList<float> poseTimes,
            float poseTime)
        {
            for (var index = 0; index < poseTimes.Count; index++)
            {
                if (Mathf.Approximately(poseTimes[index], poseTime))
                {
                    return index;
                }
            }

            return -1;
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

        private static bool ChannelNeedsWrite(
            AnimationClip clip,
            BlendShapeChannel channel,
            FaceExpressionClipChange change)
        {
            if (clip == null)
            {
                return true;
            }

            var curve = AnimationUtility.GetEditorCurve(clip, channel.Binding);
            if (!change.Animated)
            {
                return curve != null;
            }

            if (curve == null || curve.length != 1)
            {
                return true;
            }

            var key = curve.keys[0];
            return !Mathf.Approximately(key.time, 0f) ||
                   !Mathf.Approximately(
                       key.value,
                       Mathf.Clamp(change.Value, 0f, 100f));
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
