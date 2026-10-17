using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
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
        private readonly HashSet<int> _initializedAttachments = new HashSet<int>();

        private VisualElement BuildAttachmentControls(int index)
        {
            var panel = new VisualElement { name = "ee4v-position-attachments" };
            panel.AddToClassList("ee4v-part-attachments");
            panel.AddToClassList("ee4v-part-attachments-section");
            if (_context.Root == null || index < 0 || index >= _context.Root.transform.childCount) { return panel; }
            var part = _context.Root.transform.GetChild(index);
            var plan = AvatarAttachmentAnalysis.Analyze(_context.Root, part);
            var proposals = plan.Connections.Where(connection => !connection.Exists).ToArray();
            var header = new SectionHeader(I18N.Get("workflow.attachment.title"));
            header.AddToClassList("ee4v-part-attachments-section__header");
            var state = UiTextFactory.Create(I18N.Get("workflow.attachment.status." + plan.State), UiClassNames.SecondaryText);
            state.tooltip = string.Join("\n", new[] { I18N.Get("workflow.attachment.states." + plan.State) }
                .Concat(plan.ReviewReasons.Select(reason => I18N.Get("workflow.attachment.reasons." + reason))));
            header.Actions.Add(state);
            panel.Add(header);
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
                    settings.Add(BuildAttachmentTargetPicker(part, proxy.target, selected =>
                    {
                        if (!ValidAttachmentTarget(part, selected)) return;
                        ChangeAttachment(proxy, () => proxy.target = selected);
                    }));
                    var advanced = BuildAttachmentDisclosure(proxy.GetInstanceID() ^ int.MinValue,
                        I18N.Get("workflow.attachment.advanced"), string.Empty, () =>
                    {
                        var fields = new VisualElement();
                        fields.AddToClassList("ee4v-part-attachment__advanced-fields");
                        var mode = UiTextFactory.CreateEnumField(string.Empty, proxy.attachmentMode);
                        mode.RegisterValueChangedCallback(evt => ChangeAttachment(proxy,
                            () => proxy.attachmentMode = (BoneProxyAttachmentMode)evt.newValue));
                        fields.Add(new FormInput(I18N.Get("workflow.attachment.mode"), mode));
                        var matchScale = UiTextFactory.CreateToggle(I18N.Get("workflow.attachment.matchScale"));
                        matchScale.SetValueWithoutNotify(proxy.matchScale);
                        matchScale.RegisterValueChangedCallback(evt => ChangeAttachment(proxy, () => proxy.matchScale = evt.newValue));
                        fields.Add(matchScale);
                        return fields;
                    });
                    advanced.AddToClassList("ee4v-part-attachment--advanced");
                    settings.Add(advanced);
                    return settings;
                }, true));
            }
            foreach (var proposal in proposals)
            {
                var candidate = proposal;
                var canApply = plan.CanApply || candidate.Reason == "estimatedBone" &&
                    plan.ReviewReasons.All(reason => reason == "estimatedBone");
                panel.Add(BuildAttachmentDisclosure(candidate.Owner.GetInstanceID(),
                    I18N.Get("workflow.attachment.recommended", AttachmentKindName(candidate.Kind)),
                    AttachmentSummary(part, candidate.Owner, candidate.Target), () => BuildNewAttachmentControls(part, candidate, canApply), true));
            }
            if (proposals.Length > 1 && plan.CanApply)
                panel.Add(new UiButton(I18N.Get("workflow.attachment.applyAll"), () =>
                    RunAttachmentEdit(() => ApplyRecommendedAttachments(part, proposals))));
            if (plan.State != AttachmentState.Connected)
            {
                var manual = BuildAttachmentDisclosure(part.GetInstanceID() ^ int.MinValue,
                    I18N.Get("workflow.attachment.manual"), string.Empty, () => BuildNewAttachmentControls(part, null));
                manual.name = "ee4v-attachment-manual";
                panel.Add(manual);
            }
            if (panel.childCount > 1) panel[panel.childCount - 1].AddToClassList("ee4v-part-attachment--last");
            return panel;
        }

        private VisualElement BuildAttachmentDisclosure(int key, string title, string summary, Func<VisualElement> buildSettings,
            bool initiallyExpanded = false)
        {
            if (_initializedAttachments.Add(key) && initiallyExpanded) _expandedAttachments.Add(key);
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
            section.Add(disclosure);
            if (!string.IsNullOrEmpty(summary))
            {
                var description = UiTextFactory.Create(summary, UiClassNames.SecondaryText,
                    "ee4v-part-attachment__summary");
                description.tooltip = summary;
                description.RegisterCallback<ClickEvent>(_ => Toggle());
                section.Add(description);
            }
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

        private static string AttachmentKindName(AttachmentKind kind) =>
            I18N.Get(kind == AttachmentKind.MergeArmature ? "workflow.attachment.mergeTitle" : "workflow.attachment.proxy");

        private VisualElement BuildNewAttachmentControls(Transform part, AttachmentConnection proposal, bool canApply = true)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-part-attachment__setup");
            var kind = proposal?.Kind ?? AttachmentKind.BoneProxy;
            panel.tooltip = I18N.Get(proposal == null ? "workflow.attachment.manualDescription" : "workflow.attachment.reasons." + proposal.Reason);
            var target = proposal?.Target;
            UiButton add = null;
            var ownerField = UiTextFactory.CreateObjectField();
            ownerField.objectType = typeof(Transform);
            ownerField.allowSceneObjects = true;
            ownerField.SetValueWithoutNotify(proposal?.Owner ?? part);
            ownerField.SetEnabled(proposal == null);
            AddAttachmentSetupField(panel, "workflow.attachment.source", ownerField);
            var targetHost = new VisualElement();
            void SelectTarget(Transform selected)
            {
                target = selected;
                if (proposal != null) proposal.Target = selected;
                add?.SetEnabled(canApply && AvatarAttachmentAnalysis.IsValidTarget(_context.Root, part, target));
            }
            void BuildTarget()
            {
                targetHost.Clear();
                if (kind == AttachmentKind.BoneProxy)
                    targetHost.Add(BuildAttachmentTargetPicker(part, target, SelectTarget));
                else
                {
                    var field = UiTextFactory.CreateObjectField();
                    field.objectType = typeof(Transform);
                    field.allowSceneObjects = true;
                    field.SetValueWithoutNotify(target);
                    field.RegisterValueChangedCallback(evt => SelectTarget(evt.newValue as Transform));
                    AddAttachmentSetupField(targetHost, "workflow.attachment.target", field);
                }
            }
            if (proposal == null)
            {
                var kindField = UiTextFactory.CreateEnumField(string.Empty, kind);
                kindField.RegisterValueChangedCallback(evt => { kind = (AttachmentKind)evt.newValue; BuildTarget(); });
                AddAttachmentSetupField(panel, "workflow.attachment.method", kindField);
            }
            BuildTarget();
            panel.Add(targetHost);
            add = new UiButton(I18N.Get("workflow.attachment.add"), () =>
            {
                if (!ValidAttachmentTarget(part, target)) { return; }
                var owner = ownerField.value as Transform;
                if (owner == null || !owner.IsChildOf(part))
                {
                    _context.Feedback = I18N.Get("workflow.attachment.invalidSource");
                    _context.FeedbackType = MessageSeverity.Warning;
                    _context.Host.Refresh();
                    return;
                }
                RunAttachmentEdit(() => ApplyAttachment(part, owner, kind.ToString(), target, null, null));
            });
            add.AddToClassList("ee4v-part-attachment__setup-add");
            add.SetPrimaryActionEnabled(true);
            add.SetEnabled(canApply && AvatarAttachmentAnalysis.IsValidTarget(_context.Root, part, target));
            var actions = new VisualElement();
            actions.AddToClassList("ee4v-part-attachment__setup-actions");
            actions.Add(add);
            panel.Add(actions);
            return panel;
        }

        private VisualElement BuildAttachmentTargetPicker(Transform part, Transform initial, Action<Transform> changed)
        {
            var panel = new VisualElement();
            var animator = _context.Root.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => candidate.isHuman && !candidate.transform.IsChildOf(part));
            var reference = PositionAvatarReferences.Where(bone => bone != HumanBodyBones.LastBone &&
                initial != null && ResolvePositionAvatarReference(animator, bone) == initial)
                .DefaultIfEmpty(HumanBodyBones.LastBone).First();
            var custom = initial;
            string Label(HumanBodyBones bone) => I18N.Get("workflow.position.references." + bone);
            var picker = UiTextFactory.CreatePopupField(string.Empty, PositionAvatarReferences,
                PositionAvatarReferences.IndexOf(reference), Label, Label);
            picker.name = "ee4v-attachment-target-reference";
            AddAttachmentSetupField(panel, "workflow.attachment.target", picker);
            var customHost = new VisualElement { name = "ee4v-attachment-custom-target" };
            var field = UiTextFactory.CreateObjectField();
            field.objectType = typeof(Transform);
            field.allowSceneObjects = true;
            field.SetValueWithoutNotify(custom);
            field.RegisterValueChangedCallback(evt =>
            {
                var selected = evt.newValue as Transform;
                if (selected != null && !AvatarAttachmentAnalysis.IsValidTarget(_context.Root, part, selected))
                { field.SetValueWithoutNotify(custom); return; }
                custom = selected;
                changed(selected);
            });
            AddAttachmentSetupField(customHost, null, field);
            panel.Add(customHost);
            void Refresh()
            {
                customHost.EnableInClassList("ee4v-part-attachment__details--hidden", reference != HumanBodyBones.LastBone);
                var target = reference == HumanBodyBones.LastBone ? custom : ResolvePositionAvatarReference(animator, reference);
                picker.tooltip = reference != HumanBodyBones.LastBone && target == null
                    ? I18N.Get("workflow.position.referenceUnavailable") : string.Empty;
            }
            picker.RegisterValueChangedCallback(evt =>
            {
                reference = evt.newValue;
                Refresh();
                changed(reference == HumanBodyBones.LastBone ? custom : ResolvePositionAvatarReference(animator, reference));
            });
            Refresh();
            return panel;
        }

        private static void AddAttachmentSetupField(VisualElement panel, string label, VisualElement input)
        {
            var field = label == null ? new FormInput(input) : new FormInput(I18N.Get(label), input);
            field.AddToClassList("ee4v-part-attachment__setup-target");
            field.AddToClassList("ee4v-part-attachment__setup-source");
            panel.Add(field);
        }

        private void RunAttachmentEdit(Action edit)
        {
            if (!_context.Edits.CanEditPrefab() || !FlushPendingPartVisibility()) return;
            try { edit(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _context.Feedback = exception.Message;
                _context.FeedbackType = MessageSeverity.Warning;
                _context.Host.Refresh();
            }
        }

        private bool ValidAttachmentTarget(Transform part, Transform target)
        {
            var valid = AvatarAttachmentAnalysis.IsValidTarget(_context.Root, part, target);
            if (!valid)
            {
                _context.Feedback = I18N.Get("workflow.attachment.invalidTarget");
                _context.FeedbackType = MessageSeverity.Warning;
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
            _context.Preview?.ReloadTargetPreservingView(_context.Root);
            _context.Host.Refresh();
        }
    }
}
