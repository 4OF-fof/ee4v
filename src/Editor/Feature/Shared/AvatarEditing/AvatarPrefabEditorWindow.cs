using System;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarEditing
{
    /// <summary>Edits an existing Prefab instance without owning its Scene or lifetime.</summary>
    public abstract class AvatarPrefabEditorWindow : EditorWindow
    {
        [SerializeField] private GameObject _instance;
        private GameObject _prefab;
        private ObjectField _prefabField;
        private UiButton _save;
        private UiButton _revert;
        private HelpBox _feedback;
        private VisualElement _body;
        protected AvatarEditingContext Context { get; private set; }
        protected VisualElement FeatureHeader { get; private set; }
        protected abstract string TitleKey { get; }
        protected virtual bool UsesBodyPartSelector => true;
        protected virtual bool ShowsSaveButton => true;
        protected virtual bool ShowsRevertButton => true;
        protected virtual bool HasPendingFeatureChanges => false;
        protected abstract void CreateFeature();
        protected abstract void RenderFeature();
        protected abstract void ClearFeatureData();
        protected abstract void DisposeFeature();
        protected virtual bool FlushFeatureChanges() => true;
        protected virtual void CancelFeatureChanges() { }
        protected virtual void CreateMaterialVariant(Material material) { }
        protected virtual void SelectPreview(string partKey, Material material) { }
        protected virtual void ClearPreviewSelection() { }

        protected void OnEnable()
        {
            I18N.Reloaded += Rebuild;
            Undo.undoRedoPerformed += OnUndoRedo;
            Undo.postprocessModifications += OnModifications;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            AvatarShapeNaming.Changed += OnNamingChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.projectChanged += OnTargetChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            if (_instance == null) _instance = GetInstanceRoot(Selection.activeGameObject);
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get(TitleKey));
            minSize = new Vector2(800f, 560f);
            hasUnsavedChanges = false;
        }

        protected void CreateGUI()
        {
            DisposeView();
            _prefab = GetSourcePrefab(_instance);
            rootVisualElement.Clear();
            UiComposition.Prepare(rootVisualElement,
                "Editor/Feature/Shared/AvatarEditing/avatar-prefab-editor.uss");
            rootVisualElement.AddToClassList("ee4v-avatar-prefab-editor");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-avatar-prefab-editor__toolbar");
            _prefabField = UiTextFactory.CreateObjectField(I18N.Get("avatarEditor.prefab"));
            _prefabField.name = "avatarPrefab";
            _prefabField.objectType = typeof(GameObject);
            _prefabField.allowSceneObjects = true;
            _prefabField.SetValueWithoutNotify(_instance);
            _prefabField.RegisterValueChangedCallback(evt => ChangePrefab(evt.newValue as GameObject));
            toolbar.Add(_prefabField);
            _revert = null;
            if (ShowsRevertButton)
            {
                _revert = new UiButton(I18N.Get("avatarEditor.revert"), ConfirmDiscardChanges,
                    variant: UiButtonVariant.Ghost) { name = "revertPrefab" };
                toolbar.Add(_revert);
            }
            _save = null;
            if (ShowsSaveButton)
            {
                _save = new UiButton(I18N.Get("avatarEditor.save"), SaveChanges) { name = "savePrefab" };
                toolbar.Add(_save);
            }
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
                Root = _instance, PrefabAsset = _prefab,
                CanEditPrefab = CanEditPrefab, CanEditMaterial = CanEditMaterial,
                FlushChanges = FlushFeatureChanges,
                GetAssetPath = () => AssetDatabase.GetAssetPath(_prefab),
                GetExcludedPartPrefixes = () => Array.Empty<string>(),
                CreateShapeNaming = AvatarShapeNaming.Create,
                WorkingSceneDirtyChanged = value =>
                {
                    if (value && _instance != null) EditorSceneManager.MarkSceneDirty(_instance.scene);
                    MarkChanged();
                },
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
            preview.SetPrefab(_instance);
            Render();
            toolbar.schedule.Execute(RefreshSaveButtons).Every(500);
        }

        private static GameObject GetInstanceRoot(GameObject target)
        {
            if (target == null || EditorUtility.IsPersistent(target) ||
                !target.scene.IsValid() || EditorSceneManager.IsPreviewScene(target.scene) ||
                PrefabUtility.GetPrefabInstanceStatus(target) != PrefabInstanceStatus.Connected) return null;
            return PrefabUtility.GetNearestPrefabInstanceRoot(target);
        }

        private static GameObject GetSourcePrefab(GameObject instance) => GetInstanceRoot(instance) == null
            ? null : AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance));

        private void ChangePrefab(GameObject target)
        {
            var instance = GetInstanceRoot(target);
            if (target != null && instance == null)
            {
                _prefabField.SetValueWithoutNotify(_instance);
                ShowError(I18N.Get("avatarEditor.invalidPrefab"));
                return;
            }
            if (instance == _instance) { _prefabField.SetValueWithoutNotify(_instance); return; }
            if (!FlushFeatureChanges()) { _prefabField.SetValueWithoutNotify(_instance); return; }
            DisposeView();
            _instance = instance;
            CreateGUI();
        }

        private bool CanEditPrefab() => GetInstanceRoot(_instance) == _instance && _instance != null && _prefab != null &&
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
            RefreshSaveButtons();
            Repaint();
        }

        protected void Render()
        {
            if (Context == null) return;
            FeatureHeader.Clear();
            Context.ControlsHost.Clear();
            _body.style.display = Context.Root == null ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshSaveButtons();
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
            if (ShowsSaveButton && !CanEditPrefab()) ShowMessage(I18N.Get("avatarEditor.readOnly"), HelpBoxMessageType.Info);
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

        private void RefreshSaveButtons()
        {
            if (_save == null && _revert == null) return;
            var enabled = CanEditPrefab() &&
                (HasPendingFeatureChanges || PrefabEditingChanges.HasContentOverrides(_instance));
            _save?.SetPrimaryActionEnabled(enabled);
            _revert?.SetEnabled(enabled);
        }

        private void ConfirmDiscardChanges()
        {
            if (!CanEditPrefab() ||
                !HasPendingFeatureChanges && !PrefabEditingChanges.HasContentOverrides(_instance)) return;
            if (EditorUtility.DisplayDialog(I18N.Get("avatarEditor.revert"),
                I18N.Get("avatarEditor.revertConfirm"), I18N.Get("avatarEditor.revert"),
                I18N.Get("avatarEditor.cancel"))) DiscardChanges();
        }

        public override void SaveChanges()
        {
            if (!ShowsSaveButton || !CanEditPrefab() || !FlushFeatureChanges()) return;
            try
            {
                // Commit delayed Undo records before clearing the saved state.
                Undo.FlushUndoRecordObjects();
                PrefabUtility.ApplyPrefabInstance(_instance, InteractionMode.AutomatedAction);
                Undo.FlushUndoRecordObjects();
                base.SaveChanges();
                RefreshTarget();
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }

        public override void DiscardChanges()
        {
            if (!CanEditPrefab()) return;
            try
            {
                CancelFeatureChanges();
                Undo.FlushUndoRecordObjects();
                PrefabUtility.RevertPrefabInstance(_instance, InteractionMode.UserAction);
                base.DiscardChanges();
                CreateGUI();
            }
            catch (Exception exception) { ShowError(exception.Message); }
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
            if (_instance != null)
                foreach (var change in changes)
                {
                    var target = change.currentValue.target;
                    var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
                    if (transform != null && transform.IsChildOf(_instance.transform)) { MarkChanged(); break; }
                }
            return changes;
        }

        private void Rebuild() { ConfigureWindow(); CreateGUI(); }
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) OnTargetChanged();
            else Render();
        }
        private void OnNamingChanged()
        {
            if (Context?.Root == null || !FlushFeatureChanges()) return;
            ClearFeatureData();
            Render();
        }
        private void BeforeReload() => FlushFeatureChanges();
        private void OnTargetChanged()
        {
            if (Context == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _prefab = GetSourcePrefab(_instance);
            Context.Root = _instance;
            Context.PrefabAsset = _prefab;
            RefreshTarget();
        }
        private void OnHierarchyChanged()
        {
            if (Context != null && _instance == null && Context.ControlsHost.childCount > 0)
                OnTargetChanged();
        }
        private void DisposeView()
        {
            if (Context == null) return;
            FlushFeatureChanges();
            DisposeFeature();
            Context.Preview.Dispose();
            Context = null;
        }
        protected void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.postprocessModifications -= OnModifications;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            AvatarShapeNaming.Changed -= OnNamingChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.projectChanged -= OnTargetChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            DisposeView();
        }
    }
}
