using System;
using System.Linq;
using Ee4v.UI;
using Ee4v.Core.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    internal sealed class AvatarExecutionViewport : VisualElement, IDisposable
    {
        private readonly GameObject _avatar;
        private readonly Action _repaint;
        private readonly PreviewOrbitController _orbit;
        private readonly IMGUIContainer _canvas;
        private AvatarPreviewRenderer _preview;
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
            RegisterCallback<DetachFromPanelEvent>(_ => ReleasePreview());
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
            if (_preview == null)
            {
                _preview = new AvatarPreviewRenderer(_avatar);
                _preview.Camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            }
            _orbit.HandleInput(rect, _preview.Camera, _preview.FieldOfView);
            _orbit.ConfigureCamera(_preview.Camera, _avatar.transform.rotation);
            if (Event.current.type != EventType.Repaint) { return; }
            var maxSize = 2048f / Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            var scale = Mathf.Min(1f, maxSize / Mathf.Max(rect.width, rect.height));
            var texture = _preview.Render(new Rect(0f, 0f, rect.width * scale, rect.height * scale));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        private void ReleasePreview()
        {
            _orbit.CancelInteraction();
            _preview?.Dispose();
            _preview = null;
        }

        public void Dispose()
        {
            _disposed = true;
            ReleasePreview();
        }
    }
}
