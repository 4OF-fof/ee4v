using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ee4v.Core.AvatarEvaluation
{
    /// <summary>Owns a script-free sampling rig; only sampled values are sent to preview renderers.</summary>
    internal sealed class AvatarPreviewAnimation : IDisposable
    {
        private readonly GameObject _source;
        private readonly Dictionary<Transform, Transform> _transforms = new Dictionary<Transform, Transform>();
        private readonly Dictionary<string, Transform> _paths = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<Renderer, Renderer> _renderers = new Dictionary<Renderer, Renderer>();
        private readonly HashSet<Renderer> _enabled = new HashSet<Renderer>();
        private readonly HashSet<Transform> _active = new HashSet<Transform>();
        private readonly Dictionary<SkinnedMeshRenderer, HashSet<string>> _shapes = new Dictionary<SkinnedMeshRenderer, HashSet<string>>();
        private readonly Dictionary<Renderer, HashSet<int>> _materials = new Dictionary<Renderer, HashSet<int>>();
        private (Transform Transform, Vector3 Position, Quaternion Rotation, Vector3 Scale)[] _rest;
        private Scene _scene;
        private GameObject _container;
        private GameObject _rig;
        private AnimationClip _samplingClip;
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;

        public AnimationClip Clip { get; }
        public float Time { get; private set; }

        internal AvatarPreviewAnimation(GameObject source, AnimationClip clip,
            IReadOnlyDictionary<Transform, AvatarBoneBinding> bindings)
        {
            _source = source;
            Clip = clip;
            try
            {
                _scene = EditorSceneManager.NewPreviewScene();
                _container = new GameObject("ee4v animation sampling") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(_container, _scene);
                _container.SetActive(false);
                _rig = Object.Instantiate(source, _container.transform, false);
                _rig.hideFlags = HideFlags.HideAndDontSave;
                Map(source.transform, _rig.transform);
                foreach (var behaviour in _rig.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var script in _rig.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                foreach (var renderer in _rig.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
                ApplyBindings(bindings);
                _samplingClip = PrepareClip(clip, bindings);
                var animator = _rig.GetComponent<Animator>();
                if (animator == null) animator = _rig.AddComponent<Animator>();
                if (clip.isHumanMotion && (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman))
                    throw new InvalidOperationException("A valid Humanoid Avatar is required for this clip.");
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.fireEvents = false;
                animator.applyRootMotion = false;
                _rig.SetActive(true);
                _container.SetActive(true);
                animator.enabled = true;
                _rest = _rig.GetComponentsInChildren<Transform>(true)
                    .Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
                _graph = PlayableGraph.Create("ee4v animation preview");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _playable = AnimationClipPlayable.Create(_graph, _samplingClip);
                _playable.SetApplyFootIK(false);
                _playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(_graph, "Animation", animator).SetSourcePlayable(_playable);
                _graph.Play();
                _playable.Pause();
                Sample(0f, null);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Map(Transform source, Transform copy)
        {
            _transforms[source] = copy;
            _paths[AnimationUtility.CalculateTransformPath(source, _source.transform)] = source;
            var renderers = source.GetComponents<Renderer>();
            var copies = copy.GetComponents<Renderer>();
            for (var i = 0; i < renderers.Length; i++) _renderers[renderers[i]] = copies[i];
            for (var i = 0; i < source.childCount; i++) Map(source.GetChild(i), copy.GetChild(i));
        }

        private Transform MergeTarget(Transform source, IReadOnlyDictionary<Transform, AvatarBoneBinding> bindings)
        {
            while (bindings.TryGetValue(source, out var binding) && binding.IsMerge) source = binding.Target;
            return source;
        }

        private void ApplyBindings(IReadOnlyDictionary<Transform, AvatarBoneBinding> bindings)
        {
            // Keep attachment offsets before any of the parents are moved.
            var attachments = bindings.Select(pair => (Source: pair.Key, Binding: pair.Value,
                Relative: MergeTarget(pair.Value.Target, bindings).worldToLocalMatrix * pair.Key.localToWorldMatrix)).ToArray();
            foreach (var attachment in attachments.Where(a => a.Binding.IsMerge))
            {
                var binding = attachment.Binding;
                var copy = _transforms[attachment.Source];
                var target = _transforms[MergeTarget(binding.Target, bindings)];
                copy.SetParent(target, false);
                var relative = attachment.Relative;
                copy.localPosition = relative.GetColumn(3);
                copy.localRotation = relative.rotation;
                copy.localScale = relative.lossyScale;
                // Retained clothing bones must not collide with actual target children in clip paths.
                copy.name += "$ee4v" + copy.GetInstanceID();
            }
            // MA processes Bone Proxy after armature merging, then restores kept world poses parent first.
            var proxies = bindings.Where(pair => !pair.Value.IsMerge).Select(pair => (
                Source: _transforms[pair.Key], Target: _transforms[MergeTarget(pair.Value.Target, bindings)],
                Binding: pair.Value, Position: _transforms[pair.Key].position, Rotation: _transforms[pair.Key].rotation)).ToArray();
            foreach (var proxy in proxies) proxy.Source.SetParent(proxy.Target, true);
            foreach (var proxy in proxies.OrderBy(p => AnimationUtility.CalculateTransformPath(p.Source, _rig.transform).Count(c => c == '/')))
            {
                var mode = proxy.Binding.Mode;
                if (mode == BoneProxyAttachmentMode.AsChildKeepWorldPose || mode == BoneProxyAttachmentMode.AsChildKeepPosition)
                    proxy.Source.position = proxy.Position;
                else proxy.Source.localPosition = Vector3.zero;
                if (mode == BoneProxyAttachmentMode.AsChildKeepWorldPose || mode == BoneProxyAttachmentMode.AsChildKeepRotation)
                    proxy.Source.rotation = proxy.Rotation;
                else proxy.Source.localRotation = Quaternion.identity;
                if (proxy.Binding.MatchScale) proxy.Source.localScale = Vector3.one;
            }
        }

        private AnimationClip PrepareClip(AnimationClip clip, IReadOnlyDictionary<Transform, AvatarBoneBinding> bindings)
        {
            var result = Object.Instantiate(clip);
            _samplingClip = result;
            result.hideFlags = HideFlags.HideAndDontSave;
            result.legacy = false;
            AnimationUtility.SetAnimationEvents(result, Array.Empty<AnimationEvent>());
            var curves = AnimationUtility.GetCurveBindings(clip);
            var objects = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in curves) AnimationUtility.SetEditorCurve(result, binding, null);
            foreach (var binding in objects) AnimationUtility.SetObjectReferenceCurve(result, binding, null);
            var used = new HashSet<EditorCurveBinding>();
            foreach (var original in curves.Concat(objects))
            {
                if (!_paths.TryGetValue(original.path, out var source))
                    throw new InvalidOperationException("Animation path was not found: " + original.path);
                var remapped = original;
                var target = original.type == typeof(Transform) ? MergeTarget(source, bindings) : source;
                remapped.path = AnimationUtility.CalculateTransformPath(_transforms[target], _rig.transform);
                if (!used.Add(remapped))
                    throw new InvalidOperationException("Animation curves conflict after armature merging: " + original.path);
                Track(original, source);
                if (original.isPPtrCurve)
                    AnimationUtility.SetObjectReferenceCurve(result, remapped, AnimationUtility.GetObjectReferenceCurve(clip, original));
                else AnimationUtility.SetEditorCurve(result, remapped, AnimationUtility.GetEditorCurve(clip, original));
            }
            // Native clip evaluation resets unkeyed components of an animated vector to zero.
            // Keep the rig's rest values for channels absent from the supplied clip.
            foreach (var group in used.Where(b => b.type == typeof(Transform)).GroupBy(b => b.path))
            {
                var transform = string.IsNullOrEmpty(group.Key) ? _rig.transform : _rig.transform.Find(group.Key);
                foreach (var prefix in new[] { "m_LocalPosition", "m_LocalScale", "m_LocalRotation", "localEulerAnglesRaw", "localEulerAnglesBaked" })
                {
                    if (!group.Any(b => b.propertyName.StartsWith(prefix + ".", StringComparison.Ordinal))) continue;
                    var components = prefix == "m_LocalRotation" ? "xyzw" : "xyz";
                    var rotation = transform.localRotation;
                    var vector = prefix == "m_LocalPosition" ? transform.localPosition :
                        prefix == "m_LocalScale" ? transform.localScale : transform.localEulerAngles;
                    for (var i = 0; i < components.Length; i++)
                    {
                        var binding = EditorCurveBinding.FloatCurve(group.Key, typeof(Transform), prefix + "." + components[i]);
                        if (used.Contains(binding)) continue;
                        var value = prefix == "m_LocalRotation" ? rotation[i] : vector[i];
                        AnimationUtility.SetEditorCurve(result, binding, AnimationCurve.Constant(0f, Mathf.Max(clip.length, .0001f), value));
                    }
                }
            }
            return result;
        }

        private void Track(EditorCurveBinding binding, Transform source)
        {
            if (binding.type == typeof(Transform) || binding.type == typeof(Animator)) return;
            if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive" && source != _source.transform)
            {
                _active.Add(source);
                return;
            }
            if (typeof(Renderer).IsAssignableFrom(binding.type))
            {
                var renderer = source.GetComponent(binding.type) as Renderer;
                if (renderer == null) throw new InvalidOperationException("Animated renderer was not found: " + binding.path);
                if (binding.propertyName == "m_Enabled") { _enabled.Add(renderer); return; }
                if (renderer is SkinnedMeshRenderer skinned && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                {
                    if (!_shapes.TryGetValue(skinned, out var shapes)) _shapes[skinned] = shapes = new HashSet<string>();
                    shapes.Add(binding.propertyName.Substring("blendShape.".Length));
                    return;
                }
                const string prefix = "m_Materials.Array.data[";
                if (binding.isPPtrCurve && binding.propertyName.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(binding.propertyName.Substring(prefix.Length).TrimEnd(']'), out var slot))
                {
                    if (!_materials.TryGetValue(renderer, out var slots)) _materials[renderer] = slots = new HashSet<int>();
                    slots.Add(slot);
                    return;
                }
            }
            throw new NotSupportedException("Unsupported preview animation binding: " + binding.type.Name + "." + binding.propertyName);
        }

        internal void Sample(float time, IReadOnlyDictionary<Transform, Vector3> scales)
        {
            if (float.IsNaN(time) || float.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            Time = Clip.isLooping && Clip.length > 0f ? Mathf.Repeat(time, Clip.length) : Mathf.Clamp(time, 0f, Clip.length);
            var parent = _source.transform.parent;
            _container.transform.position = parent == null ? Vector3.zero : parent.position;
            _container.transform.rotation = parent == null ? Quaternion.identity : parent.rotation;
            _container.transform.localScale = parent == null ? Vector3.one : parent.lossyScale;
            foreach (var state in _rest)
            {
                state.Transform.localPosition = state.Position;
                state.Transform.localRotation = state.Rotation;
                state.Transform.localScale = state.Scale;
            }
            _playable.SetTime(Time);
            _graph.Evaluate(0f);
            if (scales != null)
                foreach (var pair in scales)
                    if (pair.Key != null && _transforms.TryGetValue(pair.Key, out var copy)) copy.localScale = pair.Value;
        }

        internal Matrix4x4 GetWorldMatrix(Transform source) => source == null ? Matrix4x4.identity :
            _transforms.TryGetValue(source, out var copy) ? copy.localToWorldMatrix : source.localToWorldMatrix;

        internal void ApplyRenderer(Renderer source, Renderer target)
        {
            if (!_renderers.TryGetValue(source, out var copy)) return;
            var active = true;
            var changed = _enabled.Contains(source);
            for (var current = source.transform; current != null; current = current.parent)
            {
                var animated = _active.Contains(current);
                active &= animated ? _transforms[current].gameObject.activeSelf : current.gameObject.activeSelf;
                changed |= animated;
                if (current == _source.transform) break;
            }
            if (changed) target.enabled = active && (_enabled.Contains(source) ? copy.enabled : source.enabled);
            if (source is SkinnedMeshRenderer skinned && target is SkinnedMeshRenderer output &&
                _shapes.TryGetValue(skinned, out var shapes) && output.sharedMesh != null)
                foreach (var name in shapes)
                {
                    var from = skinned.sharedMesh == null ? -1 : skinned.sharedMesh.GetBlendShapeIndex(name);
                    var to = output.sharedMesh.GetBlendShapeIndex(name);
                    if (from >= 0 && to >= 0) output.SetBlendShapeWeight(to, ((SkinnedMeshRenderer)copy).GetBlendShapeWeight(from));
                }
            if (_materials.TryGetValue(source, out var slots))
            {
                var values = target.sharedMaterials;
                var animated = copy.sharedMaterials;
                foreach (var slot in slots) if (slot < values.Length && slot < animated.Length) values[slot] = animated[slot];
                target.sharedMaterials = values;
            }
        }

        internal void ApplySnapshot(
            Dictionary<GameObject, bool> activeStates, Dictionary<Renderer, bool> enabledStates,
            Dictionary<Renderer, Material[]> materials, Dictionary<(SkinnedMeshRenderer, int), float> shapes)
        {
            foreach (var source in _active)
            {
                activeStates[source.gameObject] = source.gameObject.activeSelf;
                source.gameObject.SetActive(_transforms[source].gameObject.activeSelf);
            }
            foreach (var source in _renderers.Keys)
            {
                enabledStates[source] = source.enabled;
                if (_materials.ContainsKey(source)) materials[source] = source.sharedMaterials;
                if (source is SkinnedMeshRenderer skinned && _shapes.TryGetValue(skinned, out var names) && skinned.sharedMesh != null)
                    foreach (var name in names)
                    {
                        var index = skinned.sharedMesh.GetBlendShapeIndex(name);
                        if (index >= 0) shapes[(skinned, index)] = skinned.GetBlendShapeWeight(index);
                    }
                ApplyRenderer(source, source);
            }
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_samplingClip != null) Object.DestroyImmediate(_samplingClip);
            if (_container != null) Object.DestroyImmediate(_container);
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }
    }
}
