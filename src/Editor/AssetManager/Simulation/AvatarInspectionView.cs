using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    public abstract class AvatarInspectionView : VisualElement, IDisposable
    {
        protected const int MaxPreviewCount = 6;
        protected readonly GameObject Avatar;
        protected readonly ScrollView Controls;
        protected readonly UiTextElement Status;
        protected readonly VisualElement PreviewActions;
        private readonly VisualElement _previewGrid;
        private readonly List<ScenePreviewViewport> _viewports = new List<ScenePreviewViewport>();
        private readonly List<UiButton> _previewTitles = new List<UiButton>();
        private readonly PreviewOrbitController _orbit;
        private readonly Action _repaint;
        private Camera _camera;
        private readonly RenderTexture[] _textures = new RenderTexture[MaxPreviewCount];
        private bool _disposed;
        protected int ActivePreview { get; private set; }
        protected int PreviewCount => _viewports.Count;

        protected AvatarInspectionView(GameObject avatar, Action repaint, string title)
        {
            Avatar = avatar;
            _repaint = repaint;
            AddToClassList("ee4v-execution");
            AddToClassList("ee4v-inspection");
            _orbit = new PreviewOrbitController(GetHashCode(), Repaint);
            var preview = new PreviewPane(title);
            preview.AddToClassList("ee4v-modification-workflow__preview-pane");
            PreviewActions = preview.Actions;
            _previewGrid = new VisualElement();
            _previewGrid.AddToClassList("ee4v-inspection__grid");
            preview.Content.Add(_previewGrid);
            Add(preview);
            Controls = new ScrollView(ScrollViewMode.Vertical);
            Controls.AddToClassList("ee4v-execution__controls");
            Controls.contentContainer.AddToClassList("ee4v-inspection__controls-content");
            Status = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            Controls.Add(Status);
            Add(Controls);
            if (avatar != null && avatar.scene.IsValid() && EditorApplication.isPlaying)
            {
                var go = new GameObject("ee4v Inspection Camera") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(go, avatar.scene);
                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0);
                _camera.nearClipPlane = 0.01f;
                _camera.farClipPlane = 100;
                _camera.fieldOfView = 35;
                _camera.allowHDR = true;
            }
            SetPreviewCount(1);
            ResetCamera();
            schedule.Execute(ResetCamera);
            schedule.Execute(() =>
            {
                if (_disposed || panel == null) { return; }
                if (!EditorApplication.isPlaying || Avatar == null) { Dispose(); return; }
                Tick();
                Repaint();
            }).Every(33);
        }

        protected virtual GameObject VisibleAvatar => Avatar;
        protected virtual void Tick() { }
        protected virtual void ActivePreviewChanged() { }
        protected virtual void RenderPreview(Camera camera, int index) { camera.Render(); }
        protected void Repaint()
        {
            foreach (var viewport in _viewports) { viewport.RequestRepaint(); }
            _repaint?.Invoke();
        }

        protected void SetPreviewCount(int count, int activeIndex = -1)
        {
            count = Mathf.Clamp(count, 1, _textures.Length);
            _orbit.CancelInteraction();
            foreach (var viewport in _viewports) { viewport.Dispose(); }
            _viewports.Clear();
            _previewTitles.Clear();
            _previewGrid.Clear();
            for (var i = count; i < _textures.Length; i++) { ReleaseTexture(i); }
            ActivePreview = Mathf.Clamp(activeIndex < 0 ? ActivePreview : activeIndex, 0, count - 1);
            var columns = count > 4 ? 3 : count == 1 ? 1 : 2;
            var rows = (count + columns - 1) / columns;
            for (var rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                var row = new VisualElement();
                row.AddToClassList("ee4v-inspection__preview-row");
                _previewGrid.Add(row);
                for (var column = 0; column < columns && _viewports.Count < count; column++)
                {
                    var index = _viewports.Count;
                    var tile = new VisualElement();
                    tile.AddToClassList("ee4v-inspection__tile");
                    var heading = new UiButton(string.Empty, () => SelectPreview(index), variant: UiButtonVariant.Ghost);
                    heading.AddToClassList("ee4v-inspection__tile-heading");
                    var header = new VisualElement();
                    header.AddToClassList("ee4v-inspection__tile-header");
                    header.Add(heading);
                    AddPreviewActions(header, index, count);
                    tile.Add(header);
                    _previewTitles.Add(heading);
                    var viewport = new ScenePreviewViewport(rect => Draw(rect, index), ResetCamera,
                        I18N.Get("workflow.inspection.background"), I18N.Get("workflow.inspection.resetCamera"));
                    viewport.SetPreviewAvailable(_camera != null);
                    viewport.RegisterCallback<PointerDownEvent>(_ => SelectPreview(index), TrickleDown.TrickleDown);
                    tile.Add(viewport);
                    _viewports.Add(viewport);
                    row.Add(tile);
                }
            }
            SelectPreview(ActivePreview);
        }

        protected virtual void AddPreviewActions(VisualElement header, int index, int count) { }

        protected void SelectPreview(int index)
        {
            ActivePreview = index;
            for (var i = 0; i < _previewTitles.Count; i++)
            {
                var selected = i == index;
                var heading = _previewTitles[i];
                heading.parent.EnableInClassList("ee4v-inspection__tile-header--selected", selected);
                heading.parent.parent.EnableInClassList("ee4v-inspection__tile--selected", selected);
                heading.SetLabelColor(selected ? UiColorTokens.TextOnState : UiColorTokens.TextPrimary);
            }
            ActivePreviewChanged();
            Repaint();
        }

        protected void SetPreviewTitle(int index, string title)
        { _previewTitles[index].SetLabel(title); }

        private void ResetCamera()
        {
            if (_disposed) { return; }
            var avatar = VisibleAvatar;
            if (avatar == null) { return; }
            var bounds = new Bounds(avatar.transform.position + Vector3.up * 0.8f, Vector3.one);
            var hasBounds = false;
            var renderers = avatar.GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.enabled && (renderer is SkinnedMeshRenderer || renderer is MeshRenderer));
            var baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                foreach (var renderer in renderers)
                {
                    var meshBounds = renderer.bounds;
                    if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                    {
                        baked.Clear();
                        skinned.BakeMesh(baked, true);
                        baked.RecalculateBounds();
                        if (baked.vertexCount == 0) { continue; }
                        var local = baked.bounds;
                        var matrix = renderer.localToWorldMatrix;
                        var x = matrix.MultiplyVector(Vector3.right * local.extents.x);
                        var y = matrix.MultiplyVector(Vector3.up * local.extents.y);
                        var z = matrix.MultiplyVector(Vector3.forward * local.extents.z);
                        var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                        meshBounds = new Bounds(matrix.MultiplyPoint3x4(local.center), extents * 2);
                    }
                    if (!hasBounds) { bounds = meshBounds; hasBounds = true; }
                    else { bounds.Encapsulate(meshBounds); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            var rect = _viewports.Count > 0 ? _viewports[0].PreviewRect : Rect.zero;
            var aspect = rect.width > 1 && rect.height > 1 ? rect.width / rect.height : 4f / 3f;
            var tangent = Mathf.Tan(35f * Mathf.Deg2Rad * 0.5f);
            var distance = Mathf.Max(bounds.extents.y / tangent, bounds.extents.x / (tangent * aspect)) * 1.1f + bounds.extents.z;
            _orbit.Reset(bounds.center, Mathf.Max(0.25f, distance));
        }

        private void Draw(Rect rect, int index)
        {
            if (_disposed || _camera == null || rect.width < 2 || rect.height < 2) { return; }
            _orbit.HandleInput(rect, _camera, _camera.fieldOfView);
            if (Event.current.type != EventType.Repaint) { return; }
            var width = Mathf.Clamp(Mathf.CeilToInt(rect.width), 2, 2048);
            var height = Mathf.Clamp(Mathf.CeilToInt(rect.height), 2, 2048);
            var texture = _textures[index];
            if (texture == null || texture.width != width || texture.height != height)
            {
                ReleaseTexture(index);
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
                    { hideFlags = HideFlags.HideAndDontSave };
                texture.Create();
                _textures[index] = texture;
            }
            _orbit.ConfigureCamera(_camera, VisibleAvatar != null ? VisibleAvatar.transform.rotation : Quaternion.identity);
            _camera.aspect = (float)width / height;
            _camera.targetTexture = texture;
            var previous = RenderTexture.active;
            try
            {
                RenderPreview(_camera, index);
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            }
            finally { RenderTexture.active = previous; _camera.targetTexture = null; }
        }

        private void ReleaseTexture(int index)
        {
            var texture = _textures[index];
            if (texture == null) { return; }
            if (_camera != null) { _camera.targetTexture = null; }
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            _textures[index] = null;
        }

        public virtual void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            _orbit.CancelInteraction();
            for (var i = 0; i < _textures.Length; i++) { ReleaseTexture(i); }
            if (_camera != null) { UnityEngine.Object.DestroyImmediate(_camera.gameObject); }
            _camera = null;
            foreach (var viewport in _viewports) { viewport.Dispose(); }
        }
    }
}
