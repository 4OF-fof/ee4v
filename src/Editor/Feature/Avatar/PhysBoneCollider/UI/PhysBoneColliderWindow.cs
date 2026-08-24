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
        private const float DefaultMinimumBoneLength = 0.08f;

        private readonly VrchatPhysBoneColliderGateway _gateway =
            new VrchatPhysBoneColliderGateway();
        private IReadOnlyList<PhysBoneColliderDraft> _drafts =
            Array.Empty<PhysBoneColliderDraft>();
        private PhysBoneColliderPreview _preview;
        private GameObject _avatar;
        private Transform _armature;
        private int _selectedIndex = -1;
        private ObjectField _avatarField;
        private FloatField _minimumLengthField;
        private ScrollView _candidateList;
        private VisualElement _detail;
        private UiTextElement _selectedBone;
        private Toggle _enabledField;
        private FloatField _radiusField;
        private FloatField _heightField;
        private FloatField _positionXField;
        private FloatField _positionYField;
        private FloatField _positionZField;
        private Toggle _assignField;
        private HelpBox _status;
        private UiTextButton _applyButton;
        private IMGUIContainer _previewElement;
        private bool _renderingDetail;

        [MenuItem("ee4v/Window/Avatar/PhysBone Collider Setup")]
        private static void Open()
        {
            var window = GetWindow<PhysBoneColliderWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            window.minSize = new Vector2(820f, 560f);
            window.Show();
        }

        private void OnDisable()
        {
            _preview?.Dispose();
            _preview = null;
        }

        private void CreateGUI()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/UI/Components/Inputs/ui-button.uss",
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

            _preview = new PhysBoneColliderPreview(() => _previewElement?.MarkDirtyRepaint());
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
            toolbar.Add(_avatarField);

            _minimumLengthField = UiTextFactory.CreateFloatField(
                I18N.Get("field.minimumLength"),
                "ee4v-physbone-collider__minimum-length");
            _minimumLengthField.value = DefaultMinimumBoneLength;
            toolbar.Add(_minimumLengthField);

            toolbar.Add(UiTextFactory.CreateButton(
                I18N.Get("action.scan"),
                RebuildLayout,
                "ee4v-physbone-collider__scan"));
            root.Add(toolbar);
        }

        private VisualElement BuildPreviewPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("ee4v-physbone-collider__preview-pane");
            _previewElement = new IMGUIContainer(() =>
                _preview?.Draw(_previewElement.contentRect));
            _previewElement.AddToClassList("ee4v-physbone-collider__preview");
            pane.Add(_previewElement);

            var resetView = UiTextFactory.CreateButton(
                I18N.Get("action.resetView"),
                () => _preview?.ResetView(),
                "ee4v-physbone-collider__reset-view");
            pane.Add(resetView);

            pane.Add(UiTextFactory.Create(
                I18N.Get("preview.controls"),
                "ee4v-physbone-collider__preview-help"));
            return pane;
        }

        private VisualElement BuildEditorPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("ee4v-physbone-collider__editor-pane");
            pane.Add(UiTextFactory.Create(
                I18N.Get("section.candidates"),
                UiClassNames.SectionTitle));

            _candidateList = new ScrollView(ScrollViewMode.Vertical);
            _candidateList.AddToClassList("ee4v-physbone-collider__candidates");
            pane.Add(_candidateList);

            _detail = BuildDetail();
            pane.Add(_detail);

            _assignField = UiTextFactory.CreateToggle(
                I18N.Get("field.assignToPhysBones"),
                "ee4v-physbone-collider__assign");
            _assignField.value = true;
            pane.Add(_assignField);

            _applyButton = UiTextFactory.CreateButton(
                I18N.Get("action.apply"),
                Apply,
                "ee4v-physbone-collider__apply");
            pane.Add(_applyButton);
            return pane;
        }

        private VisualElement BuildDetail()
        {
            var detail = new VisualElement();
            detail.AddToClassList("ee4v-physbone-collider__detail");
            _selectedBone = UiTextFactory.Create(
                string.Empty,
                "ee4v-physbone-collider__selected-bone");
            detail.Add(_selectedBone);

            _enabledField = UiTextFactory.CreateToggle(I18N.Get("field.enabled"));
            _enabledField.RegisterValueChangedCallback(evt =>
            {
                if (!_renderingDetail && TryGetSelected(out var draft))
                {
                    draft.Enabled = evt.newValue;
                    RefreshPreview();
                    RefreshCandidateList();
                }
            });
            detail.Add(_enabledField);

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
            detail.Add(_radiusField);

            _heightField = CreateDetailField(I18N.Get("field.height"), value =>
            {
                if (TryGetSelected(out var draft))
                {
                    draft.Height = Mathf.Max(draft.Radius * 2f, value);
                    RenderDetail();
                    RefreshPreview();
                }
            });
            detail.Add(_heightField);

            var positionLabel = UiTextFactory.Create(
                I18N.Get("field.position"),
                UiClassNames.FormLabel);
            detail.Add(positionLabel);
            var position = new VisualElement();
            position.AddToClassList("ee4v-physbone-collider__position");
            _positionXField = CreatePositionField("X", 0);
            _positionYField = CreatePositionField("Y", 1);
            _positionZField = CreatePositionField("Z", 2);
            position.Add(_positionXField);
            position.Add(_positionYField);
            position.Add(_positionZField);
            detail.Add(position);

            detail.Add(UiTextFactory.CreateButton(
                I18N.Get("action.resetCollider"),
                () =>
                {
                    if (TryGetSelected(out var draft))
                    {
                        draft.Reset();
                        RenderDetail();
                        RefreshPreview();
                        RefreshCandidateList();
                    }
                }));
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

        private void SetAvatar(GameObject avatar)
        {
            _avatar = avatar;
            RebuildLayout();
        }

        private void RebuildLayout()
        {
            _selectedIndex = -1;
            _armature = BoneColliderLayout.FindArmature(_avatar);
            _drafts = BoneColliderLayout.Create(
                _avatar,
                Mathf.Max(0.001f, _minimumLengthField?.value ?? DefaultMinimumBoneLength));
            _gateway.LoadOwned(_avatar, _drafts);
            if (_drafts.Count > 0)
            {
                _selectedIndex = 0;
            }

            _preview?.SetAvatar(_avatar, _drafts, _selectedIndex);
            Render();
        }

        private void Render()
        {
            RefreshCandidateList();
            RenderDetail();
            _applyButton?.SetEnabled(
                _avatar != null &&
                !EditorUtility.IsPersistent(_avatar) &&
                _gateway.IsAvailable &&
                HasEnabledDrafts());

            if (_avatar == null)
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
                UiTextFactory.SetText(
                    _status,
                    I18N.Get("status.candidateCount", _drafts.Count));
                _status.messageType = HelpBoxMessageType.Info;
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

            var byBone = new Dictionary<Transform, int>();
            for (var index = 0; index < _drafts.Count; index++)
            {
                byBone[_drafts[index].Bone] = index;
            }

            var tree = CreateHierarchyBranch(_armature, byBone, true);
            if (tree != null)
            {
                _candidateList.Add(tree);
            }
        }

        private VisualElement CreateHierarchyBranch(
            Transform bone,
            IReadOnlyDictionary<Transform, int> byBone,
            bool isRoot)
        {
            var children = new List<VisualElement>();
            for (var index = 0; index < bone.childCount; index++)
            {
                var child = CreateHierarchyBranch(
                    bone.GetChild(index),
                    byBone,
                    false);
                if (child != null)
                {
                    children.Add(child);
                }
            }

            var isCandidate = byBone.TryGetValue(bone, out var candidateIndex);
            if (!isRoot && !isCandidate && children.Count == 0)
            {
                return null;
            }

            var branch = new VisualElement();
            branch.AddToClassList("ee4v-physbone-collider__branch");
            var row = new VisualElement();
            row.AddToClassList("ee4v-physbone-collider__candidate");
            if (isCandidate)
            {
                var draft = _drafts[candidateIndex];
                row.EnableInClassList(
                    "ee4v-physbone-collider__candidate--selected",
                    candidateIndex == _selectedIndex);
                var toggle = UiTextFactory.CreateToggle();
                toggle.SetValueWithoutNotify(draft.Enabled);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    draft.Enabled = evt.newValue;
                    _selectedIndex = candidateIndex;
                    RenderDetail();
                    RefreshPreview();
                    RefreshCandidateList();
                });
                row.Add(toggle);
                var select = UiTextFactory.CreateButton(
                    bone.name,
                    () =>
                    {
                        _selectedIndex = candidateIndex;
                        RenderDetail();
                        RefreshPreview();
                        RefreshCandidateList();
                    });
                select.tooltip = draft.Path;
                row.Add(select);
            }
            else
            {
                row.AddToClassList("ee4v-physbone-collider__candidate--group");
                row.Add(UiTextFactory.Create(
                    bone.name,
                    "ee4v-physbone-collider__group-name"));
            }

            branch.Add(row);
            if (children.Count > 0)
            {
                var childContainer = new VisualElement();
                childContainer.AddToClassList(
                    "ee4v-physbone-collider__branch-children");
                foreach (var child in children)
                {
                    childContainer.Add(child);
                }

                branch.Add(childContainer);
            }

            return branch;
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
            _selectedBone.SetText(draft.Path);
            _enabledField.SetValueWithoutNotify(draft.Enabled);
            _radiusField.SetValueWithoutNotify(draft.Radius);
            _heightField.SetValueWithoutNotify(draft.Height);
            _positionXField.SetValueWithoutNotify(draft.Position.x);
            _positionYField.SetValueWithoutNotify(draft.Position.y);
            _positionZField.SetValueWithoutNotify(draft.Position.z);
            _renderingDetail = false;
        }

        private void RefreshPreview()
        {
            _preview?.Update(_drafts, _selectedIndex);
            _applyButton?.SetEnabled(
                _avatar != null &&
                !EditorUtility.IsPersistent(_avatar) &&
                _gateway.IsAvailable &&
                HasEnabledDrafts());
        }

        private void Apply()
        {
            if (!_gateway.TryApply(
                    _avatar,
                    _drafts,
                    _assignField.value,
                    out var created,
                    out var error))
            {
                SetStatus("status." + error, HelpBoxMessageType.Error);
                return;
            }

            if (_avatar.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(_avatar.scene);
            }

            UiTextFactory.SetText(_status, I18N.Get("status.applied", created));
            _status.messageType = HelpBoxMessageType.Info;
        }

        private void SetStatus(string key, HelpBoxMessageType type)
        {
            if (_status == null)
            {
                return;
            }

            UiTextFactory.SetText(_status, I18N.Get(key));
            _status.messageType = type;
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
