using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarEditing
{
    /// <summary>Standalone Prefab session. Feature windows supply only their editor controls.</summary>
    public abstract class AvatarPrefabEditorWindow : EditorWindow
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private GameObject _contents;
        [SerializeField] private bool _dirty;
        private bool _reloading;
        private ObjectField _prefabField;
        private UiButton _save;
        private HelpBox _feedback;
        private VisualElement _body;
        protected AvatarEditingContext Context { get; private set; }
        protected VisualElement FeatureHeader { get; private set; }
        protected abstract string TitleKey { get; }
        protected virtual bool UsesBodyPartSelector => true;
        protected abstract void CreateFeature();
        protected abstract void RenderFeature();
        protected abstract void ClearFeatureData();
        protected abstract void DisposeFeature();
        protected virtual bool FlushFeatureChanges() => true;
        protected virtual void SaveAdditionalAssets() { }
        protected virtual void CreateMaterialVariant(Material material) { }
        protected virtual void SelectPreview(string partKey, Material material) { }
        protected virtual void ClearPreviewSelection() { }

        protected void OnEnable()
        {
            _reloading = false;
            I18N.Reloaded += Rebuild;
            Undo.undoRedoPerformed += OnUndoRedo;
            Undo.postprocessModifications += OnModifications;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            AvatarShapeNaming.Changed += OnNamingChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get(TitleKey));
            minSize = new Vector2(800f, 560f);
            saveChangesMessage = I18N.Get("avatarEditor.unsaved");
            hasUnsavedChanges = _dirty;
        }

        protected void CreateGUI()
        {
            DisposeView();
            rootVisualElement.Clear();
            UiComposition.Prepare(rootVisualElement,
                "Editor/Feature/Shared/AvatarEditing/avatar-prefab-editor.uss");
            rootVisualElement.AddToClassList("ee4v-avatar-prefab-editor");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-avatar-prefab-editor__toolbar");
            _prefabField = UiTextFactory.CreateObjectField(I18N.Get("avatarEditor.prefab"));
            _prefabField.name = "avatarPrefab";
            _prefabField.objectType = typeof(GameObject);
            _prefabField.allowSceneObjects = false;
            _prefabField.SetValueWithoutNotify(_prefab);
            _prefabField.RegisterValueChangedCallback(evt => ChangePrefab(evt.newValue as GameObject));
            toolbar.Add(_prefabField);
            _save = new UiButton(I18N.Get("avatarEditor.save"), SaveChanges) { name = "savePrefab" };
            toolbar.Add(_save);
            rootVisualElement.Add(toolbar);
            _feedback = UiTextFactory.CreateHelpBox(string.Empty, HelpBoxMessageType.Info);
            _feedback.style.display = DisplayStyle.None;
            rootVisualElement.Add(_feedback);
            _body = new VisualElement();
            _body.AddToClassList("ee4v-avatar-prefab-editor__body");
            rootVisualElement.Add(_body);
            var pane = new PreviewPane(I18N.Get("avatarEditor.preview"));
            pane.AddToClassList("ee4v-avatar-prefab-editor__preview");
            var preview = new PrefabScenePreview();
            preview.SetFlexibleLayout(true);
            preview.SetFullBodyFraming(true);
            pane.Content.Add(preview);
            _body.Add(pane);
            var sidebar = new VisualElement();
            sidebar.AddToClassList("ee4v-avatar-prefab-editor__sidebar");
            FeatureHeader = new VisualElement();
            sidebar.Add(FeatureHeader);
            var controls = new ScrollView(ScrollViewMode.Vertical);
            controls.AddToClassList("ee4v-avatar-prefab-editor__controls");
            sidebar.Add(controls);
            _body.Add(sidebar);
            Context = new AvatarEditingContext
            {
                UiRoot = rootVisualElement, ControlsHost = controls, Preview = preview,
                Root = _contents, PrefabAsset = _prefab,
                CanEditPrefab = CanEditPrefab, CanEditMaterial = CanEditMaterial,
                FlushChanges = FlushFeatureChanges,
                GetAssetPath = () => AssetDatabase.GetAssetPath(_prefab),
                GetExcludedPartPrefixes = () => Array.Empty<string>(),
                CreateShapeNaming = AvatarShapeNaming.Create,
                WorkingSceneDirtyChanged = value => { if (value) MarkChanged(); },
                Changed = MarkChanged, Refresh = Render, ShowParts = Render, ShowMaterials = Render,
                Rebuild = Rebuild, ClearCaches = ClearFeatureData,
                InvalidateControls = _ => { }, InvalidateMaterialData = () => { },
                Repaint = Repaint, SyncPreviewSelection = SyncPreviewSelection,
                ShowAssetError = ShowError, CreateMaterialVariant = CreateMaterialVariant,
                RefreshPrefabGroupVisibility = (_, __, ___) => { },
                RefreshPrefabHeaderActiveSelf = (_, __) => { },
                RefreshPrefabPreviewVisibilityControls = _ => { },
                BuildPrefabGroup = (_, content, __, ___) => content
            };
            preview.PreviewObjectClicked += SelectPreview;
            preview.PreviewSelectionCleared += ClearPreviewSelection;
            CreateFeature();
            LoadContents();
            Render();
        }

        private void LoadContents()
        {
            if (_prefab == null) return;
            try
            {
                if (_contents == null)
                    _contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(_prefab));
                Context.Root = _contents;
                Context.PrefabAsset = _prefab;
                Context.Preview.SetPrefab(_contents);
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }

        private void ChangePrefab(GameObject prefab)
        {
            if (prefab == _prefab) return;
            if (prefab != null && (!PrefabUtility.IsPartOfPrefabAsset(prefab) ||
                string.IsNullOrEmpty(AssetDatabase.GetAssetPath(prefab))))
            {
                _prefabField.SetValueWithoutNotify(_prefab);
                ShowError(I18N.Get("avatarEditor.invalidPrefab"));
                return;
            }
            if (!FlushFeatureChanges()) { _prefabField.SetValueWithoutNotify(_prefab); return; }
            if (_dirty)
            {
                var answer = EditorUtility.DisplayDialogComplex(titleContent.text,
                    saveChangesMessage, I18N.Get("avatarEditor.save"),
                    I18N.Get("avatarEditor.cancel"), I18N.Get("avatarEditor.discard"));
                if (answer == 0) SaveChanges();
                if (answer == 1 || (answer == 0 && _dirty))
                { _prefabField.SetValueWithoutNotify(_prefab); return; }
            }
            DisposeView();
            ReleaseContents();
            _prefab = prefab;
            _dirty = false;
            hasUnsavedChanges = false;
            CreateGUI();
        }

        private bool CanEditPrefab() => _contents != null && _prefab != null &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            PrefabUtility.GetPrefabAssetType(_prefab) != PrefabAssetType.Model &&
            (_prefab.hideFlags & HideFlags.NotEditable) == 0 &&
            AssetDatabase.IsOpenForEdit(AssetDatabase.GetAssetPath(_prefab));

        protected virtual bool CanEditMaterial(Material material) => material != null &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            EditorUtility.IsPersistent(material) &&
            (material.hideFlags & HideFlags.NotEditable) == 0 &&
            AssetDatabase.IsOpenForEdit(AssetDatabase.GetAssetPath(material));

        protected void MarkChanged()
        {
            if (_contents == null) return;
            _dirty = true;
            hasUnsavedChanges = true;
            _save?.SetEnabled(CanEditPrefab());
            Repaint();
        }

        protected void Render()
        {
            if (Context == null) return;
            FeatureHeader.Clear();
            Context.ControlsHost.Clear();
            _body.style.display = Context.Root == null ? DisplayStyle.None : DisplayStyle.Flex;
            _save.SetEnabled(_dirty && CanEditPrefab());
            if (Context.Root == null)
            {
                if (_prefab == null) ShowMessage(I18N.Get("avatarEditor.selectPrefab"), HelpBoxMessageType.Info);
                return;
            }
            if (UsesBodyPartSelector)
                FeatureHeader.Add(new BodyPartSelector(Context.SelectedBodyPart,
                    part => PrefabScenePreview.HasFocusBone(Context.Root, part, _ => true), part =>
                    {
                        if (!FlushFeatureChanges()) return;
                        Context.SelectedBodyPart = part;
                        ClearFeatureData();
                        Context.Preview.FocusBodyPart(part);
                        Render();
                    }));
            if (!CanEditPrefab()) ShowMessage(I18N.Get("avatarEditor.readOnly"), HelpBoxMessageType.Info);
            else _feedback.style.display = DisplayStyle.None;
            RenderFeature();
            SyncPreviewSelection();
        }

        protected void RefreshTarget()
        {
            ClearFeatureData();
            Context.Preview.ReloadPrefabPreservingView(Context.Root);
            Render();
        }

        protected void SyncPreviewSelection() => Context?.Preview.SetListSelection(
            Context.SelectedPartKey, Context.SelectedMaterial);

        public override void SaveChanges()
        {
            if (!CanEditPrefab() || !FlushFeatureChanges()) return;
            try
            {
                // Commit delayed Undo records before clearing the saved state.
                Undo.FlushUndoRecordObjects();
                SaveAdditionalAssets();
                PrefabUtility.SaveAsPrefabAsset(_contents, AssetDatabase.GetAssetPath(_prefab), out var success);
                if (!success) { ShowError(I18N.Get("avatarEditor.saveFailed")); return; }
                Undo.FlushUndoRecordObjects();
                _dirty = false;
                base.SaveChanges();
                RefreshTarget();
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }

        public override void DiscardChanges()
        {
            // Prefab changes live only in the isolated contents scene.
            DisposeView();
            ReleaseContents();
            _dirty = false;
            base.DiscardChanges();
            CreateGUI();
        }

        protected void ShowError(string message) => ShowMessage(message, HelpBoxMessageType.Error);
        private void ShowMessage(string message, HelpBoxMessageType type)
        {
            if (_feedback == null) return;
            UiTextFactory.SetText(_feedback, message);
            _feedback.messageType = type;
            _feedback.style.display = DisplayStyle.Flex;
        }

        private void OnUndoRedo()
        {
            if (Context?.Root == null) return;
            MarkChanged();
            RefreshTarget();
        }

        private UndoPropertyModification[] OnModifications(UndoPropertyModification[] changes)
        {
            if (_contents != null)
                foreach (var change in changes)
                {
                    var target = change.currentValue.target;
                    var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
                    if (transform != null && transform.IsChildOf(_contents.transform)) { MarkChanged(); break; }
                }
            return changes;
        }

        private void Rebuild() { ConfigureWindow(); CreateGUI(); }
        private void OnPlayModeChanged(PlayModeStateChange state) => Render();
        private void OnNamingChanged()
        {
            if (Context?.Root == null || !FlushFeatureChanges()) return;
            ClearFeatureData();
            Render();
        }
        private void BeforeReload() { FlushFeatureChanges(); _reloading = true; }
        private void DisposeView()
        {
            if (Context == null) return;
            FlushFeatureChanges();
            DisposeFeature();
            Context.Preview.Dispose();
            Context = null;
        }
        private void ReleaseContents()
        {
            if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
            _contents = null;
        }
        protected void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.postprocessModifications -= OnModifications;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            AvatarShapeNaming.Changed -= OnNamingChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            DisposeView();
            if (!_reloading) ReleaseContents();
        }
    }
}
