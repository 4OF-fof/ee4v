using System;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEngine;

namespace Ee4v.UI
{
    public sealed partial class PrefabScenePreview
    {
        private AnimationClip _bodyPartAnimation;
        private BodyPartCategory? _bodyPartAnimationPart;
        private bool _bodyPartAnimationLoop;
        private double _bodyPartAnimationStartedAt;

        private void UnsubscribePreviewMotion()
        {
            AvatarPreviewMotionSettings.Changed -= OnPreviewMotionSettingsChanged;
            EditorApplication.playModeStateChanged -= OnPreviewMotionPlayModeChanged;
        }

        private void OnPreviewMotionSettingsChanged()
        {
            if (_animationSampler == null) ApplyBodyPartPose();
            RequestPreviewRepaint();
        }

        private void OnPreviewMotionPlayModeChanged(PlayModeStateChange state)
        {
            if (_utility == null) return;
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _utility.SetAnimation(null);
                _bodyPartAnimation = null;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (_animationSampler != null) _utility.SetAnimationSampler(_animationSampler);
                ApplyBodyPartPose();
            }
            RequestPreviewRepaint();
        }

        private void ApplyBodyPartMotion()
        {
            if (_utility == null) return;
            // A feature's explicit sampler (for example ExMenu) owns the preview while active.
            if (_animationSampler != null)
            {
                _bodyPartAnimation = null;
                return;
            }
            var selection = AvatarPreviewMotionSettings.Get(_focusedBodyPart);
            var clip = _bodyPartPoseEnabled && !_tPose && !EditorApplication.isPlayingOrWillChangePlaymode
                ? AvatarPreviewMotionSettings.ResolveClip(selection) : null;
            if (_bodyPartAnimation == clip && _utility.Animation == clip &&
                _bodyPartAnimationPart == _focusedBodyPart && _bodyPartAnimationLoop == selection.Loop) return;
            try
            {
                if (clip != null || _utility.Animation != null) _utility.SetAnimation(clip, selection.Loop);
                _bodyPartAnimation = clip;
                _bodyPartAnimationPart = _focusedBodyPart;
                _bodyPartAnimationLoop = selection.Loop;
                _bodyPartAnimationStartedAt = EditorApplication.timeSinceStartup;
            }
            catch (Exception exception)
            {
                _utility.SetAnimation(null);
                _bodyPartAnimation = null;
                Debug.LogWarning("ee4v: Cannot play the selected body-part preview animation: " + exception.Message);
            }
        }

        private void UpdateBodyPartAnimationTime()
        {
            if (_bodyPartAnimation == null || _utility == null || _animationSampler != null ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            var duration = _bodyPartAnimation.length;
            var elapsed = (float)(EditorApplication.timeSinceStartup - _bodyPartAnimationStartedAt);
            var time = duration <= 0f ? 0f : _bodyPartAnimationLoop
                ? Mathf.Repeat(elapsed, duration) : Mathf.Min(elapsed, duration);
            _utility.SampleAnimation(time);
        }
    }
}
