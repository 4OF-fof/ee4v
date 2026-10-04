using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    internal sealed class AvatarShapePartsWindow : AvatarPrefabEditorWindow
    {
        private AvatarPartsEditor _editor;
        protected override string TitleKey => "avatarEditor.shapePartsTitle";
        protected override bool HasPendingFeatureChanges => _editor != null &&
            (_editor.BodyScaleDirty || _editor.HasPendingPartVisibility || _editor.HasPendingBodySizeChange);
        [MenuItem("ee4v/Window/Avatar/Shape and Parts", false, 200)]
        private static void ShowWindow() => GetWindow<AvatarShapePartsWindow>().Show();
        protected override void CreateFeature() => _editor = new AvatarPartsEditor(Context);
        protected override void RenderFeature()
        {
            FeatureHeader.Add(_editor.BuildShapePartsTabs());
            Context.ControlsHost.Add(_editor.BuildControls());
        }
        protected override void ClearFeatureData() => _editor?.ClearData();
        protected override bool PrepareFeatureSave() =>
            _editor == null || _editor.PrepareBodyBlendShapeSyncForSave();
        protected override bool FlushFeatureChanges()
        {
            if (_editor == null) return true;
            _editor.EndBodyScaleDrag(false);
            _editor.SaveBodyScalePrefab(false);
            return !_editor.BodyScaleDirty && _editor.FlushPendingPartVisibility();
        }
        protected override void DisposeFeature() => _editor = null;
        protected override void CancelFeatureChanges()
        {
            _editor?.ClearPendingPartVisibility();
            _editor?.CancelBodySizeChange();
            if (_editor != null) _editor.BodyScaleDirty = false;
        }
        protected override void SelectPreview(string key, Material material) => _editor.SelectPreviewPart(key);
        protected override void ClearPreviewSelection() => _editor.ClearPreviewSelection();
    }
}
