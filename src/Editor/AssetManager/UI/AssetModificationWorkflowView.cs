using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.FaceExpression;
using Ee4v.PhysBoneCollider;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal enum BodyPartCategory
    {
        Head,
        Chest,
        Waist,
        Shoulders,
        Arms,
        Hands,
        Legs,
        Feet,
        Other
    }

    internal enum ModificationEditorMode
    {
        All,
        ShapeParts,
        Materials,
        Composition
    }

    internal sealed class AssetModificationWorkflowView : VisualElement, IDisposable
    {
        private const float MinimumBodyScale = 0.5f;
        private const float MaximumBodyScale = 2f;
        private const float MinimumBodyBlendShapeWeight = 0f;
        private const float MaximumBodyBlendShapeWeight = 100f;
        private const double PartVisibilitySaveDelaySeconds = 0.5d;
        private const string AvatarDescriptorTypeName =
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        private static readonly string[] HeadBlendShapeTerms =
        {
            "head", "neck", "face", "facial", "hair", "ear",
            "eye", "brow", "頭", "首", "顔", "髪", "耳", "目", "眉"
        };
        private static readonly string[] ChestBlendShapeTerms =
        {
            "chest", "breast", "bust", "rib",
            "胸", "バスト", "乳"
        };
        private static readonly string[] WaistBlendShapeTerms =
        {
            "waist", "hip", "pelvis", "belly", "stomach", "abdomen",
            "torso", "body", "腰", "尻", "お尻", "腹", "お腹", "胴"
        };
        private static readonly string[] ShoulderBlendShapeTerms =
            { "shoulder", "肩" };
        private static readonly string[] ArmBlendShapeTerms =
            { "arm", "elbow", "腕", "肘" };
        private static readonly string[] HandBlendShapeTerms =
            { "hand", "finger", "wrist", "nail", "手", "指", "手首" };
        private static readonly string[] LegBlendShapeTerms =
        {
            "leg", "thigh", "calf", "knee", "shin",
            "脚", "太もも", "腿", "膝", "ふくらはぎ", "すね"
        };
        private static readonly string[] FootBlendShapeTerms =
            { "foot", "feet", "toe", "ankle", "足", "つま先", "足首" };
        private static readonly BodyPartCategory[] BodyPartSelectorOrder =
        {
            BodyPartCategory.Head,
            BodyPartCategory.Shoulders,
            BodyPartCategory.Hands,
            BodyPartCategory.Chest,
            BodyPartCategory.Waist,
            BodyPartCategory.Legs,
            BodyPartCategory.Feet
        };

        private enum WorkflowCategory
        {
            ShapeParts,
            Material,
            ExpressionAnimation,
            PhysBone
        }

        private enum ShapePartsSection
        {
            Shape,
            Parts
        }

        private enum PendingBodySizeChange
        {
            None,
            Scale,
            BlendShape
        }

        private sealed class AvatarMaterialEntry
        {
            internal Material Material { get; set; }
            internal List<MaterialUsage> Usages { get; } =
                new List<MaterialUsage>();
        }

        private sealed class MaterialUsage
        {
            internal string RendererPath { get; set; }
            internal int SlotIndex { get; set; }
            internal IReadOnlyCollection<BodyPartCategory> Categories { get; set; }
        }

        private sealed class MaterialGeometryCacheEntry
        {
            internal Mesh Mesh { get; set; }
            internal IReadOnlyCollection<BodyPartCategory>[] Slots { get; set; }
        }

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

        private sealed class EmbeddedMaterialInspector
            : VisualElement, IDisposable
        {
            private const float HorizontalPadding = 12f;

            private readonly Material _material;
            private readonly Action _onChanged;
            private readonly IMGUIContainer _container;
            private MaterialEditor _editor;

            internal bool IsAvailable => _editor != null;

            internal EmbeddedMaterialInspector(
                Material material,
                Action onChanged)
            {
                _material = material;
                _onChanged = onChanged;
                AddToClassList(
                    "ee4v-modification-workflow__material-editor");

                _editor = Editor.CreateEditor(material) as MaterialEditor;
                if (_editor == null)
                {
                    return;
                }

                _container = new IMGUIContainer(DrawInspector);
                _container.AddToClassList(
                    "ee4v-modification-workflow__material-editor-imgui");
                _container.RegisterCallback<GeometryChangedEvent>(
                    OnGeometryChanged);
                Add(_container);
            }

            public void Dispose()
            {
                if (_container != null)
                {
                    _container.UnregisterCallback<GeometryChangedEvent>(
                        OnGeometryChanged);
                }

                if (_editor == null)
                {
                    return;
                }

                UnityEngine.Object.DestroyImmediate(_editor);
                _editor = null;
            }

            private void OnGeometryChanged(GeometryChangedEvent evt)
            {
                if (Mathf.Abs(
                        evt.newRect.width - evt.oldRect.width) < 0.5f)
                {
                    return;
                }

                _container?.MarkDirtyRepaint();
            }

            private void DrawInspector()
            {
                if (_editor == null || _material == null)
                {
                    return;
                }

                var containerWidth = Mathf.Floor(
                    _container.contentRect.width);
                if (containerWidth < 2f)
                {
                    return;
                }

                var contentWidth = Mathf.Max(
                    1f,
                    containerWidth - HorizontalPadding * 2f);
                var previousWideMode = EditorGUIUtility.wideMode;
                var previousLabelWidth = EditorGUIUtility.labelWidth;
                var previousHierarchyMode =
                    EditorGUIUtility.hierarchyMode;
                var previousIndentLevel = EditorGUI.indentLevel;
                var materialChanged = false;
                EditorGUI.BeginChangeCheck();
                try
                {
                    EditorGUIUtility.wideMode = contentWidth > 330f;
                    EditorGUIUtility.labelWidth = Mathf.Clamp(
                        contentWidth * 0.45f,
                        120f,
                        220f);
                    EditorGUIUtility.hierarchyMode = true;
                    EditorGUI.indentLevel = 0;

                    using (new GUILayout.HorizontalScope(
                               GUILayout.Width(containerWidth)))
                    {
                        GUILayout.Space(HorizontalPadding);
                        using (new GUILayout.VerticalScope(
                                   GUILayout.Width(contentWidth)))
                        {
                            DrawMaterialInspector(_editor, _material);
                        }
                        GUILayout.Space(HorizontalPadding);
                    }
                }
                finally
                {
                    materialChanged = EditorGUI.EndChangeCheck();
                    EditorGUIUtility.wideMode = previousWideMode;
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                    EditorGUIUtility.hierarchyMode =
                        previousHierarchyMode;
                    EditorGUI.indentLevel = previousIndentLevel;
                }

                if (!materialChanged)
                {
                    return;
                }

                EditorUtility.SetDirty(_material);
                _onChanged?.Invoke();
            }

            private static void DrawMaterialInspector(
                MaterialEditor materialEditor,
                Material material)
            {
                var shaderGui = materialEditor.customShaderGUI;
                if (shaderGui == null)
                {
                    materialEditor.PropertiesGUI();
                    return;
                }

                var properties = MaterialEditor.GetMaterialProperties(
                    new UnityEngine.Object[] { material });
                shaderGui.OnGUI(materialEditor, properties);
            }
        }

        private sealed class VariantSourceOption
        {
            internal AssetItem Item { get; set; }
            internal IReadOnlyList<GameObject> Prefabs { get; set; }
        }

        private sealed class AssetChildEntry
        {
            internal int SiblingIndex { get; set; }
            internal string Name { get; set; }
            internal bool IsAdded { get; set; }
            internal bool IsVisible { get; set; }
        }

        private sealed class PrefabObjectEntry
        {
            internal int PrefabSiblingIndex { get; set; }
            internal string PrefabName { get; set; }
            internal int[] SiblingPath { get; set; }
            internal string Name { get; set; }
            internal bool IsVisible { get; set; }
            internal bool IsActiveSelf { get; set; }
            internal bool ParentActiveInHierarchy { get; set; }
            internal bool IsVisibleInHierarchy { get; set; }
            internal string Path { get; set; }
            internal IReadOnlyCollection<BodyPartCategory> Categories { get; set; }
        }

        private sealed class PrefabObjectRowState
        {
            internal VisualElement Row { get; set; }
            internal Toggle Visibility { get; set; }
        }

        private sealed class PendingPartVisibility
        {
            internal PrefabObjectEntry Entry;
            internal bool OriginalVisible;
            internal bool OriginalActiveSelf;
            internal bool Visible;
            internal string RestoreKey;
        }

        private struct PartTagChange
        {
            internal string RestoreKey;
            internal string OriginalTag;
            internal bool Visible;
        }

        private sealed class PrefabPartClassifier
        {
            private const float MinimumClassifiedWeightRatio = 0.65f;
            private const float MinimumPartWeightRatio = 0.1f;
            private static readonly IReadOnlyCollection<BodyPartCategory> Empty =
                Array.Empty<BodyPartCategory>();
            private readonly Dictionary<Transform, BodyPartCategory>
                _humanoidBones = new Dictionary<Transform, BodyPartCategory>();
            private readonly Dictionary<Transform,
                    IReadOnlyCollection<BodyPartCategory>>
                _objects = new Dictionary<Transform,
                    IReadOnlyCollection<BodyPartCategory>>();
            private readonly Dictionary<Transform, HashSet<BodyPartCategory>>
                _anchors = new Dictionary<Transform,
                    HashSet<BodyPartCategory>>();

            internal PrefabPartClassifier(GameObject root)
            {
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                {
                    if (animator.avatar == null ||
                        !animator.avatar.isValid || !animator.isHuman)
                    {
                        continue;
                    }
                    foreach (var pair in GetHumanoidMaterialBoneCategories(animator))
                    {
                        _humanoidBones[pair.Key] = pair.Value;
                    }
                }
            }

            internal IReadOnlyCollection<BodyPartCategory> Classify(Transform target)
            {
                if (_objects.TryGetValue(target, out var cached))
                {
                    return cached;
                }

                var skinned = target.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && TryClassifySkin(skinned, out var skinParts))
                {
                    return _objects[target] = skinParts;
                }

                var anchors = GetAnchorCategories(target);
                if (anchors.Count > 0)
                {
                    if (anchors.Count == 1 &&
                        !anchors.Contains(BodyPartCategory.Other))
                    {
                        return _objects[target] = anchors.ToArray();
                    }
                    return _objects[target] = Empty;
                }

                var children = new HashSet<BodyPartCategory>();
                var hasRenderableChild = false;
                for (var index = 0; index < target.childCount; index++)
                {
                    var child = target.GetChild(index);
                    if (child.GetComponentInChildren<Renderer>(true) == null)
                    {
                        continue;
                    }
                    hasRenderableChild = true;
                    var childParts = Classify(child);
                    if (childParts.Count == 0)
                    {
                        return _objects[target] = Empty;
                    }
                    children.UnionWith(childParts);
                    if (children.Count > 2)
                    {
                        return _objects[target] = Empty;
                    }
                }
                if (hasRenderableChild)
                {
                    return _objects[target] = children.ToArray();
                }

                var named = ClassifyStructuralName(target.name);
                return _objects[target] = named == BodyPartCategory.Other
                    ? Empty
                    : (IReadOnlyCollection<BodyPartCategory>)new[] { named };
            }

            private bool TryClassifySkin(
                SkinnedMeshRenderer renderer,
                out IReadOnlyCollection<BodyPartCategory> categories)
            {
                categories = Empty;
                var mesh = renderer.sharedMesh;
                var bones = renderer.bones;
                if (mesh == null || bones == null || bones.Length == 0)
                {
                    return false;
                }

                BoneWeight[] weights;
                try
                {
                    weights = mesh.boneWeights;
                }
                catch (UnityException)
                {
                    return false;
                }
                if (weights == null || weights.Length != mesh.vertexCount)
                {
                    return false;
                }

                var boneParts = bones.Select(GetAnchorCategories)
                    .ToArray();
                var scores = new Dictionary<BodyPartCategory, float>();
                var totalWeight = 0f;
                var classifiedWeight = 0f;
                foreach (var weight in weights)
                {
                    Add(weight.boneIndex0, weight.weight0);
                    Add(weight.boneIndex1, weight.weight1);
                    Add(weight.boneIndex2, weight.weight2);
                    Add(weight.boneIndex3, weight.weight3);
                }

                if (totalWeight <= 0f ||
                    classifiedWeight < totalWeight *
                    MinimumClassifiedWeightRatio)
                {
                    return true;
                }

                var significant = scores
                    .Where(pair => pair.Value >= classifiedWeight *
                        MinimumPartWeightRatio)
                    .Select(pair => pair.Key)
                    .ToArray();
                if (significant.Length >= 1 && significant.Length <= 2)
                {
                    categories = significant;
                }
                return true;

                void Add(int boneIndex, float value)
                {
                    if (value <= 0f || boneIndex < 0 ||
                        boneIndex >= boneParts.Length)
                    {
                        return;
                    }
                    totalWeight += value;
                    var parts = boneParts[boneIndex];
                    if (parts.Count != 1 ||
                        parts.Contains(BodyPartCategory.Other))
                    {
                        return;
                    }
                    var part = parts.First();
                    scores.TryGetValue(part, out var score);
                    scores[part] = score + value;
                    classifiedWeight += value;
                }
            }

            private HashSet<BodyPartCategory> GetAnchorCategories(
                Transform target)
            {
                if (target == null)
                {
                    return new HashSet<BodyPartCategory>();
                }
                if (!_anchors.TryGetValue(target, out var categories))
                {
                    categories = ResolveAnchor(target,
                        new HashSet<Transform>());
                    _anchors.Add(target, categories);
                }
                return categories;
            }

            private HashSet<BodyPartCategory> ResolveAnchor(
                Transform target,
                HashSet<Transform> visiting)
            {
                var result = new HashSet<BodyPartCategory>();
                if (target == null || !visiting.Add(target))
                {
                    return result;
                }
                try
                {
                    if (_humanoidBones.TryGetValue(target, out var humanoid))
                    {
                        result.Add(NormalizePart(humanoid));
                        return result;
                    }

                    var hasConstraintSource = false;
                    foreach (var component in target.GetComponents<Component>())
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        var typeName = component.GetType().Name;
                        if (typeName == "ModularAvatarBoneProxy")
                        {
                            var property = new SerializedObject(component)
                                .FindProperty("boneReference");
                            if (property != null)
                            {
                                var name = ((HumanBodyBones)property.intValue)
                                    .ToString();
                                var part = ClassifyStructuralName(name);
                                if (part != BodyPartCategory.Other)
                                {
                                    result.Add(part);
                                    return result;
                                }
                            }
                        }
                        if (typeName.IndexOf("Constraint",
                                StringComparison.Ordinal) < 0)
                        {
                            continue;
                        }
                        var properties = new SerializedObject(component)
                            .GetIterator();
                        while (properties.NextVisible(true))
                        {
                            if (properties.propertyType !=
                                    SerializedPropertyType.ObjectReference ||
                                properties.propertyPath.IndexOf("source",
                                    StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }
                            var source = properties.objectReferenceValue as Transform;
                            if (source == null &&
                                properties.objectReferenceValue is GameObject gameObject)
                            {
                                source = gameObject.transform;
                            }
                            if (source != null)
                            {
                                hasConstraintSource = true;
                                // Animation can activate a source whose current weight is zero.
                                var sourceParts = ResolveAnchor(source, visiting);
                                if (sourceParts.Count == 0)
                                {
                                    result.Add(BodyPartCategory.Other);
                                }
                                else
                                {
                                    result.UnionWith(sourceParts);
                                }
                            }
                        }
                    }
                    if (hasConstraintSource)
                    {
                        return result;
                    }
                    if (result.Count > 0)
                    {
                        return result;
                    }

                    var named = ClassifyStructuralName(target.name);
                    if (named != BodyPartCategory.Other)
                    {
                        result.Add(named);
                        return result;
                    }
                    return ResolveAnchor(target.parent, visiting);
                }
                finally
                {
                    visiting.Remove(target);
                }
            }

            private static BodyPartCategory NormalizePart(
                BodyPartCategory part)
            {
                return part == BodyPartCategory.Arms
                    ? BodyPartCategory.Shoulders
                    : part;
            }

            private static BodyPartCategory ClassifyStructuralName(string name)
            {
                if (string.IsNullOrEmpty(name) ||
                    name.Equals("Armature", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Armature.",
                        StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Root", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Body", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Body_", StringComparison.OrdinalIgnoreCase))
                {
                    return BodyPartCategory.Other;
                }
                var words = AssetManagerPrefabUtility.SplitName(name);
                if (ContainsWord(words, "head", "neck", "face", "facial",
                        "hair", "ear", "eye", "brow", "goggle", "goggles",
                        "visor") || ContainsJapanese(name, "頭", "首", "顔", "髪", "耳"))
                {
                    return BodyPartCategory.Head;
                }
                if (ContainsWord(words, "chest", "breast", "bust", "bra",
                        "ribcage") ||
                    ContainsJapanese(name, "胸", "バスト", "乳"))
                {
                    return BodyPartCategory.Chest;
                }
                if (ContainsWord(words, "hand", "hands", "wrist", "finger",
                        "thumb", "index", "middle", "ring", "little",
                        "bracelet", "bracelets", "watch", "glove", "gloves") ||
                    ContainsJapanese(name, "手", "指"))
                {
                    return BodyPartCategory.Hands;
                }
                if (ContainsWord(words, "shoulder", "shoulders", "arm",
                        "arms", "forearm", "elbow") ||
                    ContainsJapanese(name, "肩", "腕", "肘"))
                {
                    return BodyPartCategory.Shoulders;
                }
                if (ContainsWord(words, "foot", "feet", "toe", "toes",
                        "ankle", "anklet", "boot", "boots", "sandal",
                        "sandals") ||
                    ContainsJapanese(name, "足", "つま先", "足首"))
                {
                    return BodyPartCategory.Feet;
                }
                if (ContainsWord(words, "leg", "legs", "thigh", "calf",
                        "knee", "shin", "tights") ||
                    ContainsJapanese(name, "脚", "腿", "膝"))
                {
                    return BodyPartCategory.Legs;
                }
                if (ContainsWord(words, "waist", "hip", "hips", "pelvis",
                        "spine", "belly", "abdomen", "skirt", "shorts",
                        "belt", "tail") ||
                    ContainsJapanese(name, "腰", "尻", "腹", "胴"))
                {
                    return BodyPartCategory.Waist;
                }
                return BodyPartCategory.Other;
            }

            private static bool ContainsWord(
                IReadOnlyCollection<string> words,
                params string[] terms)
            {
                return words.Any(word => terms.Contains(word));
            }

            private static bool ContainsJapanese(
                string name,
                params string[] terms)
            {
                return terms.Any(term => name.IndexOf(
                    term, StringComparison.Ordinal) >= 0);
            }
        }

        private readonly Dictionary<WorkflowCategory, UiButton>
            _categoryButtons =
                new Dictionary<WorkflowCategory, UiButton>();
        private readonly Dictionary<Material, UiButton>
            _materialVisibilityButtons =
                new Dictionary<Material, UiButton>();
        private readonly HashSet<Material> _hiddenMaterials =
            new HashSet<Material>();
        private readonly Dictionary<SkinnedMeshRenderer,
            MaterialGeometryCacheEntry> _materialGeometryCache =
                new Dictionary<SkinnedMeshRenderer,
                    MaterialGeometryCacheEntry>();
        private IAssetManager _manager;
        private bool _savingVariant;
        private DerivedAssetInfo _workingAsset;
        private GameObject _workingObject;
        private Material _selectedMaterial;
        private UiButton _allMaterialsVisibilityButton;
        private EmbeddedMaterialInspector _materialInspector;
        private DerivedAssetPrefabScenePreview _scenePreview;
        private FaceExpressionWindow.EmbeddedEditor _faceExpressionEditor;
        private PhysBoneColliderWindow.EmbeddedEditor _physBoneEditor;
        private VisualElement _customizerHost;
        private VisualElement _faceExpressionHost;
        private VisualElement _physBoneHost;
        private ScrollView _controlsHost;
        private VisualElement _appearanceHeader;
        private UiTextElement _previewTitle;
        private WorkflowCategory _currentCategory =
            WorkflowCategory.ShapeParts;
        private ShapePartsSection _shapePartsSection =
            ShapePartsSection.Parts;
        private BodyPartCategory? _selectedBodyPart;
        private bool _creatingDerivedAsset;
        private string _creationItemId = string.Empty;
        private GameObject _creationPrefab;
        private string _derivedName = string.Empty;
        private string _derivedDescription = string.Empty;
        private string _feedback = string.Empty;
        private string _assetFeedback = string.Empty;
        private int? _selectedPrefabSiblingIndex;
        private string _selectedPrefabName = string.Empty;
        private IReadOnlyList<PrefabObjectEntry> _objectEntriesCache;
        private readonly Dictionary<PrefabObjectEntry, PrefabObjectRowState>
            _objectRows = new Dictionary<PrefabObjectEntry,
                PrefabObjectRowState>();
        private readonly Dictionary<PrefabObjectEntry, PendingPartVisibility>
            _pendingPartVisibility =
                new Dictionary<PrefabObjectEntry, PendingPartVisibility>();
        private string _pendingPartAssetPath;
        private double _partVisibilitySaveDueAt;
        private IReadOnlyList<int> _prefabSiblingIndices =
            Array.Empty<int>();
        private readonly HashSet<int> _hiddenPrefabSiblingIndices =
            new HashSet<int>();
        private bool _basePrefabHidden;
        private bool _prefabPreviewVisibilityInitialized;
        private HelpBoxMessageType _feedbackType =
            HelpBoxMessageType.Info;
        private bool _bodyScaleDragging;
        private PendingBodySizeChange _pendingBodySizeChange;
        private IReadOnlyList<string> _pendingBodyScaleTargetPaths;
        private Vector3 _pendingBodyScaleMultipliers;
        private bool _pendingBodyScaleUpdatesViewPosition;
        private string _pendingBodyBlendShapeRendererPath;
        private string _pendingBodyBlendShapeName;
        private float _pendingBodyBlendShapeWeight;
        private IReadOnlyList<BodyBlendShapeDefinition>
            _pendingBodyBlendShapeGroup;
        private BodyBlendShapeDefinition _pendingIndividualBlendShape;
        private bool _bodyScaleDirty;
        private bool _advancedBodyScaleExpanded;
        private readonly HashSet<string> _expandedBodyScaleAxes =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _expandedBodyBlendShapeGroups =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> _bodyScaleBaseScales =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, Transform>>
            _resolvedBodyScaleTargets =
                new List<KeyValuePair<string, Transform>>();
        private readonly Dictionary<string, Vector3>
            _bodyScalePreviewScales =
                new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private Vector3? _baseAvatarViewPosition;
        private Component _avatarDescriptor;

        private readonly ModificationEditorMode _mode;
        private readonly Action _repaint;
        private readonly Action _openAssetManager;
        private bool _disposed;
        private AssetManagerWorkspaceView _assetManagerView;
        private AssetManagerViewState _assetManagerViewState = new AssetManagerViewState();

        internal event Action<DerivedAssetInfo> DerivedAssetChanged;

        internal AssetModificationWorkflowView(
            ModificationEditorMode mode,
            Action repaint,
            Action openAssetManager)
        {
            _mode = mode;
            _repaint = repaint;
            _openAssetManager = mode == ModificationEditorMode.All
                ? ShowAssetManagerSelection : openAssetManager;
            ApplyEditorMode();
            OnEnable();
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
            if (_mode == ModificationEditorMode.Materials)
            {
                _currentCategory = WorkflowCategory.Material;
            }
            else if (_mode != ModificationEditorMode.All)
            {
                _currentCategory = WorkflowCategory.ShapeParts;
            }
        }

        private void RequestRepaint()
        {
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
            BlendShapePresetStorage.Shared.Changed -= OnBlendShapePresetChanged;
            BlendShapePresetStorage.Shared.Changed += OnBlendShapePresetChanged;
            AssetManagerSettings.PartListExclusionsChanged -=
                OnPartListExclusionsChanged;
            AssetManagerSettings.PartListExclusionsChanged +=
                OnPartListExclusionsChanged;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            EndBodyScaleDrag(false);
            FlushPendingPartVisibility();
            I18N.Reloaded -= Rebuild;
            AssetManagerWindowSession.ManagerInvalidated -=
                OnManagerInvalidated;
            Undo.undoRedoPerformed -= RefreshAfterUndoRedo;
            BlendShapePresetStorage.Shared.Changed -= OnBlendShapePresetChanged;
            AssetManagerSettings.PartListExclusionsChanged -=
                OnPartListExclusionsChanged;
            DisposeEditors();
        }

        private void OnPartListExclusionsChanged()
        {
            _objectEntriesCache = null;
            if (_currentCategory == WorkflowCategory.ShapeParts &&
                _shapePartsSection == ShapePartsSection.Parts)
            {
                Rebuild();
            }
        }

        private void OnBlendShapePresetChanged()
        {
            if (_workingObject != null &&
                _currentCategory == WorkflowCategory.ShapeParts &&
                _shapePartsSection == ShapePartsSection.Shape &&
                _controlsHost != null &&
                this.panel != null)
            {
                this.schedule.Execute(() =>
                {
                    if (_controlsHost != null &&
                        _currentCategory == WorkflowCategory.ShapeParts &&
                        _shapePartsSection == ShapePartsSection.Shape &&
                        this.panel != null)
                    {
                        ShowCategory(WorkflowCategory.ShapeParts, false);
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
            _manager = null;
            _assetManagerViewState = new AssetManagerViewState();
            Rebuild();
        }

        private void BuildWindow()
        {
            ApplyEditorMode();
            FlushPendingPartVisibility();
            DisposeEditors();
            _materialGeometryCache.Clear();
            _objectEntriesCache = null;
            _objectRows.Clear();
            var root = this;
            root.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(root);
            root.AddToClassList("ee4v-modification-workflow");

            if (_workingObject == null ||
                string.IsNullOrEmpty(AssetDatabase.GetAssetPath(
                    _workingObject)))
            {
                _workingAsset = null;
                _workingObject = null;
                root.Add(_creatingDerivedAsset
                    ? BuildDerivedAssetCreation()
                    : BuildDerivedAssetSelection());
                return;
            }

            root.Add(BuildWorkspaceHeader());
            var body = new VisualElement();
            body.AddToClassList("ee4v-modification-workflow__body");
            if (_mode == ModificationEditorMode.All)
            {
                body.Add(BuildCategoryRail());
            }
            else if (_mode == ModificationEditorMode.Composition)
            {
                body.Add(BuildPreviewPane());
                root.Add(body);
                SetPreviewTitle("workflow.preview.appearanceTitle");
                return;
            }

            _customizerHost = new VisualElement();
            _customizerHost.AddToClassList(
                "ee4v-modification-workflow__customizer-host");
            _customizerHost.Add(BuildPreviewPane());
            var controlsColumn = new VisualElement();
            controlsColumn.AddToClassList(
                "ee4v-modification-workflow__controls-column");
            _appearanceHeader = new VisualElement();
            _appearanceHeader.AddToClassList(
                "ee4v-modification-workflow__appearance-header");
            controlsColumn.Add(_appearanceHeader);
            _controlsHost = new ScrollView(ScrollViewMode.Vertical);
            _controlsHost.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            _controlsHost.AddToClassList(
                "ee4v-modification-workflow__controls");
            controlsColumn.Add(_controlsHost);
            _customizerHost.Add(controlsColumn);
            body.Add(_customizerHost);

            _faceExpressionHost = new VisualElement();
            _faceExpressionHost.AddToClassList(
                "ee4v-modification-workflow__face-expression-host");
            _faceExpressionHost.AddToClassList(
                "ee4v-modification-workflow__hidden");
            body.Add(_faceExpressionHost);

            _physBoneHost = new VisualElement();
            _physBoneHost.AddToClassList(
                "ee4v-modification-workflow__physbone-host");
            _physBoneHost.AddToClassList(
                "ee4v-modification-workflow__hidden");
            body.Add(_physBoneHost);
            root.Add(body);
            ShowCategory(_currentCategory, false);
        }

        private VisualElement BuildDerivedAssetSelection()
        {
            var page = new VisualElement();
            page.AddToClassList(
                "ee4v-modification-workflow__selection-page");
            if (_mode == ModificationEditorMode.All)
            {
                try
                {
                    _assetManagerView = new AssetManagerWorkspaceView(
                        SelectDerivedAsset,
                        StartDerivedAssetCreation,
                        (mode, createDerivedAsset) => new AssetManagerView(
                            _manager = _manager ?? AssetManagerWindowSession.GetManager(),
                            _assetManagerViewState,
                            mode,
                            createDerivedAsset: createDerivedAsset));
                    page.Add(_assetManagerView);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    page.Add(UiTextFactory.CreateHelpBox(
                        I18N.Get("workflow.selection.managerLoadFailed"),
                        HelpBoxMessageType.Error));
                }
                return page;
            }

            var header = BuildSelectionHeader(
                "workflow.selection.title",
                "workflow.selection.description");
            header.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.selection.createNew"),
                "add.png",
                StartDerivedAssetCreation,
                "ee4v-modification-workflow__primary-action",
                "ee4v-modification-workflow__selection-new"));
            page.Add(header);
            var content = new ScrollView(ScrollViewMode.Vertical);
            content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            content.AddToClassList(
                "ee4v-modification-workflow__selection-scroll");
            var assets = DerivedAssetCreator.FindAll();
            if (assets.Count == 0)
            {
                content.Add(CreateEmptyState(
                    "workflow.selection.emptyTitle",
                    "workflow.selection.emptyDescription"));
                content.Add(AssetManagerControls.CreateIconTextButton(
                    I18N.Get("workflow.selection.openManager"),
                    "library.png",
                    _openAssetManager,
                    "ee4v-modification-workflow__selection-manager"));
                page.Add(content);
                return page;
            }

            var grid = new VisualElement();
            grid.AddToClassList(
                "ee4v-modification-workflow__selection-grid");
            foreach (var asset in assets)
            {
                grid.Add(BuildDerivedAssetCard(asset));
            }
            content.Add(grid);
            page.Add(content);
            return page;
        }

        private void ShowAssetManagerSelection()
        {
            _creatingDerivedAsset = false;
            BuildWindow();
        }

        private VisualElement BuildDerivedAssetCreation()
        {
            var page = new VisualElement();
            page.AddToClassList(
                "ee4v-modification-workflow__selection-page");
            var header = BuildSelectionHeader(
                "workflow.selection.createTitle",
                "workflow.selection.createDescription");
            header.Insert(0, AssetManagerControls.CreateIconButton(
                I18N.Get(_mode == ModificationEditorMode.All
                    ? "workflow.selection.backToAssets" : "workflow.selection.back"),
                "arrow_left.png",
                CancelDerivedAssetCreation,
                "ee4v-modification-workflow__selection-back"));
            page.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList(
                "ee4v-modification-workflow__creation-scroll");
            var sources = GetVariantSources();
            if (_mode == ModificationEditorMode.All)
            {
                sources = sources.Where(source => string.Equals(
                    source.Item.Id, _creationItemId, StringComparison.Ordinal)).ToArray();
            }
            if (sources.Count == 0)
            {
                scroll.Add(CreateEmptyState(
                    _mode == ModificationEditorMode.All
                        ? "workflow.selection.assetNoSourceTitle" : "workflow.selection.noSourceTitle",
                    "workflow.selection.noSourceDescription"));
                scroll.Add(AssetManagerControls.CreateIconTextButton(
                    I18N.Get("workflow.selection.openManager"),
                    "library.png",
                    _openAssetManager,
                    "ee4v-modification-workflow__selection-manager"));
                page.Add(scroll);
                return page;
            }

            var selectedSource = sources.FirstOrDefault(source =>
                string.Equals(
                    source.Item.Id,
                    _creationItemId,
                    StringComparison.Ordinal)) ?? sources[0];
            if (!string.Equals(
                    _creationItemId,
                    selectedSource.Item.Id,
                    StringComparison.Ordinal))
            {
                _creationItemId = selectedSource.Item.Id;
                _creationPrefab = null;
                _derivedName = selectedSource.Item.Name + " Variant";
            }
            if (_creationPrefab == null ||
                !selectedSource.Prefabs.Contains(_creationPrefab))
            {
                _creationPrefab = selectedSource.Prefabs[0];
            }

            VisualElement sourceField;
            if (_mode == ModificationEditorMode.All)
            {
                sourceField = UiTextFactory.Create(FormatVariantSource(selectedSource));
            }
            else
            {
                var sourceChoices = sources.ToList();
                var sourcePopup = UiTextFactory.CreatePopupField(
                    string.Empty,
                    sourceChoices,
                    Mathf.Max(0, sourceChoices.IndexOf(selectedSource)),
                    FormatVariantSource,
                    FormatVariantSource);
                sourcePopup.RegisterValueChangedCallback(evt =>
                    SelectVariantSource(evt.newValue));
                sourceField = sourcePopup;
            }
            var sourceInput = new FormInput(
                I18N.Get("workflow.selection.sourceItem"),
                sourceField);
            sourceInput.AddToClassList(
                "ee4v-modification-workflow__creation-source");
            scroll.Add(sourceInput);

            var layout = new VisualElement();
            layout.AddToClassList(
                "ee4v-modification-workflow__creation-layout");
            var previewColumn = new VisualElement();
            previewColumn.AddToClassList(
                "ee4v-modification-workflow__creation-preview-column");
            var preview = new DerivedAssetPrefabScenePreview();
            preview.SetPrefab(_creationPrefab);
            previewColumn.Add(preview);
            var selector = new DerivedAssetPrefabSelector(
                selectedSource.Prefabs);
            selector.SetValueWithoutNotify(_creationPrefab);
            selector.ValueChanged += prefab =>
            {
                _creationPrefab = prefab;
                preview.SetPrefab(prefab);
            };
            var prefabInput = new FormInput(
                I18N.Get("workflow.selection.sourcePrefab"),
                selector);
            previewColumn.Add(prefabInput);
            layout.Add(previewColumn);

            var fields = new VisualElement();
            fields.AddToClassList(
                "ee4v-modification-workflow__creation-fields");
            var name = AssetManagerControls.CreateTextField(
                I18N.Get("workflow.selection.name"));
            name.value = _derivedName;
            fields.Add(name);
            var description = AssetManagerControls.CreateTextField(
                I18N.Get("workflow.selection.assetDescription"));
            description.value = _derivedDescription;
            description.SetMultiline(true, 144f);
            fields.Add(description);
            AddFeedback(fields);
            fields.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get(_mode == ModificationEditorMode.All
                    ? "workflow.selection.createAndStart" : "workflow.selection.create"),
                "add.png",
                () =>
                {
                    _derivedName = name.value;
                    _derivedDescription = description.value;
                    CreateDerivedAsset();
                },
                "ee4v-modification-workflow__primary-action",
                "ee4v-modification-workflow__creation-submit"));
            layout.Add(fields);
            scroll.Add(layout);
            page.Add(scroll);
            return page;
        }

        private VisualElement BuildSelectionHeader(
            string titleKey,
            string descriptionKey)
        {
            var header = new VisualElement();
            header.AddToClassList(
                "ee4v-modification-workflow__selection-header");
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__selection-header-text");
            text.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__selection-title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__selection-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            text.Add(description);
            header.Add(text);
            return header;
        }

        private VisualElement BuildDerivedAssetCard(DerivedAssetInfo asset)
        {
            var card = new VisualElement();
            card.AddToClassList(
                "ee4v-modification-workflow__selection-card");
            var preview = new DerivedAssetPrefabPreview(asset.Prefab);
            preview.AddToClassList(
                "ee4v-modification-workflow__selection-preview");
            card.Add(preview);
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__selection-card-text");
            text.Add(UiTextFactory.Create(
                asset.Name,
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__selection-card-title"));
            var description = UiTextFactory.Create(
                string.IsNullOrWhiteSpace(asset.Description)
                    ? I18N.Get("workflow.selection.noDescription")
                    : asset.Description,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__selection-card-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            text.Add(description);
            card.Add(text);
            card.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.selection.choose"),
                "arrow_right.png",
                () => SelectDerivedAsset(asset),
                "ee4v-modification-workflow__primary-action",
                "ee4v-modification-workflow__selection-choose"));
            return card;
        }

        private VisualElement BuildWorkspaceHeader()
        {
            _prefabSiblingIndices = Array.Empty<int>();
            var header = new VisualElement();
            header.AddToClassList("ee4v-modification-workflow__header");
            var cards = new VisualElement();
            cards.AddToClassList("ee4v-modification-workflow__header-cards");
            var combined = CreateHeaderPrefabCard(
                _workingObject.name,
                "ee4v-modification-workflow__header-card--combined");
            combined.tooltip = _workingObject.name;
            combined.RegisterCallback<ClickEvent>(
                _ => SelectPrefabCard(null, string.Empty));
            combined.EnableInClassList(
                "ee4v-modification-workflow__header-card--selected",
                !_selectedPrefabSiblingIndex.HasValue);
            var changeLabel = I18N.Get("workflow.asset.change");
            var change = new UiButton(
                changeLabel,
                ClearDerivedAsset,
                changeLabel,
                variant: UiButtonVariant.Ghost);
            change.AddToClassList(
                "ee4v-modification-workflow__header-change");
            change.RegisterCallback<ClickEvent>(
                evt => evt.StopPropagation());
            var changeSeparator = new VisualElement();
            changeSeparator.AddToClassList(
                "ee4v-modification-workflow__header-change-separator");
            combined.Add(changeSeparator);
            combined.Add(change);
            cards.Add(combined);

            var strip = new ScrollView(ScrollViewMode.Horizontal);
            strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.AddToClassList("ee4v-modification-workflow__header-strip");
            VisualElement selectedCard = null;
            var basePrefab = PrefabUtility.GetCorrespondingObjectFromSource(
                _workingObject) as GameObject;
            var baseCard = CreateHeaderPrefabCard(
                basePrefab != null ? basePrefab.name : _workingObject.name,
                "ee4v-modification-workflow__header-card--base");
            baseCard.tooltip = I18N.Get("workflow.assets.base");
            baseCard.EnableInClassList(
                "ee4v-modification-workflow__header-card--hidden",
                _basePrefabHidden);
            UiButton baseVisibility = null;
            baseVisibility = CreatePrefabVisibilityButton(
                !_basePrefabHidden,
                I18N.Get(_basePrefabHidden
                    ? "workflow.assets.clickToShow"
                    : "workflow.assets.clickToHide"),
                () => TogglePrefabPreviewVisibility(
                    -1, baseCard, baseVisibility),
                "ee4v-modification-workflow__header-card-visibility");
            baseVisibility.RegisterCallback<ClickEvent>(
                evt => evt.StopPropagation());
            baseCard.Add(baseVisibility);
            baseCard.EnableInClassList(
                "ee4v-modification-workflow__header-card--selected",
                _selectedPrefabSiblingIndex == -1);
            baseCard.RegisterCallback<ClickEvent>(
                _ => SelectPrefabCard(-1, _workingObject.name));
            strip.Add(baseCard);
            if (_selectedPrefabSiblingIndex == -1)
            {
                selectedCard = baseCard;
            }

            if (IsEditableWorkflowPrefab())
            {
                try
                {
                    var children = ReadAssetChildren();
                    if (!_prefabPreviewVisibilityInitialized)
                    {
                        _hiddenPrefabSiblingIndices.Clear();
                        foreach (var child in children)
                        {
                            if (!child.IsVisible)
                            {
                                _hiddenPrefabSiblingIndices.Add(
                                    child.SiblingIndex);
                            }
                        }
                        _prefabPreviewVisibilityInitialized = true;
                    }
                    _prefabSiblingIndices = children
                        .Select(child => child.SiblingIndex).ToArray();
                    foreach (var child in children)
                    {
                        var card = BuildAssetChildCard(child);
                        strip.Add(card);
                        if (_selectedPrefabSiblingIndex ==
                            child.SiblingIndex)
                        {
                            selectedCard = card;
                        }
                    }
                }
                catch (Exception exception)
                {
                    _prefabSiblingIndices = Array.Empty<int>();
                    Debug.LogException(exception);
                    header.Add(UiTextFactory.CreateHelpBox(
                        I18N.Get("workflow.assets.readFailed"),
                        HelpBoxMessageType.Error));
                }
                var sources = GetVariantSources();
                UiButton add = null;
                add = new UiButton(
                    "+",
                    () => OpenAssetPicker(add, sources),
                    I18N.Get("workflow.assets.add"),
                    variant: UiButtonVariant.Ghost);
                add.SetLabelFontSize(20);
                add.SetLabelTextAlign(TextAnchor.MiddleCenter);
                add.AddToClassList(
                    "ee4v-modification-workflow__header-add");
                add.SetEnabled(sources.Count > 0);
                strip.Add(add);
                if (sources.Count == 0)
                {
                    add.tooltip = I18N.Get(
                        "workflow.assets.noSourceDescription");
                }
            }
            if (selectedCard != null)
            {
                var cardToReveal = selectedCard;
                strip.schedule.Execute(() => strip.ScrollTo(cardToReveal));
            }
            cards.Add(strip);
            var save = AssetManagerControls.CreateButton(I18N.Get("variant.save"), SaveVariantRevision);
            save.SetEnabled(!_savingVariant);
            cards.Add(save);
            header.Add(cards);
            if (!string.IsNullOrEmpty(_assetFeedback))
            {
                header.Add(UiTextFactory.CreateHelpBox(
                    _assetFeedback,
                    HelpBoxMessageType.Error));
            }
            return header;
        }

        private async void SaveVariantRevision()
        {
            if (_savingVariant || _workingObject == null) { return; }
            EndBodyScaleDrag();
            if (!FlushPendingPartVisibility()) { return; }
            _savingVariant = true;
            var assetPath = AssetDatabase.GetAssetPath(_workingObject);
            BuildWindow();
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                var variants = AssetManagerWindowSession.TryGetVariantManager(_manager);
                await variants.Save(new AssetVariantSaveRequest { RootAssetPath = assetPath });
                if (!_disposed)
                {
                    _assetFeedback = string.Empty;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!_disposed) { _assetFeedback = exception.Message; }
            }
            finally
            {
                _savingVariant = false;
                if (!_disposed) { BuildWindow(); }
            }
        }

        private static VisualElement CreateHeaderPrefabCard(
            string name,
            string variantClass)
        {
            var card = new VisualElement();
            card.AddToClassList("ee4v-modification-workflow__header-card");
            card.AddToClassList(variantClass);
            var label = UiTextFactory.Create(
                name,
                "ee4v-modification-workflow__header-card-name");
            label.SetFontSize(UiTypographyTokens.SubtitleFontSize);
            label.SetTextAlign(TextAnchor.MiddleLeft);
            label.tooltip = name;
            card.Add(label);
            return card;
        }

        private static UiButton CreatePrefabVisibilityButton(
            bool isVisible,
            string tooltip,
            Action onClick,
            string className)
        {
            var button = new UiButton(
                string.Empty,
                onClick,
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(className);
            button.tooltip = tooltip;
            button.SetIcon(FluentUiIcons.CreateState(
                isVisible
                    ? "eye.png"
                    : "eye_off.png",
                UiSizeTokens.Size18,
                tooltip));
            return button;
        }

        private VisualElement BuildCategoryRail()
        {
            _categoryButtons.Clear();
            var rail = new VisualElement();
            rail.AddToClassList(
                "ee4v-modification-workflow__category-rail");
            AddCategoryButton(
                rail,
                WorkflowCategory.ShapeParts,
                "workflow.category.shapeParts",
                "cube.png");
            AddCategoryButton(
                rail,
                WorkflowCategory.Material,
                "workflow.category.material",
                "image.png");
            AddCategoryButton(
                rail,
                WorkflowCategory.ExpressionAnimation,
                "workflow.category.expressionAnimation",
                "star.png");
            AddCategoryButton(
                rail,
                WorkflowCategory.PhysBone,
                "workflow.category.physBone",
                "cube.png");
            return rail;
        }

        private void AddCategoryButton(
            VisualElement rail,
            WorkflowCategory category,
            string labelKey,
            string icon)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () => ShowCategory(category),
                icon: AssetManagerControls.LoadFluentIconState(
                    icon,
                    UiSizeTokens.Size18),
                variant: UiButtonVariant.Ghost,
                labelTypographyClassName:
                    UiClassNames.NavigationItemLabel);
            button.AddToClassList(
                "ee4v-modification-workflow__category-button");
            button.SetEnabled(category == WorkflowCategory.ShapeParts ||
                category == WorkflowCategory.Material ||
                !_selectedPrefabSiblingIndex.HasValue);
            _categoryButtons[category] = button;
            rail.Add(button);
        }

        private VisualElement BuildPreviewPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList(
                "ee4v-modification-workflow__preview-pane");
            var toolbar = new VisualElement();
            toolbar.AddToClassList(
                "ee4v-modification-workflow__preview-toolbar");
            _previewTitle = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__preview-title");
            toolbar.Add(_previewTitle);
            pane.Add(toolbar);

            var viewport = new VisualElement();
            viewport.AddToClassList(
                "ee4v-modification-workflow__preview-viewport");
            _scenePreview = new DerivedAssetPrefabScenePreview();
            _scenePreview.SetFlexibleLayout(true);
            _scenePreview.AddToClassList(
                "ee4v-modification-workflow__preview");
            _scenePreview.SetScope(
                _selectedPrefabSiblingIndex,
                _prefabSiblingIndices);
            _scenePreview.SetHiddenPrefabs(
                _basePrefabHidden,
                _hiddenPrefabSiblingIndices);
            _scenePreview.SetPrefab(_workingObject);
            viewport.Add(_scenePreview);
            pane.Add(viewport);
            return pane;
        }

        private void ShowCategory(
            WorkflowCategory category,
            bool clearFeedback = true)
        {
            if (category != WorkflowCategory.ShapeParts ||
                _shapePartsSection != ShapePartsSection.Parts)
            {
                if (!FlushPendingPartVisibility())
                {
                    BuildWindow();
                    return;
                }
            }
            if (_selectedPrefabSiblingIndex.HasValue &&
                category != WorkflowCategory.ShapeParts &&
                category != WorkflowCategory.Material)
            {
                category = WorkflowCategory.ShapeParts;
            }
            if (_currentCategory == WorkflowCategory.ShapeParts &&
                _shapePartsSection == ShapePartsSection.Shape &&
                category != WorkflowCategory.ShapeParts)
            {
                EndBodyScaleDrag();
            }
            if (_customizerHost == null ||
                _faceExpressionHost == null ||
                _physBoneHost == null)
            {
                _currentCategory = category;
                return;
            }
            var categoryChanged = _currentCategory != category;
            if (categoryChanged && clearFeedback)
            {
                _feedback = string.Empty;
            }
            _currentCategory = category;
            _scenePreview?.SetHiddenMaterials(
                category == WorkflowCategory.Material
                    ? _hiddenMaterials
                    : null);
            foreach (var pair in _categoryButtons)
            {
                pair.Value.EnableInClassList(
                    "ee4v-modification-workflow__category-button--active",
                    pair.Key == category);
            }

            var faceExpression =
                category == WorkflowCategory.ExpressionAnimation;
            var physBone = category == WorkflowCategory.PhysBone;
            DisposeMaterialEditor();
            _customizerHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                faceExpression || physBone);
            _faceExpressionHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !faceExpression);
            _physBoneHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !physBone);
            if (faceExpression)
            {
                if (_faceExpressionEditor == null)
                {
                    _faceExpressionEditor = FaceExpressionWindow.Embed(
                        _faceExpressionHost,
                        _workingObject,
                        RequestRepaint);
                }
                _faceExpressionEditor.SetActive(true);
                return;
            }

            _faceExpressionEditor?.SetActive(false);
            if (physBone)
            {
                if (_physBoneEditor == null)
                {
                    _physBoneEditor = PhysBoneColliderWindow.Embed(
                        _physBoneHost,
                        _workingObject,
                        RefreshMaterialPreview);
                }
                return;
            }

            if (_selectedBodyPart.HasValue &&
                !HasFocusBone(_selectedBodyPart.Value))
            {
                _selectedBodyPart = null;
                _scenePreview?.FocusBodyPart(null);
            }

            _appearanceHeader.Clear();
            _appearanceHeader.style.display = DisplayStyle.Flex;
            if (category == WorkflowCategory.ShapeParts)
            {
                _appearanceHeader.Add(BuildBodyPartSelector());
                _appearanceHeader.Add(BuildShapePartsTabs());
            }
            else
            {
                var selector = BuildBodyPartSelector();
                selector.AddToClassList(
                    "ee4v-modification-workflow__part-selector--standalone");
                _appearanceHeader.Add(selector);
            }
            _controlsHost.Clear();
            _controlsHost.Add(BuildAppearanceControls());
            if (categoryChanged)
            {
                _controlsHost.scrollOffset = Vector2.zero;
            }
            SetPreviewTitle(
                category == WorkflowCategory.Material
                    ? "workflow.preview.appearanceTitle"
                    : "workflow.preview.sizeTitle");
        }

        private VisualElement BuildAssetChildCard(AssetChildEntry child)
        {
            var card = CreateHeaderPrefabCard(
                child.Name,
                "ee4v-modification-workflow__header-card--part");
            card.EnableInClassList(
                "ee4v-modification-workflow__header-card--selected",
                _selectedPrefabSiblingIndex == child.SiblingIndex);
            card.RegisterCallback<ClickEvent>(
                _ => SelectPrefabCard(child.SiblingIndex, child.Name));
            card.EnableInClassList(
                "ee4v-modification-workflow__header-card--hidden",
                _hiddenPrefabSiblingIndices.Contains(child.SiblingIndex));
            UiButton visibility = null;
            visibility = CreatePrefabVisibilityButton(
                !_hiddenPrefabSiblingIndices.Contains(child.SiblingIndex),
                I18N.Get(_hiddenPrefabSiblingIndices.Contains(child.SiblingIndex)
                    ? "workflow.assets.clickToShow"
                    : "workflow.assets.clickToHide"),
                () => TogglePrefabPreviewVisibility(
                    child.SiblingIndex, card, visibility),
                "ee4v-modification-workflow__header-card-visibility");
            visibility.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            card.Add(visibility);
            if (child.IsAdded)
            {
                card.RegisterCallback<ContextClickEvent>(evt =>
                {
                    var menu = new GenericMenu();
                    menu.AddItem(
                        UiTextFactory.CreateGuiContent(
                            I18N.Get("workflow.assets.remove")),
                        false,
                        () => RemoveAssetChild(child));
                    menu.ShowAsContext();
                    evt.PreventDefault();
                    evt.StopPropagation();
                });
            }
            return card;
        }

        private void TogglePrefabPreviewVisibility(
            int siblingIndex,
            VisualElement card,
            UiButton button)
        {
            bool hidden;
            if (siblingIndex == -1)
            {
                _basePrefabHidden = !_basePrefabHidden;
                hidden = _basePrefabHidden;
            }
            else if (_hiddenPrefabSiblingIndices.Contains(siblingIndex))
            {
                _hiddenPrefabSiblingIndices.Remove(siblingIndex);
                hidden = false;
            }
            else
            {
                _hiddenPrefabSiblingIndices.Add(siblingIndex);
                hidden = true;
            }
            card.EnableInClassList(
                "ee4v-modification-workflow__header-card--hidden",
                hidden);
            var tooltip = I18N.Get(hidden
                ? "workflow.assets.clickToShow"
                : "workflow.assets.clickToHide");
            button.tooltip = tooltip;
            button.SetIcon(FluentUiIcons.CreateState(
                hidden
                    ? "eye_off.png"
                    : "eye.png",
                UiSizeTokens.Size18,
                tooltip));
            _scenePreview?.SetHiddenPrefabs(
                _basePrefabHidden,
                _hiddenPrefabSiblingIndices);
        }

        private void SelectPrefabCard(int? siblingIndex, string name)
        {
            if (_selectedPrefabSiblingIndex == siblingIndex)
            {
                return;
            }

            EndBodyScaleDrag();
            _selectedPrefabSiblingIndex = siblingIndex;
            _selectedPrefabName = name ?? string.Empty;
            _selectedBodyPart = null;
            _selectedMaterial = null;
            if (siblingIndex.HasValue &&
                _currentCategory != WorkflowCategory.Material)
            {
                _currentCategory = WorkflowCategory.ShapeParts;
            }
            _assetFeedback = string.Empty;
            BuildWindow();
        }

        private VisualElement BuildObjectControls()
        {
            _objectRows.Clear();
            var panel = new VisualElement();
            panel.AddToClassList(
                "ee4v-modification-workflow__objects-content");
            if (!IsEditableWorkflowPrefab())
            {
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.assets.protected"),
                    HelpBoxMessageType.Warning));
                return panel;
            }

            IReadOnlyList<PrefabObjectEntry> entries;
            try
            {
                entries = ReadPrefabObjects();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.assets.readFailed"),
                    HelpBoxMessageType.Error));
                return panel;
            }

            IReadOnlyList<PrefabObjectEntry> displayed = entries;
            if (_selectedBodyPart.HasValue)
            {
                displayed = entries.Where(entry =>
                    entry.Categories.Any(category => MatchesBodyPartGroup(
                        _selectedBodyPart.Value, category))).ToArray();
            }
            if (displayed.Count == 0)
            {
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.objects.empty"),
                    HelpBoxMessageType.Info));
                return panel;
            }

            var list = new VisualElement();
            list.AddToClassList(
                "ee4v-modification-workflow__objects-list");
            panel.Add(list);
            foreach (var entry in displayed)
            {
                list.Add(BuildObjectRow(entry));
            }
            return panel;
        }

        private VisualElement BuildObjectRow(PrefabObjectEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-modification-workflow__object-row");
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__object-text");
            var name = UiTextFactory.Create(
                entry.Name,
                "ee4v-modification-workflow__object-name");
            name.tooltip = entry.Path;
            text.Add(name);
            var path = UiTextFactory.Create(
                entry.Path,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__object-path");
            path.tooltip = entry.Path;
            text.Add(path);
            row.Add(text);
            var visibility = UiTextFactory.CreateToggle();
            visibility.AddToClassList(
                "ee4v-modification-workflow__object-visibility");
            visibility.RegisterValueChangedCallback(evt =>
                ChangePrefabObject(entry, evt.newValue));
            row.Add(visibility);
            var state = new PrefabObjectRowState
            {
                Row = row,
                Visibility = visibility
            };
            _objectRows[entry] = state;
            UpdateObjectRow(entry, state);
            return row;
        }

        private static void UpdateObjectRow(
            PrefabObjectEntry entry,
            PrefabObjectRowState state)
        {
            var parentHidden = !entry.IsVisibleInHierarchy &&
                               entry.IsVisible;
            state.Row.EnableInClassList(
                "ee4v-modification-workflow__object-row--parent-hidden",
                parentHidden);
            state.Row.tooltip = parentHidden
                ? I18N.Get("workflow.objects.parentHidden")
                : string.Empty;
            state.Visibility.SetValueWithoutNotify(entry.IsVisible);
            state.Visibility.tooltip = I18N.Get(entry.IsVisible
                ? "workflow.objects.turnOff"
                : "workflow.objects.turnOn");
        }

        private IReadOnlyList<PrefabObjectEntry> ReadPrefabObjects()
        {
            if (_objectEntriesCache != null)
            {
                return _objectEntriesCache;
            }
            var path = AssetDatabase.GetAssetPath(_workingObject);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var result = new List<PrefabObjectEntry>();
                var classifier = new PrefabPartClassifier(root);
                var excludedPrefixes =
                    AssetManagerSettings.ExcludedPartPrefixes;
                var scopes = _selectedPrefabSiblingIndex.HasValue
                    ? new[] { _selectedPrefabSiblingIndex.Value }
                    : new[] { -1 }.Concat(_prefabSiblingIndices).ToArray();
                foreach (var prefabSiblingIndex in scopes)
                {
                    var selected = ResolveObjectPrefab(
                        root,
                        prefabSiblingIndex,
                        _selectedPrefabSiblingIndex.HasValue
                            ? _selectedPrefabName
                            : null);
                    if (selected == null)
                    {
                        throw new InvalidOperationException(
                            "The selected Prefab is no longer present.");
                    }
                    if (IsExcludedPartName(
                            selected.name,
                            excludedPrefixes))
                    {
                        continue;
                    }

                    void Visit(
                        Transform parent,
                        IReadOnlyList<int> parentIndices,
                        string parentPath)
                    {
                        for (var index = 0; index < parent.childCount; index++)
                        {
                            var child = parent.GetChild(index);
                            if (IsExcludedPartName(
                                    child.name,
                                    excludedPrefixes))
                            {
                                continue;
                            }
                            if (parent == selected.transform &&
                                prefabSiblingIndex == -1 &&
                                PrefabUtility.IsAnyPrefabInstanceRoot(
                                    child.gameObject))
                            {
                                continue;
                            }
                            var indices = parentIndices.Concat(
                                new[] { index }).ToArray();
                            var objectPath = string.IsNullOrEmpty(parentPath)
                                ? child.name
                                : parentPath + "/" + child.name;
                            result.Add(new PrefabObjectEntry
                            {
                                PrefabSiblingIndex = prefabSiblingIndex,
                                PrefabName = selected.name,
                                SiblingPath = indices,
                                Name = child.name,
                                Path = objectPath,
                                IsVisible = child.gameObject.activeSelf &&
                                    !string.Equals(child.gameObject.tag,
                                        "EditorOnly", StringComparison.Ordinal) &&
                                    (child.gameObject.hideFlags &
                                        HideFlags.HideInHierarchy) == 0,
                                IsActiveSelf = child.gameObject.activeSelf,
                                ParentActiveInHierarchy =
                                    child.parent.gameObject.activeInHierarchy,
                                IsVisibleInHierarchy =
                                    child.gameObject.activeInHierarchy,
                                Categories = classifier.Classify(child)
                            });
                            Visit(child, indices, objectPath);
                        }
                    }
                    Visit(
                        selected.transform,
                        Array.Empty<int>(),
                        _selectedPrefabSiblingIndex.HasValue
                            ? string.Empty
                            : selected.name);
                }
                _objectEntriesCache = result.ToArray();
                return _objectEntriesCache;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsExcludedPartName(
            string name,
            IReadOnlyList<string> excludedPrefixes)
        {
            return !string.IsNullOrEmpty(name) &&
                   excludedPrefixes.Any(prefix =>
                       name.StartsWith(
                           prefix,
                           StringComparison.OrdinalIgnoreCase));
        }

        private void ChangePrefabObject(
            PrefabObjectEntry entry,
            bool visible)
        {
            if (entry.IsVisible == visible)
            {
                return;
            }
            try
            {
                if (!IsEditableWorkflowPrefab())
                {
                    throw new InvalidOperationException(
                        "The selected Prefab is not a derived asset.");
                }
                var assetPath = AssetDatabase.GetAssetPath(_workingObject);
                if (_pendingPartVisibility.Count > 0 &&
                    !string.Equals(_pendingPartAssetPath, assetPath,
                        StringComparison.Ordinal) &&
                    !FlushPendingPartVisibility())
                {
                    BuildWindow();
                    return;
                }
                if (!_pendingPartVisibility.TryGetValue(entry,
                        out var pending))
                {
                    pending = new PendingPartVisibility
                    {
                        Entry = entry,
                        OriginalVisible = entry.IsVisible,
                        OriginalActiveSelf = entry.IsActiveSelf,
                        RestoreKey = "ee4v.asset-manager.object-tag." +
                            AssetDatabase.AssetPathToGUID(assetPath) + "." +
                            entry.PrefabSiblingIndex + "." +
                            string.Join(".", entry.SiblingPath.Select(index =>
                                index.ToString()).ToArray()) + "." +
                            entry.Name
                    };
                    _pendingPartVisibility.Add(entry, pending);
                }
                pending.Visible = visible;
                _pendingPartAssetPath = assetPath;
                var returningToOriginal =
                    visible == pending.OriginalVisible;
                var activeSelf = returningToOriginal
                    ? pending.OriginalActiveSelf
                    : visible;
                if (returningToOriginal)
                {
                    _pendingPartVisibility.Remove(entry);
                }
                UpdateCachedObjectVisibility(entry, visible, activeSelf);
                _scenePreview?.SetPartVisibility(
                    _workingObject,
                    entry.PrefabSiblingIndex,
                    entry.SiblingPath,
                    entry.Name,
                    activeSelf);
                if (_pendingPartVisibility.Count == 0)
                {
                    _pendingPartAssetPath = null;
                    EditorApplication.update -= OnPartVisibilitySaveUpdate;
                    return;
                }
                _partVisibilitySaveDueAt =
                    EditorApplication.timeSinceStartup +
                    PartVisibilitySaveDelaySeconds;
                EditorApplication.update -= OnPartVisibilitySaveUpdate;
                EditorApplication.update += OnPartVisibilitySaveUpdate;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void UpdateCachedObjectVisibility(
            PrefabObjectEntry entry,
            bool visible,
            bool activeSelf)
        {
            entry.IsVisible = visible;
            entry.IsActiveSelf = activeSelf;
            entry.IsVisibleInHierarchy =
                entry.ParentActiveInHierarchy && activeSelf;
            if (_objectRows.TryGetValue(entry, out var changedRow))
            {
                UpdateObjectRow(entry, changedRow);
            }
            if (_objectEntriesCache == null)
            {
                return;
            }

            var activeAtDepth = new Dictionary<int, bool>
            {
                [entry.SiblingPath.Length] = entry.IsVisibleInHierarchy
            };
            foreach (var candidate in _objectEntriesCache)
            {
                if (candidate == entry ||
                    candidate.PrefabSiblingIndex !=
                        entry.PrefabSiblingIndex ||
                    candidate.SiblingPath.Length <
                        entry.SiblingPath.Length)
                {
                    continue;
                }
                var descendant = true;
                for (var index = 0;
                     index < entry.SiblingPath.Length;
                     index++)
                {
                    if (candidate.SiblingPath[index] ==
                        entry.SiblingPath[index])
                    {
                        continue;
                    }
                    descendant = false;
                    break;
                }
                if (!descendant)
                {
                    continue;
                }
                var parentDepth = candidate.SiblingPath.Length - 1;
                if (!activeAtDepth.TryGetValue(parentDepth,
                        out var parentActive))
                {
                    throw new InvalidOperationException(
                        "The part hierarchy changed before editing.");
                }
                candidate.ParentActiveInHierarchy = parentActive;
                candidate.IsVisibleInHierarchy =
                    parentActive && candidate.IsActiveSelf;
                activeAtDepth[candidate.SiblingPath.Length] =
                    candidate.IsVisibleInHierarchy;
                if (_objectRows.TryGetValue(candidate, out var row))
                {
                    UpdateObjectRow(candidate, row);
                }
            }
        }

        private void OnPartVisibilitySaveUpdate()
        {
            if (EditorApplication.timeSinceStartup <
                _partVisibilitySaveDueAt)
            {
                return;
            }
            if (!FlushPendingPartVisibility())
            {
                BuildWindow();
            }
        }

        private bool FlushPendingPartVisibility()
        {
            EditorApplication.update -= OnPartVisibilitySaveUpdate;
            if (_pendingPartVisibility.Count == 0)
            {
                return true;
            }

            var changes = _pendingPartVisibility.Values.ToArray();
            var assetPath = _pendingPartAssetPath;
            _pendingPartVisibility.Clear();
            _pendingPartAssetPath = null;
            try
            {
                var restoreTags = new List<PartTagChange>();
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    foreach (var change in changes)
                    {
                        ApplyPartVisibility(root, change, restoreTags);
                    }
                    var saved = PrefabUtility.SaveAsPrefabAsset(
                        root, assetPath, out var success);
                    if (!success || saved == null)
                    {
                        throw new InvalidOperationException(
                            "The derived Prefab could not be saved.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                foreach (var tag in restoreTags)
                {
                    if (tag.Visible)
                    {
                        EditorPrefs.DeleteKey(tag.RestoreKey);
                    }
                    else if (tag.OriginalTag != null)
                    {
                        EditorPrefs.SetString(
                            tag.RestoreKey, tag.OriginalTag);
                    }
                }
                if (_workingObject != null &&
                    string.Equals(AssetDatabase.GetAssetPath(_workingObject),
                        assetPath, StringComparison.Ordinal))
                {
                    _workingObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                        assetPath);
                    if (_workingObject == null)
                    {
                        throw new InvalidOperationException(
                            "The saved derived Prefab could not be loaded.");
                    }
                    if (_workingAsset != null)
                    {
                        _workingAsset.Prefab = _workingObject;
                    }
                    _scenePreview?.UpdatePrefabReference(_workingObject);
                }
                _assetFeedback = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _assetFeedback = I18N.Get("workflow.assets.saveFailed");
                return false;
            }
        }

        private static void ApplyPartVisibility(
            GameObject root,
            PendingPartVisibility change,
            ICollection<PartTagChange> restoreTags)
        {
            var entry = change.Entry;
            var selected = ResolveObjectPrefab(
                root, entry.PrefabSiblingIndex, entry.PrefabName);
            if (selected == null)
            {
                throw new InvalidOperationException(
                    "The selected Prefab is no longer present.");
            }
            var current = selected.transform;
            foreach (var index in entry.SiblingPath)
            {
                if (index < 0 || index >= current.childCount)
                {
                    throw new InvalidOperationException(
                        "The selected object changed before editing.");
                }
                current = current.GetChild(index);
            }
            if (!string.Equals(current.name, entry.Name,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The selected object changed before editing.");
            }

            var gameObject = current.gameObject;
            if (change.Visible)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(
                    gameObject) as GameObject;
                var tag = EditorPrefs.HasKey(change.RestoreKey)
                    ? EditorPrefs.GetString(change.RestoreKey)
                    : source != null &&
                      !string.Equals(source.tag, "EditorOnly",
                          StringComparison.Ordinal)
                        ? source.tag
                        : "Untagged";
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
                gameObject.tag = tag;
                gameObject.SetActive(true);
            }
            else
            {
                restoreTags.Add(new PartTagChange
                {
                    RestoreKey = change.RestoreKey,
                    OriginalTag = string.Equals(gameObject.tag, "EditorOnly",
                        StringComparison.Ordinal)
                        ? null
                        : gameObject.tag
                });
                gameObject.SetActive(false);
                gameObject.tag = "EditorOnly";
                gameObject.hideFlags |= HideFlags.HideInHierarchy;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    gameObject);
            }
            if (change.Visible)
            {
                restoreTags.Add(new PartTagChange
                {
                    RestoreKey = change.RestoreKey,
                    Visible = true
                });
            }
        }

        private static GameObject ResolveObjectPrefab(
            GameObject root,
            int prefabSiblingIndex,
            string prefabName)
        {
            if (prefabSiblingIndex == -1)
            {
                return root;
            }
            if (prefabSiblingIndex < 0 ||
                prefabSiblingIndex >= root.transform.childCount)
            {
                return null;
            }
            var child = root.transform.GetChild(prefabSiblingIndex).gameObject;
            return PrefabUtility.IsAnyPrefabInstanceRoot(child) &&
                   (prefabName == null ||
                    string.Equals(child.name, prefabName,
                        StringComparison.Ordinal))
                ? child
                : null;
        }

        private IReadOnlyList<AssetChildEntry> ReadAssetChildren()
        {
            var path = AssetDatabase.GetAssetPath(_workingObject);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var children = new List<AssetChildEntry>();
                for (var index = 0; index < root.transform.childCount; index++)
                {
                    var child = root.transform.GetChild(index).gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(child))
                    {
                        continue;
                    }
                    children.Add(new AssetChildEntry
                    {
                        SiblingIndex = index,
                        Name = child.name,
                        IsAdded = PrefabUtility.IsAddedGameObjectOverride(child),
                        IsVisible = child.activeSelf
                    });
                }
                return children.OrderBy(child => child.SiblingIndex)
                    .ToArray();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private void OpenAssetPicker(
            VisualElement anchor,
            IReadOnlyList<VariantSourceOption> sources)
        {
            var prefabs = sources
                .SelectMany(source => source.Prefabs)
                .Where(prefab => prefab != null)
                .GroupBy(AssetDatabase.GetAssetPath,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(prefab => prefab.name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var target = _workingObject;
            DerivedAssetPrefabPickerWindow.Show(
                anchor,
                prefabs,
                null,
                prefab =>
                {
                    if (_workingObject == target)
                    {
                        AddPrefab(prefab);
                    }
                },
                showAsGrid: true);
        }

        private void AddPrefab(GameObject prefab)
        {
            if (!IsEditableWorkflowPrefab() || prefab == null)
            {
                ShowAssetError("workflow.assets.invalidPrefab");
                return;
            }
            var previousSelection = _selectedPrefabSiblingIndex;
            var previousName = _selectedPrefabName;
            var previousCategory = _currentCategory;
            var previousSection = _shapePartsSection;
            try
            {
                var prefabPath = AssetDatabase.GetAssetPath(prefab);
                var variantPath = AssetDatabase.GetAssetPath(_workingObject);
                if (string.IsNullOrEmpty(prefabPath) ||
                    !prefabPath.EndsWith(".prefab",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefabPath, variantPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.GetDependencies(prefabPath, true).Any(path =>
                        string.Equals(path, variantPath,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    ShowAssetError("workflow.assets.invalidPrefab");
                    return;
                }

                EditAssetChildren(root =>
                {
                    var added = PrefabUtility.InstantiatePrefab(
                        prefab,
                        root.scene) as GameObject;
                    if (added == null)
                    {
                        throw new InvalidOperationException(
                            "The child Prefab could not be instantiated.");
                    }
                    added.transform.SetParent(root.transform, false);
                    _selectedPrefabSiblingIndex =
                        added.transform.GetSiblingIndex();
                    _selectedPrefabName = added.name;
                    _currentCategory = WorkflowCategory.ShapeParts;
                    _shapePartsSection = ShapePartsSection.Parts;
                    _hiddenPrefabSiblingIndices.Clear();
                    _prefabPreviewVisibilityInitialized = false;
                });
            }
            catch (Exception exception)
            {
                _selectedPrefabSiblingIndex = previousSelection;
                _selectedPrefabName = previousName;
                _currentCategory = previousCategory;
                _shapePartsSection = previousSection;
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void RemoveAssetChild(AssetChildEntry entry)
        {
            if (!EditorUtility.DisplayDialog(
                    I18N.Get("workflow.assets.removeConfirmTitle"),
                    string.Format(
                        I18N.Get("workflow.assets.removeConfirmMessage"),
                        entry.Name),
                    I18N.Get("workflow.assets.remove"),
                    I18N.Get("action.cancel")))
            {
                return;
            }
            var previousSelection = _selectedPrefabSiblingIndex;
            var previousName = _selectedPrefabName;
            var previousCategory = _currentCategory;
            try
            {
                EditAssetChildren(root =>
                {
                    if (entry.SiblingIndex >= root.transform.childCount)
                    {
                        throw new InvalidOperationException(
                            "The child Prefab is no longer present.");
                    }
                    var child = root.transform
                        .GetChild(entry.SiblingIndex).gameObject;
                    if (!string.Equals(child.name, entry.Name,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "The child Prefab changed before editing.");
                    }
                    if (!PrefabUtility.IsAddedGameObjectOverride(child))
                    {
                        throw new InvalidOperationException(
                            "Inherited Prefab children cannot be removed.");
                    }
                    UnityEngine.Object.DestroyImmediate(child);
                    _selectedPrefabSiblingIndex = null;
                    _selectedPrefabName = string.Empty;
                    _currentCategory = previousCategory ==
                                       WorkflowCategory.Material
                        ? WorkflowCategory.Material
                        : WorkflowCategory.ShapeParts;
                    _hiddenPrefabSiblingIndices.Clear();
                    _prefabPreviewVisibilityInitialized = false;
                });
            }
            catch (Exception exception)
            {
                _selectedPrefabSiblingIndex = previousSelection;
                _selectedPrefabName = previousName;
                _currentCategory = previousCategory;
                Debug.LogException(exception);
                ShowAssetError("workflow.assets.saveFailed");
            }
        }

        private void EditAssetChildren(
            Action<GameObject> edit)
        {
            if (!FlushPendingPartVisibility())
            {
                throw new InvalidOperationException(
                    "Pending part visibility could not be saved.");
            }
            if (!IsEditableWorkflowPrefab())
            {
                throw new InvalidOperationException(
                    "The selected Prefab is not a derived asset.");
            }
            var path = AssetDatabase.GetAssetPath(_workingObject);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(
                    root, path, out var success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException(
                        "The derived Prefab could not be saved.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            _workingObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (_workingObject == null)
            {
                throw new InvalidOperationException(
                    "The saved derived Prefab could not be loaded.");
            }
            if (_workingAsset != null)
            {
                _workingAsset.Prefab = _workingObject;
            }
            _bodyScaleBaseScales.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
            _selectedMaterial = null;
            _hiddenMaterials.Clear();
            _feedback = string.Empty;
            _assetFeedback = string.Empty;
            BuildWindow();
        }

        private void ShowAssetError(string key)
        {
            _assetFeedback = I18N.Get(key);
            BuildWindow();
        }

        private VisualElement BuildAppearanceControls()
        {
            var panel = new VisualElement();
            panel.AddToClassList(
                "ee4v-modification-workflow__controls-content");
            AddFeedback(panel);
            if (_currentCategory == WorkflowCategory.ShapeParts)
            {
                panel.Add(_shapePartsSection == ShapePartsSection.Shape
                    ? BuildBodyScaleControls()
                    : BuildObjectControls());
                return panel;
            }

            var allMaterials = GetAvatarMaterials();
            var filteredMaterials = new List<AvatarMaterialEntry>();
            foreach (var entry in allMaterials)
            {
                var usages = entry.Usages.Where(usage =>
                    usage.Categories.Any(MatchesSelectedBodyPart)).ToArray();
                if (usages.Length == 0)
                {
                    continue;
                }
                var filtered = new AvatarMaterialEntry
                {
                    Material = entry.Material
                };
                filtered.Usages.AddRange(usages);
                filteredMaterials.Add(filtered);
            }
            _materialVisibilityButtons.Clear();
            _allMaterialsVisibilityButton = null;
            if (filteredMaterials.Count == 0)
            {
                panel.Add(CreateEmptyState(
                    "workflow.appearance.emptyTitle",
                    _selectedBodyPart.HasValue
                        ? "workflow.appearance.emptyPartDescription"
                        : "workflow.appearance.emptyDescription"));
                return panel;
            }

            if (_selectedMaterial == null ||
                !filteredMaterials.Any(entry =>
                    entry.Material == _selectedMaterial))
            {
                _selectedMaterial = filteredMaterials[0].Material;
            }
            var availableMaterials = new HashSet<Material>(
                filteredMaterials.Select(entry => entry.Material));
            var allAvailableMaterials = new HashSet<Material>(
                allMaterials.Select(entry => entry.Material));
            _hiddenMaterials.RemoveWhere(material => material == null ||
                !allAvailableMaterials.Contains(material));
            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);

            var materialChoices = new VisualElement();
            materialChoices.AddToClassList(
                "ee4v-modification-workflow__material-list");
            materialChoices.Add(BuildAllMaterialsVisibilityRow(
                availableMaterials));
            foreach (var entry in filteredMaterials)
            {
                var material = entry.Material;
                var choice = new NavigationItem(
                    new NavigationItemState(
                        material.name,
                        FormatMaterialUsageSummary(entry),
                        CreateMaterialIcon(material),
                        material == _selectedMaterial),
                    () =>
                    {
                        _selectedMaterial = material;
                        ShowCategory(WorkflowCategory.Material, false);
                    });
                choice.AddToClassList(
                    "ee4v-modification-workflow__material-item");
                choice.tooltip = FormatMaterialUsageTooltip(entry);
                if (!IsEditableWorkflowMaterial(material))
                {
                    choice.Trailing.Add(new Badge(
                        I18N.Get("workflow.appearance.readOnly")));
                }
                var visibility = new UiButton(
                    string.Empty,
                    () => ToggleMaterialVisibility(material),
                    variant: UiButtonVariant.Ghost);
                visibility.AddToClassList(
                    "ee4v-modification-workflow__material-visibility");
                visibility.RegisterCallback<ClickEvent>(
                    evt => evt.StopPropagation());
                choice.Trailing.Add(visibility);
                _materialVisibilityButtons[material] = visibility;
                materialChoices.Add(choice);
            }
            RefreshMaterialVisibilityButtons(availableMaterials);
            panel.Add(materialChoices);

            if (!IsEditableWorkflowMaterial(_selectedMaterial))
            {
                if (IsEditableWorkflowPrefab())
                {
                    var sourceMaterial = _selectedMaterial;
                    var makeEditable = new UiButton(
                        I18N.Get("workflow.appearance.makeEditable"),
                        () => CreateEditableMaterialVariant(sourceMaterial));
                    makeEditable.AddToClassList(
                        "ee4v-modification-workflow__make-material-editable");
                    panel.Add(makeEditable);
                }
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.protected"),
                    HelpBoxMessageType.Warning));
                return panel;
            }

            panel.Add(BuildMaterialEditor(_selectedMaterial));
            return panel;
        }

        private VisualElement BuildBodyPartSelector()
        {
            var selector = new VisualElement();
            selector.AddToClassList(
                "ee4v-modification-workflow__part-selector");
            AddBodyPartButton(
                selector,
                null,
                "workflow.appearance.bodyPart.wholeBody");
            foreach (var part in BodyPartSelectorOrder)
            {
                AddBodyPartButton(
                    selector,
                    part,
                    GetBodyPartCategoryLocalizationKey(part));
            }
            return selector;
        }

        private void AddBodyPartButton(
            VisualElement selector,
            BodyPartCategory? part,
            string labelKey)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () =>
                {
                    if (_selectedBodyPart == part)
                    {
                        _scenePreview?.FocusBodyPart(part);
                        return;
                    }
                    EndBodyScaleDrag();
                    _selectedBodyPart = part;
                    _selectedMaterial = null;
                    ShowCategory(_currentCategory, false);
                    _scenePreview?.FocusBodyPart(part);
                },
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__part-button");
            button.EnableInClassList(
                "ee4v-modification-workflow__part-button--active",
                _selectedBodyPart == part);
            button.SetEnabled(!part.HasValue || HasFocusBone(part.Value));
            selector.Add(button);
        }

        private bool HasFocusBone(BodyPartCategory part)
        {
            return DerivedAssetPrefabScenePreview.HasFocusBone(
                _workingObject, part, IsInSelectedPrefabScope);
        }

        private VisualElement BuildBodyScaleControls()
        {
            var content = new VisualElement();
            content.AddToClassList(
                "ee4v-modification-workflow__size-content");

            if (!IsEditableWorkflowPrefab())
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.sizeProtected"),
                    HelpBoxMessageType.Warning));
                return content;
            }

            var animator = FindHumanoidAnimator();
            if (!_selectedBodyPart.HasValue &&
                (!_selectedPrefabSiblingIndex.HasValue ||
                 _selectedPrefabSiblingIndex == -1))
            {
                var primaryScaleList = new VisualElement();
                primaryScaleList.AddToClassList(
                    "ee4v-modification-workflow__size-list");
                var wholeBody = BodyScaleDefinitions.First(definition =>
                    definition.UsesAvatarRoot);
                var wholeBodyTargets = ResolveBodyScaleTargets(
                    wholeBody,
                    animator);
                if (TryGetWorkingAvatarViewPosition(
                        out var avatarDescriptor,
                        out var currentViewPosition))
                {
                    var baseViewPosition = GetBaseAvatarViewPosition(
                        avatarDescriptor,
                        currentViewPosition);
                    primaryScaleList.Add(BuildBodyHeightControl(
                        wholeBody,
                        GetTransformPaths(wholeBodyTargets),
                        currentViewPosition,
                        baseViewPosition));
                }
                else
                {
                    primaryScaleList.Add(UiTextFactory.CreateHelpBox(
                        I18N.Get("workflow.appearance.heightDescriptorRequired"),
                        HelpBoxMessageType.Warning));
                }
                content.Add(primaryScaleList);
            }

            var allBodyShapes = GetBodyBlendShapes();
            EnsureBodyBlendShapeSyncBindings(allBodyShapes);
            var bodyShapes = allBodyShapes
                .Where(definition =>
                    MatchesSelectedBodyPart(definition.Category))
                .ToArray();
            if (bodyShapes.Length == 0)
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get(_selectedBodyPart.HasValue
                        ? "workflow.appearance.bodyShapePartEmpty"
                        : "workflow.appearance.bodyShapeEmpty"),
                    HelpBoxMessageType.Info));
            }
            else
            {
                var bodyShapeList = new VisualElement();
                bodyShapeList.AddToClassList(
                    "ee4v-modification-workflow__size-list");
                foreach (BodyPartCategory category in Enum.GetValues(
                             typeof(BodyPartCategory)))
                {
                    if (category == BodyPartCategory.Arms)
                    {
                        continue;
                    }
                    var categoryShapes = bodyShapes
                        .Where(definition =>
                            MatchesBodyPartGroup(
                                category, definition.Category))
                        .ToArray();
                    if (categoryShapes.Length == 0)
                    {
                        continue;
                    }

                    if (!_selectedBodyPart.HasValue &&
                        category != BodyPartCategory.Other)
                    {
                        var categoryLabel = UiTextFactory.Create(
                            I18N.Get(GetBodyPartCategoryLocalizationKey(
                                category)),
                            UiClassNames.SecondaryText,
                            "ee4v-modification-workflow__size-group-title");
                        bodyShapeList.Add(categoryLabel);
                    }
                    var shapeGroups = categoryShapes
                        .GroupBy(shape => shape.ShapeName,
                            StringComparer.Ordinal)
                        .ToArray();
                    var roleGroups = shapeGroups
                        .Select((shapes, index) => new
                        {
                            Shapes = shapes.ToArray(),
                            Key = string.IsNullOrWhiteSpace(shapes.First().Group)
                                ? "\0" + index
                                : shapes.First().Group
                        })
                        .GroupBy(item => item.Key, StringComparer.Ordinal);
                    foreach (var roleGroup in roleGroups)
                    {
                        var groupedShapes = roleGroup
                            .SelectMany(item => item.Shapes)
                            .ToArray();
                        var hasRoleTitle = !string.IsNullOrWhiteSpace(
                                               groupedShapes[0].Group) &&
                                           (groupedShapes.Length > 1 ||
                                            !string.Equals(
                                                groupedShapes[0].Group,
                                                groupedShapes[0].DisplayName,
                                                StringComparison.OrdinalIgnoreCase));
                        if (hasRoleTitle)
                        {
                            bodyShapeList.Add(UiTextFactory.Create(
                                groupedShapes[0].Group,
                                UiClassNames.SecondaryText,
                                "ee4v-modification-workflow__size-role-title"));
                        }
                        foreach (var shapeGroup in roleGroup)
                        {
                            var control = shapeGroup.Shapes.Length == 1
                                ? BuildBodyBlendShapeControl(
                                    shapeGroup.Shapes[0],
                                    weight => ApplyBodyBlendShape(
                                        shapeGroup.Shapes[0].RendererPath,
                                        shapeGroup.Shapes[0].ShapeName,
                                        weight),
                                    out _)
                                : BuildGroupedBodyBlendShapeControl(
                                    shapeGroup.Shapes);
                            if (hasRoleTitle)
                            {
                                control.AddToClassList(
                                    "ee4v-modification-workflow__size-control--child");
                            }
                            bodyShapeList.Add(control);
                        }
                    }
                }
                content.Add(bodyShapeList);
            }

            var advancedList = new VisualElement();
            advancedList.AddToClassList(
                "ee4v-modification-workflow__size-list");
            var hasBodyPartControls = false;
            for (var index = 1; index < BodyScaleDefinitions.Count; index++)
            {
                var definition = BodyScaleDefinitions[index];
                if (_selectedBodyPart.HasValue &&
                    !MatchesSelectedBodyPart(
                        (BodyPartCategory)(index - 1)))
                {
                    continue;
                }
                var targets = ResolveBodyScaleTargets(definition, animator);
                if (targets.Count == 0)
                {
                    continue;
                }

                hasBodyPartControls = true;
                advancedList.Add(BuildBodyScaleControl(
                    definition,
                    GetTransformPaths(targets),
                    GetBodyScaleMultipliers(targets)));
            }

            if (hasBodyPartControls)
            {
                if (_selectedBodyPart.HasValue)
                {
                    content.Insert(0, advancedList);
                }
                else
                {
                    content.Add(BuildAdvancedBodyScaleFoldout(
                        advancedList));
                }
            }
            else if (_selectedBodyPart != BodyPartCategory.Other)
            {
                var warning = UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.sizeHumanoidRequired"),
                    HelpBoxMessageType.Warning);
                if (_selectedBodyPart.HasValue)
                {
                    content.Insert(0, warning);
                }
                else
                {
                    content.Add(warning);
                }
            }
            return content;
        }

        private bool MatchesSelectedBodyPart(BodyPartCategory category)
        {
            return !_selectedBodyPart.HasValue ||
                MatchesBodyPartGroup(_selectedBodyPart.Value, category);
        }

        private static bool MatchesBodyPartGroup(
            BodyPartCategory selected,
            BodyPartCategory category)
        {
            return selected == category ||
                selected == BodyPartCategory.Shoulders &&
                category == BodyPartCategory.Arms;
        }

        private IReadOnlyList<string> GetTransformPaths(
            IReadOnlyList<Transform> targets)
        {
            return targets
                .Where(target => target != null)
                .Select(target =>
                    AnimationUtility.CalculateTransformPath(
                        target,
                        _workingObject.transform))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private VisualElement BuildAdvancedBodyScaleFoldout(
            VisualElement advancedList)
        {
            var foldout = new VisualElement();
            foldout.AddToClassList(
                "ee4v-modification-workflow__advanced-size");

            UiButton toggle = null;
            toggle = new UiButton(
                I18N.Get("workflow.appearance.advancedSizeTitle"),
                () =>
                {
                    _advancedBodyScaleExpanded =
                        !_advancedBodyScaleExpanded;
                    UpdateAdvancedBodyScaleFoldout(toggle, advancedList);
                },
                icon: AssetManagerControls.LoadFluentIconState(
                    _advancedBodyScaleExpanded
                        ? "chevron_down.png"
                        : "chevron_right.png",
                    UiSizeTokens.Size12),
                variant: UiButtonVariant.Ghost);
            toggle.AddToClassList(
                "ee4v-modification-workflow__advanced-size-toggle");
            foldout.Add(toggle);

            foldout.Add(advancedList);
            UpdateAdvancedBodyScaleFoldout(toggle, advancedList);
            return foldout;
        }

        private void UpdateAdvancedBodyScaleFoldout(
            UiButton toggle,
            VisualElement content)
        {
            toggle?.SetIcon(AssetManagerControls.LoadFluentIconState(
                _advancedBodyScaleExpanded
                    ? "chevron_down.png"
                    : "chevron_right.png",
                UiSizeTokens.Size12));
            content?.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !_advancedBodyScaleExpanded);
        }

        private VisualElement BuildBodyHeightControl(
            BodyScaleDefinition definition,
            IReadOnlyList<string> targetPaths,
            Vector3 currentViewPosition,
            Vector3 baseViewPosition)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var baseHeightMeters = baseViewPosition.y;
            var minimumHeight = Mathf.Max(0.01f, baseHeightMeters - 1f);
            var maximumHeight = baseHeightMeters + 1f;
            var row = CreateSizeControlRow(
                I18N.Get(definition.LocalizationKey),
                out var controls);
            var slider = new Slider(minimumHeight, maximumHeight);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = I18N.Get(
                "workflow.appearance.heightSliderTooltip",
                FormatBodyHeight(minimumHeight),
                FormatBodyHeight(maximumHeight));
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            controls.Add(CreateSizeUnit("m"));
            var reset = CreateSizeResetButton(() =>
                slider.value = baseHeightMeters);
            controls.Add(reset);

            var rendering = false;
            var appliedHeight = Mathf.Clamp(
                Mathf.Round(currentViewPosition.y * 100f) / 100f,
                minimumHeight,
                maximumHeight);
            void SetHeight(float height, bool apply)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(height * 100f) / 100f,
                    minimumHeight,
                    maximumHeight);
                rendering = true;
                slider.SetValueWithoutNotify(normalized);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                reset.SetEnabled(!Mathf.Approximately(
                    normalized,
                    baseHeightMeters));
                if (apply &&
                    !Mathf.Approximately(normalized, appliedHeight))
                {
                    appliedHeight = normalized;
                    var multiplier = normalized / baseHeightMeters;
                    ApplyBodyScale(
                        targetPaths,
                        Vector3.one * multiplier,
                        updateAvatarViewPosition: true);
                }
            }

            RegisterBodySizeSliderDrag(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetHeight(evt.newValue, true);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetHeight(evt.newValue, true);
                }
            });
            SetHeight(currentViewPosition.y, false);
            group.Add(row);
            return group;
        }

        private VisualElement BuildBodyScaleControl(
            BodyScaleDefinition definition,
            IReadOnlyList<string> targetPaths,
            Vector3 initialMultipliers)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var partName = I18N.Get(definition.LocalizationKey);
            var multipliers = RoundBodyScaleMultipliers(initialMultipliers);
            var axisRenderers = new Action<float>[3];
            var axisRows = new VisualElement();
            axisRows.AddToClassList(
                "ee4v-modification-workflow__size-axis-rows");
            UiButton axisToggle = null;
            void ToggleAxes()
            {
                if (!_expandedBodyScaleAxes.Add(
                        definition.LocalizationKey))
                {
                    _expandedBodyScaleAxes.Remove(
                        definition.LocalizationKey);
                }
                UpdateBodyScaleAxisFoldout(
                    axisToggle,
                    axisRows,
                    definition.LocalizationKey,
                    partName);
            }
            axisToggle = AssetManagerControls.CreateIconButton(
                string.Empty,
                "chevron_right.png",
                UiSizeTokens.Size12,
                UiButtonVariant.Ghost,
                ToggleAxes,
                "ee4v-modification-workflow__size-axis-toggle");
            var row = BuildScalePercentControl(
                partName,
                I18N.Get(
                    "workflow.appearance.sizeSliderTooltip",
                    partName),
                AverageVectorComponent(multipliers) * 100f,
                isChild: false,
                percent =>
                {
                    var multiplier = percent / 100f;
                    multipliers = Vector3.one * multiplier;
                    foreach (var renderAxis in axisRenderers)
                    {
                        renderAxis?.Invoke(percent);
                    }
                    ApplyBodyScale(targetPaths, multipliers);
                },
                out var renderUniform,
                axisToggle,
                ToggleAxes);
            group.Add(row);

            for (var axis = 0; axis < 3; axis++)
            {
                var capturedAxis = axis;
                var axisName = GetScaleAxisName(axis);
                var axisRow = BuildScalePercentControl(
                    axisName,
                    I18N.Get(
                        "workflow.appearance.axisScaleSliderTooltip",
                        partName,
                        axisName),
                    GetVectorComponent(multipliers, axis) * 100f,
                    isChild: true,
                    percent =>
                    {
                        multipliers = SetVectorComponent(
                            multipliers,
                            capturedAxis,
                            percent / 100f);
                        renderUniform(
                            AverageVectorComponent(multipliers) * 100f);
                        ApplyBodyScale(targetPaths, multipliers);
                    },
                    out axisRenderers[axis]);
                axisRows.Add(axisRow);
            }
            group.Add(axisRows);
            UpdateBodyScaleAxisFoldout(
                axisToggle,
                axisRows,
                definition.LocalizationKey,
                partName);
            return group;
        }

        private void UpdateBodyScaleAxisFoldout(
            UiButton toggle,
            VisualElement axisRows,
            string key,
            string partName)
        {
            var expanded = _expandedBodyScaleAxes.Contains(key);
            toggle?.SetIcon(AssetManagerControls.LoadFluentIconState(
                expanded
                    ? "chevron_down.png"
                    : "chevron_right.png",
                UiSizeTokens.Size12));
            if (toggle != null)
            {
                toggle.tooltip = I18N.Get(
                    expanded
                        ? "workflow.appearance.sizeAxisCollapse"
                        : "workflow.appearance.sizeAxisExpand",
                    partName);
            }
            axisRows?.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !expanded);
        }

        private VisualElement BuildScalePercentControl(
            string label,
            string tooltip,
            float initialPercent,
            bool isChild,
            Action<float> changed,
            out Action<float> render,
            VisualElement leading = null,
            Action labelClicked = null)
        {
            var row = CreateSizeControlRow(
                label,
                out var controls,
                isChild,
                leading,
                labelClicked);
            var slider = new Slider(
                MinimumBodyScale * 100f,
                MaximumBodyScale * 100f);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = tooltip;
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            controls.Add(CreateSizeUnit("%"));
            var reset = CreateSizeResetButton(() => slider.value = 100f);
            controls.Add(reset);

            var rendering = false;
            var renderedPercent = Mathf.Round(initialPercent);
            void RenderScalePercent(float percent)
            {
                var normalized = Mathf.Round(percent);
                var sliderValue = Mathf.Clamp(
                    normalized,
                    MinimumBodyScale * 100f,
                    MaximumBodyScale * 100f);
                rendering = true;
                slider.SetValueWithoutNotify(sliderValue);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                renderedPercent = normalized;
                reset.SetEnabled(!Mathf.Approximately(normalized, 100f));
            }

            void SetScalePercent(float percent)
            {
                var normalized = Mathf.Round(percent);
                var changedValue = !Mathf.Approximately(
                    normalized,
                    renderedPercent);
                RenderScalePercent(normalized);
                if (changedValue)
                {
                    changed?.Invoke(normalized);
                }
            }

            RegisterBodySizeSliderDrag(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetScalePercent(evt.newValue);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetScalePercent(evt.newValue);
                }
            });
            render = RenderScalePercent;
            RenderScalePercent(initialPercent);
            return row;
        }

        private VisualElement BuildGroupedBodyBlendShapeControl(
            IReadOnlyList<BodyBlendShapeDefinition> definitions)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var first = definitions[0];
            var key = first.Category + "|" + first.ShapeName;
            var children = new VisualElement();
            children.AddToClassList(
                "ee4v-modification-workflow__size-axis-rows");
            UiButton toggle = null;
            void UpdateFoldout()
            {
                var expanded = _expandedBodyBlendShapeGroups.Contains(key);
                toggle.SetIcon(AssetManagerControls.LoadFluentIconState(
                    expanded ? "chevron_down.png" : "chevron_right.png",
                    UiSizeTokens.Size12));
                toggle.tooltip = I18N.Get(expanded
                    ? "workflow.appearance.bodyShapeCollapse"
                    : "workflow.appearance.bodyShapeExpand", first.DisplayName);
                children.EnableInClassList(
                    "ee4v-modification-workflow__hidden", !expanded);
            }
            void ToggleChildren()
            {
                if (!_expandedBodyBlendShapeGroups.Add(key))
                {
                    _expandedBodyBlendShapeGroups.Remove(key);
                }
                UpdateFoldout();
            }
            toggle = AssetManagerControls.CreateIconButton(
                string.Empty,
                "chevron_right.png",
                UiSizeTokens.Size12,
                UiButtonVariant.Ghost,
                ToggleChildren,
                "ee4v-modification-workflow__size-axis-toggle");

            var source = ResolveBodyBlendShapeGroupSource(definitions);
            var sourceValue = source == null
                ? first.Value
                : GetEffectiveBodyBlendShapeWeight(
                    source, first.ShapeName);
            var sourceDefinition = definitions.FirstOrDefault(definition =>
                IsBodyBlendShapeGroupSource(definition, source));
            var parentDefinition = new BodyBlendShapeDefinition
            {
                DisplayName = first.DisplayName,
                Value = sourceValue,
                BaseValue = sourceDefinition?.BaseValue ?? first.BaseValue
            };
            var childRenderers = new List<Action<float>>();
            var parent = BuildBodyBlendShapeControl(
                parentDefinition,
                weight =>
                {
                    ApplyBodyBlendShapeGroup(definitions, weight);
                    foreach (var render in childRenderers)
                    {
                        render(weight);
                    }
                },
                out var renderParent,
                leading: toggle,
                labelClicked: ToggleChildren);
            group.Add(parent);
            foreach (var definition in definitions
                         .OrderBy(definition =>
                             IsBodyBlendShapeGroupSource(
                                 definition, source) ? 0 : 1))
            {
                var child = BuildBodyBlendShapeControl(
                    definition,
                    weight =>
                    {
                        ApplyIndividualBodyBlendShape(
                            definitions, definition, weight);
                        if (IsBodyBlendShapeGroupSource(
                                definition, source))
                        {
                            renderParent(weight);
                        }
                    },
                    out var renderChild,
                    isChild: true,
                    showRenderer: true);
                child.AddToClassList(
                    "ee4v-modification-workflow__size-control--blendshape-child");
                children.Add(child);
                childRenderers.Add(renderChild);
            }
            group.Add(children);
            UpdateFoldout();
            return group;
        }

        private VisualElement BuildBodyBlendShapeControl(
            BodyBlendShapeDefinition definition,
            Action<float> changed,
            out Action<float> render,
            bool isChild = false,
            VisualElement leading = null,
            Action labelClicked = null,
            bool showRenderer = false)
        {
            var label = definition.DisplayName;
            var tooltip = showRenderer
                ? $"{definition.RendererDisplayPath}\n{definition.ShapeName}"
                : label;
            var row = CreateSizeControlRow(
                label,
                out var controls,
                isChild: isChild,
                leading: leading,
                labelClicked: labelClicked,
                tooltip: tooltip,
                detail: showRenderer
                    ? definition.RendererName
                    : null);
            var slider = new Slider(
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = I18N.Get(
                "workflow.appearance.bodyShapeSliderTooltip",
                label) + (showRenderer
                    ? $"\n{definition.RendererDisplayPath}\n{definition.ShapeName}"
                    : string.Empty);
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            var reset = CreateSizeResetButton(() =>
                slider.value = definition.BaseValue);
            controls.Add(reset);

            var rendering = false;
            var appliedWeight = Mathf.Clamp(
                Mathf.Round(definition.Value),
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
            void RenderWeight(float weight)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(weight),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
                rendering = true;
                slider.SetValueWithoutNotify(normalized);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                reset.SetEnabled(!Mathf.Approximately(
                    normalized,
                    definition.BaseValue));
                appliedWeight = normalized;
            }
            void SetWeight(float weight)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(weight),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
                if (!Mathf.Approximately(normalized, appliedWeight))
                {
                    RenderWeight(normalized);
                    changed?.Invoke(normalized);
                }
            }

            RegisterBodySizeSliderDrag(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetWeight(evt.newValue);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetWeight(evt.newValue);
                }
            });
            render = RenderWeight;
            RenderWeight(definition.Value);
            return row;
        }

        private VisualElement CreateSizeControlRow(
            string label,
            out VisualElement controls,
            bool isChild = false,
            VisualElement leading = null,
            Action labelClicked = null,
            string tooltip = null,
            string detail = null)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-modification-workflow__size-control");
            row.EnableInClassList(
                "ee4v-modification-workflow__size-control--child",
                isChild);
            if (leading != null)
            {
                row.Add(leading);
            }
            else if (isChild)
            {
                var indent = new VisualElement();
                indent.AddToClassList(
                    "ee4v-modification-workflow__size-axis-indent");
                row.Add(indent);
            }
            var name = UiTextFactory.Create(
                label,
                "ee4v-modification-workflow__size-control-name");
            name.tooltip = tooltip ?? label ?? string.Empty;
            if (labelClicked != null)
            {
                name.RegisterCallback<ClickEvent>(evt =>
                {
                    labelClicked();
                    evt.StopPropagation();
                });
            }
            if (detail == null)
            {
                row.Add(name);
            }
            else
            {
                row.AddToClassList(
                    "ee4v-modification-workflow__size-control--detailed");
                var labels = new VisualElement();
                labels.AddToClassList(
                    "ee4v-modification-workflow__size-control-labels");
                labels.Add(name);
                var detailLabel = UiTextFactory.Create(
                    detail,
                    UiClassNames.SecondaryText,
                    "ee4v-modification-workflow__size-control-detail");
                detailLabel.tooltip = tooltip ?? detail;
                labels.Add(detailLabel);
                row.Add(labels);
            }
            controls = new VisualElement();
            controls.AddToClassList(
                "ee4v-modification-workflow__size-control-row");
            row.Add(controls);
            return row;
        }

        private UiTextElement CreateSizeUnit(string unit)
        {
            var label = UiTextFactory.Create(
                unit,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__size-unit");
            label.SetTextAlign(TextAnchor.MiddleLeft);
            return label;
        }

        private UiButton CreateSizeResetButton(Action reset)
        {
            var button = new UiButton(
                I18N.Get("workflow.appearance.sizeReset"),
                reset,
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__size-reset");
            return button;
        }

        private IReadOnlyList<BodyBlendShapeDefinition>
            GetBodyBlendShapes()
        {
            if (_workingObject == null)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            var renderers = _workingObject
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer =>
                    renderer != null &&
                    IsInSelectedPrefabScope(renderer.transform) &&
                    renderer.sharedMesh != null &&
                    renderer.sharedMesh.blendShapeCount > 0)
                .ToArray();
            if (renderers.Length == 0)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            FaceExpressionSettings.EnsureNamePresets(
                _workingObject,
                renderers.Select(renderer => renderer.sharedMesh),
                null,
                BlendShapePresetStorage.Shared);
            var result = new List<BodyBlendShapeDefinition>();
            var separators = FaceExpressionSettings.GetSeparators();
            var namingRule = FaceExpressionSettings.GetNameRule(
                BlendShapePresetStorage.Shared);
            foreach (var renderer in renderers)
            {
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _workingObject.transform);
                var meshAssetPath = AssetDatabase.GetAssetPath(
                    renderer.sharedMesh);
                var sourceAssetGuid = string.Empty;
                var sourceMeshLocalId = 0L;
                if (string.Equals(
                        System.IO.Path.GetExtension(meshAssetPath),
                        ".fbx",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        renderer.sharedMesh,
                        out sourceAssetGuid,
                        out sourceMeshLocalId);
                }
                for (var shapeIndex = 0;
                     shapeIndex < renderer.sharedMesh.blendShapeCount;
                     shapeIndex++)
                {
                    var shapeName = renderer.sharedMesh
                        .GetBlendShapeName(shapeIndex);
                    if (FaceExpressionClipEditor.TryGetHeader(
                            shapeName,
                            separators,
                            out _))
                    {
                        continue;
                    }
                    namingRule.TryGetMapping(
                        sourceAssetGuid,
                        sourceMeshLocalId,
                        shapeName,
                        out var mapping);
                    if (string.Equals(
                            mapping?.appearancePart,
                            BlendShapeAppearancePart.Expression,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!TryGetPresetBodyPart(
                            mapping?.appearancePart,
                            out var category))
                    {
                        if (IsBodyMesh(renderer))
                        {
                            continue;
                        }
                        category = ClassifyBodyPart(
                            shapeName,
                            renderer.name);
                    }
                    result.Add(new BodyBlendShapeDefinition
                    {
                        RendererPath = rendererPath,
                        RendererDisplayPath = string.IsNullOrEmpty(rendererPath)
                            ? renderer.name
                            : rendererPath,
                        RendererName = renderer.name,
                        ShapeName = shapeName,
                        DisplayName = GetBodyBlendShapeDisplayName(shapeName),
                        Category = category,
                        Group = string.IsNullOrWhiteSpace(
                            mapping?.appearanceGroup)
                            ? mapping?.role?.Trim() ?? string.Empty
                            : mapping.appearanceGroup.Trim(),
                        Value = GetEffectiveBodyBlendShapeWeight(
                            renderer, shapeName),
                        BaseValue = GetBaseBlendShapeWeight(
                            renderer,
                            shapeName)
                    });
                }
            }
            AddSyncedBodyBlendShapes(result);
            return result;
        }

        private void AddSyncedBodyBlendShapes(
            List<BodyBlendShapeDefinition> definitions)
        {
            var synced = definitions
                .Where(definition => ResolveBodyBlendShapeSource(
                    GetBodyBlendShapeRenderer(definition),
                    definition.ShapeName) != null)
                .GroupBy(definition => definition.ShapeName,
                    StringComparer.Ordinal)
                .ToArray();
            foreach (var group in synced)
            {
                var representative = group.First();
                foreach (var renderer in _workingObject
                             .GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer?.sharedMesh == null ||
                        renderer.sharedMesh.GetBlendShapeIndex(group.Key) < 0)
                    {
                        continue;
                    }
                    var path = AnimationUtility.CalculateTransformPath(
                        renderer.transform, _workingObject.transform);
                    if (definitions.Any(definition =>
                            definition.RendererPath == path &&
                            definition.ShapeName == group.Key))
                    {
                        continue;
                    }
                    definitions.Add(new BodyBlendShapeDefinition
                    {
                        RendererPath = path,
                        RendererDisplayPath = path,
                        RendererName = renderer.name,
                        ShapeName = group.Key,
                        DisplayName = representative.DisplayName,
                        Category = representative.Category,
                        Group = representative.Group,
                        Value = GetEffectiveBodyBlendShapeWeight(
                            renderer, group.Key),
                        BaseValue = GetBaseBlendShapeWeight(renderer, group.Key)
                    });
                }
                var source = ResolveBodyBlendShapeGroupSource(group.ToArray());
                var sourceDefinition = definitions.FirstOrDefault(definition =>
                    definition.ShapeName == group.Key &&
                    GetBodyBlendShapeRenderer(definition) == source);
                var category = sourceDefinition == null
                    ? representative.Category
                    : sourceDefinition.Category;
                foreach (var definition in definitions.Where(definition =>
                             definition.ShapeName == group.Key))
                {
                    definition.Category = category;
                    definition.Group = representative.Group;
                }
            }
        }

        private SkinnedMeshRenderer GetBodyBlendShapeRenderer(
            BodyBlendShapeDefinition definition)
        {
            if (_workingObject == null || definition == null)
            {
                return null;
            }
            var target = string.IsNullOrEmpty(definition.RendererPath)
                ? _workingObject.transform
                : _workingObject.transform.Find(definition.RendererPath);
            return target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
        }

        private SkinnedMeshRenderer ResolveBodyBlendShapeGroupSource(
            IReadOnlyList<BodyBlendShapeDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                var source = ResolveBodyBlendShapeSource(
                    GetBodyBlendShapeRenderer(definition),
                    definition.ShapeName);
                if (source != null)
                {
                    return source;
                }
            }
            return definitions.Count == 0
                ? null
                : GetBodyBlendShapeRenderer(definitions[0]);
        }

        private bool IsBodyBlendShapeGroupSource(
            BodyBlendShapeDefinition definition,
            SkinnedMeshRenderer source)
        {
            return source != null &&
                   GetBodyBlendShapeRenderer(definition) == source;
        }

        private SkinnedMeshRenderer ResolveBodyBlendShapeSource(
            SkinnedMeshRenderer target,
            string localShapeName)
        {
            if (target == null || _workingObject == null)
            {
                return null;
            }
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync?.Bindings == null)
            {
                return null;
            }
            foreach (var binding in sync.Bindings)
            {
                var localName = string.IsNullOrWhiteSpace(
                    binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;
                if (localName != localShapeName ||
                    binding.ReferenceMesh == null)
                {
                    continue;
                }
                var path = binding.ReferenceMesh.referencePath;
                GameObject sourceObject = null;
                if (!string.IsNullOrEmpty(path))
                {
                    var sourceTransform = path ==
                        AvatarObjectReference.AVATAR_ROOT
                        ? _workingObject.transform
                        : _workingObject.transform.Find(path);
                    sourceObject = sourceTransform == null
                        ? null
                        : sourceTransform.gameObject;
                }
                if (sourceObject == null)
                {
                    sourceObject = binding.ReferenceMesh.Get(sync);
                }
                var source = sourceObject == null
                    ? null
                    : sourceObject.GetComponent<SkinnedMeshRenderer>();
                if (source?.sharedMesh != null &&
                    source.sharedMesh.GetBlendShapeIndex(
                        binding.Blendshape) >= 0)
                {
                    return source;
                }
            }
            return null;
        }

        private static bool HasBodyBlendShapeSyncBinding(
            SkinnedMeshRenderer target,
            string shapeName)
        {
            var sync = target == null
                ? null
                : target.GetComponent<ModularAvatarBlendshapeSync>();
            return sync?.Bindings != null && sync.Bindings.Any(binding =>
                (string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape) == shapeName);
        }

        private float GetEffectiveBodyBlendShapeWeight(
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            var index = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0)
            {
                return 0f;
            }
            var sync = renderer.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync?.Bindings != null)
            {
                foreach (var binding in sync.Bindings)
                {
                    var localName = string.IsNullOrWhiteSpace(
                        binding.LocalBlendshape)
                        ? binding.Blendshape
                        : binding.LocalBlendshape;
                    if (localName != shapeName)
                    {
                        continue;
                    }
                    var source = ResolveBodyBlendShapeSource(
                        renderer, shapeName);
                    var sourceIndex = source?.sharedMesh == null
                        ? -1
                        : source.sharedMesh.GetBlendShapeIndex(
                            binding.Blendshape);
                    if (sourceIndex >= 0)
                    {
                        var weight = source.GetBlendShapeWeight(sourceIndex);
                        return Mathf.Clamp(
                            binding.RemapCurveIsValid
                                ? EvaluateBodyBlendShapeRemap(
                                    binding.RemapCurve, weight)
                                : weight,
                            MinimumBodyBlendShapeWeight,
                            MaximumBodyBlendShapeWeight);
                    }
                }
            }
            return Mathf.Clamp(renderer.GetBlendShapeWeight(index),
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
        }

        private static float EvaluateBodyBlendShapeRemap(
            AnimationCurve curve,
            float weight)
        {
            if (curve == null || curve.length < 2)
            {
                return weight;
            }
            var keys = curve.keys;
            for (var index = 1; index < keys.Length; index++)
            {
                if (weight > keys[index].time && index < keys.Length - 1)
                {
                    continue;
                }
                var previous = keys[index - 1];
                var next = keys[index];
                var duration = next.time - previous.time;
                return Mathf.Approximately(duration, 0f)
                    ? next.value
                    : Mathf.LerpUnclamped(previous.value, next.value,
                        (weight - previous.time) / duration);
            }
            return keys[keys.Length - 1].value;
        }

        private static bool TryGetPresetBodyPart(
            string part,
            out BodyPartCategory category)
        {
            switch (part)
            {
                case BlendShapeAppearancePart.Head:
                    category = BodyPartCategory.Head;
                    break;
                case BlendShapeAppearancePart.Chest:
                    category = BodyPartCategory.Chest;
                    break;
                case BlendShapeAppearancePart.Waist:
                    category = BodyPartCategory.Waist;
                    break;
                case BlendShapeAppearancePart.Shoulders:
                    category = BodyPartCategory.Shoulders;
                    break;
                case BlendShapeAppearancePart.Arms:
                    category = BodyPartCategory.Arms;
                    break;
                case BlendShapeAppearancePart.Hands:
                    category = BodyPartCategory.Hands;
                    break;
                case BlendShapeAppearancePart.Legs:
                    category = BodyPartCategory.Legs;
                    break;
                case BlendShapeAppearancePart.Feet:
                    category = BodyPartCategory.Feet;
                    break;
                case BlendShapeAppearancePart.Other:
                    category = BodyPartCategory.Other;
                    break;
                default:
                    category = BodyPartCategory.Other;
                    return false;
            }

            return true;
        }

        private static string GetBodyBlendShapeDisplayName(string shapeName)
        {
            var name = (shapeName ?? string.Empty).Trim();
            var separator = name.IndexOf('/');
            if (separator < 0 || separator >= name.Length - 1)
            {
                return name;
            }

            var role = name.Substring(separator + 1).Trim();
            return role.Length > 0 ? role : name;
        }

        private static bool IsBodyMesh(SkinnedMeshRenderer renderer)
        {
            return renderer != null &&
                   (string.Equals(
                        NormalizeBlendShapeName(renderer.name),
                        "body",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        NormalizeBlendShapeName(renderer.sharedMesh?.name),
                        "body",
                        StringComparison.Ordinal));
        }

        private static BodyPartCategory ClassifyBodyPart(
            string shapeName,
            string rendererName)
        {
            var normalized = NormalizeBlendShapeName(shapeName) +
                             NormalizeBlendShapeName(rendererName);
            if (ContainsBlendShapeTerm(normalized, HeadBlendShapeTerms))
            {
                return BodyPartCategory.Head;
            }
            if (ContainsBlendShapeTerm(normalized, ChestBlendShapeTerms))
            {
                return BodyPartCategory.Chest;
            }
            if (ContainsBlendShapeTerm(normalized, WaistBlendShapeTerms))
            {
                return BodyPartCategory.Waist;
            }
            if (ContainsBlendShapeTerm(
                    normalized,
                    ShoulderBlendShapeTerms))
            {
                return BodyPartCategory.Shoulders;
            }
            if (ContainsBlendShapeTerm(normalized, HandBlendShapeTerms))
            {
                return BodyPartCategory.Hands;
            }
            if (ContainsBlendShapeTerm(normalized, ArmBlendShapeTerms))
            {
                return BodyPartCategory.Arms;
            }
            if (ContainsBlendShapeTerm(normalized, FootBlendShapeTerms))
            {
                return BodyPartCategory.Feet;
            }
            if (ContainsBlendShapeTerm(normalized, LegBlendShapeTerms))
            {
                return BodyPartCategory.Legs;
            }
            return BodyPartCategory.Other;
        }

        private static bool ContainsBlendShapeTerm(
            string normalized,
            IEnumerable<string> terms)
        {
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return terms.Any(term => normalized.IndexOf(
                NormalizeBlendShapeName(term),
                StringComparison.Ordinal) >= 0);
        }

        private static string GetBodyPartCategoryLocalizationKey(
            BodyPartCategory category)
        {
            switch (category)
            {
                case BodyPartCategory.Head:
                    return "workflow.appearance.bodyShapePart.head";
                case BodyPartCategory.Chest:
                    return "workflow.appearance.bodyShapePart.chest";
                case BodyPartCategory.Waist:
                    return "workflow.appearance.bodyShapePart.waist";
                case BodyPartCategory.Shoulders:
                    return "workflow.appearance.bodyShapePart.shoulders";
                case BodyPartCategory.Arms:
                    return "workflow.appearance.bodyShapePart.arms";
                case BodyPartCategory.Hands:
                    return "workflow.appearance.bodyShapePart.hands";
                case BodyPartCategory.Legs:
                    return "workflow.appearance.bodyShapePart.legs";
                case BodyPartCategory.Feet:
                    return "workflow.appearance.bodyShapePart.feet";
                default:
                    return "workflow.appearance.bodyShapePart.other";
            }
        }

        private static string NormalizeBlendShapeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value
                .Trim()
                .ToLowerInvariant()
                .Where(character =>
                    !char.IsWhiteSpace(character) &&
                    character != '_' &&
                    character != '-' &&
                    character != '.')
                .ToArray());
        }

        private static float GetBaseBlendShapeWeight(
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(renderer) as
                SkinnedMeshRenderer;
            var index = source?.sharedMesh == null
                ? -1
                : source.sharedMesh.GetBlendShapeIndex(shapeName);
            return index < 0
                ? MinimumBodyBlendShapeWeight
                : Mathf.Clamp(
                    source.GetBlendShapeWeight(index),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
        }

        private static bool TryGetAvatarViewPosition(
            GameObject avatar,
            out Component descriptor,
            out Vector3 viewPosition)
        {
            descriptor = avatar == null
                ? null
                : avatar.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(component =>
                        component != null &&
                        string.Equals(
                            component.GetType().FullName,
                            AvatarDescriptorTypeName,
                            StringComparison.Ordinal));
            return TryReadAvatarViewPosition(descriptor, out viewPosition) &&
                   viewPosition.y > 0.01f;
        }

        private bool TryGetWorkingAvatarViewPosition(
            out Component descriptor,
            out Vector3 viewPosition)
        {
            if (_avatarDescriptor != null &&
                TryReadAvatarViewPosition(
                    _avatarDescriptor,
                    out viewPosition) &&
                viewPosition.y > 0.01f)
            {
                descriptor = _avatarDescriptor;
                return true;
            }

            var found = TryGetAvatarViewPosition(
                _workingObject,
                out descriptor,
                out viewPosition);
            _avatarDescriptor = found ? descriptor : null;
            return found;
        }

        private static bool TryReadAvatarViewPosition(
            Component descriptor,
            out Vector3 viewPosition)
        {
            viewPosition = Vector3.zero;
            if (descriptor == null)
            {
                return false;
            }

            var serialized = new SerializedObject(descriptor);
            var property = serialized.FindProperty("ViewPosition");
            if (property == null ||
                property.propertyType != SerializedPropertyType.Vector3)
            {
                return false;
            }

            viewPosition = property.vector3Value;
            return true;
        }

        private Vector3 GetBaseAvatarViewPosition(
            Component descriptor,
            Vector3 currentViewPosition)
        {
            if (_baseAvatarViewPosition.HasValue)
            {
                return _baseAvatarViewPosition.Value;
            }

            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(descriptor) as Component;
            if (!TryReadAvatarViewPosition(source, out var baseViewPosition) ||
                baseViewPosition.y <= 0.01f)
            {
                baseViewPosition = currentViewPosition;
            }
            _baseAvatarViewPosition = baseViewPosition;
            return baseViewPosition;
        }

        private Animator FindHumanoidAnimator()
        {
            if (_workingObject == null)
            {
                return null;
            }

            return _workingObject
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(animator =>
                    animator != null &&
                    IsInSelectedPrefabScope(animator.transform) &&
                    animator.avatar != null &&
                    animator.avatar.isHuman &&
                    animator.isHuman);
        }

        private IReadOnlyList<Transform> ResolveBodyScaleTargets(
            BodyScaleDefinition definition,
            Animator animator)
        {
            if (definition.UsesAvatarRoot)
            {
                return _workingObject != null &&
                       (!_selectedPrefabSiblingIndex.HasValue ||
                        _selectedPrefabSiblingIndex == -1)
                    ? new[] { _workingObject.transform }
                    : Array.Empty<Transform>();
            }
            if (animator == null)
            {
                return Array.Empty<Transform>();
            }

            var targets = new List<Transform>();
            foreach (var bone in definition.Bones)
            {
                var target = animator.GetBoneTransform(bone);
                if (target == null ||
                    !IsInSelectedPrefabScope(target))
                {
                    continue;
                }

                targets.Add(target);
                if (definition.UsesFirstAvailableBone)
                {
                    break;
                }
            }
            return targets;
        }

        private static Vector3 GetBodyScaleMultipliers(
            IReadOnlyList<Transform> targets)
        {
            var sum = Vector3.zero;
            var count = 0;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                var baseScale = GetBaseLocalScale(target);
                sum += new Vector3(
                    GetScaleRatio(target.localScale.x, baseScale.x),
                    GetScaleRatio(target.localScale.y, baseScale.y),
                    GetScaleRatio(target.localScale.z, baseScale.z));
                count++;
            }

            return count == 0
                ? Vector3.one
                : RoundBodyScaleMultipliers(sum / count);
        }

        private static float GetScaleRatio(float value, float baseValue)
        {
            return Mathf.Abs(baseValue) < 0.0001f
                ? 1f
                : value / baseValue;
        }

        private static Vector3 RoundBodyScaleMultipliers(Vector3 value)
        {
            return new Vector3(
                Mathf.Round(value.x * 100f) / 100f,
                Mathf.Round(value.y * 100f) / 100f,
                Mathf.Round(value.z * 100f) / 100f);
        }

        private static float AverageVectorComponent(Vector3 value)
        {
            return (value.x + value.y + value.z) / 3f;
        }

        private static float GetVectorComponent(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static Vector3 SetVectorComponent(
            Vector3 value,
            int axis,
            float component)
        {
            if (axis == 0)
            {
                value.x = component;
            }
            else if (axis == 1)
            {
                value.y = component;
            }
            else
            {
                value.z = component;
            }
            return value;
        }

        private static string GetScaleAxisName(int axis)
        {
            return I18N.Get(
                axis == 0
                    ? "workflow.appearance.sizeAxis.x"
                    : axis == 1
                        ? "workflow.appearance.sizeAxis.y"
                        : "workflow.appearance.sizeAxis.z");
        }

        private static Vector3 GetBaseLocalScale(Transform target)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(target) as Transform;
            return source != null ? source.localScale : Vector3.one;
        }

        private Vector3 GetCachedBaseLocalScale(
            string path,
            Transform target)
        {
            var key = path ?? string.Empty;
            if (_bodyScaleBaseScales.TryGetValue(key, out var scale))
            {
                return scale;
            }

            scale = GetBaseLocalScale(target);
            _bodyScaleBaseScales[key] = scale;
            return scale;
        }

        private void ApplyBodyScale(
            IReadOnlyList<string> targetPaths,
            Vector3 multipliers,
            bool updateAvatarViewPosition = false,
            bool rebuildOnFailure = true)
        {
            if (_workingObject == null ||
                targetPaths == null ||
                targetPaths.Count == 0)
            {
                return;
            }

            var targets = _resolvedBodyScaleTargets;
            targets.Clear();
            foreach (var path in targetPaths)
            {
                var target = string.IsNullOrEmpty(path)
                    ? _workingObject.transform
                    : _workingObject.transform.Find(path);
                if (target != null)
                {
                    targets.Add(new KeyValuePair<string, Transform>(
                        path,
                        target));
                }
            }
            if (targets.Count == 0)
            {
                return;
            }

            var previewScales = _bodyScalePreviewScales;
            previewScales.Clear();
            foreach (var pair in targets)
            {
                previewScales[pair.Key] = Vector3.Scale(
                    GetCachedBaseLocalScale(pair.Key, pair.Value),
                    multipliers);
            }

            if (_bodyScaleDragging)
            {
                _scenePreview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: false);
                RequestRepaint();
                _pendingBodySizeChange = PendingBodySizeChange.Scale;
                _pendingBodyScaleTargetPaths = targetPaths;
                _pendingBodyScaleMultipliers = multipliers;
                _pendingBodyScaleUpdatesViewPosition =
                    updateAvatarViewPosition;
                return;
            }
            if (!IsEditableWorkflowPrefab())
            {
                return;
            }

            try
            {
                Component avatarDescriptor = null;
                var baseViewPosition = Vector3.zero;
                if (updateAvatarViewPosition &&
                    TryGetWorkingAvatarViewPosition(
                        out avatarDescriptor,
                        out var currentViewPosition))
                {
                    baseViewPosition = GetBaseAvatarViewPosition(
                        avatarDescriptor,
                        currentViewPosition);
                }
                var undoObjects = targets
                    .Select(pair => (UnityEngine.Object)pair.Value)
                    .ToList();
                if (avatarDescriptor != null)
                {
                    undoObjects.Add(avatarDescriptor);
                }
                Undo.RecordObjects(
                    undoObjects.Distinct().ToArray(),
                    I18N.Get("workflow.appearance.sizeUndo"));
                foreach (var pair in targets)
                {
                    var scale = previewScales[pair.Key];
                    pair.Value.localScale = scale;
                    if (PrefabUtility.IsPartOfPrefabInstance(pair.Value))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                pair.Value);
                    }
                    EditorUtility.SetDirty(pair.Value);
                }
                if (avatarDescriptor != null)
                {
                    var serialized = new SerializedObject(avatarDescriptor);
                    var viewPosition = serialized.FindProperty("ViewPosition");
                    if (viewPosition != null)
                    {
                        viewPosition.vector3Value = Vector3.Scale(
                            baseViewPosition,
                            multipliers);
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (PrefabUtility.IsPartOfPrefabInstance(
                            avatarDescriptor))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                avatarDescriptor);
                    }
                    EditorUtility.SetDirty(avatarDescriptor);
                }
                EditorUtility.SetDirty(_workingObject);
                _bodyScaleDirty = true;
                _scenePreview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: true);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ApplyBodyBlendShape(
            string rendererPath,
            string shapeName,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (_workingObject == null ||
                string.IsNullOrEmpty(shapeName))
            {
                return;
            }

            if (_bodyScaleDragging)
            {
                _scenePreview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: false);
                RequestRepaint();
                _pendingBodySizeChange =
                    PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeRendererPath = rendererPath;
                _pendingBodyBlendShapeName = shapeName;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!IsEditableWorkflowPrefab())
            {
                return;
            }

            var target = string.IsNullOrEmpty(rendererPath)
                ? _workingObject.transform
                : _workingObject.transform.Find(rendererPath);
            var renderer = target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
            var shapeIndex = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (renderer == null || shapeIndex < 0)
            {
                return;
            }

            try
            {
                Undo.RecordObject(
                    renderer,
                    I18N.Get("workflow.appearance.sizeUndo"));
                renderer.SetBlendShapeWeight(shapeIndex, weight);
                if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                        renderer);
                }
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(_workingObject);
                _bodyScaleDirty = true;
                _scenePreview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: false);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ApplyBodyBlendShapeGroup(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (definitions == null || definitions.Count == 0 ||
                _workingObject == null)
            {
                return;
            }
            if (_bodyScaleDragging)
            {
                PreviewBodyBlendShapeGroup(definitions, weight);
                _pendingBodySizeChange = PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeGroup = definitions;
                _pendingIndividualBlendShape = null;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!IsEditableWorkflowPrefab())
            {
                return;
            }

            try
            {
                var source = ResolveBodyBlendShapeGroupSource(definitions);
                var shapeName = definitions[0].ShapeName;
                var undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(
                    I18N.Get("workflow.appearance.sizeUndo"));
                var targets = new HashSet<SkinnedMeshRenderer>();
                foreach (var definition in definitions)
                {
                    var target = GetBodyBlendShapeRenderer(definition);
                    if (target?.sharedMesh == null)
                    {
                        continue;
                    }
                    var existingSource = ResolveBodyBlendShapeSource(
                        target, definition.ShapeName);
                    var bindingSource = existingSource ?? source;
                    if (bindingSource != null && target != bindingSource)
                    {
                        SetBodyBlendShapeSyncBinding(
                            target,
                            bindingSource,
                            definition.ShapeName,
                            null);
                        targets.Add(bindingSource);
                    }
                    targets.Add(target);
                }
                foreach (var target in targets)
                {
                    SetBodyBlendShapeRendererWeight(
                        target, shapeName, weight);
                }
                Undo.CollapseUndoOperations(undoGroup);
                _bodyScaleDirty = true;
                PreviewBodyBlendShapeGroup(definitions, weight);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void EnsureBodyBlendShapeSyncBindings(
            IReadOnlyList<BodyBlendShapeDefinition> definitions)
        {
            if (!IsEditableWorkflowPrefab())
            {
                return;
            }
            var changed = false;
            try
            {
                var undoGroup = -1;
                foreach (var group in definitions
                             .GroupBy(definition => new
                             {
                                 definition.Category,
                                 definition.ShapeName
                             })
                             .Where(group => group.Count() > 1))
                {
                    var members = group.ToArray();
                    var source = ResolveBodyBlendShapeGroupSource(members);
                    var sourceIndex = source?.sharedMesh == null
                        ? -1
                        : source.sharedMesh.GetBlendShapeIndex(
                            group.Key.ShapeName);
                    if (sourceIndex < 0)
                    {
                        continue;
                    }
                    var sourceWeight = source.GetBlendShapeWeight(sourceIndex);
                    foreach (var definition in members)
                    {
                        var target = GetBodyBlendShapeRenderer(definition);
                        if (target == null || target == source ||
                            HasBodyBlendShapeSyncBinding(
                                target, definition.ShapeName))
                        {
                            continue;
                        }
                        if (!changed)
                        {
                            Undo.IncrementCurrentGroup();
                            undoGroup = Undo.GetCurrentGroup();
                            Undo.SetCurrentGroupName(
                                I18N.Get("workflow.appearance.sizeUndo"));
                        }
                        SetBodyBlendShapeSyncBinding(
                            target, source, definition.ShapeName, null);
                        SetBodyBlendShapeRendererWeight(
                            target, definition.ShapeName, sourceWeight);
                        definition.Value = sourceWeight;
                        changed = true;
                    }
                }
                if (changed)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                    _bodyScaleDirty = true;
                    SaveBodyScalePrefab(false);
                }
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, false);
            }
        }

        private void ApplyIndividualBodyBlendShape(
            IReadOnlyList<BodyBlendShapeDefinition> group,
            BodyBlendShapeDefinition definition,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (_workingObject == null || definition == null)
            {
                return;
            }
            if (_bodyScaleDragging)
            {
                _scenePreview?.SetBlendShapeWeight(
                    definition.RendererPath,
                    definition.ShapeName,
                    weight,
                    recalculateBounds: false);
                RequestRepaint();
                _pendingBodySizeChange = PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeGroup = group;
                _pendingIndividualBlendShape = definition;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!IsEditableWorkflowPrefab())
            {
                return;
            }

            try
            {
                var target = GetBodyBlendShapeRenderer(definition);
                if (target?.sharedMesh == null)
                {
                    return;
                }
                var source = ResolveBodyBlendShapeSource(
                    target, definition.ShapeName) ??
                    ResolveBodyBlendShapeGroupSource(group);
                var editsSource = source == target;
                if (source != null && source != target)
                {
                    var sourceIndex = source.sharedMesh.GetBlendShapeIndex(
                        definition.ShapeName);
                    if (sourceIndex >= 0)
                    {
                        SetBodyBlendShapeSyncBinding(
                            target,
                            source,
                            definition.ShapeName,
                            BuildIndividualBodyBlendShapeRemap(
                                source.GetBlendShapeWeight(sourceIndex),
                                weight));
                    }
                }
                SetBodyBlendShapeRendererWeight(
                    target, definition.ShapeName, weight);
                _bodyScaleDirty = true;
                _scenePreview?.SetBlendShapeWeight(
                    definition.RendererPath,
                    definition.ShapeName,
                    weight,
                    recalculateBounds: false);
                SaveBodyScalePrefab(rebuildOnFailure);
                if (editsSource && this.panel != null)
                {
                    this.schedule.Execute(() =>
                        ShowCategory(WorkflowCategory.ShapeParts, false));
                }
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void PreviewBodyBlendShapeGroup(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            float weight)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                paths.Add(definition.RendererPath);
            }
            var source = ResolveBodyBlendShapeGroupSource(definitions);
            if (source != null)
            {
                paths.Add(AnimationUtility.CalculateTransformPath(
                    source.transform, _workingObject.transform));
            }
            foreach (var path in paths)
            {
                _scenePreview?.SetBlendShapeWeight(
                    path,
                    definitions[0].ShapeName,
                    weight,
                    recalculateBounds: false);
            }
            RequestRepaint();
        }

        private void SetBodyBlendShapeRendererWeight(
            SkinnedMeshRenderer renderer,
            string shapeName,
            float weight)
        {
            var index = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0 || Mathf.Approximately(
                    renderer.GetBlendShapeWeight(index), weight))
            {
                return;
            }
            Undo.RecordObject(renderer,
                I18N.Get("workflow.appearance.sizeUndo"));
            renderer.SetBlendShapeWeight(index, weight);
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    renderer);
            }
            EditorUtility.SetDirty(renderer);
        }

        private void SetBodyBlendShapeSyncBinding(
            SkinnedMeshRenderer target,
            SkinnedMeshRenderer source,
            string shapeName,
            AnimationCurve remap)
        {
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync == null)
            {
                sync = Undo.AddComponent<ModularAvatarBlendshapeSync>(
                    target.gameObject);
            }
            Undo.RecordObject(sync,
                I18N.Get("workflow.appearance.sizeUndo"));
            if (sync.Bindings == null)
            {
                sync.Bindings = new List<BlendshapeBinding>();
            }
            var index = sync.Bindings.FindIndex(binding =>
                (string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape) == shapeName);
            var bindingValue = index >= 0
                ? sync.Bindings[index]
                : new BlendshapeBinding
                {
                    ReferenceMesh = new AvatarObjectReference
                    {
                        referencePath = AnimationUtility.CalculateTransformPath(
                            source.transform, _workingObject.transform)
                    },
                    Blendshape = shapeName,
                    LocalBlendshape = string.Empty
                };
            bindingValue.RemapCurveIsValid = true;
            bindingValue.RemapCurve = remap ??
                AnimationCurve.Linear(0f, 0f, 100f, 100f);
            if (index >= 0)
            {
                sync.Bindings[index] = bindingValue;
            }
            else
            {
                sync.Bindings.Add(bindingValue);
            }
            if (PrefabUtility.IsPartOfPrefabInstance(sync))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(sync);
            }
            EditorUtility.SetDirty(sync);
        }

        private static AnimationCurve BuildIndividualBodyBlendShapeRemap(
            float sourceWeight,
            float targetWeight)
        {
            var source = Mathf.Clamp(sourceWeight, 0f, 100f);
            var target = Mathf.Clamp(targetWeight, 0f, 100f);
            if (source <= 0f)
            {
                return AnimationCurve.Linear(0f, target, 100f, 100f);
            }
            if (source >= 100f)
            {
                return AnimationCurve.Linear(0f, 0f, 100f, target);
            }
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(source, target),
                new Keyframe(100f, 100f));
        }

        private void RegisterBodySizeSliderDrag(Slider slider)
        {
            var dragContainer = slider.Q<VisualElement>(
                className: "unity-base-slider__drag-container") ?? slider;
            dragContainer.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    BeginBodyScaleDrag();
                }
            }, TrickleDown.TrickleDown);
            dragContainer.RegisterCallback<PointerUpEvent>(_ =>
                EndBodyScaleDrag());
            dragContainer.RegisterCallback<PointerCaptureOutEvent>(_ =>
                EndBodyScaleDrag());
        }

        private void BeginBodyScaleDrag()
        {
            if (_bodyScaleDragging)
            {
                return;
            }

            _bodyScaleDragging = true;
            ClearPendingBodySizeChange();
        }

        private void EndBodyScaleDrag(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDragging &&
                _pendingBodySizeChange == PendingBodySizeChange.None)
            {
                return;
            }

            var pendingChange = _pendingBodySizeChange;
            var targetPaths = _pendingBodyScaleTargetPaths;
            var multipliers = _pendingBodyScaleMultipliers;
            var updateAvatarViewPosition =
                _pendingBodyScaleUpdatesViewPosition;
            var rendererPath = _pendingBodyBlendShapeRendererPath;
            var shapeName = _pendingBodyBlendShapeName;
            var weight = _pendingBodyBlendShapeWeight;
            var blendShapeGroup = _pendingBodyBlendShapeGroup;
            var individualBlendShape = _pendingIndividualBlendShape;
            ClearPendingBodySizeChange();
            _bodyScaleDragging = false;
            if (pendingChange == PendingBodySizeChange.Scale)
            {
                ApplyBodyScale(
                    targetPaths,
                    multipliers,
                    updateAvatarViewPosition,
                    rebuildOnFailure);
            }
            else if (pendingChange == PendingBodySizeChange.BlendShape)
            {
                if (individualBlendShape != null)
                {
                    ApplyIndividualBodyBlendShape(
                        blendShapeGroup,
                        individualBlendShape,
                        weight,
                        rebuildOnFailure);
                }
                else if (blendShapeGroup != null)
                {
                    ApplyBodyBlendShapeGroup(
                        blendShapeGroup,
                        weight,
                        rebuildOnFailure);
                }
                else
                {
                    ApplyBodyBlendShape(
                        rendererPath,
                        shapeName,
                        weight,
                        rebuildOnFailure);
                }
            }
            _scenePreview?.FlushUpdates(
                recalculateBounds:
                pendingChange == PendingBodySizeChange.Scale);
            if (pendingChange == PendingBodySizeChange.None)
            {
                SaveBodyScalePrefab(rebuildOnFailure);
            }
        }

        private void ClearPendingBodySizeChange()
        {
            _pendingBodySizeChange = PendingBodySizeChange.None;
            _pendingBodyScaleTargetPaths = null;
            _pendingBodyScaleUpdatesViewPosition = false;
            _pendingBodyBlendShapeRendererPath = null;
            _pendingBodyBlendShapeName = null;
            _pendingBodyBlendShapeGroup = null;
            _pendingIndividualBlendShape = null;
        }

        private void SaveBodyScalePrefab(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDirty || !IsEditableWorkflowPrefab())
            {
                return;
            }

            try
            {
                var savedPrefab = PrefabUtility.SavePrefabAsset(
                    _workingObject,
                    out var savedSuccessfully);
                if (!savedSuccessfully || savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "The derived Prefab size could not be saved.");
                }

                _workingObject = savedPrefab;
                if (_workingAsset != null)
                {
                    _workingAsset.Prefab = savedPrefab;
                }
                _scenePreview?.SetPrefab(savedPrefab);
                _bodyScaleDirty = false;
                _feedback = string.Empty;
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ReportBodyScaleFailure(
            Exception exception,
            bool rebuildControls)
        {
            Debug.LogException(exception);
            _feedback = I18N.Get(
                "workflow.appearance.sizeSaveFailed");
            _feedbackType = HelpBoxMessageType.Error;
            if (rebuildControls && this.panel != null)
            {
                this.schedule.Execute(() =>
                    ShowCategory(WorkflowCategory.ShapeParts, false));
            }
        }

        private bool IsEditableWorkflowPrefab()
        {
            if (_workingObject == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(_workingObject);
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(
                       DerivedAssetCreator.VariantRoot + "/",
                       StringComparison.OrdinalIgnoreCase) &&
                   PrefabUtility.IsPartOfPrefabAsset(_workingObject);
        }

        private static string FormatBodyHeight(float value)
        {
            return value.ToString("0.00") + " m";
        }

        private VisualElement BuildShapePartsTabs()
        {
            var tabs = new VisualElement();
            tabs.AddToClassList(
                "ee4v-modification-workflow__shape-parts-tabs");
            AddShapePartsButton(
                tabs,
                ShapePartsSection.Parts,
                "workflow.shapeParts.parts");
            AddShapePartsButton(
                tabs,
                ShapePartsSection.Shape,
                "workflow.shapeParts.shape");
            return tabs;
        }

        private void AddShapePartsButton(
            VisualElement tabs,
            ShapePartsSection section,
            string labelKey)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () =>
                {
                    if (_shapePartsSection == section)
                    {
                        return;
                    }
                    if (_shapePartsSection == ShapePartsSection.Shape)
                    {
                        EndBodyScaleDrag();
                    }
                    _shapePartsSection = section;
                    ShowCategory(WorkflowCategory.ShapeParts, false);
                    _controlsHost.scrollOffset = Vector2.zero;
                },
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__shape-parts-tab");
            button.EnableInClassList(
                "ee4v-modification-workflow__shape-parts-tab--active",
                section == _shapePartsSection);
            tabs.Add(button);
        }

        private VisualElement BuildMaterialEditor(Material material)
        {
            var section = new VisualElement();
            section.AddToClassList(
                "ee4v-modification-workflow__material-editor-section");

            var header = new ItemRow(new ItemRowState(
                material.name,
                icon: CreateMaterialIcon(material)));
            header.AddToClassList(
                "ee4v-modification-workflow__material-editor-header");

            var surface = new VisualElement();
            surface.AddToClassList(
                "ee4v-modification-workflow__material-editor-surface");
            surface.Add(header);

            _materialInspector = new EmbeddedMaterialInspector(
                material,
                RefreshMaterialPreview);
            if (!_materialInspector.IsAvailable)
            {
                _materialInspector.Dispose();
                _materialInspector = null;
                surface.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.editorUnavailable"),
                    HelpBoxMessageType.Error));
                section.Add(surface);
                return section;
            }

            surface.Add(_materialInspector);
            section.Add(surface);
            return section;
        }

        private VisualElement BuildAllMaterialsVisibilityRow(
            IReadOnlyCollection<Material> materials)
        {
            var row = new ItemRow(new ItemRowState(
                I18N.Get("workflow.appearance.allMaterials")));
            row.AddToClassList(
                "ee4v-modification-workflow__material-visibility-all");
            _allMaterialsVisibilityButton = new UiButton(
                string.Empty,
                () => ToggleAllMaterialsVisibility(materials),
                variant: UiButtonVariant.Ghost);
            _allMaterialsVisibilityButton.AddToClassList(
                "ee4v-modification-workflow__material-visibility");
            row.Trailing.Add(_allMaterialsVisibilityButton);
            return row;
        }

        private void ToggleMaterialVisibility(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (!_hiddenMaterials.Add(material))
            {
                _hiddenMaterials.Remove(material);
            }
            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);
            RefreshMaterialVisibilityButtons(
                _materialVisibilityButtons.Keys.ToArray());
        }

        private void ToggleAllMaterialsVisibility(
            IReadOnlyCollection<Material> materials)
        {
            if (materials.All(material =>
                    _hiddenMaterials.Contains(material)))
            {
                _hiddenMaterials.ExceptWith(materials);
            }
            else
            {
                _hiddenMaterials.UnionWith(materials);
            }

            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);
            RefreshMaterialVisibilityButtons(materials);
        }

        private void RefreshMaterialVisibilityButtons(
            IReadOnlyCollection<Material> materials)
        {
            foreach (var pair in _materialVisibilityButtons)
            {
                var material = pair.Key;
                var button = pair.Value;
                var isVisible = !_hiddenMaterials.Contains(material);
                var tooltipKey = isVisible
                    ? "workflow.appearance.hideMaterial"
                    : "workflow.appearance.showMaterial";
                var tooltip = I18N.Get(tooltipKey);
                button.tooltip = tooltip;
                button.SetIcon(FluentUiIcons.CreateState(
                    isVisible
                        ? "eye.png"
                        : "eye_off.png",
                    UiSizeTokens.Size18,
                    tooltip));
            }

            var allVisible = materials.Count > 0 &&
                             !_hiddenMaterials.Overlaps(materials);
            var allTooltip = I18N.Get(allVisible
                ? "workflow.appearance.hideAll"
                : "workflow.appearance.showAll");
            _allMaterialsVisibilityButton?.SetIcon(
                FluentUiIcons.CreateState(
                    allVisible
                        ? "eye.png"
                        : "eye_off.png",
                    UiSizeTokens.Size18,
                    allTooltip));
            if (_allMaterialsVisibilityButton != null)
            {
                _allMaterialsVisibilityButton.tooltip = allTooltip;
            }
        }

        private void AddFeedback(VisualElement panel)
        {
            if (string.IsNullOrWhiteSpace(_feedback))
            {
                return;
            }
            panel.Add(UiTextFactory.CreateHelpBox(
                _feedback,
                _feedbackType,
                "ee4v-modification-workflow__feedback"));
        }

        private VisualElement CreateEmptyState(
            string titleKey,
            string descriptionKey)
        {
            var empty = new VisualElement();
            empty.AddToClassList("ee4v-ui-empty-state");
            empty.AddToClassList(
                "ee4v-modification-workflow__empty-state");
            empty.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-ui-empty-state__title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-ui-empty-state__description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            empty.Add(description);
            return empty;
        }

        private void StartDerivedAssetCreation()
        {
            _creatingDerivedAsset = true;
            _feedback = string.Empty;
            BuildWindow();
        }

        private void StartDerivedAssetCreation(string itemId)
        {
            _manager = _manager ?? AssetManagerWindowSession.GetManager();
            var item = _manager.GetItem(itemId);
            if (item == null || item.IsArchived)
            {
                return;
            }
            _creationItemId = item.Id;
            _creationPrefab = null;
            _derivedName = item.Name + " Variant";
            _derivedDescription = string.Empty;
            StartDerivedAssetCreation();
        }

        private void CancelDerivedAssetCreation()
        {
            _creatingDerivedAsset = false;
            _feedback = string.Empty;
            BuildWindow();
        }

        private IReadOnlyList<VariantSourceOption> GetVariantSources()
        {
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                return _manager.SearchItems(new AssetItemQuery())
                    .Items
                    .Where(item => item != null && !item.IsArchived)
                    .Select(item => new VariantSourceOption
                    {
                        Item = item,
                        Prefabs = DerivedAssetCreator.FindPrefabCandidates(
                            _manager.GetItemImportedAssetGuids(item.Id))
                    })
                    .Where(source => source.Prefabs.Count > 0)
                    .OrderBy(
                        source => source.Item.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Array.Empty<VariantSourceOption>();
            }
        }

        private void SelectVariantSource(VariantSourceOption source)
        {
            if (source?.Item == null || source.Prefabs.Count == 0)
            {
                return;
            }
            _creationItemId = source.Item.Id;
            _creationPrefab = source.Prefabs[0];
            _derivedName = source.Item.Name + " Variant";
            _derivedDescription = string.Empty;
            _feedback = string.Empty;
            BuildWindow();
        }

        private void CreateDerivedAsset()
        {
            if (!DerivedAssetCreator.IsValidName(_derivedName))
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetNameInvalid"),
                    HelpBoxMessageType.Error);
                return;
            }
            if (string.IsNullOrEmpty(_creationItemId) ||
                _creationPrefab == null)
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetPrefabInvalid"),
                    HelpBoxMessageType.Error);
                return;
            }
            if (AssetDatabase.IsValidFolder(
                    DerivedAssetCreator.GetVariantFolder(_derivedName)))
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetAlreadyExists"),
                    HelpBoxMessageType.Error);
                return;
            }

            try
            {
                var result = DerivedAssetCreator.Create(
                    new DerivedAssetCreationRequest
                    {
                        ParentItemId = _creationItemId,
                        Name = _derivedName,
                        Description = _derivedDescription,
                        Prefab = _creationPrefab
                    });
                _creatingDerivedAsset = false;
                SelectDerivedAsset(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetCreateFailed"),
                    HelpBoxMessageType.Error);
            }
        }

        private void SetCreationFeedback(
            string message,
            HelpBoxMessageType type)
        {
            _feedback = message ?? string.Empty;
            _feedbackType = type;
            BuildWindow();
        }

        internal void SelectDerivedAsset(DerivedAssetInfo asset)
        {
            if (asset?.Prefab == null)
            {
                return;
            }
            EndBodyScaleDrag();
            _bodyScaleDirty = false;
            _bodyScaleDragging = false;
            ClearPendingBodySizeChange();
            _bodyScaleBaseScales.Clear();
            _advancedBodyScaleExpanded = false;
            _expandedBodyScaleAxes.Clear();
            _expandedBodyBlendShapeGroups.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
            _workingAsset = asset;
            _workingObject = asset.Prefab;
            _basePrefabHidden = false;
            _hiddenPrefabSiblingIndices.Clear();
            _prefabPreviewVisibilityInitialized = false;
            _selectedPrefabSiblingIndex = null;
            _selectedPrefabName = string.Empty;
            _creatingDerivedAsset = false;
            _currentCategory = WorkflowCategory.ShapeParts;
            ApplyEditorMode();
            _shapePartsSection = ShapePartsSection.Parts;
            _selectedBodyPart = null;
            _selectedMaterial = null;
            _hiddenMaterials.Clear();
            _feedback = string.Empty;
            _assetFeedback = string.Empty;
            DerivedAssetChanged?.Invoke(asset);
            BuildWindow();
        }

        private void ClearDerivedAsset()
        {
            EndBodyScaleDrag();
            _bodyScaleDirty = false;
            _bodyScaleDragging = false;
            ClearPendingBodySizeChange();
            _bodyScaleBaseScales.Clear();
            _advancedBodyScaleExpanded = false;
            _expandedBodyScaleAxes.Clear();
            _expandedBodyBlendShapeGroups.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
            _workingAsset = null;
            _workingObject = null;
            _basePrefabHidden = false;
            _hiddenPrefabSiblingIndices.Clear();
            _prefabPreviewVisibilityInitialized = false;
            _selectedPrefabSiblingIndex = null;
            _selectedPrefabName = string.Empty;
            _selectedBodyPart = null;
            _selectedMaterial = null;
            _hiddenMaterials.Clear();
            _feedback = string.Empty;
            _assetFeedback = string.Empty;
            DerivedAssetChanged?.Invoke(null);
            BuildWindow();
        }

        private bool IsInSelectedPrefabScope(Transform target)
        {
            return IsInSelectedPrefabScope(
                target,
                _workingObject == null
                    ? null
                    : _workingObject.transform);
        }

        private bool IsInSelectedPrefabScope(
            Transform target,
            Transform root)
        {
            return AssetManagerPrefabUtility.IsInScope(
                target,
                root,
                _selectedPrefabSiblingIndex,
                _prefabSiblingIndices);
        }

        private IReadOnlyList<AvatarMaterialEntry> GetAvatarMaterials()
        {
            if (_workingObject == null)
            {
                return Array.Empty<AvatarMaterialEntry>();
            }

            var entries = new List<AvatarMaterialEntry>();
            var byMaterial = new Dictionary<Material, AvatarMaterialEntry>();
            var boneCategories = GetHumanoidMaterialBoneCategories(
                FindHumanoidAnimator());
            foreach (var renderer in _workingObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    !IsInSelectedPrefabScope(renderer.transform))
                {
                    continue;
                }
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _workingObject.transform);
                if (string.IsNullOrWhiteSpace(rendererPath))
                {
                    rendererPath = _workingObject.name;
                }

                var assigned = renderer.sharedMaterials;
                for (var index = 0; index < assigned.Length; index++)
                {
                    var material = assigned[index];
                    if (material == null)
                    {
                        continue;
                    }

                    if (!byMaterial.TryGetValue(material, out var entry))
                    {
                        entry = new AvatarMaterialEntry
                        {
                            Material = material
                        };
                        byMaterial.Add(material, entry);
                        entries.Add(entry);
                    }

                    entry.Usages.Add(new MaterialUsage
                    {
                        RendererPath = rendererPath,
                        SlotIndex = index,
                        Categories = ClassifyMaterialUsage(
                            renderer,
                            material,
                            rendererPath,
                            index,
                            boneCategories)
                    });
                }
            }

            return entries;
        }

        private IReadOnlyCollection<BodyPartCategory>
            ClassifyMaterialUsage(
            Renderer renderer,
            Material material,
            string rendererPath,
            int slotIndex,
            IReadOnlyDictionary<Transform, BodyPartCategory> boneCategories)
        {
            if (renderer is SkinnedMeshRenderer skinned &&
                TryGetCachedSkinnedMaterialCategories(
                    skinned,
                    slotIndex,
                    boneCategories,
                    out var skinnedCategories))
            {
                return skinnedCategories;
            }

            var materialPart = ClassifyBodyPart(
                material?.name,
                string.Empty);
            if (materialPart != BodyPartCategory.Other)
            {
                return new[] { materialPart };
            }

            var rendererPart = ClassifyBodyPart(
                renderer?.name,
                renderer is SkinnedMeshRenderer meshRenderer
                    ? meshRenderer.sharedMesh?.name
                    : string.Empty);
            var category = rendererPart != BodyPartCategory.Other
                ? rendererPart
                : ClassifyBodyPart(rendererPath, string.Empty);
            return new[] { category };
        }

        private bool TryGetCachedSkinnedMaterialCategories(
            SkinnedMeshRenderer renderer,
            int slotIndex,
            IReadOnlyDictionary<Transform, BodyPartCategory> boneCategories,
            out IReadOnlyCollection<BodyPartCategory> categories)
        {
            categories = null;
            var mesh = renderer.sharedMesh;
            if (mesh == null || slotIndex < 0 ||
                slotIndex >= mesh.subMeshCount)
            {
                return false;
            }

            if (!_materialGeometryCache.TryGetValue(
                    renderer, out var cached) ||
                cached.Mesh != mesh ||
                cached.Slots.Length != mesh.subMeshCount)
            {
                cached = new MaterialGeometryCacheEntry
                {
                    Mesh = mesh,
                    Slots = new IReadOnlyCollection<BodyPartCategory>[
                        mesh.subMeshCount]
                };
                _materialGeometryCache[renderer] = cached;
            }

            categories = cached.Slots[slotIndex];
            if (categories != null)
            {
                return categories.Count > 0;
            }

            if (TryGetSkinnedMaterialCategories(
                    renderer,
                    slotIndex,
                    boneCategories,
                    out categories))
            {
                cached.Slots[slotIndex] = categories;
                return true;
            }

            cached.Slots[slotIndex] = Array.Empty<BodyPartCategory>();
            return false;
        }

        private static bool TryGetSkinnedMaterialCategories(
            SkinnedMeshRenderer renderer,
            int slotIndex,
            IReadOnlyDictionary<Transform, BodyPartCategory> humanoidCategories,
            out IReadOnlyCollection<BodyPartCategory> categories)
        {
            categories = null;
            var mesh = renderer.sharedMesh;
            var bones = renderer.bones;
            if (mesh == null || bones == null || bones.Length == 0 ||
                slotIndex < 0 || slotIndex >= mesh.subMeshCount)
            {
                return false;
            }

            BoneWeight[] weights;
            int[] indices;
            try
            {
                weights = mesh.boneWeights;
                indices = mesh.GetIndices(slotIndex);
            }
            catch (UnityException)
            {
                return false;
            }
            if (weights == null || weights.Length != mesh.vertexCount ||
                indices == null || indices.Length == 0)
            {
                return false;
            }

            var boneParts = bones.Select(bone =>
                    GetMaterialBoneCategory(bone, humanoidCategories))
                .ToArray();
            var usedVertices = new bool[weights.Length];
            var counts = new int[Enum.GetValues(typeof(BodyPartCategory)).Length];
            var classifiedCount = 0;
            foreach (var vertexIndex in indices)
            {
                if (vertexIndex < 0 || vertexIndex >= weights.Length ||
                    usedVertices[vertexIndex])
                {
                    continue;
                }

                usedVertices[vertexIndex] = true;
                var weight = weights[vertexIndex];
                var part = BodyPartCategory.Other;
                var strongestWeight = 0f;
                ConsiderBoneWeight(weight.boneIndex0, weight.weight0);
                ConsiderBoneWeight(weight.boneIndex1, weight.weight1);
                ConsiderBoneWeight(weight.boneIndex2, weight.weight2);
                ConsiderBoneWeight(weight.boneIndex3, weight.weight3);
                if (part == BodyPartCategory.Other)
                {
                    continue;
                }

                counts[(int)part]++;
                classifiedCount++;

                void ConsiderBoneWeight(int boneIndex, float value)
                {
                    if (value <= strongestWeight ||
                        boneIndex < 0 || boneIndex >= boneParts.Length ||
                        boneParts[boneIndex] == BodyPartCategory.Other)
                    {
                        return;
                    }

                    strongestWeight = value;
                    part = boneParts[boneIndex];
                }
            }

            if (classifiedCount == 0)
            {
                return false;
            }

            var minimumVertices = Mathf.Max(
                3,
                Mathf.CeilToInt(classifiedCount * 0.001f));
            var result = new List<BodyPartCategory>();
            foreach (BodyPartCategory part in Enum.GetValues(
                         typeof(BodyPartCategory)))
            {
                if (part != BodyPartCategory.Other &&
                    counts[(int)part] >= minimumVertices)
                {
                    result.Add(part);
                }
            }

            if (result.Count == 0)
            {
                return false;
            }

            categories = result;
            return true;
        }

        private static BodyPartCategory GetMaterialBoneCategory(
            Transform bone,
            IReadOnlyDictionary<Transform, BodyPartCategory> humanoidCategories)
        {
            for (var current = bone; current != null;
                 current = current.parent)
            {
                if (humanoidCategories.TryGetValue(
                        current, out var category))
                {
                    return category;
                }

                category = ClassifyBodyPart(current.name, string.Empty);
                if (category != BodyPartCategory.Other)
                {
                    return category;
                }
            }

            return BodyPartCategory.Other;
        }

        private static IReadOnlyDictionary<Transform, BodyPartCategory>
            GetHumanoidMaterialBoneCategories(Animator animator)
        {
            var categories = new Dictionary<Transform, BodyPartCategory>();
            if (animator == null)
            {
                return categories;
            }

            Add(HumanBodyBones.Head, BodyPartCategory.Head);
            Add(HumanBodyBones.Neck, BodyPartCategory.Head);
            Add(HumanBodyBones.UpperChest, BodyPartCategory.Chest);
            Add(HumanBodyBones.Chest, BodyPartCategory.Chest);
            Add(HumanBodyBones.Spine, BodyPartCategory.Waist);
            Add(HumanBodyBones.Hips, BodyPartCategory.Waist);
            Add(HumanBodyBones.LeftShoulder, BodyPartCategory.Shoulders);
            Add(HumanBodyBones.RightShoulder, BodyPartCategory.Shoulders);
            Add(HumanBodyBones.LeftUpperArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.RightUpperArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.LeftLowerArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.RightLowerArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.LeftHand, BodyPartCategory.Hands);
            Add(HumanBodyBones.RightHand, BodyPartCategory.Hands);
            Add(HumanBodyBones.LeftUpperLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.RightUpperLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.LeftLowerLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.RightLowerLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.LeftFoot, BodyPartCategory.Feet);
            Add(HumanBodyBones.RightFoot, BodyPartCategory.Feet);
            Add(HumanBodyBones.LeftToes, BodyPartCategory.Feet);
            Add(HumanBodyBones.RightToes, BodyPartCategory.Feet);
            return categories;

            void Add(HumanBodyBones bone, BodyPartCategory category)
            {
                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    categories[transform] = category;
                }
            }
        }

        private static IconState CreateMaterialIcon(Material material)
        {
            var texture = AssetPreview.GetMiniThumbnail(material) ??
                          EditorGUIUtility.ObjectContent(
                              material,
                              typeof(Material)).image;
            return texture != null
                ? IconState.FromTexture(texture, UiSizeTokens.Size31)
                : AssetManagerControls.LoadFluentIconState(
                    "image.png",
                    UiSizeTokens.Size31,
                    tintColor: UiColorTokens.TextMuted);
        }

        private static string FormatMaterialUsageSummary(
            AvatarMaterialEntry entry)
        {
            if (entry == null || entry.Usages.Count == 0)
            {
                return string.Empty;
            }

            var first = FormatMaterialUsage(entry.Usages[0]);
            return entry.Usages.Count > 1
                ? first + " " + I18N.Get(
                    "workflow.appearance.moreUsages",
                    entry.Usages.Count - 1)
                : first;
        }

        private static string FormatMaterialUsageTooltip(
            AvatarMaterialEntry entry)
        {
            return entry == null
                ? string.Empty
                : string.Join(
                    "\n",
                    entry.Usages.Select(FormatMaterialUsage));
        }

        private static string FormatMaterialUsage(MaterialUsage usage)
        {
            return I18N.Get(
                "workflow.appearance.materialUsage",
                usage?.RendererPath ?? string.Empty,
                (usage?.SlotIndex ?? 0) + 1);
        }

        private void CreateEditableMaterialVariant(Material sourceMaterial)
        {
            if (!IsEditableWorkflowPrefab() || sourceMaterial == null)
            {
                return;
            }
            try
            {
                var prefabPath = AssetDatabase.GetAssetPath(_workingObject);
                var variantFolder = System.IO.Path
                    .GetDirectoryName(prefabPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(variantFolder))
                {
                    throw new InvalidOperationException(
                        "The derived asset folder could not be found.");
                }
                var assetsFolder = variantFolder + "/Assets";
                if (!AssetDatabase.IsValidFolder(assetsFolder))
                {
                    AssetDatabase.CreateFolder(variantFolder, "Assets");
                }
                var materialsFolder = assetsFolder + "/Materials";
                if (!AssetDatabase.IsValidFolder(materialsFolder))
                {
                    AssetDatabase.CreateFolder(assetsFolder, "Materials");
                }
                var safeName = new string(sourceMaterial.name
                    .Select(character =>
                        char.IsLetterOrDigit(character) ||
                        character == ' ' ||
                        character == '_' ||
                        character == '-'
                            ? character
                            : '_')
                    .Take(80)
                    .ToArray());
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = "Material";
                }
                var materialPath = AssetDatabase.GenerateUniqueAssetPath(
                    materialsFolder + "/" + safeName + ".mat");
                var variant = new Material(sourceMaterial)
                {
                    name = sourceMaterial.name,
                    parent = sourceMaterial
                };
                AssetDatabase.CreateAsset(variant, materialPath);
                AssetDatabase.SaveAssets();
                EditAssetChildren(root =>
                {
                    var replaced = false;
                    foreach (var renderer in root
                                 .GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer == null ||
                            !IsInSelectedPrefabScope(
                                renderer.transform,
                                root.transform))
                        {
                            continue;
                        }
                        var materials = renderer.sharedMaterials;
                        var changed = false;
                        for (var index = 0; index < materials.Length; index++)
                        {
                            if (materials[index] != sourceMaterial)
                            {
                                continue;
                            }
                            materials[index] = variant;
                            changed = true;
                        }
                        if (!changed)
                        {
                            continue;
                        }
                        renderer.sharedMaterials = materials;
                        if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(
                                renderer);
                        }
                        EditorUtility.SetDirty(renderer);
                        replaced = true;
                    }
                    if (!replaced)
                    {
                        throw new InvalidOperationException(
                            "The selected Material is no longer assigned.");
                    }
                });
                _selectedMaterial = variant;
                ShowCategory(WorkflowCategory.Material, false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _feedback = I18N.Get(
                    "workflow.appearance.materialVariantFailed");
                _feedbackType = HelpBoxMessageType.Error;
                ShowCategory(WorkflowCategory.Material, false);
            }
        }

        private bool IsEditableWorkflowMaterial(Material material)
        {
            if (material == null || _workingObject == null)
            {
                return false;
            }
            var path = AssetDatabase.GetAssetPath(material);
            var prefabPath = AssetDatabase.GetAssetPath(_workingObject);
            var variantFolder = System.IO.Path
                .GetDirectoryName(prefabPath)?.Replace('\\', '/');
            return !string.IsNullOrEmpty(path) &&
                   !string.IsNullOrEmpty(variantFolder) &&
                   path.StartsWith(variantFolder + "/",
                       StringComparison.OrdinalIgnoreCase) &&
                   !IsMaterialSharedOutsideSelectedPrefab(material);
        }

        private bool IsMaterialSharedOutsideSelectedPrefab(Material material)
        {
            if (!_selectedPrefabSiblingIndex.HasValue ||
                _workingObject == null)
            {
                return false;
            }
            return _workingObject.GetComponentsInChildren<Renderer>(true)
                .Any(renderer =>
                    renderer != null &&
                    !IsInSelectedPrefabScope(renderer.transform) &&
                    renderer.sharedMaterials.Contains(material));
        }

        private void RefreshMaterialPreview()
        {
            _scenePreview?.RefreshPreview();
        }

        private void RefreshAfterUndoRedo()
        {
            if (_currentCategory != WorkflowCategory.ShapeParts ||
                _shapePartsSection != ShapePartsSection.Shape)
            {
                RefreshMaterialPreview();
                return;
            }

            if (IsEditableWorkflowPrefab())
            {
                try
                {
                    var savedPrefab = PrefabUtility.SavePrefabAsset(
                        _workingObject,
                        out var savedSuccessfully);
                    if (savedSuccessfully && savedPrefab != null)
                    {
                        _workingObject = savedPrefab;
                        if (_workingAsset != null)
                        {
                            _workingAsset.Prefab = savedPrefab;
                        }
                        _bodyScaleDirty = false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _scenePreview?.ReloadPrefab();
            if (_controlsHost == null)
            {
                return;
            }
            _objectEntriesCache = null;
            _controlsHost.Clear();
            _controlsHost.Add(BuildAppearanceControls());
        }

        private void DisposeMaterialEditor()
        {
            _materialInspector?.Dispose();
            _materialInspector = null;
        }

        private void SetPreviewTitle(string titleKey)
        {
            _previewTitle?.SetText(I18N.Get(titleKey));
        }

        private void DisposeEditors()
        {
            _assetManagerView?.Dispose();
            _assetManagerView = null;
            DisposeMaterialEditor();
            _faceExpressionEditor?.Dispose();
            _faceExpressionEditor = null;
            _physBoneEditor?.Dispose();
            _physBoneEditor = null;
            _scenePreview?.Dispose();
            _scenePreview = null;
        }

        private static string FormatVariantSource(
            VariantSourceOption source)
        {
            return source?.Item?.Name ?? string.Empty;
        }
    }
}
