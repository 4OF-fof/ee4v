using System;
using System.Linq;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    internal sealed class AvatarExecutionViewport : VisualElement, IDisposable
    {
        private readonly GameObject _avatar;
        private readonly Action _repaint;
        private readonly PreviewOrbitController _orbit;
        private readonly IMGUIContainer _canvas;
        private Camera _camera;
        private RenderTexture _texture;
        private bool _disposed;

        internal AvatarExecutionViewport(GameObject avatar, Action repaint)
        {
            _avatar = avatar;
            _repaint = repaint;
            AddToClassList("ee4v-execution__viewport");
            _orbit = new PreviewOrbitController(GetHashCode(), Repaint);
            _canvas = new IMGUIContainer(Draw);
            _canvas.AddToClassList("ee4v-execution__canvas");
            Add(_canvas);
            RegisterCallback<DetachFromPanelEvent>(_ => ReleaseCamera());
            schedule.Execute(() =>
            {
                if (!_disposed && EditorApplication.isPlaying) { Repaint(); }
            }).Every(33);
            ResetView();
        }

        internal void ResetView()
        {
            if (_avatar == null) { return; }
            var renderers = _avatar.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
            var bounds = new Bounds(_avatar.transform.position + Vector3.up, Vector3.one);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) { bounds.Encapsulate(renderer.bounds); }
            }
            var aspect = _canvas.contentRect.height > 0f
                ? Mathf.Max(0.5f, _canvas.contentRect.width / _canvas.contentRect.height) : 1f;
            var distance = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) /
                Mathf.Tan(15f * Mathf.Deg2Rad) + bounds.extents.z;
            _orbit.Reset(bounds.center, Mathf.Max(0.5f, distance * 1.15f));
        }

        private void Repaint()
        {
            _canvas.MarkDirtyRepaint();
            _repaint?.Invoke();
        }

        private void Draw()
        {
            var rect = _canvas.contentRect;
            if (_disposed || _avatar == null || !EditorApplication.isPlaying ||
                rect.width < 1 || rect.height < 1) { return; }
            if (_camera == null)
            {
                var cameraObject = new GameObject("ee4v Execution Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                SceneManager.MoveGameObjectToScene(cameraObject, _avatar.scene);
                _camera = cameraObject.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.scene = _avatar.scene;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                _camera.fieldOfView = 30f;
                AddLight("ee4v Execution Key Light", 1.2f, Quaternion.Euler(25f, -30f, 0f));
                AddLight("ee4v Execution Fill Light", 0.4f, Quaternion.Euler(-15f, 150f, 0f));
            }
            _orbit.HandleInput(rect, _camera, _camera.fieldOfView);
            _orbit.ConfigureCamera(_camera, _avatar.transform.rotation);
            if (Event.current.type != EventType.Repaint) { return; }
            var width = Mathf.Clamp(Mathf.CeilToInt(rect.width * EditorGUIUtility.pixelsPerPoint), 1, 2048);
            var height = Mathf.Clamp(Mathf.CeilToInt(rect.height * EditorGUIUtility.pixelsPerPoint), 1, 2048);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                ReleaseTexture();
                _texture = new RenderTexture(width, height, 24)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 4
                };
                _texture.Create();
            }
            _camera.targetTexture = _texture;
            _camera.aspect = (float)width / height;
            var previous = RenderTexture.active;
            try
            {
                _camera.Render();
                GUI.DrawTexture(rect, _texture, ScaleMode.StretchToFill, false);
            }
            finally { RenderTexture.active = previous; }
        }

        private void ReleaseTexture()
        {
            if (_camera != null) { _camera.targetTexture = null; }
            if (_texture == null) { return; }
            _texture.Release();
            UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
        }

        private void AddLight(string name, float intensity, Quaternion rotation)
        {
            var lightObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            lightObject.transform.SetParent(_camera.transform, false);
            lightObject.transform.localRotation = rotation;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void ReleaseCamera()
        {
            _orbit.CancelInteraction();
            ReleaseTexture();
            if (_camera != null) { UnityEngine.Object.DestroyImmediate(_camera.gameObject); }
            _camera = null;
        }

        public void Dispose()
        {
            _disposed = true;
            ReleaseCamera();
        }
    }
}
