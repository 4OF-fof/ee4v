using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionPreview : IDisposable
    {
        private static readonly int PreviewControlHash =
            nameof(FaceExpressionPreview).GetHashCode();

        private readonly Action _repaint;
        private PreviewRenderUtility _utility;
        private GameObject _clone;
        private SkinnedMeshRenderer _bodyRenderer;
        private readonly Dictionary<string, RendererPreviewState> _renderers =
            new Dictionary<string, RendererPreviewState>(StringComparer.Ordinal);
        private Vector3 _target;
        private float _distance = 1f;
        private float _yaw;
        private float _pitch;
        private int _dragButton = -1;

        public FaceExpressionPreview(Action repaint)
        {
            _repaint = repaint;
        }

        public void SetAvatar(GameObject avatar)
        {
            Cleanup();
            if (avatar == null)
            {
                return;
            }

            _utility = new PreviewRenderUtility();
            _utility.cameraFieldOfView = 30f;
            _utility.lights[0].intensity = 1.1f;
            _utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            _utility.lights[1].intensity = 0.7f;
            _clone = UnityEngine.Object.Instantiate(avatar);
            _clone.name = avatar.name + " (Face Preview)";
            SetHideFlags(_clone.transform);
            _utility.AddSingleGO(_clone);

            _bodyRenderer = FaceExpressionClipEditor.FindBodyRenderer(_clone);
            foreach (var renderer in _clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null)
                {
                    continue;
                }

                renderer.forceMatrixRecalculationPerRender = true;
                var path = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _clone.transform);
                _renderers[path] = new RendererPreviewState(renderer);
            }

            ResetView();
        }

        public void SetChannels(
            IReadOnlyList<BlendShapeChannel> channels,
            bool repaint = true)
        {
            if (_clone == null)
            {
                return;
            }

            foreach (var renderer in _renderers.Values)
            {
                renderer.Reset();
            }

            if (channels != null)
            {
                for (var index = 0; index < channels.Count; index++)
                {
                    var channel = channels[index];
                    if (channel.IsHeader ||
                        !channel.Animated)
                    {
                        continue;
                    }

                    if (!_renderers.TryGetValue(channel.RendererPath, out var renderer))
                    {
                        continue;
                    }

                    var shapeIndex = renderer.Renderer.sharedMesh.GetBlendShapeIndex(channel.Name);
                    if (shapeIndex >= 0)
                    {
                        renderer.Renderer.SetBlendShapeWeight(
                            shapeIndex,
                            channel.Value);
                    }
                }
            }

            if (repaint)
            {
                _repaint?.Invoke();
            }
        }

        public void ResetView()
        {
            if (_clone == null)
            {
                return;
            }

            var animator = _clone.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    var bounds = CalculateBounds();
                    var centerOffset = Mathf.Max(0.12f, bounds.size.y * 0.2f) * 0.15f;
                    var focusSize = Mathf.Max(0.12f, bounds.size.y * 0.07f);
                    SetView(new Bounds(
                        head.position + Vector3.up * centerOffset,
                        Vector3.one * focusSize));
                    return;
                }
            }

            if (_bodyRenderer != null)
            {
                SetUpperView(_bodyRenderer.bounds);
                return;
            }

            SetView(CalculateBounds());
        }

        public void Draw(Rect rect)
        {
            if (_utility == null || _clone == null || rect.width < 2f || rect.height < 2f)
            {
                EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
                return;
            }

            HandleInput(rect);
            ConfigureCamera();
            _utility.BeginPreview(rect, GUIStyle.none);
            _utility.camera.Render();
            var texture = _utility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        public Texture2D RenderThumbnail(
            IReadOnlyList<BlendShapeChannel> channels,
            int width,
            int height)
        {
            if (_utility == null || _clone == null || width < 2 || height < 2)
            {
                return null;
            }

            SetChannels(channels, false);
            ConfigureCamera();
            _utility.BeginStaticPreview(new Rect(0f, 0f, width, height));
            _utility.camera.Render();
            return _utility.EndStaticPreview();
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void HandleInput(Rect rect)
        {
            var current = Event.current;
            if (current == null)
            {
                return;
            }

            var controlId = GUIUtility.GetControlID(
                PreviewControlHash,
                FocusType.Passive,
                rect);
            if (current.type == EventType.MouseDown &&
                rect.Contains(current.mousePosition) &&
                (current.button == 1 || current.button == 2))
            {
                GUIUtility.hotControl = controlId;
                _dragButton = current.button;
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
                        Mathf.Tan(_utility.cameraFieldOfView * 0.5f * Mathf.Deg2Rad) /
                        Mathf.Max(1f, rect.height);
                    _target +=
                        -_utility.camera.transform.right * current.delta.x * unitsPerPixel +
                        _utility.camera.transform.up * current.delta.y * unitsPerPixel;
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
                current.Use();
                return;
            }

            if (current.type == EventType.ScrollWheel &&
                rect.Contains(current.mousePosition))
            {
                _distance = Mathf.Clamp(
                    _distance * (1f + current.delta.y * 0.05f),
                    0.03f,
                    100f);
                current.Use();
                _repaint?.Invoke();
            }
        }

        private void SetView(Bounds bounds)
        {
            _target = bounds.center;
            var radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            _distance = radius / Mathf.Tan(15f * Mathf.Deg2Rad) * 1.1f;
            _yaw = 0f;
            _pitch = 0f;
            _repaint?.Invoke();
        }

        private void SetUpperView(Bounds bounds)
        {
            var size = Mathf.Max(0.12f, bounds.size.y * 0.25f);
            var center = new Vector3(
                bounds.center.x,
                bounds.max.y - size * 0.5f,
                bounds.center.z);
            SetView(new Bounds(center, Vector3.one * size));
        }

        private void ConfigureCamera()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var direction = _clone.transform.rotation * rotation * Vector3.forward;
            var up = _clone.transform.rotation * rotation * Vector3.up;
            _utility.camera.transform.position = _target + direction * _distance;
            _utility.camera.transform.rotation = Quaternion.LookRotation(-direction, up);
            _utility.camera.nearClipPlane = Mathf.Max(0.001f, _distance * 0.01f);
            _utility.camera.farClipPlane = Mathf.Max(100f, _distance * 20f);
        }

        private Bounds CalculateBounds()
        {
            var hasBounds = false;
            var bounds = new Bounds(_clone.transform.position, Vector3.one * 0.2f);
            foreach (var renderer in _clone.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static void SetHideFlags(Transform transform)
        {
            transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (var index = 0; index < transform.childCount; index++)
            {
                SetHideFlags(transform.GetChild(index));
            }
        }

        private void Cleanup()
        {
            _bodyRenderer = null;
            _renderers.Clear();
            if (_utility != null)
            {
                _utility.Cleanup();
                _utility = null;
            }

            if (_clone != null)
            {
                UnityEngine.Object.DestroyImmediate(_clone);
                _clone = null;
            }
        }

        private sealed class RendererPreviewState
        {
            private readonly float[] _defaultWeights;

            internal RendererPreviewState(SkinnedMeshRenderer renderer)
            {
                Renderer = renderer;
                _defaultWeights = new float[renderer.sharedMesh.blendShapeCount];
                for (var index = 0; index < _defaultWeights.Length; index++)
                {
                    _defaultWeights[index] = renderer.GetBlendShapeWeight(index);
                }
            }

            internal SkinnedMeshRenderer Renderer { get; }

            internal void Reset()
            {
                for (var index = 0; index < _defaultWeights.Length; index++)
                {
                    Renderer.SetBlendShapeWeight(index, _defaultWeights[index]);
                }
            }
        }
    }
}
