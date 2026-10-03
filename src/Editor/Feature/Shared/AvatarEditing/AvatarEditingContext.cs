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

        public Func<bool> CanEditPrefab { get; set; }
        public Func<Material, bool> CanEditMaterial { get; set; }
        public Func<bool> FlushChanges { get; set; }
        public Func<string> GetAssetPath { get; set; }
        public Func<IReadOnlyList<string>> GetExcludedPartPrefixes { get; set; }
        public Func<GameObject, IEnumerable<Mesh>, IAvatarShapeNaming> CreateShapeNaming { get; set; }
        public Action<bool> WorkingSceneDirtyChanged { get; set; }
        public bool WorkingSceneDirty { set => WorkingSceneDirtyChanged(value); }
        public Action Changed { get; set; }
        public Action<Material> MaterialChanged { get; set; }
        public Action Refresh { get; set; }
        public Action ShowParts { get; set; }
        public Action ShowMaterials { get; set; }
        public Action Rebuild { get; set; }
        public Action ClearCaches { get; set; }
        public Action<AvatarEditorPanel> InvalidateControls { get; set; }
        public Action InvalidateMaterialData { get; set; }
        public Action Repaint { get; set; }
        public Action SyncPreviewSelection { get; set; }
        public Action<string> ShowAssetError { get; set; }
        public Action<Material> CreateMaterialVariant { get; set; }
        public Action<int, bool, bool> RefreshPrefabGroupVisibility { get; set; }
        public Action<int, bool> RefreshPrefabHeaderActiveSelf { get; set; }
        public Action<int> RefreshPrefabPreviewVisibilityControls { get; set; }
        public Func<int, VisualElement, HashSet<int>, bool, VisualElement> BuildPrefabGroup { get; set; }

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
