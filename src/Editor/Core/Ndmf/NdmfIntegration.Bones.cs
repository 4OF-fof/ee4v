using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace Ee4v.Core.Ndmf
{
    public sealed class NdmfBoneBinding
    {
        public Transform Target { get; internal set; }
        public BoneProxyAttachmentMode Mode { get; internal set; }
        public bool MatchScale { get; internal set; }
    }

    public static partial class NdmfIntegration
    {
        public static IReadOnlyDictionary<Transform, NdmfBoneBinding> GetBoneBindings(GameObject avatar)
        {
            if (avatar == null) throw new ArgumentNullException(nameof(avatar));
            var bindings = new Dictionary<Transform, NdmfBoneBinding>();
            void Add(Transform source, Transform target, BoneProxyAttachmentMode mode, bool matchScale = false)
            {
                if (source == null || target == null || !target.IsChildOf(avatar.transform)) return;
                var visited = new HashSet<Transform>();
                for (var current = target; current != null;)
                {
                    if (current == source || !visited.Add(current)) return;
                    current = bindings.TryGetValue(current, out var binding) ? binding.Target : current.parent;
                }
                bindings[source] = new NdmfBoneBinding { Target = target, Mode = mode, MatchScale = matchScale };
            }
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeArmature>(true))
            {
                if (merge.mergeTargetObject == null) continue;
                Add(merge.transform, merge.mergeTargetObject.transform, BoneProxyAttachmentMode.AsChildKeepWorldPose);
                var mapping = merge.GetBonesMapping();
                if (mapping == null) continue;
                foreach (var pair in mapping) Add(pair.Item2, pair.Item1, BoneProxyAttachmentMode.AsChildKeepWorldPose);
            }
            foreach (var proxy in avatar.GetComponentsInChildren<ModularAvatarBoneProxy>(true))
                Add(proxy.transform, proxy.target, proxy.attachmentMode, proxy.matchScale);
            return new System.Collections.ObjectModel.ReadOnlyDictionary<Transform, NdmfBoneBinding>(bindings);
        }

        public static Matrix4x4 GetBoneWorldMatrix(Transform source,
            IReadOnlyDictionary<Transform, NdmfBoneBinding> bindings,
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
                        var keepPosition = binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose ||
                            binding.Mode == BoneProxyAttachmentMode.AsChildKeepPosition;
                        var keepRotation = binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose ||
                            binding.Mode == BoneProxyAttachmentMode.AsChildKeepRotation;
                        matrix = Evaluate(binding.Target) * Matrix4x4.TRS(
                            keepPosition ? (Vector3)relative.GetColumn(3) : Vector3.zero,
                            keepRotation ? relative.rotation : Quaternion.identity,
                            binding.MatchScale ? Vector3.one : relative.lossyScale);
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
