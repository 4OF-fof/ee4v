using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Simulation;
using Ee4v.FaceExpression;
using Ee4v.AvatarParts;
using Ee4v.AvatarMaterials;
using Ee4v.AvatarInfo;
using static Ee4v.AvatarParts.AvatarPartsEditor;
using AppearancePanel = Ee4v.AvatarEditing.AvatarEditorPanel;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView : VisualElement, IDisposable
    {
        private readonly AvatarEditingContext _avatarContext;
        private readonly bool _previewOnly;
        private readonly AvatarPartsEditor _parts;
        private readonly AvatarMaterialsEditor _materials;

        private AppearancePanel _appearancePanel = AppearancePanel.Parts;

        private const double VariantSaveStatusDelaySeconds = 0.5d;

        private sealed class CachedAppearanceControls
        {
            internal VisualElement Content { get; set; }
            internal BodyPartCategory? BodyPart { get; set; }
            internal Material Material { get; set; }
            internal string Feedback { get; set; }
            internal MessageSeverity FeedbackType { get; set; }
        }

        internal sealed class VariantSourceOption
        {
            private IReadOnlyList<GameObject> _prefabs;
            internal AssetItem Item { get; set; }
            internal IReadOnlyList<string> PrefabPaths { get; set; }
            internal int PrefabCount => PrefabPaths?.Count ?? Prefabs?.Count ?? 0;
            internal IReadOnlyList<GameObject> Prefabs
            {
                get => _prefabs ?? (_prefabs = (PrefabPaths ?? Array.Empty<string>())
                    .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(prefab => prefab != null)
                    .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase).ToArray());
                set => _prefabs = value;
            }
        }

        private sealed class AssetChildEntry
        {
            internal int SiblingIndex { get; set; }
            internal string Name { get; set; }
            internal bool IsAdded { get; set; }
            internal bool IsVisible { get; set; }
            internal bool IsActiveSelf { get; set; }
        }

        private sealed class WorkingSceneSession
        {
            internal GameObject Root;
            internal string PrefabPath;
            internal Scene Scene;
            internal int Users;
            internal bool Dirty;
        }

        private static readonly Dictionary<string, WorkingSceneSession>
            WorkingScenes = new Dictionary<string, WorkingSceneSession>(
                StringComparer.OrdinalIgnoreCase);

        private WorkflowCategoryRail _categoryRail;

        private WorkflowEditorLayout _editorLayout;

        private readonly Dictionary<AppearancePanel, CachedAppearanceControls>
            _appearanceControlsCache =
                new Dictionary<AppearancePanel, CachedAppearanceControls>();

        private readonly Dictionary<BodyPartCategory, bool> _bodyPartAvailabilityCache =
            new Dictionary<BodyPartCategory, bool>();

        private IReadOnlyCollection<BodyPartCategory> _meshBodyPartCategoriesCache;


        private bool _appearanceDataDirty;

        private IAssetManager _manager;

        private IAssetVariantManager _variantStatusManager;

        private bool _variantSaveStatusDirty = true;

        private bool _variantSaveStatusRefreshing;

        private long _variantSaveStatusVersion;

        private bool _variantHasChanges = true;

        private bool _variantHasDiscardableChanges;

        private bool _variantHasPrefabOverrides;

        private string _variantSaveStatusError;

        private double _variantSaveStatusDueAt;

        private bool _savingVariant;

        private bool _discardingVariant;

        private DerivedAssetInfo _workingAsset;

        private int? _previewScopeSiblingIndex;

        private VisualElement _workspaceHeader;

        private FaceExpressionEmbeddedView _faceExpressionEditor;

        private VisualElement _customizerHost;

        private VisualElement _faceExpressionHost;

        private VisualElement _overviewContent;

        private bool _overviewMobile = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        private bool _playModeTransition;

        private VisualElement _appearanceHeader;

        private ExclusiveButtonGroup<BodyPartCategory?> _bodyPartSelector;

        private VisualElement _appearanceTabs;

        private PreviewPane _previewPane;

        private WorkflowCategory _currentCategory =
            WorkflowCategory.Overview;

        private WorkflowCategory _editingCategory = WorkflowCategory.Overview;

        private bool _creatingDerivedAsset;
        private bool _creatingOriginalAsset;

        private string _creationItemId = string.Empty;

        private GameObject _creationPrefab;

        private string _derivedName = string.Empty;

        private string _derivedDescription = string.Empty;

        private bool _prefabPreviewVisibilityInitialized;

        private WorkingSceneSession _workingSceneSession;

        private bool _workingSceneDirty
        {
            get => _workingSceneSession != null &&
                   (_workingSceneSession.Dirty ||
                    _workingSceneSession.Root != null &&
                    _workingSceneSession.Root.scene.isDirty);
            set
            {
                if (_workingSceneSession != null)
                {
                    _workingSceneSession.Dirty = value;
                    if (value && _workingSceneSession.Root != null &&
                        !_workingSceneSession.Root.scene.isDirty)
                    {
                        EditorSceneManager.MarkSceneDirty(
                            _workingSceneSession.Root.scene);
                    }
                    if (value) { QueueWorkingSceneSave(); }
                }
            }
        }


        private readonly Action _repaint;


        private bool _disposed;

        private VisualElement _executionHost;

        private AvatarExecutionView _executionView;
        private AvatarInspectionView _inspectionView;
        private WorkflowCategory? _executionCategory;

        internal WorkflowCategory EditingCategory => _currentCategory.IsPlayMode()
            ? _editingCategory : _currentCategory;

        internal AppearancePanel EditingAppearancePanel => _appearancePanel;

        internal void RestoreEditingCategory(WorkflowCategory category, AppearancePanel appearancePanel)
        {
            if (!Enum.IsDefined(typeof(WorkflowCategory), category)) { category = WorkflowCategory.Appearance; }
            _appearancePanel = appearancePanel;
            _parts.Section = appearancePanel == AppearancePanel.Shape
                ? ShapePartsSection.Shape : ShapePartsSection.Parts;
            _editingCategory = category.IsPlayMode()
                ? WorkflowCategory.Overview : category;
            if (!EditorApplication.isPlaying) { ShowCategory(_editingCategory, false); }
        }

        private AssetManagerWorkspaceView _assetManagerView;

        private VisualElement _editingWorkspace;
        private VisualElement _temporaryAssetManagerPage;
        private AssetManagerWorkspaceView _temporaryAssetManagerView;

        internal bool IsBrowsingAssets => _temporaryAssetManagerPage != null;

        private AssetManagerViewState _assetManagerViewState = new AssetManagerViewState();

        internal event Action<DerivedAssetInfo> DerivedAssetChanged;

        internal AssetModificationWorkflowView(Action repaint, IAssetManager previewManager = null,
            GameObject previewAvatar = null)
        {
            _previewOnly = previewManager != null && previewAvatar != null;
            _manager = previewManager;
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) { HideVariantChangesHover(); }
            });
            _avatarContext = new AvatarEditingContext(
                new AvatarEditingServices(IsEditableWorkflowPrefab, IsEditableWorkflowMaterial,
                    () =>
                    {
                        _parts.EndBodyScaleDrag();
                        _parts.SaveBodyScalePrefab();
                        return !_parts.BodyScaleDirty && _parts.FlushPendingPartVisibility();
                    },
                    GetWorkingAssetPath, value => _workingSceneDirty = value,
                    () => { QueueWorkingSceneSave(); InvalidateVariantSaveStatus(); },
                    ShowAssetError, CreateEditableMaterialVariant),
                new AvatarEditingHost(() => ShowCategory(_currentCategory, false),
                    BuildWindow, ClearAppearanceCaches, RequestRepaint, SyncPreviewSelection)
                {
                    GetExcludedPartPrefixes = () => AssetManagerSettings.ExcludedPartPrefixes,
                    ShowParts = () => ShowAppearancePanel(_parts.Section == ShapePartsSection.Shape
                        ? AppearancePanel.Shape : AppearancePanel.Parts),
                    ShowMaterials = () => ShowAppearancePanel(AppearancePanel.Material),
                    InvalidateControls = InvalidateAppearanceControls,
                    InvalidateMaterialData = () =>
                    {
                        _materials.ClearData();
                        InvalidateAppearanceControls(AppearancePanel.Material);
                    },
                    RefreshPrefabGroupVisibility = RefreshPrefabGroupVisibility,
                    RefreshPrefabHeaderActiveSelf = RefreshPrefabHeaderActiveSelf,
                    RefreshPrefabPreviewVisibilityControls = RefreshPrefabPreviewVisibilityControls,
                    BuildPrefabGroup = BuildCollapsiblePrefabGroup
                })
            {
                UiRoot = this,
                Root = previewAvatar
            };
            _parts = new AvatarPartsEditor(_avatarContext);
            _materials = new AvatarMaterialsEditor(_avatarContext);
            _repaint = repaint;
            ApplyEditorMode();
            if (!_previewOnly) { OnEnable(); }
            try
            {
                BuildWindow();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void ApplyEditorMode()
        {
            if (EditorApplication.isPlaying)
            {
                if (!_currentCategory.IsPlayMode())
                {
                    _editingCategory = _currentCategory;
                    _currentCategory = DerivedAssetCreator.IsAvatarPrefab(_avatarContext.Root)
                        ? WorkflowCategory.MenuAndGestures : WorkflowCategory.Lighting;
                }
            }
            else if (_currentCategory.IsPlayMode())
            {
                _currentCategory = _editingCategory;
            }
        }

        private void RequestRepaint()
        {
            if (_previewOnly)
            {
                foreach (var button in this.Query<UiButton>().ToList()) { button.SetEnabled(false); }
                _avatarContext.ControlsHost?.SetEnabled(false);
                _faceExpressionHost?.SetEnabled(false);
                _editorLayout?.ExpressionMenuHost.SetEnabled(false);
            }
            MarkDirtyRepaint();
            _repaint?.Invoke();
        }

        private void OnEnable()
        {
            I18N.Reloaded -= Rebuild;
            I18N.Reloaded += Rebuild;
            AssetManagerWindowSession.ManagerInvalidated -=
                OnManagerInvalidated;
            AssetManagerWindowSession.ManagerInvalidated +=
                OnManagerInvalidated;
            Undo.undoRedoPerformed -= RefreshAfterUndoRedo;
            Undo.undoRedoPerformed += RefreshAfterUndoRedo;
            Undo.postprocessModifications += OnUndoModifications;
            AvatarShapeNaming.Changed -=
                OnBlendShapePresetChanged;
            AvatarShapeNaming.Changed +=
                OnBlendShapePresetChanged;
            AssetManagerSettings.PartListExclusionsChanged -=
                OnPartListExclusionsChanged;
            AssetManagerSettings.PartListExclusionsChanged +=
                OnPartListExclusionsChanged;
            AssetManagerSettings.ItemListExclusionsChanged += OnItemListExclusionsChanged;
            AvatarInfoSettings.PrefabExclusionsChanged += OnItemListExclusionsChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            AvatarPlayModePerformanceCache.Changed += OnPlayModePerformanceChanged;
            AvatarBuildSizeCache.Changed += OnPlayModePerformanceChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneClosed += OnWorkingSceneClosed;
            EditorSceneManager.sceneSaved += OnWorkingSceneSaved;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _variantChangesSnapshot = null;
            HideVariantChangesHover();
            _parts.EndBodyScaleDrag(false);
            _parts.SaveBodyScalePrefab(false);
            _parts.FlushPendingPartVisibility();
            ReleaseWorkingScene();
            I18N.Reloaded -= Rebuild;
            AssetManagerWindowSession.ManagerInvalidated -=
                OnManagerInvalidated;
            Undo.undoRedoPerformed -= RefreshAfterUndoRedo;
            Undo.postprocessModifications -= OnUndoModifications;
            AvatarShapeNaming.Changed -=
                OnBlendShapePresetChanged;
            AssetManagerSettings.PartListExclusionsChanged -=
                OnPartListExclusionsChanged;
            AssetManagerSettings.ItemListExclusionsChanged -= OnItemListExclusionsChanged;
            AvatarInfoSettings.PrefabExclusionsChanged -= OnItemListExclusionsChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            AvatarPlayModePerformanceCache.Changed -= OnPlayModePerformanceChanged;
            AvatarBuildSizeCache.Changed -= OnPlayModePerformanceChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneClosed -= OnWorkingSceneClosed;
            EditorSceneManager.sceneSaved -= OnWorkingSceneSaved;
            ReleaseVariantStatusManager();
            DisposeEditors();
            ClearAppearanceCaches();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _playModeTransition = true;
                _editingCategory = EditingCategory;
                _parts.EndBodyScaleDrag();
                _parts.SaveBodyScalePrefab();
                _parts.FlushPendingPartVisibility();
                DisposeEditors();
                SetEnabled(false);
                return;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _playModeTransition = true;
                DisposeEditors();
                SetEnabled(false);
                return;
            }
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _playModeTransition = false;
                SetEnabled(true);
                schedule.Execute(() =>
                {
                    if (!_disposed && _avatarContext.Root != null) { BuildWindow(); }
                });
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode) { return; }
            SetEnabled(true);
            if (_workingAsset?.Prefab == null)
            {
                _playModeTransition = false;
                return;
            }
            schedule.Execute(() =>
            {
                if (_disposed || EditorApplication.isPlaying || _workingAsset?.Prefab == null) { return; }
                ReleaseWorkingScene();
                _avatarContext.PrefabAsset = _workingAsset.Prefab;
                _avatarContext.Root = AcquireWorkingScene(_avatarContext.PrefabAsset);
                ClearAppearanceCaches();
                _playModeTransition = false;
                BuildWindow();
            });
        }

        private void OnPlayModePerformanceChanged()
        {
            if (!_disposed && _avatarContext.Root != null && _currentCategory == WorkflowCategory.Overview)
            {
                schedule.Execute(() =>
                {
                    if (!_disposed && _avatarContext.Root != null && _currentCategory == WorkflowCategory.Overview)
                    {
                        ShowCategory(WorkflowCategory.Overview, false);
                    }
                });
            }
        }

        private void OnProjectChanged()
        {
            _appearanceDataDirty = true;
            InvalidateVariantSaveStatus();
        }

        private void OnWorkingSceneClosed(Scene scene)
        {
            if (_playModeTransition || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (_workingSceneSession == null ||
                _workingSceneSession.Scene != scene)
            {
                return;
            }
            ReleaseWorkingScene();
            _workingAsset = null;
            _parts.BodyScaleDirty = false;
            if (!_disposed && this.panel != null)
            {
                this.schedule.Execute(BuildWindow);
            }
        }

        private void OnWorkingSceneSaved(Scene scene)
        {
            if (_workingSceneSession != null && _workingSceneSession.Scene == scene)
            {
                InvalidateVariantSaveStatus();
            }
        }

        private UndoPropertyModification[] OnUndoModifications(
            UndoPropertyModification[] modifications)
        {
            if (_avatarContext.Root == null)
            {
                return modifications;
            }
            foreach (var modification in modifications)
            {
                var target = modification.currentValue?.target;
                if (target == null)
                {
                    continue;
                }
                var transform = target is Component component
                    ? component.transform
                    : (target as GameObject)?.transform;
                if (target is Material || target is Mesh ||
                    transform != null &&
                        transform.IsChildOf(_avatarContext.Root.transform))
                {
                    _appearanceDataDirty = true;
                    InvalidateVariantSaveStatus();
                    break;
                }
            }
            return modifications;
        }

        private void ClearAppearanceCaches()
        {
            foreach (var cached in _appearanceControlsCache.Values) { cached.Content.RemoveFromHierarchy(); }
            _appearanceControlsCache.Clear();
            _parts.ClearData();
            _materials.ClearData();
            _bodyPartAvailabilityCache.Clear();
            _meshBodyPartCategoriesCache = null;
            _appearanceDataDirty = false;
        }

        private void InvalidateAppearanceControls(AppearancePanel appearancePanel)
        {
            if (_appearanceControlsCache.TryGetValue(appearancePanel,
                    out var cached))
            {
                cached.Content.RemoveFromHierarchy();
                _appearanceControlsCache.Remove(appearancePanel);
            }
        }

        private void OnPartListExclusionsChanged()
        {
            _parts.InvalidateParts();
            InvalidateAppearanceControls(AppearancePanel.Parts);
            if (_currentCategory == WorkflowCategory.Appearance &&
                _appearancePanel == AppearancePanel.Parts)
            {
                Rebuild();
            }
        }

        private void OnItemListExclusionsChanged()
        {
            _parts.EndBodyScaleDrag();
            _parts.SaveBodyScalePrefab();
            if (_parts.BodyScaleDirty || !_parts.FlushPendingPartVisibility()) return;
            _avatarContext.SelectedPrefabSiblingIndex = null;
            _avatarContext.SelectedPrefabName = string.Empty;
            _avatarContext.SelectedBodyPart = null;
            _avatarContext.SelectedPartKey = null;
            _avatarContext.SelectedMaterial = null;
            Rebuild();
        }

        private void OnBlendShapePresetChanged()
        {
            _parts.InvalidateBlendShapes();
            InvalidateAppearanceControls(AppearancePanel.Shape);
            if (_avatarContext.Root != null &&
                _currentCategory == WorkflowCategory.Appearance &&
                _appearancePanel == AppearancePanel.Shape &&
                _avatarContext.ControlsHost != null &&
                this.panel != null)
            {
                this.schedule.Execute(() =>
                {
                    if (_avatarContext.ControlsHost != null &&
                        _currentCategory == WorkflowCategory.Appearance &&
                        _appearancePanel == AppearancePanel.Shape &&
                        this.panel != null)
                    {
                        ShowCategory(WorkflowCategory.Appearance, false);
                    }
                });
            }
        }

        private void Rebuild()
        {
            if (!_disposed && this.panel != null)
            {
                BuildWindow();
            }
        }

        private void OnManagerInvalidated()
        {
            ReleaseVariantStatusManager();
            _manager = null;
            _assetManagerViewState = new AssetManagerViewState();
            if (IsBrowsingAssets)
            {
                CloseTemporaryAssetManager();
                ShowTemporaryAssetManager();
                return;
            }
            Rebuild();
        }

        private bool IsInSelectedPrefabScope(Transform target)
        {
            return _avatarContext.IsInSelectedPrefabScope(target);
        }

        private void SetPreviewTitle(string titleKey)
        {
            _previewPane?.SetTitle(I18N.Get(titleKey));
        }

        private void DisposeEditors()
        {
            CloseTemporaryAssetManager();
            _executionView?.Dispose();
            _executionView = null;
            _inspectionView?.Dispose();
            _inspectionView = null;
            _executionCategory = null;
            _assetManagerView?.Dispose();
            _assetManagerView = null;
            _materials.DisposeMaterialEditor();
            _faceExpressionEditor?.Dispose();
            _faceExpressionEditor = null;
            DisposePreview();
        }

        private void DisposePreview()
        {
            _avatarContext.Preview?.Dispose();
            _avatarContext.Preview = null;
        }
    }
}
