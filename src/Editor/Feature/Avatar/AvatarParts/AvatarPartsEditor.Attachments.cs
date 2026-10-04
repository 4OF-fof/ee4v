using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private readonly HashSet<int> _expandedAttachments = new HashSet<int>();

        private VisualElement BuildAttachmentControls(int index)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-part-attachments");
            if (_context.Root == null || index < 0 || index >= _context.Root.transform.childCount) { return panel; }
            var part = _context.Root.transform.GetChild(index);
            panel.Add(UiTextFactory.Create(part.name, UiClassNames.SectionTitle, "ee4v-part-attachments__part-name"));
            var merges = part.GetComponentsInChildren<ModularAvatarMergeArmature>(true);
            var proxies = part.GetComponentsInChildren<ModularAvatarBoneProxy>(true);
            foreach (var merge in merges)
            {
                var sourcePath = merge.transform == part ? part.name
                    : AnimationUtility.CalculateTransformPath(merge.transform, part);
                panel.Add(new BoneMergeSettings(merge, sourcePath, selected =>
                {
                    if (!ValidAttachmentTarget(part, selected?.transform)) { return false; }
                    ChangeAttachment(merge, () => merge.mergeTarget.Set(selected));
                    return merge.mergeTargetObject == selected;
                }));
            }
            foreach (var proxy in proxies)
            {
                panel.Add(BuildAttachmentDisclosure(proxy.GetInstanceID(),
                    I18N.Get("workflow.attachment.proxy"),
                    AttachmentSummary(part, proxy.transform, proxy.target), () =>
                {
                    var settings = new VisualElement();
                    var target = UiTextFactory.CreateObjectField();
                    target.objectType = typeof(Transform);
                    target.allowSceneObjects = true;
                    target.SetValueWithoutNotify(proxy.target);
                    target.RegisterValueChangedCallback(evt =>
                    {
                        var selected = evt.newValue as Transform;
                        if (!ValidAttachmentTarget(part, selected))
                        { target.SetValueWithoutNotify(proxy.target); return; }
                        ChangeAttachment(proxy, () => proxy.target = selected);
                    });
                    settings.Add(new FormInput(I18N.Get("workflow.attachment.target"), target));
                    var mode = UiTextFactory.CreateEnumField(string.Empty, proxy.attachmentMode);
                    mode.RegisterValueChangedCallback(evt => ChangeAttachment(proxy,
                        () => proxy.attachmentMode = (BoneProxyAttachmentMode)evt.newValue));
                    settings.Add(new FormInput(I18N.Get("workflow.attachment.mode"), mode));
                    var matchScale = UiTextFactory.CreateToggle(I18N.Get("workflow.attachment.matchScale"));
                    matchScale.SetValueWithoutNotify(proxy.matchScale);
                    matchScale.RegisterValueChangedCallback(evt => ChangeAttachment(proxy, () => proxy.matchScale = evt.newValue));
                    settings.Add(matchScale);
                    return settings;
                }));
            }
            if (merges.Length != 0 || proxies.Length != 0)
            {
                panel[panel.childCount - 1].AddToClassList("ee4v-part-attachment--last");
                return panel;
            }
            var armature = FindPartArmature(part);
            panel.Add(BuildAttachmentDisclosure(part.GetInstanceID(),
                I18N.Get("workflow.attachment.missing"),
                I18N.Get(armature != null ? "workflow.attachment.addMerge" : "workflow.attachment.addProxy"), () =>
                BuildNewAttachmentControls(part, armature)));
            panel[panel.childCount - 1].AddToClassList("ee4v-part-attachment--last");
            return panel;
        }

        private VisualElement BuildAttachmentDisclosure(int key, string title, string summary, Func<VisualElement> buildSettings)
        {
            var section = new VisualElement();
            section.AddToClassList("ee4v-part-attachment");
            var details = new VisualElement();
            details.AddToClassList("ee4v-part-attachment__details");
            var initialized = false;
            UiButton disclosure = null;
            void Update()
            {
                var expanded = _expandedAttachments.Contains(key);
                disclosure.SetIcon(FluentUiIcons.CreateState(
                    expanded ? "chevron_down.png" : "chevron_right.png", UiSizeTokens.Size12));
                details.EnableInClassList("ee4v-part-attachment__details--hidden", !expanded);
                if (expanded && !initialized)
                {
                    details.Add(buildSettings());
                    initialized = true;
                }
            }
            void Toggle()
            {
                if (!_expandedAttachments.Add(key)) { _expandedAttachments.Remove(key); }
                Update();
            }
            disclosure = new UiButton(title, Toggle, variant: UiButtonVariant.Ghost);
            disclosure.AddToClassList("ee4v-part-attachment__disclosure");
            var description = UiTextFactory.Create(summary, UiClassNames.SecondaryText,
                "ee4v-part-attachment__summary");
            description.tooltip = summary;
            description.RegisterCallback<ClickEvent>(_ => Toggle());
            section.Add(disclosure);
            section.Add(description);
            section.Add(details);
            Update();
            return section;
        }

        private string AttachmentSummary(Transform part, Transform owner, Transform target)
        {
            var path = owner == part ? part.name : AnimationUtility.CalculateTransformPath(owner, part);
            var destination = target != null
                ? AnimationUtility.CalculateTransformPath(target, _context.Root.transform)
                : I18N.Get("workflow.attachment.unassigned");
            if (target == _context.Root.transform) { destination = _context.Root.name; }
            return path + " → " + destination;
        }

        private VisualElement BuildNewAttachmentControls(Transform part, Transform armature)
        {
            var panel = new VisualElement();
            var animator = _context.Root.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => !candidate.transform.IsChildOf(part) && candidate.isHuman);
            var recommended = armature != null
                ? animator?.GetBoneTransform(HumanBodyBones.Hips)?.parent
                : animator?.GetBoneTransform(HumanBodyBones.Head);
            if (armature != null && recommended == null)
            {
                recommended = _context.Root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => !candidate.IsChildOf(part) && candidate != _context.Root.transform &&
                        string.Equals(candidate.name, armature.name, StringComparison.OrdinalIgnoreCase));
            }
            var targetField = UiTextFactory.CreateObjectField();
            targetField.objectType = typeof(Transform);
            targetField.allowSceneObjects = true;
            targetField.SetValueWithoutNotify(recommended);
            panel.Add(UiTextFactory.CreateHelpBox(I18N.Get(armature != null
                ? "workflow.attachment.mergeRecommended" : "workflow.attachment.proxyRecommended"), HelpBoxMessageType.Warning));
            panel.Add(new FormInput(I18N.Get("workflow.attachment.target"), targetField));
            panel.Add(new UiButton(I18N.Get(armature != null
                ? "workflow.attachment.addMerge" : "workflow.attachment.addProxy"), () =>
            {
                var target = targetField.value as Transform;
                if (!ValidAttachmentTarget(part, target)) { return; }
                if (!_context.Edits.CanEditPrefab() || !FlushPendingPartVisibility()) { return; }
                Undo.IncrementCurrentGroup();
                var undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(I18N.Get("workflow.attachment.undo"));
                try
                {
                    if (armature != null)
                    {
                        var merge = Undo.AddComponent<ModularAvatarMergeArmature>(armature.gameObject);
                        merge.mergeTarget.Set(target.gameObject);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(merge);
                    }
                    else
                    {
                        var proxy = Undo.AddComponent<ModularAvatarBoneProxy>(part.gameObject);
                        proxy.target = target;
                        proxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);
                    }
                    Undo.CollapseUndoOperations(undo);
                    AttachmentChanged();
                }
                catch (Exception exception)
                {
                    Undo.RevertAllDownToGroup(undo);
                    Debug.LogException(exception);
                    _context.Edits.ShowAssetError("workflow.assets.saveFailed");
                }
            }));
            return panel;
        }

        private static Transform FindPartArmature(Transform part)
        {
            var transforms = part.GetComponentsInChildren<Transform>(true);
            var named = transforms.FirstOrDefault(candidate =>
                candidate.childCount > 0 && string.Equals(candidate.name, "Armature", StringComparison.OrdinalIgnoreCase));
            if (named != null) { return named; }
            var roots = part.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(renderer => renderer.rootBone).Where(bone => bone != null && bone.IsChildOf(part)).Distinct().ToArray();
            var hips = roots.FirstOrDefault(bone => bone.name.IndexOf("hip", StringComparison.OrdinalIgnoreCase) >= 0);
            return hips != null && hips.parent != null && hips.parent.IsChildOf(part) ? hips.parent : null;
        }

        private bool ValidAttachmentTarget(Transform part, Transform target)
        {
            var valid = target != null && _context.Root != null && target.IsChildOf(_context.Root.transform) && !target.IsChildOf(part);
            if (!valid)
            {
                _context.Feedback = I18N.Get("workflow.attachment.invalidTarget");
                _context.FeedbackType = HelpBoxMessageType.Warning;
                _context.Host.Refresh();
            }
            return valid;
        }

        private void ChangeAttachment(Component component, Action change)
        {
            if (component == null || !_context.Edits.CanEditPrefab()) { return; }
            Undo.RecordObject(component, I18N.Get("workflow.attachment.undo"));
            change();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component);
            AttachmentChanged();
        }

        private void AttachmentChanged()
        {
            _context.Edits.WorkingSceneDirty = true;
            _context.Host.ClearCaches();
            _context.Edits.Changed();
            _context.Preview?.ReloadPrefabPreservingView(_context.Root);
            _context.Host.Refresh();
        }
    }
}
