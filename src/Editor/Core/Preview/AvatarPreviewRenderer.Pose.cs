using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Preview
{
    public sealed partial class AvatarPreviewRenderer
    {
        private readonly Dictionary<Transform, (Transform Target, BoneProxyAttachmentMode Mode, bool MatchScale)> _poseBindings =
            new Dictionary<Transform, (Transform, BoneProxyAttachmentMode, bool)>();

        private void RefreshPoseBindings()
        {
            _poseBindings.Clear();
            foreach (var merge in Root.GetComponentsInChildren<ModularAvatarMergeArmature>(true))
            {
                if (merge.mergeTargetObject == null) continue;
                AddPoseBinding(merge.transform, merge.mergeTargetObject.transform, BoneProxyAttachmentMode.AsChildKeepWorldPose);
                var mapping = merge.GetBonesMapping();
                if (mapping == null) continue;
                foreach (var pair in mapping)
                    AddPoseBinding(pair.Item2, pair.Item1, BoneProxyAttachmentMode.AsChildKeepWorldPose);
            }
            foreach (var proxy in Root.GetComponentsInChildren<ModularAvatarBoneProxy>(true))
                AddPoseBinding(proxy.transform, proxy.target, proxy.attachmentMode, proxy.matchScale);
        }

        private void AddPoseBinding(Transform source, Transform target, BoneProxyAttachmentMode mode, bool matchScale = false)
        {
            if (source == null || target == null || !target.IsChildOf(Root.transform)) return;
            var visited = new HashSet<Transform>();
            for (var current = target; current != null;)
            {
                if (current == source || !visited.Add(current)) return;
                current = _poseBindings.TryGetValue(current, out var binding) ? binding.Target : current.parent;
            }
            _poseBindings[source] = (target, mode, matchScale);
        }

        public bool SupportsHumanoidPose => Root != null && Root.GetComponentsInChildren<Animator>(true)
            .Any(animator => animator.avatar != null && animator.avatar.isValid && animator.isHuman);

        public void SetHumanoidPose(bool tPose)
        {
            _rotations.Clear();
            if (Root == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var animator in Root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman) continue;
                var reference = animator.avatar.humanDescription.skeleton
                    .GroupBy(bone => bone.name).ToDictionary(group => group.Key, group => group.First().rotation);
                for (var index = 0; index < (int)HumanBodyBones.LastBone; index++)
                {
                    var bone = animator.GetBoneTransform((HumanBodyBones)index);
                    if (bone != null && reference.TryGetValue(bone.name, out var rotation))
                        _rotations[bone] = rotation;
                }
                var up = animator.transform.up;
                var right = animator.transform.right;
                var leftDirection = tPose ? -right : (-right * 0.5f - up * 0.8660254f);
                var rightDirection = tPose ? right : (right * 0.5f - up * 0.8660254f);
                AlignBone(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), leftDirection);
                AlignBone(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                    animator.GetBoneTransform(HumanBodyBones.LeftHand), leftDirection);
                AlignBone(animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm), rightDirection);
                AlignBone(animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                    animator.GetBoneTransform(HumanBodyBones.RightHand), rightDirection);
            }
        }

        public Vector3 GetTransformPosition(Transform source) => source == null
            ? Vector3.zero : GetPoseMatrix(source).MultiplyPoint3x4(Vector3.zero);

        private Matrix4x4 GetPoseMatrix(Transform source)
        {
            if (source == null) return Matrix4x4.identity;
            if ((_rotations.Count > 0 || _scales.Count > 0) && _poseBindings.TryGetValue(source, out var binding) && binding.Target != null)
            {
                var relative = binding.Target.worldToLocalMatrix * source.localToWorldMatrix;
                if (binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose && !binding.MatchScale)
                    return GetPoseMatrix(binding.Target) * relative;
                var keepPosition = binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose ||
                    binding.Mode == BoneProxyAttachmentMode.AsChildKeepPosition;
                var keepRotation = binding.Mode == BoneProxyAttachmentMode.AsChildKeepWorldPose ||
                    binding.Mode == BoneProxyAttachmentMode.AsChildKeepRotation;
                return GetPoseMatrix(binding.Target) * Matrix4x4.TRS(
                    keepPosition ? (Vector3)relative.GetColumn(3) : Vector3.zero,
                    keepRotation ? relative.rotation : Quaternion.identity,
                    binding.MatchScale ? Vector3.one : relative.lossyScale);
            }
            var local = Matrix4x4.TRS(source.localPosition,
                _rotations.TryGetValue(source, out var rotation) ? rotation : source.localRotation,
                _scales.TryGetValue(source, out var scale) ? scale : source.localScale);
            return GetPoseMatrix(source.parent) * local;
        }

        private Matrix4x4 GetPoseLocalMatrix(Transform source) =>
            GetPoseMatrix(source.parent).inverse * GetPoseMatrix(source);

        private void AlignBone(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null) return;
            var matrix = GetPoseMatrix(bone);
            var current = GetTransformPosition(child) - matrix.MultiplyPoint3x4(Vector3.zero);
            if (current.sqrMagnitude < 0.000001f) return;
            var worldRotation = Quaternion.FromToRotation(current, direction) * matrix.rotation;
            _rotations[bone] = Quaternion.Inverse(GetPoseMatrix(bone.parent).rotation) * worldRotation;
        }
    }
}
