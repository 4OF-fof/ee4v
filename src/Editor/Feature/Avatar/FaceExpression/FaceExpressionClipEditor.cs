using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapeChannel
    {
        private float _value;

        public BlendShapeChannel(
            string rendererPath,
            string name,
            float value,
            bool animated,
            string headerText = null,
            string rendererDisplayName = null,
            string sourceAssetGuid = null,
            long sourceMeshLocalId = 0L,
            float? initialValue = null)
        {
            RendererPath = rendererPath ?? string.Empty;
            Name = name ?? string.Empty;
            Value = value;
            InitialValue = Mathf.Clamp(initialValue ?? value, 0f, 100f);
            Animated = animated;
            HeaderText = headerText;
            RendererDisplayName = rendererDisplayName;
            SourceAssetGuid = sourceAssetGuid ?? string.Empty;
            SourceMeshLocalId = sourceMeshLocalId;
        }

        public string RendererPath { get; }
        public string Name { get; }
        public float Value
        {
            get => _value;
            set => _value = Mathf.Clamp(value, 0f, 100f);
        }
        public bool Animated { get; set; }
        public float InitialValue { get; }
        public string HeaderText { get; }
        public string RendererDisplayName { get; }
        public string SourceAssetGuid { get; }
        public long SourceMeshLocalId { get; }
        public bool IsHeader => !string.IsNullOrEmpty(HeaderText);
        public string DisplayName => string.IsNullOrEmpty(RendererDisplayName)
            ? Name
            : RendererDisplayName + " / " + Name;
        public string DisplayHeaderText => string.IsNullOrEmpty(RendererDisplayName)
            ? HeaderText
            : RendererDisplayName + " / " + HeaderText;

        public EditorCurveBinding Binding => EditorCurveBinding.FloatCurve(
            RendererPath,
            typeof(SkinnedMeshRenderer),
            "blendShape." + Name);
    }

    internal static class FaceExpressionClipEditor
    {
        public static IReadOnlyList<BlendShapeChannel> Read(
            GameObject avatar,
            AnimationClip clip,
            IReadOnlyList<string> separators,
            IReadOnlyList<string> rendererPaths = null,
            float sampleTime = 0f)
        {
            if (avatar == null)
            {
                return Array.Empty<BlendShapeChannel>();
            }

            var channels = new List<BlendShapeChannel>();
            var renderers = ResolveRenderers(avatar, rendererPaths);
            for (var rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var path = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    avatar.transform);
                var mesh = renderer.sharedMesh;
                var sourceAssetGuid = string.Empty;
                var sourceMeshLocalId = 0L;
                var meshAssetPath = AssetDatabase.GetAssetPath(mesh);
                if (string.Equals(
                        System.IO.Path.GetExtension(meshAssetPath),
                        ".fbx",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        mesh,
                        out sourceAssetGuid,
                        out sourceMeshLocalId);
                }
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var name = mesh.GetBlendShapeName(index);
                    var binding = EditorCurveBinding.FloatCurve(
                        path,
                        typeof(SkinnedMeshRenderer),
                        "blendShape." + name);
                    var curve = clip == null ? null : AnimationUtility.GetEditorCurve(clip, binding);
                    var initialValue = renderer.GetBlendShapeWeight(index);
                    TryGetHeader(name, separators, out var headerText);
                    var sampledTime = SnapTime(clip, sampleTime);
                    channels.Add(new BlendShapeChannel(
                        path,
                        name,
                        curve == null ? initialValue : curve.Evaluate(sampledTime),
                        curve != null,
                        headerText,
                        renderers.Count > 1 ? renderer.name : null,
                        sourceAssetGuid,
                        sourceMeshLocalId,
                        initialValue));
                }
            }

            return channels;
        }

        private static IReadOnlyList<SkinnedMeshRenderer> ResolveRenderers(
            GameObject avatar,
            IReadOnlyList<string> rendererPaths)
        {
            if (rendererPaths == null)
            {
                var body = FindBodyRenderer(avatar);
                return body == null
                    ? Array.Empty<SkinnedMeshRenderer>()
                    : new[] { body };
            }

            var result = new List<SkinnedMeshRenderer>();
            for (var index = 0; index < rendererPaths.Count; index++)
            {
                var path = rendererPaths[index] ?? string.Empty;
                var target = string.IsNullOrEmpty(path)
                    ? avatar.transform
                    : avatar.transform.Find(path);
                var renderer = target == null
                    ? null
                    : target.GetComponent<SkinnedMeshRenderer>();
                if (renderer?.sharedMesh != null && !result.Contains(renderer))
                {
                    result.Add(renderer);
                }
            }

            return result;
        }

        public static void Write(AnimationClip clip, BlendShapeChannel channel)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (channel.IsHeader)
            {
                return;
            }

            Undo.RecordObject(clip, "Edit Face Expression");
            AnimationUtility.SetEditorCurve(
                clip,
                channel.Binding,
                channel.Animated
                    ? new AnimationCurve(new Keyframe(0f, channel.Value))
                    : null);
            clip.frameRate = 60f;
            EditorUtility.SetDirty(clip);
        }

        public static void WritePose(
            AnimationClip clip,
            BlendShapeChannel channel,
            float poseTime,
            IReadOnlyList<float> poseTimes)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (channel.IsHeader)
            {
                return;
            }

            if (!channel.Animated)
            {
                Write(clip, channel);
                return;
            }

            var sampledTime = SnapTime(clip, poseTime);
            var curve = AnimationUtility.GetEditorCurve(clip, channel.Binding);
            if (curve == null)
            {
                curve = new AnimationCurve();
            }

            var normalizedPoseTimes = (poseTimes ?? Array.Empty<float>())
                .Concat(new[] { sampledTime })
                .Select(time => SnapTime(clip, time))
                .Distinct()
                .OrderBy(time => time)
                .ToArray();
            if (curve.length == 0)
            {
                for (var index = 0; index < normalizedPoseTimes.Length; index++)
                {
                    AddClampedAutoKey(
                        curve,
                        normalizedPoseTimes[index],
                        channel.InitialValue);
                }
            }
            else
            {
                NormalizeCurveAtPoseTimes(clip, curve, normalizedPoseTimes);
            }

            SetKeyValue(clip, curve, sampledTime, channel.Value);

            Undo.RecordObject(clip, "Edit Face Expression Pose");
            AnimationUtility.SetEditorCurve(clip, channel.Binding, curve);
            clip.frameRate = 60f;
            EditorUtility.SetDirty(clip);
        }

        public static IReadOnlyList<float> GetPoseTimes(AnimationClip clip)
        {
            if (clip == null)
            {
                return Array.Empty<float>();
            }

            var times = new List<float> { 0f };
            times.AddRange(AnimationUtility.GetCurveBindings(clip)
                .Where(IsBlendShapeBinding)
                .Select(binding => AnimationUtility.GetEditorCurve(clip, binding))
                .Where(curve => curve != null)
                .SelectMany(curve => curve.keys)
                .Select(key => SnapTime(clip, key.time)));
            times.Sort();

            var result = new List<float>();
            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            for (var index = 0; index < times.Count; index++)
            {
                if (result.Count == 0 ||
                    Mathf.Abs(times[index] - result[result.Count - 1]) > tolerance)
                {
                    result.Add(times[index]);
                }
            }

            return result;
        }

        public static bool CanAddPose(AnimationClip clip)
        {
            return clip != null && AnimationUtility.GetCurveBindings(clip)
                .Where(IsBlendShapeBinding)
                .Select(binding => AnimationUtility.GetEditorCurve(clip, binding))
                .Any(curve => curve != null && curve.length > 0);
        }

        public static IReadOnlyList<AnimationClip> GetPoseSources(
            AnimationClip clip,
            IReadOnlyList<float> poseTimes)
        {
            if (clip == null || poseTimes == null)
            {
                return Array.Empty<AnimationClip>();
            }

            var result = new AnimationClip[poseTimes.Count];
            var data = FindSequenceData(clip);
            if (data == null)
            {
                return result;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            for (var poseIndex = 0; poseIndex < poseTimes.Count; poseIndex++)
            {
                var poseTime = poseTimes[poseIndex];
                var source = data.PoseSources.FirstOrDefault(item =>
                    Mathf.Abs(item.Time - poseTime) <= tolerance);
                result[poseIndex] = source?.Clip;
            }

            return result;
        }

        public static IReadOnlyList<string> GetPoseNames(
            AnimationClip clip,
            IReadOnlyList<float> poseTimes)
        {
            if (clip == null || poseTimes == null)
            {
                return Array.Empty<string>();
            }

            var result = new string[poseTimes.Count];
            var data = FindSequenceData(clip);
            if (data == null)
            {
                return result;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            for (var poseIndex = 0; poseIndex < poseTimes.Count; poseIndex++)
            {
                var poseTime = poseTimes[poseIndex];
                var item = data.PoseSources.FirstOrDefault(candidate =>
                    Mathf.Abs(candidate.Time - poseTime) <= tolerance);
                result[poseIndex] = item?.Name ?? string.Empty;
            }

            return result;
        }

        public static bool SetPoseName(
            AnimationClip clip,
            float poseTime,
            string name)
        {
            if (clip == null)
            {
                return false;
            }

            var poseTimes = GetPoseTimes(clip);
            var poseIndex = FindPoseIndex(clip, poseTimes, poseTime);
            if (poseIndex < 0)
            {
                return false;
            }

            var normalizedName = (name ?? string.Empty).Trim();
            var data = FindSequenceData(clip);
            if (data == null && normalizedName.Length == 0)
            {
                return true;
            }

            data = data == null ? GetOrCreateSequenceData(clip) : data;
            if (data == null)
            {
                return false;
            }

            Undo.RecordObject(data, "Rename Face Expression Pose");
            var targetTime = poseTimes[poseIndex];
            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            var item = data.PoseSources.FirstOrDefault(candidate =>
                Mathf.Abs(candidate.Time - targetTime) <= tolerance);
            if (item == null)
            {
                if (normalizedName.Length > 0)
                {
                    data.PoseSources.Add(new FaceExpressionPoseSource(
                        targetTime,
                        null,
                        normalizedName));
                }
            }
            else
            {
                item.Name = normalizedName;
                if (item.Clip == null && item.Name.Length == 0)
                {
                    data.PoseSources.Remove(item);
                }
            }

            EditorUtility.SetDirty(data);
            SaveSequenceData(data);
            return true;
        }

        public static bool SetPoseSource(
            AnimationClip clip,
            float poseTime,
            AnimationClip source,
            IReadOnlyList<BlendShapeChannel> availableChannels)
        {
            if (clip == null || ReferenceEquals(clip, source))
            {
                return false;
            }

            var poseTimes = GetPoseTimes(clip);
            var poseIndex = FindPoseIndex(clip, poseTimes, poseTime);
            if (poseIndex < 0)
            {
                return false;
            }

            var data = FindSequenceData(clip);
            if (source == null)
            {
                if (data == null)
                {
                    return true;
                }

                Undo.RecordObject(data, "Clear Face Expression Pose Source");
                ClearPoseSource(data, clip, poseTimes[poseIndex]);
                SaveSequenceData(data);
                return true;
            }

            if (availableChannels == null)
            {
                return false;
            }

            var sourceCurves = AnimationUtility.GetCurveBindings(source)
                .Where(IsBlendShapeBinding)
                .Select(binding => new
                {
                    Binding = binding,
                    Curve = AnimationUtility.GetEditorCurve(source, binding)
                })
                .Where(item => item.Curve != null && item.Curve.length > 0)
                .GroupBy(item => GetBindingKey(item.Binding))
                .ToDictionary(group => group.Key, group => group.First().Curve);
            var channels = availableChannels
                .Where(channel => channel != null && !channel.IsHeader)
                .GroupBy(channel => GetBindingKey(channel.Binding))
                .Select(group => group.First())
                .ToArray();
            if (!channels.Any(channel =>
                    sourceCurves.ContainsKey(GetBindingKey(channel.Binding))))
            {
                return false;
            }

            data = data == null ? GetOrCreateSequenceData(clip) : data;
            if (data == null)
            {
                return false;
            }

            Undo.RecordObject(clip, "Assign Face Expression Pose Source");
            Undo.RecordObject(data, "Assign Face Expression Pose Source");
            var targetTime = poseTimes[poseIndex];
            for (var channelIndex = 0;
                 channelIndex < channels.Length;
                 channelIndex++)
            {
                var channel = channels[channelIndex];
                sourceCurves.TryGetValue(
                    GetBindingKey(channel.Binding),
                    out var sourceCurve);
                var targetCurve = AnimationUtility.GetEditorCurve(
                    clip,
                    channel.Binding);
                if (targetCurve == null && sourceCurve == null)
                {
                    continue;
                }

                if (targetCurve == null)
                {
                    targetCurve = new AnimationCurve();
                    for (var timeIndex = 0;
                         timeIndex < poseTimes.Count;
                         timeIndex++)
                    {
                        AddClampedAutoKey(
                            targetCurve,
                            poseTimes[timeIndex],
                            channel.InitialValue);
                    }
                }
                else
                {
                    NormalizeCurveAtPoseTimes(clip, targetCurve, poseTimes);
                }

                var value = sourceCurve == null
                    ? channel.InitialValue
                    : Mathf.Clamp(sourceCurve.Evaluate(0f), 0f, 100f);
                SetKeyValue(clip, targetCurve, targetTime, value);
                AnimationUtility.SetEditorCurve(
                    clip,
                    channel.Binding,
                    targetCurve);
            }

            SetPoseSource(data, clip, targetTime, source);
            clip.frameRate = 60f;
            EditorUtility.SetDirty(clip);
            SaveSequenceData(data);
            return true;
        }

        public static float AddPose(
            AnimationClip clip,
            float afterPoseTime,
            float transitionDuration)
        {
            if (!CanAddPose(clip))
            {
                return -1f;
            }

            var poseTimes = GetPoseTimes(clip);
            var afterIndex = FindPoseIndex(clip, poseTimes, afterPoseTime);
            if (afterIndex < 0)
            {
                return -1f;
            }

            var afterTime = poseTimes[afterIndex];
            var duration = Mathf.Max(
                FrameDuration(clip),
                SnapTime(clip, transitionDuration));

            Undo.RecordObject(clip, "Add Face Expression Pose");
            var newTime = InsertDuplicatePose(
                clip,
                poseTimes,
                afterIndex,
                duration);
            ShiftPoseSources(
                clip,
                time => time > afterTime + FrameDuration(clip) * 0.5f,
                duration);
            clip.frameRate = 60f;
            EditorUtility.SetDirty(clip);
            return newTime;
        }

        public static bool RemovePose(AnimationClip clip, float poseTime)
        {
            if (clip == null)
            {
                return false;
            }

            var poseTimes = GetPoseTimes(clip);
            var poseIndex = FindPoseIndex(clip, poseTimes, poseTime);
            if (poseIndex < 0 || poseTimes.Count <= 1)
            {
                return false;
            }

            var selectedTime = poseTimes[poseIndex];
            var shift = poseIndex == 0
                ? -poseTimes[1]
                : -(selectedTime - poseTimes[poseIndex - 1]);
            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(IsBlendShapeBinding)
                .ToArray();

            Undo.RecordObject(clip, "Remove Face Expression Pose");
            for (var bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                {
                    continue;
                }

                NormalizeCurveAtPoseTimes(clip, curve, poseTimes);
                var keys = curve.keys
                    .Where(key => Mathf.Abs(key.time - selectedTime) > tolerance)
                    .Select(key =>
                    {
                        if (key.time > selectedTime + tolerance)
                        {
                            key.time = SnapTime(clip, key.time + shift);
                        }

                        return key;
                    })
                    .ToArray();
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    curve.length == 0 ? null : curve);
            }

            var data = FindSequenceData(clip);
            if (data != null)
            {
                Undo.RecordObject(data, "Remove Face Expression Pose");
                RemovePoseData(data, clip, selectedTime);
                ShiftPoseSources(
                    data,
                    clip,
                    time => time > selectedTime + tolerance,
                    shift);
                SaveSequenceData(data);
            }

            EditorUtility.SetDirty(clip);
            return true;
        }

        public static bool MovePose(
            AnimationClip clip,
            int poseIndex,
            int targetIndex)
        {
            if (clip == null || Mathf.Abs(targetIndex - poseIndex) != 1)
            {
                return false;
            }

            var poseTimes = GetPoseTimes(clip);
            if (poseIndex < 0 ||
                targetIndex < 0 ||
                poseIndex >= poseTimes.Count ||
                targetIndex >= poseTimes.Count)
            {
                return false;
            }

            var poseTime = poseTimes[poseIndex];
            var targetTime = poseTimes[targetIndex];
            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(IsBlendShapeBinding)
                .ToArray();
            Undo.RecordObject(clip, "Move Face Expression Pose");
            for (var bindingIndex = 0;
                 bindingIndex < bindings.Length;
                 bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                {
                    continue;
                }

                NormalizeCurveAtPoseTimes(clip, curve, poseTimes);
                var poseValue = curve.Evaluate(poseTime);
                var targetValue = curve.Evaluate(targetTime);
                SetKeyValue(clip, curve, poseTime, targetValue);
                SetKeyValue(clip, curve, targetTime, poseValue);
                SetClampedAutoTangents(clip, curve, poseTime);
                SetClampedAutoTangents(clip, curve, targetTime);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            var data = FindSequenceData(clip);
            if (data != null)
            {
                var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
                var poseSource = data.PoseSources.FirstOrDefault(item =>
                    Mathf.Abs(item.Time - poseTime) <= tolerance);
                var targetSource = data.PoseSources.FirstOrDefault(item =>
                    Mathf.Abs(item.Time - targetTime) <= tolerance);
                if (poseSource != null || targetSource != null)
                {
                    Undo.RecordObject(data, "Move Face Expression Pose Source");
                    if (poseSource != null)
                    {
                        poseSource.Time = targetTime;
                    }

                    if (targetSource != null)
                    {
                        targetSource.Time = poseTime;
                    }

                    EditorUtility.SetDirty(data);
                    SaveSequenceData(data);
                }
            }

            EditorUtility.SetDirty(clip);
            return true;
        }

        public static bool SetTransitionDuration(
            AnimationClip clip,
            float fromPoseTime,
            float transitionDuration)
        {
            if (clip == null)
            {
                return false;
            }

            var poseTimes = GetPoseTimes(clip);
            var poseIndex = FindPoseIndex(clip, poseTimes, fromPoseTime);
            if (poseIndex < 0 || poseIndex >= poseTimes.Count - 1)
            {
                return false;
            }

            var currentTime = poseTimes[poseIndex];
            var nextTime = poseTimes[poseIndex + 1];
            var duration = Mathf.Max(
                FrameDuration(clip),
                SnapTime(clip, transitionDuration));
            var delta = duration - (nextTime - currentTime);
            if (Mathf.Abs(delta) <= FrameDuration(clip) * 0.5f)
            {
                return false;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(IsBlendShapeBinding)
                .ToArray();
            Undo.RecordObject(clip, "Change Face Expression Transition");
            for (var bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                {
                    continue;
                }

                NormalizeCurveAtPoseTimes(clip, curve, poseTimes);
                var keys = curve.keys;
                for (var keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    if (keys[keyIndex].time >= nextTime - tolerance)
                    {
                        keys[keyIndex].time = SnapTime(
                            clip,
                            keys[keyIndex].time + delta);
                    }
                }

                curve.keys = keys;
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            ShiftPoseSources(
                clip,
                time => time >= nextTime - tolerance,
                delta);

            EditorUtility.SetDirty(clip);
            return true;
        }

        public static void Sample(
            AnimationClip clip,
            IReadOnlyList<BlendShapeChannel> channels,
            float time)
        {
            if (channels == null)
            {
                return;
            }

            var sampledTime = SnapTime(clip, time);
            for (var index = 0; index < channels.Count; index++)
            {
                var channel = channels[index];
                if (channel == null || channel.IsHeader)
                {
                    continue;
                }

                var curve = clip == null
                    ? null
                    : AnimationUtility.GetEditorCurve(clip, channel.Binding);
                channel.Animated = curve != null;
                channel.Value = curve == null
                    ? channel.InitialValue
                    : curve.Evaluate(sampledTime);
            }
        }

        public static bool IsLooping(AnimationClip clip)
        {
            return clip != null &&
                   AnimationUtility.GetAnimationClipSettings(clip).loopTime;
        }

        public static void SetLooping(AnimationClip clip, bool looping)
        {
            if (clip == null)
            {
                return;
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime == looping)
            {
                return;
            }

            Undo.RecordObject(clip, "Change Face Expression Looping");
            settings.loopTime = looping;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        public static float SnapTime(AnimationClip clip, float time)
        {
            var frameRate = clip == null || clip.frameRate <= 0f
                ? 60f
                : clip.frameRate;
            return Mathf.Max(0f, Mathf.Round(time * frameRate) / frameRate);
        }

        private static bool IsBlendShapeBinding(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer) &&
                   binding.propertyName.StartsWith(
                       "blendShape.",
                       StringComparison.Ordinal);
        }

        private static float FrameDuration(AnimationClip clip)
        {
            var frameRate = clip == null || clip.frameRate <= 0f
                ? 60f
                : clip.frameRate;
            return 1f / frameRate;
        }

        private static int FindKeyIndex(
            AnimationCurve curve,
            float time,
            AnimationClip clip)
        {
            if (curve == null)
            {
                return -1;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            var keys = curve.keys;
            for (var index = 0; index < keys.Length; index++)
            {
                if (Mathf.Abs(keys[index].time - time) <= tolerance)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindPoseIndex(
            AnimationClip clip,
            IReadOnlyList<float> poseTimes,
            float time)
        {
            if (poseTimes == null)
            {
                return -1;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            for (var index = 0; index < poseTimes.Count; index++)
            {
                if (Mathf.Abs(poseTimes[index] - time) <= tolerance)
                {
                    return index;
                }
            }

            return -1;
        }

        private static float InsertDuplicatePose(
            AnimationClip clip,
            IReadOnlyList<float> poseTimes,
            int afterIndex,
            float duration)
        {
            var afterTime = poseTimes[afterIndex];
            var newTime = SnapTime(clip, afterTime + duration);
            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(IsBlendShapeBinding)
                .ToArray();
            for (var bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0)
                {
                    continue;
                }

                NormalizeCurveAtPoseTimes(clip, curve, poseTimes);
                var value = curve.Evaluate(afterTime);
                var keys = curve.keys;
                for (var keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    if (keys[keyIndex].time > afterTime + tolerance)
                    {
                        keys[keyIndex].time += duration;
                    }
                }

                curve.keys = keys;
                AddClampedAutoKey(curve, newTime, value);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            return newTime;
        }

        private static string GetBindingKey(EditorCurveBinding binding)
        {
            return binding.path + "\n" + binding.propertyName;
        }

        private static void NormalizeCurveAtPoseTimes(
            AnimationClip clip,
            AnimationCurve curve,
            IReadOnlyList<float> poseTimes)
        {
            if (curve == null || poseTimes == null || poseTimes.Count == 0)
            {
                return;
            }

            var values = new float[poseTimes.Count];
            for (var index = 0; index < poseTimes.Count; index++)
            {
                values[index] = curve.Evaluate(poseTimes[index]);
            }

            for (var index = 0; index < poseTimes.Count; index++)
            {
                if (FindKeyIndex(curve, poseTimes[index], clip) < 0)
                {
                    AddClampedAutoKey(curve, poseTimes[index], values[index]);
                }
            }
        }

        private static void SetKeyValue(
            AnimationClip clip,
            AnimationCurve curve,
            float time,
            float value)
        {
            var keyIndex = FindKeyIndex(curve, time, clip);
            if (keyIndex < 0)
            {
                AddClampedAutoKey(curve, time, value);
                return;
            }

            var key = curve.keys[keyIndex];
            key.time = time;
            key.value = value;
            curve.MoveKey(keyIndex, key);
        }

        private static void SetClampedAutoTangents(
            AnimationClip clip,
            AnimationCurve curve,
            float time)
        {
            var keyIndex = FindKeyIndex(curve, time, clip);
            if (keyIndex < 0)
            {
                return;
            }

            AnimationUtility.SetKeyLeftTangentMode(
                curve,
                keyIndex,
                AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(
                curve,
                keyIndex,
                AnimationUtility.TangentMode.ClampedAuto);
        }

        private static void AddClampedAutoKey(
            AnimationCurve curve,
            float time,
            float value)
        {
            var keyIndex = curve.AddKey(new Keyframe(time, value));
            if (keyIndex < 0)
            {
                return;
            }

            AnimationUtility.SetKeyLeftTangentMode(
                curve,
                keyIndex,
                AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(
                curve,
                keyIndex,
                AnimationUtility.TangentMode.ClampedAuto);
        }

        public static AnimationClip Create(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var clip = new AnimationClip
            {
                frameRate = 60f,
                name = System.IO.Path.GetFileNameWithoutExtension(assetPath)
            };
            AssetDatabase.CreateAsset(clip, assetPath);
            Undo.RegisterCreatedObjectUndo(clip, "Create Face Expression");
            AssetDatabase.SaveAssets();
            return clip;
        }

        public static AnimationClip Copy(
            AnimationClip source,
            string assetPath)
        {
            if (source == null || string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var clip = new AnimationClip();
            EditorUtility.CopySerialized(source, clip);
            clip.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            clip.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(clip, assetPath);
            CopySequenceData(source, clip);
            Undo.RegisterCreatedObjectUndo(clip, "Copy Face Expression");
            AssetDatabase.SaveAssets();
            return clip;
        }

        private static FaceExpressionSequenceData FindSequenceData(
            AnimationClip clip)
        {
            var path = clip == null ? string.Empty : AssetDatabase.GetAssetPath(clip);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<FaceExpressionSequenceData>()
                    .FirstOrDefault();
        }

        private static FaceExpressionSequenceData GetOrCreateSequenceData(
            AnimationClip clip)
        {
            var data = FindSequenceData(clip);
            if (data != null)
            {
                return data;
            }

            var path = clip == null ? string.Empty : AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            data = ScriptableObject.CreateInstance<FaceExpressionSequenceData>();
            data.name = "Face Expression Sequence";
            data.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(data, clip);
            Undo.RegisterCreatedObjectUndo(
                data,
                "Create Face Expression Sequence Data");
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SetPoseSource(
            FaceExpressionSequenceData data,
            AnimationClip clip,
            float poseTime,
            AnimationClip source)
        {
            if (data == null)
            {
                return;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            var item = data.PoseSources.FirstOrDefault(candidate =>
                Mathf.Abs(candidate.Time - poseTime) <= tolerance);
            if (item == null)
            {
                data.PoseSources.Add(
                    new FaceExpressionPoseSource(poseTime, source));
            }
            else
            {
                item.Time = poseTime;
                item.Clip = source;
            }

            EditorUtility.SetDirty(data);
        }

        private static void ClearPoseSource(
            FaceExpressionSequenceData data,
            AnimationClip clip,
            float poseTime)
        {
            if (data == null)
            {
                return;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            var item = data.PoseSources.FirstOrDefault(candidate =>
                Mathf.Abs(candidate.Time - poseTime) <= tolerance);
            if (item == null)
            {
                return;
            }

            item.Clip = null;
            if (item.Name.Length == 0)
            {
                data.PoseSources.Remove(item);
            }

            EditorUtility.SetDirty(data);
        }

        private static void RemovePoseData(
            FaceExpressionSequenceData data,
            AnimationClip clip,
            float poseTime)
        {
            if (data == null)
            {
                return;
            }

            var tolerance = FrameDuration(clip) * 0.5f + 0.00001f;
            data.PoseSources.RemoveAll(item =>
                Mathf.Abs(item.Time - poseTime) <= tolerance);
            EditorUtility.SetDirty(data);
        }

        private static void ShiftPoseSources(
            AnimationClip clip,
            Func<float, bool> shouldShift,
            float delta)
        {
            var data = FindSequenceData(clip);
            if (data == null)
            {
                return;
            }

            Undo.RecordObject(data, "Move Face Expression Pose Sources");
            ShiftPoseSources(data, clip, shouldShift, delta);
            SaveSequenceData(data);
        }

        private static void ShiftPoseSources(
            FaceExpressionSequenceData data,
            AnimationClip clip,
            Func<float, bool> shouldShift,
            float delta)
        {
            if (data == null || shouldShift == null)
            {
                return;
            }

            for (var index = 0; index < data.PoseSources.Count; index++)
            {
                var item = data.PoseSources[index];
                if (shouldShift(item.Time))
                {
                    item.Time = SnapTime(clip, item.Time + delta);
                }
            }

            EditorUtility.SetDirty(data);
        }

        private static void CopySequenceData(
            AnimationClip source,
            AnimationClip destination)
        {
            var sourceData = FindSequenceData(source);
            if (sourceData == null || sourceData.PoseSources.Count == 0)
            {
                return;
            }

            var destinationData = GetOrCreateSequenceData(destination);
            if (destinationData == null)
            {
                return;
            }

            for (var index = 0; index < sourceData.PoseSources.Count; index++)
            {
                var item = sourceData.PoseSources[index];
                destinationData.PoseSources.Add(
                    new FaceExpressionPoseSource(
                        item.Time,
                        item.Clip,
                        item.Name));
            }

            EditorUtility.SetDirty(destinationData);
        }

        private static void SaveSequenceData(FaceExpressionSequenceData data)
        {
            if (data != null)
            {
                AssetDatabase.SaveAssetIfDirty(data);
            }
        }

        internal static bool TryGetHeader(
            string shapeName,
            IReadOnlyList<string> separators,
            out string headerText)
        {
            headerText = null;
            if (separators == null)
            {
                return false;
            }

            for (var index = 0; index < separators.Count; index++)
            {
                if (TryGetHeader(
                        shapeName,
                        separators[index],
                        out headerText))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetHeader(
            string shapeName,
            string separator,
            out string headerText)
        {
            headerText = null;
            var name = (shapeName ?? string.Empty).Trim();
            var marker = (separator ?? string.Empty).Trim();
            if (name.Length == 0 || marker.Length == 0)
            {
                return false;
            }

            string candidate;
            if (marker.All(character => character == marker[0]))
            {
                if (CountLeading(name, marker[0]) < marker.Length ||
                    CountTrailing(name, marker[0]) < marker.Length)
                {
                    return false;
                }

                candidate = name.Trim(marker[0]).Trim();
            }
            else
            {
                if (!name.StartsWith(marker, StringComparison.Ordinal) ||
                    !name.EndsWith(marker, StringComparison.Ordinal) ||
                    name.Length <= marker.Length * 2)
                {
                    return false;
                }

                candidate = name.Substring(
                        marker.Length,
                        name.Length - marker.Length * 2)
                    .Trim();
            }

            if (candidate.Length == 0)
            {
                return false;
            }

            headerText = candidate;
            return true;
        }

        private static int CountLeading(string value, char marker)
        {
            var count = 0;
            while (count < value.Length && value[count] == marker)
            {
                count++;
            }

            return count;
        }

        private static int CountTrailing(string value, char marker)
        {
            var count = 0;
            while (count < value.Length && value[value.Length - count - 1] == marker)
            {
                count++;
            }

            return count;
        }

        internal static SkinnedMeshRenderer FindBodyRenderer(GameObject avatar)
        {
            return avatar == null
                ? null
                : avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(renderer =>
                        renderer.sharedMesh != null &&
                        string.Equals(
                            renderer.name,
                            "Body",
                            StringComparison.OrdinalIgnoreCase));
        }

        internal static IReadOnlyList<string> GetRendererPaths(GameObject avatar)
        {
            if (avatar == null)
            {
                return Array.Empty<string>();
            }

            return avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null &&
                                   renderer.sharedMesh.blendShapeCount > 0)
                .Select(renderer => AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    avatar.transform))
                .ToArray();
        }
    }
}
