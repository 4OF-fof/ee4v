using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed partial class PrefabScenePreview
    {
        public void ResetView()
        {
            if (_instance == null)
            {
                return;
            }

            FrameCurrentSelection(true);
        }

        private void FrameCurrentSelection(bool animate = false)
        {
            if (_instance == null)
            {
                return;
            }

            ApplyPendingUpdates();
            if (!_focusedBodyPart.HasValue ||
                _focusedBodyPart.Value == BodyPartCategory.Other)
            {
                FrameWholeAvatar(animate);
                return;
            }

            var part = _focusedBodyPart.Value;
            var avatarHeight = part == BodyPartCategory.Head ||
                part == BodyPartCategory.Legs
                    ? Mathf.Max(0.2f, GetAvatarFocusBounds().size.y)
                    : GetAvatarReferenceHeight();
            var leftSide = IsLeftSide(part);
            if (!TryGetBoneFocusBounds(part, avatarHeight, leftSide,
                    out var focusBounds))
            {
                FrameWholeAvatar(animate);
                return;
            }

            var minimumHalfView = avatarHeight * GetMinimumHalfView(part);
            if (part == BodyPartCategory.Head &&
                TryGetHeadFaceFraming(out var faceCenter,
                    out var faceHeight, out var faceWidth))
            {
                var up = _instance.transform.up;
                var right = _instance.transform.right;
                var offset = faceCenter - focusBounds.center;
                focusBounds.center += up * Vector3.Dot(offset, up) +
                    right * Vector3.Dot(offset, right);
                var previewRect = _viewport.PreviewRect;
                var aspect = previewRect.height > 1f
                    ? previewRect.width / previewRect.height
                    : 1f;
                minimumHalfView = Mathf.Max(faceHeight,
                    faceWidth / Mathf.Max(0.01f, aspect)) *
                    HeadFaceViewScale;
            }

            if (part == BodyPartCategory.Chest)
            {
                focusBounds.center += _instance.transform.up *
                    avatarHeight * 0.14f;
            }

            var viewAngles = GetViewAngles(part, leftSide);
            if (IsBackView(part))
            {
                viewAngles.x += 180f;
                if (part == BodyPartCategory.Waist)
                {
                    viewAngles.y = 28f;
                }
            }
            FrameBounds(
                focusBounds,
                minimumHalfView,
                viewAngles,
                animate,
                GetDistanceScale(part));
        }

        private void FrameWholeAvatar(bool animate)
        {
            var bounds = GetAvatarFocusBounds();
            var appearanceFraming = _flexibleLayout && !_fitWholeAvatar;
            if (appearanceFraming)
            {
                bounds.center -= _instance.transform.up *
                    bounds.size.y * AppearanceFullBodyVerticalOffsetScale;
            }
            FrameBounds(
                bounds,
                0f,
                _flexibleLayout && _wholeBackView
                    ? new Vector2(180f, 0f)
                    : Vector2.zero,
                animate,
                appearanceFraming
                    ? AppearanceFullBodyDistanceScale
                    : 1f);
        }

        private float GetAvatarReferenceHeight()
        {
            var animator = _instance
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => candidate != null &&
                    IsInFocusScope(candidate.transform) &&
                    candidate.avatar != null &&
                    candidate.avatar.isHuman && candidate.isHuman);
            if (animator != null)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (head != null && foot != null)
                {
                    var height = Vector3.Dot(
                        head.position - foot.position,
                        _instance.transform.up) + 0.18f;
                    if (height > 0.3f)
                    {
                        return height;
                    }
                }
            }

            return Mathf.Max(0.2f, GetAvatarFocusBounds().size.y);
        }

        private Bounds GetAvatarFocusBounds()
        {
            return CalculateBounds(_instance.transform.position,
                _renderers.Where(renderer => renderer != null && IsInAvatarFocusScope(renderer))
                    .Select(renderer => _utility.ResolveRenderer(renderer)).ToArray());
        }

        private bool IsInAvatarFocusScope(Renderer renderer)
        {
            if (!IsInScope(renderer, null)) { return false; }
            for (var current = renderer.transform; current != null; current = current.parent)
            {
                if (current.CompareTag("EditorOnly") && !_temporarilyEnabledEditorOnlyParts.Contains(current))
                {
                    return false;
                }
                if (current == _instance.transform) { break; }
            }
            if (_hiddenPartKeys.Count == 0) { return true; }
            var path = new Stack<int>();
            for (var current = renderer.transform; current != null && current != _instance.transform;
                 current = current.parent)
            {
                path.Push(current.GetSiblingIndex());
            }
            var key = string.Join("/", path);
            return !_hiddenPartKeys.Any(hidden => key == hidden ||
                key.StartsWith(hidden + "/", StringComparison.Ordinal));
        }

        private void FrameBounds(
            Bounds bounds,
            float minimumHalfView,
            Vector2 viewAngles,
            bool animate,
            float distanceScale = 1f)
        {
            if (_utility == null)
            {
                return;
            }

            var previewRect = _viewport.PreviewRect;
            var aspect = previewRect.height > 1f
                ? previewRect.width / previewRect.height
                : 1f;
            var halfViewSize = Mathf.Max(
                minimumHalfView,
                bounds.extents.y,
                bounds.extents.x / Mathf.Max(0.01f, aspect));
            var distance = bounds.extents.z +
                Mathf.Max(0.05f, halfViewSize) /
                Mathf.Tan(_utility.FieldOfView * 0.5f *
                          Mathf.Deg2Rad) * PreviewFitPadding *
                distanceScale;
            if (animate)
            {
                _orbit.AnimateTo(
                    bounds.center,
                    distance,
                    viewAngles.x,
                    viewAngles.y,
                    EditorApplication.timeSinceStartup,
                    CameraTransitionDuration);
                StartCameraAnimation();
            }
            else
            {
                StopCameraAnimation();
                _orbit.SetView(
                    bounds.center,
                    distance,
                    viewAngles.x,
                    viewAngles.y);
            }
        }

        private void AdvanceCameraAnimation()
        {
            if (_instance == null || panel == null)
            {
                StopCameraAnimation();
                return;
            }

            if (_orbit.UpdateTransition(EditorApplication.timeSinceStartup))
            {
                RequestPreviewRepaint();
            }
            if (!_orbit.IsTransitioning)
            {
                StopCameraAnimation();
            }
        }

        private void StartCameraAnimation()
        {
            if (_cameraAnimationSubscribed)
            {
                return;
            }

            EditorApplication.update += AdvanceCameraAnimation;
            _cameraAnimationSubscribed = true;
        }

        private void StopCameraAnimation()
        {
            if (!_cameraAnimationSubscribed)
            {
                return;
            }

            EditorApplication.update -= AdvanceCameraAnimation;
            _cameraAnimationSubscribed = false;
        }

        private bool TryGetBoneFocusBounds(
            BodyPartCategory part,
            float avatarHeight,
            bool leftSide,
            out Bounds bounds)
        {
            bounds = default;
            var animator = _instance
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    IsInFocusScope(candidate.transform) &&
                    HasFocusBone(candidate, part, IsInFocusScope));
            Vector3[] points;
            if (animator != null)
            {
                points = GetFocusBones(part, leftSide)
                    .Select(animator.GetBoneTransform)
                    .Where(bone => bone != null && IsInFocusScope(bone))
                    .Select(bone => bone.position)
                    .ToArray();
                if (points.Length == 0)
                {
                    points = GetFocusBones(part, !leftSide)
                        .Select(animator.GetBoneTransform)
                        .Where(bone => bone != null && IsInFocusScope(bone))
                        .Select(bone => bone.position)
                        .ToArray();
                }
            }
            else
            {
                points = GetSkinnedFocusBones(
                        _instance, part, IsInFocusScope, leftSide)
                    .Select(bone => bone.position)
                    .ToArray();
                if (points.Length == 0)
                {
                    points = GetSkinnedFocusBones(
                            _instance, part, IsInFocusScope, !leftSide)
                        .Select(bone => bone.position)
                        .ToArray();
                }
            }
            if (points.Length == 0)
            {
                return false;
            }

            bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
            {
                bounds.Encapsulate(point);
            }

            if (part == BodyPartCategory.Head)
            {
                bounds.Encapsulate(points[0] +
                    _instance.transform.up * avatarHeight * 0.1f);
            }
            bounds.Expand(avatarHeight * 0.06f);
            return true;
        }

        private bool TryGetHeadFaceFraming(
            out Vector3 faceCenter,
            out float faceHeight,
            out float faceWidth)
        {
            faceCenter = default;
            faceHeight = 0f;
            faceWidth = 0f;
            var animator = _instance
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => candidate != null &&
                    IsInFocusScope(candidate.transform) &&
                    candidate.avatar != null &&
                    candidate.avatar.isHuman && candidate.isHuman);
            var head = animator != null
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            if (head == null || !IsInFocusScope(head))
            {
                return false;
            }

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var up = _instance.transform.up;
            var right = _instance.transform.right;
            var bestCount = 0;
            var bestHasHips = false;
            var bestMin = 0f;
            var bestMax = 0f;
            var bestLeft = 0f;
            var bestRight = 0f;
            foreach (var renderer in _renderers.OfType<SkinnedMeshRenderer>())
            {
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    !IsInAvatarFocusScope(renderer) ||
                    renderer.sharedMesh == null ||
                    !renderer.sharedMesh.isReadable)
                {
                    continue;
                }

                var bones = renderer.bones;
                var headIndex = Array.IndexOf(bones, head);
                if (headIndex < 0)
                {
                    continue;
                }

                var weights = renderer.sharedMesh.boneWeights;
                if (weights.Length != renderer.sharedMesh.vertexCount)
                {
                    continue;
                }

                var baked = new Mesh();
                var count = 0;
                var minimum = float.MaxValue;
                var maximum = float.MinValue;
                var left = float.MaxValue;
                var rightmost = float.MinValue;
                try
                {
                    renderer.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    if (vertices.Length != weights.Length)
                    {
                        continue;
                    }
                    for (var index = 0; index < vertices.Length; index++)
                    {
                        var weight = weights[index];
                        var headWeight = 0f;
                        if (weight.boneIndex0 == headIndex)
                        {
                            headWeight += weight.weight0;
                        }
                        if (weight.boneIndex1 == headIndex)
                        {
                            headWeight += weight.weight1;
                        }
                        if (weight.boneIndex2 == headIndex)
                        {
                            headWeight += weight.weight2;
                        }
                        if (weight.boneIndex3 == headIndex)
                        {
                            headWeight += weight.weight3;
                        }
                        if (headWeight < 0.5f)
                        {
                            continue;
                        }

                        var point = renderer.transform.TransformPoint(
                            vertices[index]);
                        var height = Vector3.Dot(point - head.position, up);
                        var horizontal = Vector3.Dot(
                            point - head.position, right);
                        minimum = Mathf.Min(minimum, height);
                        maximum = Mathf.Max(maximum, height);
                        left = Mathf.Min(left, horizontal);
                        rightmost = Mathf.Max(rightmost, horizontal);
                        count++;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(baked);
                }

                var hasHips = hips != null && Array.IndexOf(bones, hips) >= 0;
                if (count < 32 ||
                    (bestCount > 0 &&
                     (bestHasHips && !hasHips ||
                      bestHasHips == hasHips && count <= bestCount)))
                {
                    continue;
                }

                bestCount = count;
                bestHasHips = hasHips;
                bestMin = minimum;
                bestMax = maximum;
                bestLeft = left;
                bestRight = rightmost;
            }

            if (bestCount == 0 || bestMax - bestMin < 0.01f)
            {
                return false;
            }

            faceCenter = head.position +
                up * ((bestMin + bestMax) * 0.5f) +
                right * ((bestLeft + bestRight) * 0.5f);
            faceHeight = bestMax - bestMin;
            faceWidth = bestRight - bestLeft;
            return true;
        }

        public static bool HasFocusBone(
            GameObject root,
            BodyPartCategory part,
            Func<Transform, bool> isInScope)
        {
            if (root == null)
            {
                return false;
            }

            return root.GetComponentsInChildren<Animator>(true)
                       .Any(animator => isInScope(animator.transform) &&
                           HasFocusBone(animator, part, isInScope)) ||
                   GetSkinnedFocusBones(root, part, isInScope).Length > 0;
        }

        public static bool HasFocusBone(
            Animator animator,
            BodyPartCategory part,
            Func<Transform, bool> isInScope)
        {
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isHuman || !animator.isHuman)
            {
                return false;
            }

            return GetFocusBones(part, true)
                .Concat(GetFocusBones(part, false))
                .Select(animator.GetBoneTransform)
                .Any(bone => bone != null && isInScope(bone));
        }

        private static Transform[] GetSkinnedFocusBones(
            GameObject root,
            BodyPartCategory part,
            Func<Transform, bool> isInScope,
            bool? leftSide = null)
        {
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => isInScope(renderer.transform))
                // Clothing renderers can use bones outside their Prefab tab.
                .SelectMany(renderer => renderer.bones ??
                    Array.Empty<Transform>())
                .Where(bone => bone != null &&
                    MatchesFocusBoneName(bone.name, part) &&
                    (!leftSide.HasValue ||
                     MatchesBoneSide(bone.name, leftSide.Value)))
                .Distinct()
                .ToArray();
        }

        private static bool MatchesFocusBoneName(
            string name,
            BodyPartCategory part)
        {
            var words = PrefabHierarchyUtility.SplitName(name);
            switch (part)
            {
                case BodyPartCategory.Head:
                    return HasBoneWord(words, "head", "neck") ||
                           HasBoneText(name, "頭", "首");
                case BodyPartCategory.Chest:
                    return HasBoneWord(words, "chest", "spine") ||
                           HasBoneText(name, "胸", "背骨");
                case BodyPartCategory.Waist:
                    return HasBoneWord(words, "hips", "hip", "pelvis",
                               "spine", "waist") ||
                           HasBoneText(name, "腰", "骨盤", "背骨");
                case BodyPartCategory.Shoulders:
                case BodyPartCategory.Arms:
                    return HasBoneWord(words, "shoulder", "arm", "elbow",
                               "forearm") ||
                           HasBoneText(name, "肩", "腕", "肘");
                case BodyPartCategory.Hands:
                    return HasBoneWord(words, "hand", "wrist", "finger",
                               "thumb", "index", "middle", "ring",
                               "little") ||
                           HasBoneText(name, "手", "指");
                case BodyPartCategory.Legs:
                    return HasBoneWord(words, "leg", "thigh", "calf",
                               "knee", "shin", "foot") ||
                           HasBoneText(name, "脚", "腿", "膝");
                case BodyPartCategory.Feet:
                    return HasBoneWord(words, "foot", "toe", "toes",
                               "ankle") ||
                           HasBoneText(name, "足", "つま先", "足首");
                default:
                    return false;
            }
        }

        private static bool MatchesBoneSide(string name, bool leftSide)
        {
            var words = PrefabHierarchyUtility.SplitName(name);
            var left = HasBoneWord(words, "left", "l");
            var right = HasBoneWord(words, "right", "r");
            return left == right || (leftSide ? left : right);
        }

        private static bool HasBoneWord(
            IReadOnlyCollection<string> words,
            params string[] terms)
        {
            return words.Any(word => terms.Contains(word));
        }

        private static bool HasBoneText(
            string name,
            params string[] terms)
        {
            return terms.Any(term => name.IndexOf(
                term, StringComparison.Ordinal) >= 0);
        }

        private bool IsInFocusScope(Transform target)
        {
            return _instance != null && target != null &&
                (target == _instance.transform || target.IsChildOf(_instance.transform));
        }

        private static HumanBodyBones[] GetFocusBones(
            BodyPartCategory part,
            bool leftSide)
        {
            switch (part)
            {
                case BodyPartCategory.Head:
                    return new[] { HumanBodyBones.Head,
                        HumanBodyBones.Neck };
                case BodyPartCategory.Chest:
                    return new[] { HumanBodyBones.Spine,
                        HumanBodyBones.Chest,
                        HumanBodyBones.UpperChest };
                case BodyPartCategory.Waist:
                    return new[] { HumanBodyBones.Hips,
                        HumanBodyBones.Spine };
                case BodyPartCategory.Shoulders:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftShoulder,
                            HumanBodyBones.LeftUpperArm }
                        : new[] { HumanBodyBones.RightShoulder,
                            HumanBodyBones.RightUpperArm };
                case BodyPartCategory.Arms:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftUpperArm,
                            HumanBodyBones.LeftLowerArm,
                            HumanBodyBones.LeftHand }
                        : new[] { HumanBodyBones.RightUpperArm,
                            HumanBodyBones.RightLowerArm,
                            HumanBodyBones.RightHand };
                case BodyPartCategory.Hands:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftHand,
                            HumanBodyBones.LeftMiddleProximal,
                            HumanBodyBones.LeftMiddleDistal }
                        : new[] { HumanBodyBones.RightHand,
                            HumanBodyBones.RightMiddleProximal,
                            HumanBodyBones.RightMiddleDistal };
                case BodyPartCategory.Legs:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftUpperLeg,
                            HumanBodyBones.LeftLowerLeg,
                            HumanBodyBones.LeftFoot }
                        : new[] { HumanBodyBones.RightUpperLeg,
                            HumanBodyBones.RightLowerLeg,
                            HumanBodyBones.RightFoot };
                case BodyPartCategory.Feet:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftFoot,
                            HumanBodyBones.LeftToes }
                        : new[] { HumanBodyBones.RightFoot,
                            HumanBodyBones.RightToes };
                default:
                    return Array.Empty<HumanBodyBones>();
            }
        }

        private static float GetMinimumHalfView(BodyPartCategory part)
        {
            switch (part)
            {
                case BodyPartCategory.Head:
                    return 0.145f;
                case BodyPartCategory.Chest:
                    return 0.30f;
                case BodyPartCategory.Waist:
                    return 0.21f;
                case BodyPartCategory.Shoulders:
                    return 0.21f;
                case BodyPartCategory.Arms:
                    return 0.21f;
                case BodyPartCategory.Legs:
                    return 0.28f;
                case BodyPartCategory.Hands:
                case BodyPartCategory.Feet:
                    return 0.15f;
                default:
                    return 0.5f;
            }
        }

        private static float GetDistanceScale(BodyPartCategory part)
        {
            switch (part)
            {
                case BodyPartCategory.Chest:
                    return 0.82f;
                case BodyPartCategory.Waist:
                case BodyPartCategory.Shoulders:
                case BodyPartCategory.Arms:
                case BodyPartCategory.Hands:
                case BodyPartCategory.Feet:
                    return 1.045f;
                default:
                    return 1f;
            }
        }

        private static Vector2 GetViewAngles(
            BodyPartCategory part,
            bool leftSide)
        {
            switch (part)
            {
                case BodyPartCategory.Head:
                    return new Vector2(-12f, 3f);
                case BodyPartCategory.Chest:
                    return new Vector2(-30f, 13f);
                case BodyPartCategory.Waist:
                    return new Vector2(-38f, 18f);
                case BodyPartCategory.Shoulders:
                case BodyPartCategory.Arms:
                    return new Vector2(leftSide ? -38f : 38f, -18f);
                case BodyPartCategory.Hands:
                    return new Vector2(leftSide ? -38f : 38f, -18f);
                case BodyPartCategory.Legs:
                    return new Vector2(-13f, 4f);
                case BodyPartCategory.Feet:
                    return new Vector2(-30f, -20f);
                default:
                    return Vector2.zero;
            }
        }
    }
}
