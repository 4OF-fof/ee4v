using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace Ee4v.Core.AvatarEvaluation
{
    public sealed class AvatarBoneBinding
    {
        public Transform Target { get; internal set; }
        public BoneProxyAttachmentMode Mode { get; internal set; }
        public bool MatchScale { get; internal set; }
        public bool IsMerge { get; internal set; }
    }

    /// <summary>Evaluates display pose overrides using MA bone bindings; does not merge hierarchy or retarget animation.</summary>
    public static class AvatarPreviewPose
    {
        // Matrix.rotation asserts for collapsed axes and some sheared attachment matrices.
        // Keep finite position/scale, and derive an orthonormal rotation without native decomposition.
        internal static void Decompose(Matrix4x4 matrix, out Vector3 position,
            out Quaternion rotation, out Vector3 scale)
        {
            position = matrix.GetColumn(3);
            var x = (Vector3)matrix.GetColumn(0);
            var y = (Vector3)matrix.GetColumn(1);
            var z = (Vector3)matrix.GetColumn(2);
            bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
            if (!Finite(position) || !Finite(x) || !Finite(y) || !Finite(z))
            {
                position = Finite(position) ? position : Vector3.zero;
                rotation = Quaternion.identity;
                scale = Vector3.zero;
                return;
            }
            if (matrix.ValidTRS())
            {
                rotation = matrix.rotation;
                scale = matrix.lossyScale;
                return;
            }
            scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (Vector3.Dot(Vector3.Cross(x, y), z) < 0f) { x = -x; scale.x = -scale.x; }
            if (z.sqrMagnitude < 0.00000001f) z = Vector3.Cross(x, y);
            if (y.sqrMagnitude < 0.00000001f) y = Vector3.Cross(z, x);
            if (z.sqrMagnitude < 0.00000001f && y.sqrMagnitude > 0.00000001f)
                z = Vector3.Cross(Mathf.Abs(y.normalized.x) < 0.9f ? Vector3.right : Vector3.forward, y);
            if (z.sqrMagnitude < 0.00000001f) z = Vector3.forward;
            z.Normalize();
            y -= z * Vector3.Dot(y, z);
            if (y.sqrMagnitude < 0.00000001f)
                y = Vector3.Cross(z, Mathf.Abs(z.x) < 0.9f ? Vector3.right : Vector3.up);
            rotation = Quaternion.LookRotation(z, y.normalized);
        }

        public static IReadOnlyDictionary<Transform, AvatarBoneBinding> GetBindings(GameObject avatar)
        {
            if (avatar == null) throw new ArgumentNullException(nameof(avatar));
            var bindings = new Dictionary<Transform, AvatarBoneBinding>();
            void Add(Transform source, Transform target, BoneProxyAttachmentMode mode, bool matchScale = false, bool isMerge = false)
            {
                if (source == null || target == null || !target.IsChildOf(avatar.transform)) return;
                var visited = new HashSet<Transform>();
                for (var current = target; current != null;)
                {
                    if (current == source || !visited.Add(current)) return;
                    current = bindings.TryGetValue(current, out var binding) ? binding.Target : current.parent;
                }
                bindings[source] = new AvatarBoneBinding { Target = target, Mode = mode, MatchScale = matchScale, IsMerge = isMerge };
            }
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeArmature>(true))
            {
                if (merge.mergeTargetObject == null) continue;
                Add(merge.transform, merge.mergeTargetObject.transform, BoneProxyAttachmentMode.AsChildKeepWorldPose, isMerge: true);
                var mapping = merge.GetBonesMapping();
                if (mapping == null) continue;
                foreach (var pair in mapping) Add(pair.Item2, pair.Item1, BoneProxyAttachmentMode.AsChildKeepWorldPose, isMerge: true);
            }
            foreach (var proxy in avatar.GetComponentsInChildren<ModularAvatarBoneProxy>(true))
                Add(proxy.transform, proxy.target, proxy.attachmentMode, proxy.matchScale);
            return new System.Collections.ObjectModel.ReadOnlyDictionary<Transform, AvatarBoneBinding>(bindings);
        }

        public static Matrix4x4 GetWorldMatrix(Transform source,
            IReadOnlyDictionary<Transform, AvatarBoneBinding> bindings,
            IReadOnlyDictionary<Transform, Quaternion> rotations,
            IReadOnlyDictionary<Transform, Vector3> scales,
            Dictionary<Transform, Matrix4x4> cache = null)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            Matrix4x4 Evaluate(Transform bone)
            {
                if (bone == null) return Matrix4x4.identity;
                if (cache != null && cache.TryGetValue(bone, out var cached)) return cached;
                Matrix4x4 matrix;
                if ((rotations != null && rotations.Count > 0 || scales != null && scales.Count > 0) &&
                    bindings.TryGetValue(bone, out var binding) && binding.Target != null)
                {
                    var relative = binding.Target.worldToLocalMatrix * bone.localToWorldMatrix;
                    if (binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose && !binding.MatchScale)
                        matrix = Evaluate(binding.Target) * relative;
                    else
                    {
                        Decompose(relative, out var position, out var rotation, out var scale);
                        var keepPosition = binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose ||
                            binding.Mode == BoneProxyAttachmentMode.AsChildKeepPosition;
                        var keepRotation = binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose ||
                            binding.Mode == BoneProxyAttachmentMode.AsChildKeepRotation;
                        matrix = Evaluate(binding.Target) * Matrix4x4.TRS(
                            keepPosition ? position : Vector3.zero,
                            keepRotation ? rotation : Quaternion.identity,
                            binding.MatchScale ? Vector3.one : scale);
                    }
                }
                else
                {
                    var rotation = bone.localRotation;
                    var scale = bone.localScale;
                    if (rotations != null && rotations.TryGetValue(bone, out var overriddenRotation)) rotation = overriddenRotation;
                    if (scales != null && scales.TryGetValue(bone, out var overriddenScale)) scale = overriddenScale;
                    matrix = Evaluate(bone.parent) * Matrix4x4.TRS(bone.localPosition, rotation, scale);
                }
                if (cache != null) cache[bone] = matrix;
                return matrix;
            }
            return Evaluate(source);
        }
    }
}
