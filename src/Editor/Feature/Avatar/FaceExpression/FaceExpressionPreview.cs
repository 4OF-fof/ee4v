using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionPreview : IDisposable
    {
        private static readonly int PreviewControlHash =
            nameof(FaceExpressionPreview).GetHashCode();
        private static readonly object ChannelUpdateKey = new object();

        private readonly PreviewOrbitController _orbit;
        private readonly PreviewUpdateScheduler _previewUpdates;
        private PreviewRenderUtility _utility;
        private GameObject _clone;
        private SkinnedMeshRenderer _bodyRenderer;
        private readonly Dictionary<string, RendererPreviewState> _renderers =
            new Dictionary<string, RendererPreviewState>(StringComparer.Ordinal);
        public FaceExpressionPreview(Action repaint)
        {
            _orbit = new PreviewOrbitController(
                PreviewControlHash,
                repaint);
            _previewUpdates = new PreviewUpdateScheduler(null, repaint);
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

            if (!repaint)
            {
                ApplyChannels(channels);
                return;
            }

            _previewUpdates.Enqueue(
                ChannelUpdateKey,
                () => ApplyChannels(channels));
        }

        private void ApplyChannels(
            IReadOnlyList<BlendShapeChannel> channels)
        {
            foreach (var renderer in _renderers.Values)
            {
                renderer.BeginApply();
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

                    renderer.Apply(channel.Name, channel.Value);
                }
            }

            foreach (var renderer in _renderers.Values)
            {
                renderer.CompleteApply();
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

            _orbit.HandleInput(
                rect,
                _utility.camera,
                _utility.cameraFieldOfView);
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
            if (_utility == null ||
                _clone == null ||
                _renderers.Count == 0 ||
                width < 2 ||
                height < 2)
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
            _previewUpdates.Dispose();
        }

        private void SetView(Bounds bounds)
        {
            var radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            _orbit.Reset(
                bounds.center,
                radius / Mathf.Tan(15f * Mathf.Deg2Rad) * 1.1f);
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
            _orbit.ConfigureCamera(
                _utility.camera,
                _clone.transform.rotation);
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
            _previewUpdates.CancelPending();
            _orbit.CancelInteraction();
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
            private readonly float[] _currentWeights;
            private readonly Dictionary<string, int> _shapeIndices =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly HashSet<int> _activeIndices =
                new HashSet<int>();
            private readonly HashSet<int> _nextIndices =
                new HashSet<int>();

            internal RendererPreviewState(SkinnedMeshRenderer renderer)
            {
                Renderer = renderer;
                _defaultWeights = new float[renderer.sharedMesh.blendShapeCount];
                _currentWeights = new float[_defaultWeights.Length];
                for (var index = 0; index < _defaultWeights.Length; index++)
                {
                    _defaultWeights[index] = renderer.GetBlendShapeWeight(index);
                    _currentWeights[index] = _defaultWeights[index];
                    _shapeIndices[
                        renderer.sharedMesh.GetBlendShapeName(index)] = index;
                }
            }

            internal SkinnedMeshRenderer Renderer { get; }

            internal void BeginApply()
            {
                _nextIndices.Clear();
            }

            internal void Apply(string shapeName, float weight)
            {
                if (!_shapeIndices.TryGetValue(
                        shapeName ?? string.Empty,
                        out var index))
                {
                    return;
                }

                _nextIndices.Add(index);
                SetWeight(index, weight);
            }

            internal void CompleteApply()
            {
                foreach (var index in _activeIndices)
                {
                    if (!_nextIndices.Contains(index))
                    {
                        SetWeight(index, _defaultWeights[index]);
                    }
                }

                _activeIndices.Clear();
                _activeIndices.UnionWith(_nextIndices);
            }

            private void SetWeight(int index, float weight)
            {
                if (Mathf.Approximately(_currentWeights[index], weight))
                {
                    return;
                }

                Renderer.SetBlendShapeWeight(index, weight);
                _currentWeights[index] = weight;
            }
        }
    }
}
