using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarEditing
{
    public enum AvatarEditorPanel { Parts, Shape, Material }

    /// <summary>Editing target, shared selection and host services for avatar features.</summary>
    public sealed class AvatarEditingContext
    {
        public GameObject Root { get; set; }
        public GameObject PrefabAsset { get; set; }
        public VisualElement UiRoot { get; set; }
        public ScrollView ControlsHost { get; set; }
        public PrefabScenePreview Preview { get; set; }
        public BodyPartCategory? SelectedBodyPart { get; set; }
        public int? SelectedPrefabSiblingIndex { get; set; }
        public string SelectedPrefabName { get; set; } = string.Empty;
        public IReadOnlyList<int> PrefabSiblingIndices { get; set; } = Array.Empty<int>();
        public string SelectedPartKey { get; set; }
        public Material SelectedMaterial { get; set; }
        public bool BasePrefabHidden { get; set; }
        public HashSet<int> HiddenPrefabSiblingIndices { get; } = new HashSet<int>();
        public HashSet<string> HiddenPreviewParts { get; } = new HashSet<string>(StringComparer.Ordinal);
        public string Feedback { get; set; } = string.Empty;
        public HelpBoxMessageType FeedbackType { get; set; } = HelpBoxMessageType.Info;
        public string AssetFeedback { get; set; } = string.Empty;

        public AvatarEditingContext(AvatarEditingServices edits, AvatarEditingHost host)
        {
            Edits = edits ?? throw new ArgumentNullException(nameof(edits));
            Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public AvatarEditingServices Edits { get; }
        public AvatarEditingHost Host { get; }

        public bool IsInSelectedPrefabScope(Transform target)
        {
            return IsInSelectedPrefabScope(target, Root == null ? null : Root.transform);
        }

        public bool IsInSelectedPrefabScope(Transform target, Transform root)
        {
            return PrefabHierarchyUtility.IsInScope(target, root, SelectedPrefabSiblingIndex, PrefabSiblingIndices);
        }

        public IEnumerable<int> GetDisplayedPrefabScopes()
        {
            return SelectedPrefabSiblingIndex.HasValue
                ? new[] { SelectedPrefabSiblingIndex.Value }
                : new[] { -1 }.Concat(PrefabSiblingIndices);
        }

        public bool MatchesSelectedBodyPart(BodyPartCategory category)
        {
            return !SelectedBodyPart.HasValue || AvatarBodyAnalysis.MatchesBodyPartGroup(SelectedBodyPart.Value, category);
        }

        public Animator FindHumanoidAnimator()
        {
            return Root == null ? null : Root.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(animator => animator != null && IsInSelectedPrefabScope(animator.transform) &&
                    animator.avatar != null && animator.avatar.isHuman && animator.isHuman);
        }
    }
}
