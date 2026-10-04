using System;
using System.Collections.Generic;
using Ee4v.AvatarEditing;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private readonly AvatarEditingContext _context;

        private IReadOnlyList<string> _pendingBodyScaleTargetPaths;

        private Vector3 _pendingBodyScaleMultipliers;

        private PendingBodySizeChange _pendingBodySizeChange;

        private readonly HashSet<string> _expandedBodyScaleAxes =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> _expandedObjectGroups =
            new HashSet<string>(StringComparer.Ordinal);

        private IReadOnlyList<BodyBlendShapeDefinition>
            _pendingBodyBlendShapeGroup;

        private string _pendingBodyBlendShapeName;

        private Vector3? _baseAvatarViewPosition;

        private const string AvatarDescriptorTypeName =
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        private ShapePartsSection _shapePartsSection =
            ShapePartsSection.Parts;

        private IReadOnlyList<BodyBlendShapeDefinition> _bodyBlendShapesCache;

        private string _pendingPartAssetPath;

        private readonly Dictionary<string, Vector3> _bodyScaleBaseScales =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);

        private readonly Dictionary<PrefabObjectEntry, PrefabObjectRowState>
            _objectRows = new Dictionary<PrefabObjectEntry,
                PrefabObjectRowState>();

        private static readonly IReadOnlyList<BodyScaleDefinition>
            BodyScaleDefinitions = new[]
            {
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.wholeBody",
                    true,
                    false),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.head",
                    false,
                    false,
                    HumanBodyBones.Head),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.chest",
                    false,
                    true,
                    HumanBodyBones.UpperChest,
                    HumanBodyBones.Chest,
                    HumanBodyBones.Spine),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.waist",
                    false,
                    false,
                    HumanBodyBones.Hips),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.shoulders",
                    false,
                    false,
                    HumanBodyBones.LeftShoulder,
                    HumanBodyBones.RightShoulder),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.arms",
                    false,
                    false,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.RightUpperArm),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.hands",
                    false,
                    false,
                    HumanBodyBones.LeftHand,
                    HumanBodyBones.RightHand),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.legs",
                    false,
                    false,
                    HumanBodyBones.LeftUpperLeg,
                    HumanBodyBones.RightUpperLeg),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.feet",
                    false,
                    false,
                    HumanBodyBones.LeftFoot,
                    HumanBodyBones.RightFoot)
            };

        private bool _bodyScaleDirty;

        private BodyBlendShapeDefinition _pendingIndividualBlendShape;

        private readonly HashSet<int> _expandedPartPrefabGroups =
            new HashSet<int>();

        private bool _advancedBodyScaleExpanded;

        private bool _bodyScaleDragging;

        private const float MaximumBodyBlendShapeWeight = 100f;

        private bool _pendingBodyScaleUpdatesViewPosition;

        private const float MinimumBodyScale = 0.5f;

        private readonly HashSet<string> _expandedBodyBlendShapeGroups =
            new HashSet<string>(StringComparer.Ordinal);

        private const float MaximumBodyScale = 2f;

        private readonly Dictionary<PrefabObjectEntry, PendingPartVisibility>
            _pendingPartVisibility =
                new Dictionary<PrefabObjectEntry, PendingPartVisibility>();

        private const float MinimumBodyBlendShapeWeight = 0f;

        private string _pendingBodyBlendShapeRendererPath;

        private readonly Dictionary<string, Vector3>
            _bodyScalePreviewScales =
                new Dictionary<string, Vector3>(StringComparer.Ordinal);

        private Component _avatarDescriptor;

        private readonly List<KeyValuePair<string, Transform>>
            _resolvedBodyScaleTargets =
                new List<KeyValuePair<string, Transform>>();

        private float _pendingBodyBlendShapeWeight;

        private IReadOnlyList<PrefabObjectEntry> _objectEntriesCache;
    }
}
