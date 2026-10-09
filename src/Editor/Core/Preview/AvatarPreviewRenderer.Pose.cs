using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.AvatarEvaluation;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Preview
{
    public sealed partial class AvatarPreviewRenderer
    {
        private IReadOnlyDictionary<Transform, AvatarBoneBinding> _poseBindings =
            new Dictionary<Transform, AvatarBoneBinding>();

        private void RefreshPoseBindings()
        {
            _poseBindings = AvatarEvaluator.GetBoneBindings(Root);
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

        private Matrix4x4 GetPoseMatrix(Transform source, Dictionary<Transform, Matrix4x4> matrices = null) =>
            AvatarEvaluator.GetBoneWorldMatrix(source, _poseBindings, _rotations, _scales, matrices);

        private Matrix4x4 GetPoseLocalMatrix(Transform source, Dictionary<Transform, Matrix4x4> matrices = null) =>
            GetPoseMatrix(source.parent, matrices).inverse * GetPoseMatrix(source, matrices);

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
