using System;
using UnityEngine;

namespace Ee4v.UI
{
    public sealed class PreviewOrbitController
    {
        private const float MinimumDistance = 0.03f;
        private const float MaximumDistance = 100f;
        private readonly int _controlHint;
        private readonly Action _repaint;
        private float _distance = 1f;
        private float _yaw;
        private float _pitch;
        private Vector3 _target;
        private Vector3 _transitionStartTarget;
        private Vector3 _transitionEndTarget;
        private float _transitionStartDistance;
        private float _transitionEndDistance;
        private float _transitionStartYaw;
        private float _transitionEndYaw;
        private float _transitionStartPitch;
        private float _transitionEndPitch;
        private double _transitionStartTime;
        private float _transitionDuration;
        private bool _isTransitioning;
        private int _dragButton = -1;
        private int _hotControlId;

        public bool IsTransitioning => _isTransitioning;

        public PreviewOrbitController(
            int controlHint,
            Action repaint)
        {
            _controlHint = controlHint;
            _repaint = repaint;
        }

        public void Reset(Vector3 target, float distance)
        {
            SetView(target, distance, 0f, 0f);
        }

        public void SetView(
            Vector3 target,
            float distance,
            float yaw,
            float pitch)
        {
            CancelInteraction();
            CancelTransition();
            _target = target;
            _distance = Mathf.Clamp(
                distance,
                MinimumDistance,
                MaximumDistance);
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -80f, 80f);
            _repaint?.Invoke();
        }

        public void AnimateTo(
            Vector3 target,
            float distance,
            float yaw,
            float pitch,
            double startTime,
            float duration)
        {
            UpdateTransition(startTime);
            CancelInteraction();
            _transitionStartTarget = _target;
            _transitionEndTarget = target;
            _transitionStartDistance = _distance;
            _transitionEndDistance = Mathf.Clamp(
                distance, MinimumDistance, MaximumDistance);
            _transitionStartYaw = _yaw;
            _transitionEndYaw = _yaw + Mathf.DeltaAngle(_yaw, yaw);
            _transitionStartPitch = _pitch;
            _transitionEndPitch = Mathf.Clamp(pitch, -80f, 80f);
            _transitionStartTime = startTime;
            _transitionDuration = Mathf.Max(0.01f, duration);
            _isTransitioning = true;
            _repaint?.Invoke();
        }

        public bool UpdateTransition(double time)
        {
            if (!_isTransitioning)
            {
                return false;
            }

            var progress = Mathf.Clamp01((float)
                ((time - _transitionStartTime) / _transitionDuration));
            var eased = progress * progress * (3f - 2f * progress);
            _target = Vector3.Lerp(
                _transitionStartTarget, _transitionEndTarget, eased);
            _distance = Mathf.Lerp(
                _transitionStartDistance, _transitionEndDistance, eased);
            _yaw = Mathf.Lerp(
                _transitionStartYaw, _transitionEndYaw, eased);
            _pitch = Mathf.Lerp(
                _transitionStartPitch, _transitionEndPitch, eased);
            if (progress >= 1f)
            {
                _isTransitioning = false;
            }
            return true;
        }

        public void CancelTransition()
        {
            _isTransitioning = false;
        }

        public void CancelInteraction()
        {
            if (_dragButton < 0)
            {
                return;
            }

            if (GUIUtility.hotControl == _hotControlId)
            {
                GUIUtility.hotControl = 0;
            }

            _dragButton = -1;
            _hotControlId = 0;
        }

        public void HandleInput(
            Rect rect,
            Camera camera,
            float fieldOfView)
        {
            var current = Event.current;
            if (current == null || camera == null)
            {
                return;
            }

            var controlId = GUIUtility.GetControlID(
                _controlHint,
                FocusType.Passive,
                rect);
            if (current.type == EventType.MouseDown &&
                rect.Contains(current.mousePosition) &&
                (current.button == 1 || current.button == 2))
            {
                CancelTransition();
                GUIUtility.hotControl = controlId;
                _dragButton = current.button;
                _hotControlId = controlId;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag &&
                GUIUtility.hotControl == controlId)
            {
                if (_dragButton == 1)
                {
                    _yaw += current.delta.x * 0.5f;
                    _pitch = Mathf.Clamp(
                        _pitch - current.delta.y * 0.5f,
                        -80f,
                        80f);
                }
                else if (_dragButton == 2)
                {
                    var unitsPerPixel = 2f * _distance *
                        Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) /
                        Mathf.Max(1f, rect.height);
                    _target +=
                        -camera.transform.right *
                        current.delta.x * unitsPerPixel +
                        camera.transform.up *
                        current.delta.y * unitsPerPixel;
                }

                current.Use();
                _repaint?.Invoke();
                return;
            }

            if (current.type == EventType.MouseUp &&
                GUIUtility.hotControl == controlId &&
                current.button == _dragButton)
            {
                GUIUtility.hotControl = 0;
                _dragButton = -1;
                _hotControlId = 0;
                current.Use();
                return;
            }

            if (current.type == EventType.ScrollWheel &&
                rect.Contains(current.mousePosition))
            {
                CancelTransition();
                _distance = Mathf.Clamp(
                    _distance * (1f + current.delta.y * 0.05f),
                    MinimumDistance,
                    MaximumDistance);
                current.Use();
                _repaint?.Invoke();
            }
        }

        public void ConfigureCamera(
            Camera camera,
            Quaternion rootRotation)
        {
            if (camera == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var direction = rootRotation * rotation * Vector3.forward;
            var up = rootRotation * rotation * Vector3.up;
            camera.transform.position = _target + direction * _distance;
            camera.transform.rotation =
                Quaternion.LookRotation(-direction, up);
            camera.nearClipPlane =
                Mathf.Max(0.001f, _distance * 0.01f);
            camera.farClipPlane =
                Mathf.Max(100f, _distance * 20f);
        }
    }
}
