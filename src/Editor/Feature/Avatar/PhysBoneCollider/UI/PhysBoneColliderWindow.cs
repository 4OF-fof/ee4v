using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.PhysBoneCollider
{
    internal sealed class PhysBoneColliderWindow : EditorWindow
    {
        internal sealed class EmbeddedEditor : IDisposable
        {
            private PhysBoneColliderWindow _controller;

            internal EmbeddedEditor(PhysBoneColliderWindow controller)
            {
                _controller = controller;
            }

            public void Dispose()
            {
                if (_controller == null)
                {
                    return;
                }

                _controller.DisposeResources();
                _controller._embeddedRoot = null;
                UnityEngine.Object.DestroyImmediate(_controller);
                _controller = null;
            }
        }

        private const float DefaultMinimumBoneLength = 0.08f;

        private readonly VrchatPhysBoneColliderGateway _gateway =
            new VrchatPhysBoneColliderGateway();
        private readonly List<GameObject> _physBoneSearchRoots =
            new List<GameObject>();
        private IReadOnlyList<PhysBoneColliderDraft> _drafts =
            Array.Empty<PhysBoneColliderDraft>();
        private IReadOnlyList<PhysBoneTarget> _physBoneTargets =
            Array.Empty<PhysBoneTarget>();
        private PhysBoneColliderPreview _preview;
        private GameObject _avatar;
        private string _selectedPhysBonePath;
        private Transform _armature;
        private int _selectedIndex = -1;
        private ObjectField _avatarField;
        private PopupField<ColliderLayoutPreset> _presetField;
        private FloatField _minimumLengthField;
        private ScrollView _candidateList;
        private VisualElement _detail;
        private UiTextElement _selectedBone;
        private Toggle _enabledField;
        private ObjectField _boneField;
        private FloatField _radiusField;
        private FloatField _heightField;
        private FloatField _positionXField;
        private FloatField _positionYField;
        private FloatField _positionZField;
        private FloatField _rotationXField;
        private FloatField _rotationYField;
        private FloatField _rotationZField;
        private ScrollView _physBoneList;
        private VisualElement _physBoneRootList;
        private Toggle _showCollidersField;
        private Toggle _showPhysBonesField;
        private HelpBox _status;
        private UiButton _applyButton;
        private ScenePreviewViewport _previewViewport;
        private VisualElement _embeddedRoot;
        private GameObject _embeddedSourceAvatar;
        private GameObject _loadedPrefabContents;
        private string _embeddedPrefabPath = string.Empty;
        private Action _embeddedSaved;
        private bool _avatarLocked;
        private bool _embeddedLoadFailed;
        private bool _renderingDetail;

        [MenuItem("ee4v/Window/Avatar/PhysBone Collider Setup")]
        private static void Open()
        {
            var window = GetWindow<PhysBoneColliderWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            window.minSize = new Vector2(900f, 680f);
            window.Show();
        }

        internal static EmbeddedEditor Embed(
            VisualElement root,
            GameObject avatar,
            Action saved)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var controller = CreateInstance<PhysBoneColliderWindow>();
            controller.hideFlags = HideFlags.HideAndDontSave;
            controller._embeddedRoot = root;
            controller._avatarLocked = true;
            controller._embeddedSaved = saved;
            controller.BuildContent();
            controller.SetEmbeddedAvatar(avatar);
            return new EmbeddedEditor(controller);
        }

        private void OnDisable()
        {
            DisposeResources();
        }

        private void CreateGUI()
        {
            if (_embeddedRoot != null)
            {
                return;
            }

            BuildContent();
            var selected = FindAvatarRoot(Selection.activeGameObject);
            if (selected != null)
            {
                _avatarField.SetValueWithoutNotify(selected);
                SetAvatar(selected);
            }
            else
            {
                Render();
            }
        }

        private void BuildContent()
        {
            if (_embeddedRoot == null)
            {
                titleContent = UiTextFactory.CreateGuiContent(
                    I18N.Get("window.title"));
            }

            var root = _embeddedRoot ?? rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/Feature/Avatar/PhysBoneCollider/UI/physbone-collider.uss");
            root.AddToClassList("ee4v-physbone-collider");

            BuildToolbar(root);
            _status = UiTextFactory.CreateHelpBox(
                I18N.Get("status.selectAvatar"),
                HelpBoxMessageType.Info,
                "ee4v-physbone-collider__status");
            root.Add(_status);

            var workspace = new VisualElement();
            workspace.AddToClassList("ee4v-physbone-collider__workspace");
            workspace.Add(BuildPreviewPane());
            workspace.Add(BuildEditorPane());
            root.Add(workspace);

            _preview?.Dispose();
            _preview = new PhysBoneColliderPreview(
                () => _previewViewport?.RequestRepaint());
        }

        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-physbone-collider__toolbar");

            _avatarField = UiTextFactory.CreateObjectField(
                I18N.Get("field.avatar"),
                "ee4v-physbone-collider__avatar");
            _avatarField.objectType = typeof(GameObject);
            _avatarField.allowSceneObjects = true;
            _avatarField.RegisterValueChangedCallback(evt =>
                SetAvatar(evt.newValue as GameObject));
            _avatarField.SetEnabled(!_avatarLocked);
            toolbar.Add(_avatarField);
            root.Add(toolbar);
        }

        private VisualElement BuildPreviewPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("ee4v-physbone-collider__preview-pane");
            var toolbar = new VisualElement();
            toolbar.AddToClassList(
                "ee4v-physbone-collider__preview-toolbar");
            toolbar.Add(UiTextFactory.Create(
                I18N.Get("preview.title"),
                UiClassNames.SectionTitle,
                "ee4v-physbone-collider__preview-title"));
            pane.Add(toolbar);

            _previewViewport = new ScenePreviewViewport(
                rect => _preview?.Draw(rect),
                () => _preview?.ResetView(),
                I18N.Get("action.toggleBackground"),
                I18N.Get("action.resetView"));
            _previewViewport.AddToClassList(
                "ee4v-physbone-collider__preview-viewport");

            var layers = new VisualElement();
            layers.AddToClassList("ee4v-physbone-collider__preview-layers");
            _showCollidersField = UiTextFactory.CreateToggle(
                I18N.Get("preview.showColliders"));
            _showCollidersField.value = true;
            _showCollidersField.RegisterValueChangedCallback(_ =>
                UpdatePreviewVisibility());
            layers.Add(_showCollidersField);
            _showPhysBonesField = UiTextFactory.CreateToggle(
                I18N.Get("preview.showPhysBones"));
            _showPhysBonesField.value = true;
            _showPhysBonesField.RegisterValueChangedCallback(_ =>
                UpdatePreviewVisibility());
            layers.Add(_showPhysBonesField);
            _previewViewport.FeatureOverlay.Add(layers);

            pane.Add(_previewViewport);
            return pane;
        }

        private VisualElement BuildEditorPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("ee4v-physbone-collider__editor-pane");
            var content = new ScrollView(ScrollViewMode.Vertical);
            content.AddToClassList("ee4v-physbone-collider__editor-content");
            content.Add(BuildLayoutControls());
            content.Add(new SectionHeader(
                I18N.Get("section.candidates")));

            _candidateList = new ScrollView(ScrollViewMode.Vertical);
            _candidateList.AddToClassList("ee4v-physbone-collider__candidates");
            content.Add(_candidateList);

            _detail = BuildDetail();
            content.Add(_detail);
            pane.Add(content);

            _applyButton = CreateButton(
                I18N.Get("action.apply"),
                Apply,
                "ee4v-physbone-collider__apply");
            pane.Add(_applyButton);
            return pane;
        }

        private VisualElement BuildLayoutControls()
        {
            var controls = new VisualElement();
            controls.AddToClassList("ee4v-physbone-collider__layout-controls");
            controls.Add(new SectionHeader(
                I18N.Get("section.layout")));

            var presets = new List<ColliderLayoutPreset>
            {
                ColliderLayoutPreset.Lightweight,
                ColliderLayoutPreset.Standard,
                ColliderLayoutPreset.Full
            };
            _presetField = UiTextFactory.CreatePopupField(
                I18N.Get("field.preset"),
                presets,
                presets.IndexOf(ColliderLayoutPreset.Standard),
                FormatPreset,
                FormatPreset,
                "ee4v-physbone-collider__preset");
            _presetField.RegisterValueChangedCallback(_ => RebuildLayout());
            controls.Add(_presetField);

            var scanRow = new VisualElement();
            scanRow.AddToClassList("ee4v-physbone-collider__scan-row");
            _minimumLengthField = UiTextFactory.CreateFloatField(
                I18N.Get("field.minimumLength"),
                "ee4v-physbone-collider__minimum-length");
            _minimumLengthField.value = DefaultMinimumBoneLength;
            scanRow.Add(_minimumLengthField);
            scanRow.Add(CreateButton(
                I18N.Get("action.scan"),
                RebuildLayout,
                "ee4v-physbone-collider__scan"));
            controls.Add(scanRow);
            return controls;
        }

        private VisualElement BuildDetail()
        {
            var detail = new VisualElement();
            detail.AddToClassList("ee4v-physbone-collider__detail");
            var placement = new VisualElement();
            placement.AddToClassList("ee4v-physbone-collider__detail-group");
            placement.Add(new SectionHeader(
                I18N.Get("section.collider")));
            _selectedBone = UiTextFactory.Create(
                string.Empty,
                "ee4v-physbone-collider__selected-bone");
            placement.Add(_selectedBone);

            _enabledField = UiTextFactory.CreateToggle(I18N.Get("field.enabled"));
            _enabledField.RegisterValueChangedCallback(evt =>
            {
                if (!_renderingDetail && TryGetSelected(out var draft))
                {
                    draft.Enabled = evt.newValue;
                    RefreshPreview();
                    Render();
                }
            });
            placement.Add(_enabledField);

            _boneField = UiTextFactory.CreateObjectField(
                I18N.Get("field.followBone"));
            _boneField.objectType = typeof(Transform);
            _boneField.allowSceneObjects = true;
            _boneField.RegisterValueChangedCallback(evt =>
            {
                if (_renderingDetail || !TryGetSelected(out var draft))
                {
                    return;
                }

                var bone = evt.newValue as Transform;
                if (bone == null ||
                    _avatar == null ||
                    (bone != _avatar.transform && !bone.IsChildOf(_avatar.transform)))
                {
                    _boneField.SetValueWithoutNotify(draft.Bone);
                    return;
                }

                draft.Rebind(bone, _avatar.transform);
                RenderDetail();
                RefreshPreview();
            });
            placement.Add(_boneField);

            _radiusField = CreateDetailField(I18N.Get("field.radius"), value =>
            {
                if (TryGetSelected(out var draft))
                {
                    draft.Radius = Mathf.Max(0.001f, value);
                    draft.Height = Mathf.Max(draft.Height, draft.Radius * 2f);
                    RenderDetail();
                    RefreshPreview();
                }
            });
            _heightField = CreateDetailField(I18N.Get("field.height"), value =>
            {
                if (TryGetSelected(out var draft))
                {
                    draft.Height = Mathf.Max(draft.Radius * 2f, value);
                    RenderDetail();
                    RefreshPreview();
                }
            });
            var size = new VisualElement();
            size.AddToClassList("ee4v-physbone-collider__size");
            size.Add(_radiusField);
            size.Add(_heightField);
            placement.Add(size);

            var positionLabel = UiTextFactory.Create(
                I18N.Get("field.position"),
                UiClassNames.FormLabel);
            placement.Add(positionLabel);
            var position = new VisualElement();
            position.AddToClassList("ee4v-physbone-collider__position");
            _positionXField = CreatePositionField("X", 0);
            _positionYField = CreatePositionField("Y", 1);
            _positionZField = CreatePositionField("Z", 2);
            position.Add(_positionXField);
            position.Add(_positionYField);
            position.Add(_positionZField);
            placement.Add(position);

            placement.Add(UiTextFactory.Create(
                I18N.Get("field.rotation"),
                UiClassNames.FormLabel));
            var rotation = new VisualElement();
            rotation.AddToClassList("ee4v-physbone-collider__rotation");
            _rotationXField = CreateRotationField("X", 0);
            _rotationYField = CreateRotationField("Y", 1);
            _rotationZField = CreateRotationField("Z", 2);
            rotation.Add(_rotationXField);
            rotation.Add(_rotationYField);
            rotation.Add(_rotationZField);
            placement.Add(rotation);

            placement.Add(CreateButton(
                I18N.Get("action.resetCollider"),
                () =>
                {
                    if (TryGetSelected(out var draft))
                    {
                        draft.Reset();
                        RefreshPreview();
                        Render();
                    }
                }));
            detail.Add(placement);

            var assignments = new VisualElement();
            assignments.AddToClassList("ee4v-physbone-collider__detail-group");
            var assignmentsHeader = new SectionHeader(
                I18N.Get("section.physBones"));
            assignmentsHeader.AddToClassList(
                "ee4v-physbone-collider__physbone-title");
            assignments.Add(assignmentsHeader);
            _physBoneRootList = new VisualElement();
            _physBoneRootList.AddToClassList(
                "ee4v-physbone-collider__physbone-roots");
            assignments.Add(_physBoneRootList);
            assignments.Add(CreateButton(
                I18N.Get("action.addPhysBoneRoot"),
                AddPhysBoneSearchRoot,
                "ee4v-physbone-collider__add-physbone-root"));
            assignments.Add(CreateButton(
                I18N.Get("action.detectPhysBones"),
                DetectPhysBones,
                "ee4v-physbone-collider__detect-physbones"));
            var targetActions = new VisualElement();
            targetActions.AddToClassList("ee4v-physbone-collider__target-actions");
            targetActions.Add(CreateButton(
                I18N.Get("action.selectAllPhysBones"),
                () => SetAllPhysBoneTargets(true)));
            targetActions.Add(CreateButton(
                I18N.Get("action.clearPhysBones"),
                () => SetAllPhysBoneTargets(false)));
            assignments.Add(targetActions);
            _physBoneList = new ScrollView(ScrollViewMode.Vertical);
            _physBoneList.AddToClassList("ee4v-physbone-collider__physbones");
            assignments.Add(_physBoneList);
            detail.Add(assignments);
            EnsurePhysBoneSearchRootRow();
            RenderPhysBoneSearchRoots();
            return detail;
        }

        private FloatField CreateDetailField(string label, Action<float> changed)
        {
            var field = UiTextFactory.CreateFloatField(label);
            field.RegisterValueChangedCallback(evt =>
            {
                if (!_renderingDetail)
                {
                    changed(evt.newValue);
                }
            });
            return field;
        }

        private FloatField CreatePositionField(string label, int axis)
        {
            return CreateDetailField(label, value =>
            {
                if (!TryGetSelected(out var draft))
                {
                    return;
                }

                var position = draft.Position;
                position[axis] = value;
                draft.Position = position;
                RefreshPreview();
            });
        }

        private FloatField CreateRotationField(string label, int axis)
        {
            return CreateDetailField(label, value =>
            {
                if (!TryGetSelected(out var draft))
                {
                    return;
                }

                var rotation = draft.Rotation.eulerAngles;
                rotation[axis] = value;
                draft.Rotation = Quaternion.Euler(rotation);
                RefreshPreview();
            });
        }

        private void SetAvatar(GameObject avatar)
        {
            if (_avatar != avatar)
            {
                _physBoneSearchRoots.Clear();
                _physBoneSearchRoots.Add(_avatarLocked ? avatar : null);
                _physBoneTargets = Array.Empty<PhysBoneTarget>();
                _selectedPhysBonePath = null;
                RenderPhysBoneSearchRoots();
            }

            _avatar = avatar;
            RebuildLayout();
        }

        private void RebuildLayout()
        {
            _selectedIndex = -1;
            _armature = BoneColliderLayout.FindArmature(_avatar);
            RefreshPhysBoneTargets();
            _drafts = BoneColliderLayout.Create(
                _avatar,
                Mathf.Max(0.001f, _minimumLengthField?.value ?? DefaultMinimumBoneLength),
                _presetField?.value ?? ColliderLayoutPreset.Standard);
            _gateway.LoadOwned(_avatar, _drafts);
            AssignDetectedPhysBonesToNewDrafts();
            if (_drafts.Count > 0)
            {
                _selectedIndex = 0;
            }

            RebuildPreview();
            Render();
        }

        private void DetectPhysBones()
        {
            var rootsChanged = false;
            for (var index = 0; index < _physBoneSearchRoots.Count; index++)
            {
                var root = _physBoneSearchRoots[index];
                if (root != null && !IsPhysBoneSearchRootValid(root))
                {
                    _physBoneSearchRoots[index] = null;
                    rootsChanged = true;
                }
            }

            if (rootsChanged)
            {
                RenderPhysBoneSearchRoots();
            }

            RefreshPhysBoneTargets();
            if (!ContainsPhysBoneTarget(_selectedPhysBonePath))
            {
                _selectedPhysBonePath = null;
            }

            AssignDetectedPhysBonesToNewDrafts();
            RebuildPreview();
            Render();
        }

        private void RefreshPhysBoneTargets()
        {
            _physBoneTargets = _gateway.FindPhysBoneTargets(
                _avatar,
                _physBoneSearchRoots);
        }

        private void EnsurePhysBoneSearchRootRow()
        {
            if (_physBoneSearchRoots.Count == 0)
            {
                _physBoneSearchRoots.Add(null);
            }
        }

        private void AddPhysBoneSearchRoot()
        {
            _physBoneSearchRoots.Add(null);
            RenderPhysBoneSearchRoots();
        }

        private void RemovePhysBoneSearchRoot(int index)
        {
            if (index < 0 || index >= _physBoneSearchRoots.Count)
            {
                return;
            }

            _physBoneSearchRoots.RemoveAt(index);
            EnsurePhysBoneSearchRootRow();
            RenderPhysBoneSearchRoots();
            DetectPhysBones();
        }

        private void RenderPhysBoneSearchRoots()
        {
            if (_physBoneRootList == null)
            {
                return;
            }

            EnsurePhysBoneSearchRootRow();
            _physBoneRootList.Clear();
            for (var index = 0; index < _physBoneSearchRoots.Count; index++)
            {
                var rootIndex = index;
                var row = new VisualElement();
                row.AddToClassList("ee4v-physbone-collider__physbone-root-row");
                var field = UiTextFactory.CreateObjectField(
                    I18N.Get("field.physBoneSearchRoot", index + 1),
                    "ee4v-physbone-collider__physbone-root");
                field.objectType = typeof(GameObject);
                field.allowSceneObjects = true;
                field.SetValueWithoutNotify(_physBoneSearchRoots[index]);
                field.RegisterValueChangedCallback(evt =>
                {
                    var root = evt.newValue as GameObject;
                    if (root != null && !IsPhysBoneSearchRootValid(root))
                    {
                        root = null;
                        field.SetValueWithoutNotify(null);
                    }

                    _physBoneSearchRoots[rootIndex] = root;
                    DetectPhysBones();
                });
                row.Add(field);
                row.Add(CreateButton(
                    I18N.Get("action.removePhysBoneRoot"),
                    () => RemovePhysBoneSearchRoot(rootIndex),
                    "ee4v-physbone-collider__remove-physbone-root"));
                _physBoneRootList.Add(row);
            }
        }

        private static UiButton CreateButton(
            string label,
            Action onClick,
            string className = null)
        {
            var button = new UiButton(label, onClick);
            if (!string.IsNullOrWhiteSpace(className))
            {
                button.AddToClassList(className);
            }

            return button;
        }

        private bool HasPhysBoneSearchRoot()
        {
            foreach (var root in _physBoneSearchRoots)
            {
                if (IsPhysBoneSearchRootValid(root))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPhysBoneSearchRootValid(GameObject root)
        {
            return root != null &&
                   _avatar != null &&
                   (root.transform == _avatar.transform ||
                    root.transform.IsChildOf(_avatar.transform));
        }

        private bool ContainsPhysBoneTarget(string path)
        {
            if (path == null)
            {
                return false;
            }

            foreach (var target in _physBoneTargets)
            {
                if (string.Equals(target.Path, path, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void AssignDetectedPhysBonesToNewDrafts()
        {
            foreach (var draft in _drafts)
            {
                if (draft.AssignmentsLoaded)
                {
                    continue;
                }

                draft.AssignedPhysBonePaths.Clear();
                foreach (var target in _physBoneTargets)
                {
                    draft.AssignedPhysBonePaths.Add(target.Path);
                }
            }
        }

        private void RebuildPreview()
        {
            _preview?.SetAvatar(
                _avatar,
                _drafts,
                _physBoneTargets,
                _selectedIndex,
                _selectedPhysBonePath);
            _previewViewport?.SetPreviewAvailable(_avatar != null);
            UpdatePreviewVisibility();
        }

        private void UpdatePreviewVisibility()
        {
            _preview?.SetVisibility(
                _showCollidersField?.value ?? true,
                _showPhysBonesField?.value ?? true);
        }

        private void Render()
        {
            RefreshCandidateList();
            RenderDetail();
            _applyButton?.SetEnabled(
                _avatar != null &&
                !_embeddedLoadFailed &&
                !EditorUtility.IsPersistent(_avatar) &&
                _gateway.IsAvailable &&
                HasEnabledDrafts());

            if (_embeddedLoadFailed)
            {
                SetStatus(
                    "status.prefabLoadFailed",
                    HelpBoxMessageType.Error);
            }
            else if (_avatar == null)
            {
                SetStatus("status.selectAvatar", HelpBoxMessageType.Info);
            }
            else if (EditorUtility.IsPersistent(_avatar))
            {
                SetStatus("status.sceneAvatarRequired", HelpBoxMessageType.Warning);
            }
            else if (_armature == null)
            {
                SetStatus("status.armatureMissing", HelpBoxMessageType.Info);
            }
            else if (!_gateway.IsAvailable)
            {
                SetStatus(
                    _gateway.IsSdkAvailable
                        ? "status.maMissing"
                        : "status.sdkMissing",
                    HelpBoxMessageType.Warning);
            }
            else if (_drafts.Count == 0)
            {
                SetStatus("status.noCandidates", HelpBoxMessageType.Info);
            }
            else
            {
                _status.EnableInClassList(
                    "ee4v-physbone-collider__status--hidden",
                    true);
            }
        }

        private void RefreshCandidateList()
        {
            if (_candidateList == null)
            {
                return;
            }

            _candidateList.Clear();
            if (_armature == null)
            {
                return;
            }

            for (var index = 0; index < _drafts.Count; index++)
            {
                _candidateList.Add(CreateCandidateRow(index));
            }
        }

        private VisualElement CreateCandidateRow(int candidateIndex)
        {
            var draft = _drafts[candidateIndex];
            var row = new PhysBoneSelectionRow(
                enabled =>
                {
                    draft.Enabled = enabled;
                    _selectedIndex = candidateIndex;
                    RefreshPreview();
                    Render();
                },
                () =>
                {
                    _selectedIndex = candidateIndex;
                    RenderDetail();
                    RefreshPreview();
                    RefreshCandidateList();
                });
            row.AddToClassList("ee4v-physbone-collider__candidate");
            row.SetState(
                FormatCandidate(draft),
                draft.SuggestedPath + " → " + draft.TargetPath,
                draft.Enabled,
                candidateIndex == _selectedIndex);
            return row;
        }

        private void RenderDetail()
        {
            if (_detail == null)
            {
                return;
            }

            var hasSelection = TryGetSelected(out var draft);
            _detail.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasSelection)
            {
                return;
            }

            _renderingDetail = true;
            _selectedBone.SetText(FormatCandidate(draft));
            _selectedBone.tooltip = draft.Path + " → " + draft.TargetPath;
            _enabledField.SetValueWithoutNotify(draft.Enabled);
            _boneField.SetValueWithoutNotify(draft.Bone);
            _radiusField.SetValueWithoutNotify(draft.Radius);
            _heightField.SetValueWithoutNotify(draft.Height);
            _positionXField.SetValueWithoutNotify(draft.Position.x);
            _positionYField.SetValueWithoutNotify(draft.Position.y);
            _positionZField.SetValueWithoutNotify(draft.Position.z);
            var rotation = draft.Rotation.eulerAngles;
            _rotationXField.SetValueWithoutNotify(rotation.x);
            _rotationYField.SetValueWithoutNotify(rotation.y);
            _rotationZField.SetValueWithoutNotify(rotation.z);
            _renderingDetail = false;
            RenderPhysBoneTargets(draft);
        }

        private void RefreshPreview()
        {
            _preview?.Update(
                _drafts,
                _selectedIndex,
                _selectedPhysBonePath);
            _applyButton?.SetEnabled(
                _avatar != null &&
                !EditorUtility.IsPersistent(_avatar) &&
                _gateway.IsAvailable &&
                HasEnabledDrafts());
        }

        private void Apply()
        {
            RestrictAssignmentsToDetectedPhysBones();
            if (!_gateway.TryApply(
                    _avatar,
                    _drafts,
                    out var created,
                    out var error))
            {
                SetStatus("status." + error, HelpBoxMessageType.Error);
                return;
            }

            if (_avatar.scene.IsValid())
            {
                if (string.IsNullOrEmpty(_embeddedPrefabPath))
                {
                    EditorSceneManager.MarkSceneDirty(_avatar.scene);
                }
                else
                {
                    var saved = PrefabUtility.SaveAsPrefabAsset(
                        _avatar,
                        _embeddedPrefabPath);
                    if (saved == null)
                    {
                        SetStatus(
                            "status.prefabSaveFailed",
                            HelpBoxMessageType.Error);
                        return;
                    }

                    _embeddedSourceAvatar = saved;
                    _avatarField?.SetValueWithoutNotify(saved);
                    _embeddedSaved?.Invoke();
                }
            }

            UiTextFactory.SetText(_status, I18N.Get("status.applied", created));
            _status.messageType = HelpBoxMessageType.Info;
            _status.EnableInClassList(
                "ee4v-physbone-collider__status--hidden",
                false);
        }

        private void SetEmbeddedAvatar(GameObject avatar)
        {
            _embeddedSourceAvatar = avatar;
            _avatarField?.SetValueWithoutNotify(avatar);
            _embeddedLoadFailed = false;
            _embeddedPrefabPath = string.Empty;

            if (avatar == null || !EditorUtility.IsPersistent(avatar))
            {
                SetAvatar(avatar);
                return;
            }

            var path = AssetDatabase.GetAssetPath(avatar);
            if (string.IsNullOrEmpty(path))
            {
                _embeddedLoadFailed = true;
                SetAvatar(null);
                return;
            }

            try
            {
                _embeddedPrefabPath = path;
                _loadedPrefabContents =
                    PrefabUtility.LoadPrefabContents(path);
                SetAvatar(_loadedPrefabContents);
                _avatarField?.SetValueWithoutNotify(
                    _embeddedSourceAvatar);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _embeddedLoadFailed = true;
                _embeddedPrefabPath = string.Empty;
                SetAvatar(null);
            }
        }

        private void DisposeResources()
        {
            _preview?.Dispose();
            _preview = null;
            _previewViewport?.Dispose();
            if (_loadedPrefabContents != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    _loadedPrefabContents);
                _loadedPrefabContents = null;
            }

            _avatar = null;
        }

        private void RestrictAssignmentsToDetectedPhysBones()
        {
            var detectedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in _physBoneTargets)
            {
                detectedPaths.Add(target.Path);
            }

            foreach (var draft in _drafts)
            {
                var removedPaths = new List<string>();
                foreach (var path in draft.AssignedPhysBonePaths)
                {
                    if (!detectedPaths.Contains(path))
                    {
                        removedPaths.Add(path);
                    }
                }

                foreach (var path in removedPaths)
                {
                    draft.AssignedPhysBonePaths.Remove(path);
                }
            }
        }

        private void SetStatus(string key, HelpBoxMessageType type)
        {
            if (_status == null)
            {
                return;
            }

            UiTextFactory.SetText(_status, I18N.Get(key));
            _status.messageType = type;
            _status.EnableInClassList(
                "ee4v-physbone-collider__status--hidden",
                false);
        }

        private bool TryGetSelected(out PhysBoneColliderDraft draft)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _drafts.Count)
            {
                draft = _drafts[_selectedIndex];
                return true;
            }

            draft = null;
            return false;
        }

        private bool HasEnabledDrafts()
        {
            for (var index = 0; index < _drafts.Count; index++)
            {
                if (_drafts[index].Enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private void RenderPhysBoneTargets(PhysBoneColliderDraft draft)
        {
            if (_physBoneList == null)
            {
                return;
            }

            _physBoneList.Clear();
            if (!HasPhysBoneSearchRoot())
            {
                _physBoneList.Add(UiTextFactory.Create(
                    I18N.Get("status.selectPhysBoneRoot"),
                    "ee4v-physbone-collider__empty-physbones"));
                return;
            }

            if (_physBoneTargets.Count == 0)
            {
                _physBoneList.Add(UiTextFactory.Create(
                    I18N.Get("status.noPhysBones"),
                    "ee4v-physbone-collider__empty-physbones"));
                return;
            }

            foreach (var target in _physBoneTargets)
            {
                var row = new PhysBoneSelectionRow(
                    selected =>
                    {
                        draft.AssignmentsLoaded = true;
                        if (selected)
                        {
                            draft.AssignedPhysBonePaths.Add(target.Path);
                            _selectedPhysBonePath = target.Path;
                        }
                        else
                        {
                            draft.AssignedPhysBonePaths.Remove(target.Path);
                            if (string.Equals(
                                    _selectedPhysBonePath,
                                    target.Path,
                                    StringComparison.Ordinal))
                            {
                                _selectedPhysBonePath = null;
                            }
                        }

                        RefreshPreview();
                        Render();
                    },
                    () =>
                    {
                        if (!draft.AssignedPhysBonePaths.Contains(target.Path))
                        {
                            return;
                        }

                        _selectedPhysBonePath = target.Path;
                        RefreshPreview();
                        RenderPhysBoneTargets(draft);
                    });
                row.AddToClassList("ee4v-physbone-collider__physbone-target");
                row.SetState(
                    FormatPhysBoneTarget(target),
                    target.Path,
                    draft.AssignedPhysBonePaths.Contains(target.Path),
                    draft.AssignedPhysBonePaths.Contains(target.Path) &&
                    string.Equals(
                        target.Path,
                        _selectedPhysBonePath,
                        StringComparison.Ordinal));
                _physBoneList.Add(row);
            }
        }

        private sealed class PhysBoneSelectionRow : ItemRow
        {
            private readonly Toggle _toggle;
            private readonly Action<bool> _onToggle;
            private readonly Action _onSelect;

            internal PhysBoneSelectionRow(
                Action<bool> onToggle,
                Action onSelect)
            {
                _onToggle = onToggle;
                _onSelect = onSelect;
                AddToClassList(
                    "ee4v-physbone-collider__selection-row");
                focusable = true;
                _toggle = UiTextFactory.CreateToggle();
                _toggle.AddToClassList(
                    "ee4v-physbone-collider__selection-toggle");
                _toggle.RegisterValueChangedCallback(evt =>
                    _onToggle?.Invoke(evt.newValue));
                Leading.Add(_toggle);
                RegisterCallback<ClickEvent>(OnClick);
                RegisterCallback<KeyDownEvent>(OnKeyDown);
            }

            internal void SetState(
                string title,
                string rowTooltip,
                bool toggled,
                bool selected)
            {
                base.SetState(new ItemRowState(title));
                tooltip = rowTooltip ?? string.Empty;
                _toggle.tooltip = tooltip;
                _toggle.SetValueWithoutNotify(toggled);
                EnableInClassList(
                    "ee4v-physbone-collider__selection-row--selected",
                    selected);
            }

            private void OnClick(ClickEvent evt)
            {
                if (evt.target is VisualElement target &&
                    _toggle.Contains(target))
                {
                    return;
                }

                _onSelect?.Invoke();
                evt.StopPropagation();
            }

            private void OnKeyDown(KeyDownEvent evt)
            {
                if (evt.keyCode != KeyCode.Return &&
                    evt.keyCode != KeyCode.Space)
                {
                    return;
                }

                _onSelect?.Invoke();
                evt.StopPropagation();
            }
        }

        private void SetAllPhysBoneTargets(bool selected)
        {
            if (!TryGetSelected(out var draft))
            {
                return;
            }

            draft.AssignmentsLoaded = true;
            draft.AssignedPhysBonePaths.Clear();
            if (selected)
            {
                foreach (var target in _physBoneTargets)
                {
                    draft.AssignedPhysBonePaths.Add(target.Path);
                }

                _selectedPhysBonePath = _physBoneTargets.Count > 0
                    ? _physBoneTargets[0].Path
                    : null;
            }
            else
            {
                _selectedPhysBonePath = null;
            }

            RefreshPreview();
            Render();
        }

        private string FormatPhysBoneTarget(PhysBoneTarget target)
        {
            var path = string.IsNullOrEmpty(target.Path)
                ? _avatar?.name ?? target.Transform.name
                : CompactPath(target.Path);
            return target.ComponentCount > 1
                ? I18N.Get("field.physBoneTargetCount", path, target.ComponentCount)
                : path;
        }

        private static string FormatCandidate(PhysBoneColliderDraft draft)
        {
            var targetName = draft.TargetPath;
            var separator = targetName.LastIndexOf('/');
            if (separator >= 0)
            {
                targetName = targetName.Substring(separator + 1);
            }

            return draft.SuggestedBone.name + " → " + targetName;
        }

        private static string CompactPath(string path)
        {
            var parts = path.Split('/');
            return parts.Length <= 2
                ? path
                : parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
        }

        private static string FormatPreset(ColliderLayoutPreset preset)
        {
            switch (preset)
            {
                case ColliderLayoutPreset.Lightweight:
                    return I18N.Get("preset.lightweight");
                case ColliderLayoutPreset.Full:
                    return I18N.Get("preset.full");
                default:
                    return I18N.Get("preset.standard");
            }
        }

        private static GameObject FindAvatarRoot(GameObject selected)
        {
            for (var current = selected?.transform; current != null; current = current.parent)
            {
                if (current.GetComponent<Animator>() != null &&
                    current.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                {
                    return current.gameObject;
                }
            }

            return selected;
        }
    }
}
