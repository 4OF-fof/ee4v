using System;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarComposition
{
    internal sealed class AvatarCompositionWindow : AvatarPrefabEditorWindow
    {
        private GameObject _addition;
        protected override string TitleKey => "avatarEditor.compositionTitle";
        protected override bool UsesBodyPartSelector => false;
        [MenuItem("ee4v/Window/Avatar/Prefab Composition", false, 202)]
        private static void ShowWindow() => GetWindow<AvatarCompositionWindow>().Show();
        protected override void CreateFeature() { }
        protected override void ClearFeatureData() { }
        protected override void DisposeFeature() { _addition = null; }
        protected override void RenderFeature()
        {
            var editable = Context.CanEditPrefab();
            var input = UiTextFactory.CreateObjectField(I18N.Get("composition.addPrefab"));
            input.name = "additionPrefab";
            input.objectType = typeof(GameObject);
            input.allowSceneObjects = false;
            input.SetValueWithoutNotify(_addition);
            var add = new UiButton(I18N.Get("composition.add"), AddPrefab) { name = "addPrefab" };
            add.SetEnabled(editable && _addition != null);
            input.RegisterValueChangedCallback(evt =>
            {
                _addition = evt.newValue as GameObject;
                add.SetEnabled(editable && _addition != null);
            });
            FeatureHeader.Add(input);
            FeatureHeader.Add(add);
            foreach (var child in Context.Root.transform.Cast<Transform>().ToArray())
            {
                var row = new VisualElement();
                row.AddToClassList("ee4v-avatar-prefab-editor__row");
                var toggle = UiTextFactory.CreateToggle(child.name);
                toggle.SetValueWithoutNotify(child.gameObject.activeSelf);
                toggle.SetEnabled(editable);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (!Context.CanEditPrefab()) return;
                    Undo.RecordObject(child.gameObject, "Change Prefab Active State");
                    child.gameObject.SetActive(evt.newValue);
                    if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                    MarkChanged();
                    RefreshTarget();
                });
                row.Add(toggle);
                var remove = new UiButton(I18N.Get("composition.remove"), () =>
                {
                    if (!Context.CanEditPrefab()) return;
                    Undo.DestroyObjectImmediate(child.gameObject);
                    MarkChanged();
                    RefreshTarget();
                });
                remove.SetEnabled(editable);
                row.Add(remove);
                Context.ControlsHost.Add(row);
            }
        }
        private void AddPrefab()
        {
            if (!Context.CanEditPrefab() || _addition == null) return;
            var path = AssetDatabase.GetAssetPath(_addition);
            var targetPath = AssetDatabase.GetAssetPath(Context.PrefabAsset);
            if (!PrefabUtility.IsPartOfPrefabAsset(_addition) || string.IsNullOrEmpty(path) ||
                path == targetPath || AssetDatabase.GetDependencies(path, true).Contains(targetPath))
            { ShowError(I18N.Get("composition.invalidAddition")); return; }
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(_addition, Context.Root.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add Prefab");
                Undo.SetTransformParent(instance.transform, Context.Root.transform, "Add Prefab");
                Undo.RecordObject(instance.transform, "Place Prefab");
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                if (PrefabUtility.IsPartOfPrefabInstance(instance.transform))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                _addition = null;
                MarkChanged();
                RefreshTarget();
            }
            catch (Exception exception)
            {
                if (instance != null) Undo.DestroyObjectImmediate(instance);
                ShowError(exception.Message);
            }
        }
    }
}
