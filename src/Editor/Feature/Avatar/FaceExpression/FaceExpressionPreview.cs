using System;
using System.Collections.Generic;
using Ee4v.Core.Preview;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionPreview : IDisposable
    {
        private static readonly int PreviewControlHash =
            nameof(FaceExpressionPreview).GetHashCode();

        private readonly Action _repaint;
        private readonly PreviewOrbitController _orbit;
        private AvatarPreviewRenderer _utility;
        private AvatarPreviewRenderer _thumbnailUtility;
        private GameObject _sourceAvatar;
        private GameObject _avatar;
        private SkinnedMeshRenderer _bodyRenderer;
        private readonly Dictionary<string, SkinnedMeshRenderer> _renderers =
            new Dictionary<string, SkinnedMeshRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, SkinnedMeshRenderer> _thumbnailRenderers =
            new Dictionary<string, SkinnedMeshRenderer>(StringComparer.Ordinal);
        public FaceExpressionPreview(Action repaint)
        {
            _repaint = repaint;
            _orbit = new PreviewOrbitController(
                PreviewControlHash,
                repaint);
        }

        public void SetAvatar(GameObject avatar)
        {
            Cleanup();
            if (avatar == null)
            {
                return;
            }

            _utility = new AvatarPreviewRenderer(avatar);
            _sourceAvatar = avatar;
            _avatar = _utility.Root;
            _bodyRenderer = FaceExpressionClipEditor.FindBodyRenderer(_avatar);
            foreach (var renderer in _avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                var path = AnimationUtility.CalculateTransformPath(renderer.transform, _avatar.transform);
                _renderers[path] = renderer;
            }

            ResetView();
        }

        public void SetChannels(
            IReadOnlyList<BlendShapeChannel> channels,
            bool repaint = true)
        {
            if (_avatar == null)
            {
                return;
            }

            ApplyChannels(_utility, _renderers, channels);
            if (repaint) _repaint?.Invoke();
        }

        private static void ApplyChannels(AvatarPreviewRenderer utility,
            IReadOnlyDictionary<string, SkinnedMeshRenderer> renderers, IReadOnlyList<BlendShapeChannel> channels)
        {
            utility.ClearOverrides();

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

                    if (!renderers.TryGetValue(channel.RendererPath, out var renderer))
                    {
                        continue;
                    }

                    var shapeIndex = renderer.sharedMesh
                        .GetBlendShapeIndex(channel.Name);
                    if (shapeIndex >= 0)
                    {
                        utility.SetBlendShapeWeight(renderer, channel.Name, channel.Value);
                    }
                }
            }

        }

        public void ResetView()
        {
            if (_avatar == null)
            {
                return;
            }

            var animator = _avatar.GetComponentInChildren<Animator>();
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
                SetUpperView(_utility.GetBounds(_bodyRenderer));
                return;
            }

            SetView(CalculateBounds());
        }

        public void Draw(Rect rect)
        {
            if (_utility == null || _avatar == null || rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            _orbit.HandleInput(
                rect,
                _utility.Camera,
                _utility.FieldOfView);
            ConfigureCamera();
            var texture = _utility.Render(rect);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }

        public Texture2D RenderThumbnail(
            IReadOnlyList<BlendShapeChannel> channels,
            int width,
            int height)
        {
            if (_utility == null ||
                _avatar == null ||
                _renderers.Count == 0 ||
                width < 2 ||
                height < 2)
            {
                return null;
            }

            ConfigureCamera();
            if (_thumbnailUtility == null)
            {
                _thumbnailUtility = new AvatarPreviewRenderer(_sourceAvatar, isolatedSnapshot: true);
                foreach (var renderer in _thumbnailUtility.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh == null) continue;
                    _thumbnailRenderers[AnimationUtility.CalculateTransformPath(renderer.transform, _thumbnailUtility.Root.transform)] = renderer;
                }
            }
            _thumbnailUtility.Camera.transform.SetPositionAndRotation(_utility.Camera.transform.position, _utility.Camera.transform.rotation);
            _thumbnailUtility.Camera.nearClipPlane = _utility.Camera.nearClipPlane;
            _thumbnailUtility.Camera.farClipPlane = _utility.Camera.farClipPlane;
            ApplyChannels(_thumbnailUtility, _thumbnailRenderers, channels);
            var texture = _thumbnailUtility.Render(new Rect(0f, 0f, width, height));
            var previous = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            RenderTexture readback = null;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                readback = RenderTexture.GetTemporary(
                    width, height, 0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(texture, readback);
                RenderTexture.active = readback;
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                result.Apply(false, false);
                return result;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(result);
                throw;
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.active = previous;
                if (readback != null)
                {
                    RenderTexture.ReleaseTemporary(readback);
                }
            }
        }

        public void Dispose()
        {
            Cleanup();
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
                _utility.Camera,
                _avatar.transform.rotation);
        }

        private Bounds CalculateBounds()
        {
            var hasBounds = false;
            var bounds = new Bounds(_avatar.transform.position, Vector3.one * 0.2f);
            foreach (var renderer in _avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = _utility.GetBounds(renderer);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(_utility.GetBounds(renderer));
                }
            }

            return bounds;
        }

        private void Cleanup()
        {
            _orbit.CancelInteraction();
            _bodyRenderer = null;
            _renderers.Clear();
            _thumbnailUtility?.Dispose();
            _thumbnailUtility = null;
            _thumbnailRenderers.Clear();
            _sourceAvatar = null;
            if (_utility != null)
            {
                _utility.Dispose();
                _utility = null;
            }

            if (_avatar != null)
            {
                _avatar = null;
            }
        }

    }
}
