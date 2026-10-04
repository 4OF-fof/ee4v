using System;
using System.Collections.Generic;
using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private sealed class BodyScaleDefinition
        {
            internal BodyScaleDefinition(
                string localizationKey,
                bool usesAvatarRoot,
                bool usesFirstAvailableBone,
                params HumanBodyBones[] bones)
            {
                LocalizationKey = localizationKey;
                UsesAvatarRoot = usesAvatarRoot;
                UsesFirstAvailableBone = usesFirstAvailableBone;
                Bones = bones ?? Array.Empty<HumanBodyBones>();
            }

            internal string LocalizationKey { get; }
            internal bool UsesAvatarRoot { get; }
            internal bool UsesFirstAvailableBone { get; }
            internal IReadOnlyList<HumanBodyBones> Bones { get; }
        }

        public enum ShapePartsSection
        {
            Shape,
            Parts
        }

        private struct PartTagChange
        {
            internal string RestoreKey;
            internal string OriginalTag;
            internal bool Visible;
        }

        private enum PendingBodySizeChange
        {
            None,
            Scale,
            BlendShape
        }

        private sealed class BodyBlendShapeDefinition
        {
            internal string RendererPath { get; set; }
            internal string RendererDisplayPath { get; set; }
            internal string RendererName { get; set; }
            internal string ShapeName { get; set; }
            internal string DisplayName { get; set; }
            internal BodyPartCategory Category { get; set; }
            internal string Group { get; set; }
            internal float Value { get; set; }
            internal float BaseValue { get; set; }
        }

        private sealed class PendingPartVisibility
        {
            internal PrefabObjectEntry Entry;
            internal bool OriginalVisible;
            internal bool OriginalActiveSelf;
            internal bool Visible;
            internal string RestoreKey;
        }

        private sealed class PrefabObjectEntry
        {
            public int PrefabSiblingIndex { get; set; }
            public string PrefabName { get; set; }
            public int[] SiblingPath { get; set; }
            public string Name { get; set; }
            public bool IsVisible { get; set; }
            public bool IsActiveSelf { get; set; }
            public bool ParentActiveInHierarchy { get; set; }
            public bool IsVisibleInHierarchy { get; set; }
            public bool HasMeshInSubtree { get; set; }
            public string Path { get; set; }
            public IReadOnlyCollection<BodyPartCategory> Categories { get; set; }
        }

        private sealed class PrefabObjectRowState
        {
            internal VisualElement Row { get; set; }
            internal UiTextElement Name { get; set; }
            internal UiTextElement Path { get; set; }
            internal Toggle Visibility { get; set; }
            internal UiButton PreviewVisibility { get; set; }
        }

        private sealed class PrefabObjectNode
        {
            internal PrefabObjectEntry Entry { get; set; }
            internal List<PrefabObjectNode> Children { get; } =
                new List<PrefabObjectNode>();
        }
    }
}
