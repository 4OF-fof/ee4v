using Ee4v.Core.EditorIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetProtection;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Simulation;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.FaceExpression;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView : VisualElement, IDisposable
    {
        private const float MinimumBodyScale = 0.5f;
        private const float MaximumBodyScale = 2f;
        private const float MinimumBodyBlendShapeWeight = 0f;
        private const float MaximumBodyBlendShapeWeight = 100f;
        private const double VariantSaveStatusDelaySeconds = 0.5d;
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
        private enum ShapePartsSection
        {
            Shape,
            Parts
        }

        private enum AppearancePanel
        {
            Parts,
            Shape,
            Material
        }

        private sealed class CachedAppearanceControls
        {
            internal VisualElement Content { get; set; }
            internal BodyPartCategory? BodyPart { get; set; }
            internal Material Material { get; set; }
            internal string Feedback { get; set; }
            internal HelpBoxMessageType FeedbackType { get; set; }
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
            internal Renderer Renderer { get; set; }
            internal string RendererPath { get; set; }
            internal int PrefabSiblingIndex { get; set; }
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
            internal bool HasMeshInSubtree { get; set; }
            internal string Path { get; set; }
            internal IReadOnlyCollection<BodyPartCategory> Categories { get; set; }
        }

        private sealed class PrefabObjectNode
        {
            internal PrefabObjectEntry Entry { get; set; }
            internal List<PrefabObjectNode> Children { get; } =
                new List<PrefabObjectNode>();
        }

        private sealed class PrefabObjectRowState
        {
            internal VisualElement Row { get; set; }
            internal UiTextElement Name { get; set; }
            internal UiTextElement Path { get; set; }
            internal Toggle Visibility { get; set; }
            internal UiButton PreviewVisibility { get; set; }
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
                var words = PrefabHierarchyUtility.SplitName(name);
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

        private WorkflowCategoryRail _categoryRail;
        private WorkflowEditorLayout _editorLayout;
        private readonly Dictionary<Material, List<UiButton>>
            _materialVisibilityButtons =
                new Dictionary<Material, List<UiButton>>();
        private readonly HashSet<Material> _hiddenMaterials =
            new HashSet<Material>();
        private readonly Dictionary<SkinnedMeshRenderer,
            MaterialGeometryCacheEntry> _materialGeometryCache =
                new Dictionary<SkinnedMeshRenderer,
                    MaterialGeometryCacheEntry>();
        private readonly Dictionary<AppearancePanel, CachedAppearanceControls>
            _appearanceControlsCache =
                new Dictionary<AppearancePanel, CachedAppearanceControls>();
        private readonly Dictionary<BodyPartCategory, bool> _focusBoneCache =
            new Dictionary<BodyPartCategory, bool>();
        private readonly HashSet<Material> _materialsOutsideSelectedPrefab =
            new HashSet<Material>();
        private IReadOnlyList<AvatarMaterialEntry> _avatarMaterialsCache;
        private IReadOnlyDictionary<Transform, BodyPartCategory>
            _materialBoneCategoriesCache;
        private IReadOnlyList<BodyBlendShapeDefinition> _bodyBlendShapesCache;
        private ISettingsService _settings;
        private bool _appearanceDataDirty;
        private IAssetManager _manager;
        private IAssetVariantManager _variantStatusManager;
        private bool _variantSaveStatusDirty = true;
        private bool _variantHasChanges = true;
        private bool _variantHasDiscardableChanges;
        private string _variantSaveStatusError;
        private double _variantSaveStatusDueAt;
        private bool _savingVariant;
        private DerivedAssetInfo _workingAsset;
        private GameObject _workingObject;
        private Material _selectedMaterial;
        private UiButton _allMaterialsVisibilityButton;
        private EmbeddedMaterialInspector _materialInspector;
        private PrefabScenePreview _scenePreview;
        private int? _previewScopeSiblingIndex;
        private FaceExpressionEmbeddedView _faceExpressionEditor;
        private VisualElement _customizerHost;
        private VisualElement _faceExpressionHost;
        private ScrollView _controlsHost;
        private VisualElement _overviewContent;
        private bool _overviewMobile = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
        private bool _playModeTransition;
        private VisualElement _appearanceHeader;
        private PreviewPane _previewPane;
        private WorkflowCategory _currentCategory =
            WorkflowCategory.Overview;
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
        private string _selectedPartKey;
        private readonly Dictionary<PrefabObjectEntry, PendingPartVisibility>
            _pendingPartVisibility =
                new Dictionary<PrefabObjectEntry, PendingPartVisibility>();
        private string _pendingPartAssetPath;
        private IReadOnlyList<int> _prefabSiblingIndices =
            Array.Empty<int>();
        private readonly HashSet<int> _hiddenPrefabSiblingIndices =
            new HashSet<int>();
        private readonly HashSet<string> _hiddenPreviewParts =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<int> _expandedPartPrefabGroups =
            new HashSet<int>();
        private readonly HashSet<string> _expandedObjectGroups =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<int> _expandedMaterialPrefabGroups =
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
                }
            }
        }
        private GameObject _workingPrefabAsset;
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
        private VisualElement _executionHost;
        private AvatarExecutionView _executionView;
        internal bool ExecutionSelected => _currentCategory == WorkflowCategory.Execution;

        internal void ShowExecutionConfirmation() => ShowCategory(WorkflowCategory.Execution, false);
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
            Undo.postprocessModifications += OnUndoModifications;
            FaceExpressionShapeNamingSnapshot.PresetsChanged -=
                OnBlendShapePresetChanged;
            FaceExpressionShapeNamingSnapshot.PresetsChanged +=
                OnBlendShapePresetChanged;
            AssetManagerSettings.PartListExclusionsChanged -=
                OnPartListExclusionsChanged;
            AssetManagerSettings.PartListExclusionsChanged +=
                OnPartListExclusionsChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            AvatarPlayModePerformanceCache.Changed += OnPlayModePerformanceChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneClosed += OnWorkingSceneClosed;
            _settings = CoreSettings.Current;
            _settings.Changed += OnSettingChanged;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            EndBodyScaleDrag(false);
            SaveBodyScalePrefab(false);
            FlushPendingPartVisibility();
            ReleaseWorkingScene();
            I18N.Reloaded -= Rebuild;
            AssetManagerWindowSession.ManagerInvalidated -=
                OnManagerInvalidated;
            Undo.undoRedoPerformed -= RefreshAfterUndoRedo;
            Undo.postprocessModifications -= OnUndoModifications;
            FaceExpressionShapeNamingSnapshot.PresetsChanged -=
                OnBlendShapePresetChanged;
            AssetManagerSettings.PartListExclusionsChanged -=
                OnPartListExclusionsChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            AvatarPlayModePerformanceCache.Changed -= OnPlayModePerformanceChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneClosed -= OnWorkingSceneClosed;
            _settings.Changed -= OnSettingChanged;
            ReleaseVariantStatusManager();
            DisposeEditors();
            ClearAppearanceCaches();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _playModeTransition = true;
                DisposePreview();
                return;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _playModeTransition = true;
                return;
            }
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _playModeTransition = false;
                schedule.Execute(() =>
                {
                    if (!_disposed && _workingObject != null) { BuildWindow(); }
                });
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode) { return; }
            if (_workingAsset?.Prefab == null)
            {
                _playModeTransition = false;
                return;
            }
            schedule.Execute(() =>
            {
                if (_disposed || EditorApplication.isPlaying || _workingAsset?.Prefab == null) { return; }
                ReleaseWorkingScene();
                _workingPrefabAsset = _workingAsset.Prefab;
                _workingObject = AcquireWorkingScene(_workingPrefabAsset);
                ClearAppearanceCaches();
                _playModeTransition = false;
                BuildWindow();
            });
        }

        private void OnPlayModePerformanceChanged()
        {
            if (!_disposed && _workingObject != null && _currentCategory == WorkflowCategory.Overview)
            {
                schedule.Execute(() =>
                {
                    if (!_disposed && _workingObject != null && _currentCategory == WorkflowCategory.Overview)
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
            _bodyScaleDirty = false;
            if (!_disposed && this.panel != null)
            {
                this.schedule.Execute(BuildWindow);
            }
        }

        private UndoPropertyModification[] OnUndoModifications(
            UndoPropertyModification[] modifications)
        {
            if (_workingObject == null)
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
                        transform.IsChildOf(_workingObject.transform))
                {
                    _appearanceDataDirty = true;
                    InvalidateVariantSaveStatus();
                    break;
                }
            }
            return modifications;
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (FaceExpressionShapeNamingSnapshot.IsSeparatorSetting(
                    args.Definition))
            {
                OnBlendShapePresetChanged();
            }
        }

        private void ClearAppearanceCaches()
        {
            DisposeMaterialEditor();
            foreach (var cached in _appearanceControlsCache.Values)
            {
                cached.Content.RemoveFromHierarchy();
            }
            _appearanceControlsCache.Clear();
            _avatarMaterialsCache = null;
            _materialBoneCategoriesCache = null;
            _bodyBlendShapesCache = null;
            _materialGeometryCache.Clear();
            _materialsOutsideSelectedPrefab.Clear();
            _focusBoneCache.Clear();
            _objectEntriesCache = null;
            _objectRows.Clear();
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
            _objectEntriesCache = null;
            InvalidateAppearanceControls(AppearancePanel.Parts);
            _objectRows.Clear();
            if (_currentCategory == WorkflowCategory.ShapeParts &&
                _shapePartsSection == ShapePartsSection.Parts)
            {
                Rebuild();
            }
        }

        private void OnBlendShapePresetChanged()
        {
            _bodyBlendShapesCache = null;
            InvalidateAppearanceControls(AppearancePanel.Shape);
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
            ReleaseVariantStatusManager();
            _manager = null;
            _assetManagerViewState = new AssetManagerViewState();
            Rebuild();
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
            return PrefabHierarchyUtility.IsInScope(
                target,
                root,
                _selectedPrefabSiblingIndex,
                _prefabSiblingIndices);
        }

        private void SetPreviewTitle(string titleKey)
        {
            _previewPane?.SetTitle(I18N.Get(titleKey));
        }

        private void DisposeEditors()
        {
            _executionView?.Dispose();
            _executionView = null;
            _assetManagerView?.Dispose();
            _assetManagerView = null;
            DisposeMaterialEditor();
            _faceExpressionEditor?.Dispose();
            _faceExpressionEditor = null;
            DisposePreview();
        }

        private void DisposePreview()
        {
            _scenePreview?.Dispose();
            _scenePreview = null;
        }

    }
}
