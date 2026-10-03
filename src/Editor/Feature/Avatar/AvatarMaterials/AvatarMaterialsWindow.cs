using System;
using System.IO;
using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarMaterials
{
    internal sealed class AvatarMaterialsWindow : AvatarPrefabEditorWindow
    {
        private AvatarMaterialsEditor _editor;
        protected override string TitleKey => "avatarEditor.materialsTitle";
        protected override bool ShowsRevertButton => false;
        [MenuItem("ee4v/Window/Avatar/Materials", false, 201)]
        private static void ShowWindow() => GetWindow<AvatarMaterialsWindow>().Show();
        protected override void CreateFeature()
        {
            _editor = new AvatarMaterialsEditor(Context);
            Context.InvalidateMaterialData = _editor.ClearData;
            Context.MaterialChanged = material =>
            {
                if (CanEditMaterial(material)) AssetDatabase.SaveAssetIfDirty(material);
            };
        }
        protected override void RenderFeature() => Context.ControlsHost.Add(_editor.BuildControls());
        protected override void ClearFeatureData() => _editor?.ClearData();
        protected override void DisposeFeature() { _editor?.Dispose(); _editor = null; }
        protected override void SelectPreview(string key, Material material) => _editor.SelectPreviewMaterial(material, -1);
        protected override void ClearPreviewSelection()
        { Context.SelectedMaterial = null; Render(); }
        protected override void CreateMaterialVariant(Material material)
        {
            if (!Context.CanEditPrefab() || material == null || !FlushFeatureChanges()) return;
            var path = string.Empty;
            Material variant = null;
            var assigned = false;
            try
            {
                var prefabPath = AssetDatabase.GetAssetPath(Context.PrefabAsset);
                var directory = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
                var folderName = Path.GetFileNameWithoutExtension(prefabPath) + ".Materials";
                var folder = directory + "/" + folderName;
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(directory, folderName);
                var name = material.name;
                foreach (var character in Path.GetInvalidFileNameChars()) name = name.Replace(character, '_');
                path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".mat");
                variant = new Material(material) { name = material.name };
                AssetDatabase.CreateAsset(variant, path);
                _editor.ReplaceMaterialAssignments(material, variant);
                assigned = true;
                _editor.RefreshAfterMaterialReplacement(material, variant);
            }
            catch (Exception exception)
            {
                if (!assigned && !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                    AssetDatabase.DeleteAsset(path);
                else if (!assigned && variant != null) DestroyImmediate(variant);
                ShowError(exception.Message);
            }
        }
    }
}
