using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.AvatarEditing;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    [Serializable]
    internal sealed class MenuTransformTarget
    {
        public string Path;
        public bool Position = true;
        public bool Rotation;
        public bool Scale;
        public Vector3 MinimumPosition, MaximumPosition;
        public Vector3 MinimumRotation, MaximumRotation;
        public Vector3 MinimumScale = Vector3.one, MaximumScale = Vector3.one;
    }

    [Serializable]
    internal sealed class MenuTransformAction
    {
        public bool Synced = true;
        public int SourceAxis;
        public int HorizontalAxis;
        public int VerticalAxis = 1;
        public List<MenuTransformTarget> Targets = new List<MenuTransformTarget>();
    }

    [Serializable]
    internal sealed class MenuComponentTarget
    {
        public string Path;
        public string Type;
        public bool Enabled = true;
    }

    [Serializable]
    internal sealed class MenuComponentAction
    {
        public bool Synced = true;
        public List<MenuComponentTarget> Targets = new List<MenuComponentTarget>();
    }

    internal enum MenuMaterialPropertyKind { Float, Integer, Color, Vector, Scale, Offset, Texture, Keyword, Shader, RenderQueue, Instancing, DoubleSidedGi, GiFlags }

    internal sealed class MenuMaterialProperty
    {
        internal string Name, Label, ShaderName;
        internal MenuMaterialPropertyKind Kind;
        internal string[] Bindings = Array.Empty<string>();
        internal bool Animated, Hdr;
        internal int Count => Kind == MenuMaterialPropertyKind.Color || Kind == MenuMaterialPropertyKind.Vector ? 4 :
            Kind == MenuMaterialPropertyKind.Scale || Kind == MenuMaterialPropertyKind.Offset ? 2 : 1;
        internal bool Boolean => Kind == MenuMaterialPropertyKind.Keyword || Kind == MenuMaterialPropertyKind.Instancing ||
            Kind == MenuMaterialPropertyKind.DoubleSidedGi;
        internal bool Integral => Kind == MenuMaterialPropertyKind.Integer || Kind == MenuMaterialPropertyKind.RenderQueue ||
            Kind == MenuMaterialPropertyKind.GiFlags;
    }

    internal sealed partial class ExpressionMenuAnimationRecipe
    {
        private sealed class PreparedAction
        {
            internal AnimationClip on, off;
            internal bool synced;
            internal int axis;
            internal MenuTransformAction transform;
            internal readonly List<Material> materials = new List<Material>();
            internal PreparedAction(AnimationClip on, AnimationClip off, bool synced, int axis)
            { this.on = on; this.off = off; this.synced = synced; this.axis = axis; }
        }

        internal static MenuMaterialProperty[] MaterialProperties(Renderer renderer)
        {
            if (renderer == null) return Array.Empty<MenuMaterialProperty>();
            var properties = new Dictionary<string, MenuMaterialProperty>(StringComparer.Ordinal);
            var renderStates = new HashSet<string>(StringComparer.Ordinal)
            {
                "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_BlendOpAlpha",
                "_ZWrite", "_ZTest", "_ZClip", "_Cull", "_CullMode", "_ColorMask", "_AlphaToMask",
                "_StencilRef", "_StencilReadMask", "_StencilWriteMask", "_StencilComp", "_StencilPass",
                "_StencilFail", "_StencilZFail", "_OffsetFactor", "_OffsetUnits"
            };
            foreach (var shader in renderer.sharedMaterials.Where(material => material != null).Select(material => material.shader).Where(shader => shader != null).Distinct())
            {
                var path = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                foreach (var dependency in AssetDatabase.GetDependencies(path).Where(dependency =>
                    dependency.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) || dependency.EndsWith(".cginc", StringComparison.OrdinalIgnoreCase) ||
                    dependency.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!File.Exists(dependency)) continue;
                    var source = File.ReadAllText(dependency);
                    foreach (Match command in Regex.Matches(source,
                        @"^[ \t]*(?:Cull|ZWrite|ZTest|ZClip|ColorMask|BlendOp|Blend|AlphaToMask|Offset|Ref|ReadMask|WriteMask|Comp\w*|Pass\w*|Fail\w*|ZFail\w*)[ \t]+[^\r\n{}]*", RegexOptions.Multiline))
                        foreach (Match property in Regex.Matches(command.Value, @"\[\s*(\w+)\s*\]")) renderStates.Add(property.Groups[1].Value);
                }
            }
            var bindings = AnimationUtility.GetAnimatableBindings(renderer.gameObject, renderer.gameObject)
                .Where(binding => binding.type == renderer.GetType()).Select(binding => binding.propertyName).ToHashSet();
            void Add(string name, string label, MenuMaterialPropertyKind kind, string shaderName = null,
                string[] channels = null, bool hdr = false)
            {
                if (properties.ContainsKey(name)) return;
                var curves = channels?.Select(channel => "material." + channel).ToArray() ?? Array.Empty<string>();
                properties.Add(name, new MenuMaterialProperty
                {
                    Name = name, Label = label, Kind = kind, ShaderName = shaderName, Bindings = curves, Hdr = hdr,
                    Animated = curves.Length > 0 && curves.All(bindings.Contains) && !renderStates.Contains(shaderName ?? name)
                });
            }
            foreach (var material in renderer.sharedMaterials.Where(value => value != null && value.shader != null))
            {
                var shader = material.shader;
                for (var index = 0; index < shader.GetPropertyCount(); index++)
                {
                    var name = shader.GetPropertyName(index);
                    var label = shader.GetPropertyDescription(index) + " (" + name + ")";
                    var type = shader.GetPropertyType(index);
                    switch (type)
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                            Add(name, label, MenuMaterialPropertyKind.Float, name, new[] { name }); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Int:
                            Add(name, label, MenuMaterialPropertyKind.Integer, name, new[] { name }); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Color:
                            Add(name, label, MenuMaterialPropertyKind.Color, name,
                                new[] { name + ".r", name + ".g", name + ".b", name + ".a" },
                                (shader.GetPropertyFlags(index) & UnityEngine.Rendering.ShaderPropertyFlags.HDR) != 0); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                            Add(name, label, MenuMaterialPropertyKind.Vector, name,
                                new[] { name + ".x", name + ".y", name + ".z", name + ".w" }); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                            Add(name, label, MenuMaterialPropertyKind.Texture, name);
                            if ((shader.GetPropertyFlags(index) & UnityEngine.Rendering.ShaderPropertyFlags.NoScaleOffset) == 0)
                            {
                                Add(name + "/Scale", label + " · " + TemplateText.Get("textureScale"), MenuMaterialPropertyKind.Scale, name,
                                    new[] { name + "_ST.x", name + "_ST.y" });
                                Add(name + "/Offset", label + " · " + TemplateText.Get("textureOffset"), MenuMaterialPropertyKind.Offset, name,
                                    new[] { name + "_ST.z", name + "_ST.w" });
                            }
                            break;
                    }
                }
                foreach (var keyword in shader.keywordSpace.keywordNames)
                    Add("#keyword/" + keyword, keyword, MenuMaterialPropertyKind.Keyword, keyword);
            }
            if (properties.Count > 0)
            {
                Add("#shader", TemplateText.Get("materialShader"), MenuMaterialPropertyKind.Shader);
                Add("#renderQueue", TemplateText.Get("renderQueue"), MenuMaterialPropertyKind.RenderQueue);
                Add("#instancing", TemplateText.Get("instancing"), MenuMaterialPropertyKind.Instancing);
                Add("#doubleSidedGi", TemplateText.Get("doubleSidedGi"), MenuMaterialPropertyKind.DoubleSidedGi);
                Add("#giFlags", TemplateText.Get("giFlags"), MenuMaterialPropertyKind.GiFlags);
            }
            return properties.Values.ToArray();
        }

        internal static MenuMaterialProperty MaterialProperty(Renderer renderer, MenuMaterialValueTarget target) =>
            MaterialProperties(renderer).FirstOrDefault(property => property.Name == target.Property);

        internal static Vector4 MaterialCurrentVector(AvatarEditingContext context, MenuMaterialValueTarget target)
        {
            var renderer = ResolveMaterialRenderer(context, target);
            var property = MaterialProperty(renderer, target);
            var material = renderer != null ? renderer.sharedMaterials.FirstOrDefault(value => value != null &&
                (property?.ShaderName == null || property.Kind == MenuMaterialPropertyKind.Keyword || value.HasProperty(property.ShaderName))) : null;
            if (property == null || material == null) return Vector4.zero;
            if (property.Animated)
            {
                var values = Vector4.zero;
                for (var channel = 0; channel < property.Count; channel++)
                    if (AnimationUtility.GetFloatValue(context.Root,
                        EditorCurveBinding.FloatCurve(target.Path, renderer.GetType(), property.Bindings[channel]), out var value))
                        values[channel] = value;
                return values;
            }
            switch (property.Kind)
            {
                case MenuMaterialPropertyKind.Color: return material.GetColor(property.ShaderName);
                case MenuMaterialPropertyKind.Vector: return material.GetVector(property.ShaderName);
                case MenuMaterialPropertyKind.Keyword: return new Vector4(material.IsKeywordEnabled(property.ShaderName) ? 1 : 0, 0, 0, 0);
                case MenuMaterialPropertyKind.RenderQueue: return new Vector4(material.renderQueue, 0, 0, 0);
                case MenuMaterialPropertyKind.Instancing: return new Vector4(material.enableInstancing ? 1 : 0, 0, 0, 0);
                case MenuMaterialPropertyKind.DoubleSidedGi: return new Vector4(material.doubleSidedGI ? 1 : 0, 0, 0, 0);
                case MenuMaterialPropertyKind.GiFlags: return new Vector4((int)material.globalIlluminationFlags, 0, 0, 0);
                case MenuMaterialPropertyKind.Integer: return new Vector4(material.GetInteger(property.ShaderName), 0, 0, 0);
                case MenuMaterialPropertyKind.Float: return new Vector4(material.GetFloat(property.ShaderName), 0, 0, 0);
                case MenuMaterialPropertyKind.Scale: return material.GetTextureScale(property.ShaderName);
                case MenuMaterialPropertyKind.Offset: return material.GetTextureOffset(property.ShaderName);
                default: return Vector4.zero;
            }
        }

        internal static float MaterialCurrentValue(AvatarEditingContext context, MenuMaterialValueTarget target) =>
            MaterialCurrentVector(context, target).x;

        private static void RegisterBinding(AvatarEditingContext context, AnimationClip clip, EditorCurveBinding binding,
            HashSet<(string, Type, string)> used)
        {
            ValidateBinding(context, binding, clip.name);
            bool Rotation(string property) => property.StartsWith("localEulerAngles", StringComparison.Ordinal) ||
                property.StartsWith("m_LocalRotation.", StringComparison.Ordinal);
            if (binding.type == typeof(Transform) && Rotation(binding.propertyName))
            {
                var prefix = binding.propertyName.Substring(0, binding.propertyName.LastIndexOf('.'));
                if (used.Any(key => key.Item1 == binding.path && key.Item2 == typeof(Transform) && Rotation(key.Item3) &&
                    !key.Item3.StartsWith(prefix + ".", StringComparison.Ordinal)))
                    throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
            }
            if (!used.Add((binding.path, binding.type, binding.propertyName)))
                throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
        }

        private void PrepareMaterialActions(AvatarEditingContext context, ModularAvatarMenuItem item,
            List<PreparedAction> clips, HashSet<(string, Type, string)> used)
        {
            var radial = EffectiveMode(item) == MenuBehaviorMode.Radial;
            foreach (var action in MaterialValues)
            {
                if (EffectiveMode(item) != MenuBehaviorMode.Toggle && !radial)
                    throw new InvalidOperationException(TemplateText.Get("incompatibleMode"));
                var prepared = new PreparedAction(new AnimationClip { name = "Material Parameters", frameRate = 60 }, null, action.Synced, 0);
                clips.Add(prepared);
                var variants = new Dictionary<(Renderer, int), Material>();
                var fields = new HashSet<(string, string)>();
                foreach (var target in action.Targets)
                {
                    if (string.IsNullOrEmpty(target.Property)) continue;
                    if (!fields.Add((target.Path, target.Property)))
                        throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
                    var renderer = ResolveMaterialRenderer(context, target);
                    var property = MaterialProperty(renderer, target);
                    if (renderer == null || property == null) throw new InvalidOperationException(TemplateText.Get("missingMaterialProperty"));
                    ExpressionMenuTemplateModel.Reference(context, renderer.gameObject);
                    if (property.Animated)
                    {
                        for (var channel = 0; channel < property.Count; channel++)
                        {
                            var min = property.Count == 1 ? target.Minimum : target.MinimumVector[channel];
                            var max = property.Count == 1 ? target.Maximum : target.MaximumVector[channel];
                            ValidateMaterialValue(max);
                            if (property.Integral && max != Math.Floor(max)) throw new InvalidOperationException(TemplateText.Get("invalidMaterialValue"));
                            if (radial)
                            {
                                ValidateMaterialValue(min);
                                ValidateMaterialValue(max - min);
                                if (property.Integral && min != Math.Floor(min)) throw new InvalidOperationException(TemplateText.Get("invalidMaterialValue"));
                            }
                            var binding = EditorCurveBinding.FloatCurve(target.Path, renderer.GetType(), property.Bindings[channel]);
                            RegisterBinding(context, prepared.on, binding, used);
                            AnimationUtility.SetEditorCurve(prepared.on, binding, radial ?
                                AnimationCurve.Linear(0, min, 1, max) : AnimationCurve.Constant(0, 1f / 60f, max));
                        }
                        continue;
                    }
                    if (radial) throw new InvalidOperationException(TemplateText.Get("discreteMaterialProperty"));
                    for (var slot = 0; slot < renderer.sharedMaterials.Length; slot++)
                    {
                        var original = renderer.sharedMaterials[slot];
                        if (original == null || property.ShaderName != null && property.Kind != MenuMaterialPropertyKind.Keyword &&
                            !original.HasProperty(property.ShaderName)) continue;
                        if (property.Kind == MenuMaterialPropertyKind.Keyword && (original.shader == null ||
                            !original.shader.keywordSpace.keywordNames.Contains(property.ShaderName))) continue;
                        if (!variants.TryGetValue((renderer, slot), out var variant))
                        {
                            variant = new Material(original) { name = original.name + " · ee4v" };
                            variants.Add((renderer, slot), variant);
                            prepared.materials.Add(variant);
                        }
                        ApplyMaterialValue(variant, property, target);
                    }
                }
                foreach (var entry in variants)
                {
                    var binding = EditorCurveBinding.PPtrCurve(AnimationUtility.CalculateTransformPath(entry.Key.Item1.transform, context.Root.transform),
                        entry.Key.Item1.GetType(), "m_Materials.Array.data[" + entry.Key.Item2 + "]");
                    RegisterBinding(context, prepared.on, binding, used);
                    AnimationUtility.SetObjectReferenceCurve(prepared.on, binding, new[] { new ObjectReferenceKeyframe { time = 0, value = entry.Value } });
                }
                FinishPrepared(context, prepared, clips, radial);
            }
        }

        private static void ApplyMaterialValue(Material material, MenuMaterialProperty property, MenuMaterialValueTarget target)
        {
            if (property.Kind != MenuMaterialPropertyKind.Texture && property.Kind != MenuMaterialPropertyKind.Shader)
                ValidateMaterialValue(target.Maximum);
            if (property.Integral && (target.Maximum != Math.Floor(target.Maximum) || target.Maximum < int.MinValue || target.Maximum >= 2147483648d))
                throw new InvalidOperationException(TemplateText.Get("invalidMaterialValue"));
            if (property.Boolean && target.Maximum != 0 && target.Maximum != 1)
                throw new InvalidOperationException(TemplateText.Get("invalidMaterialValue"));
            for (var channel = 0; channel < property.Count; channel++)
                if (property.Count > 1) ValidateMaterialValue(target.MaximumVector[channel]);
            switch (property.Kind)
            {
                case MenuMaterialPropertyKind.Texture:
                    var dimension = material.shader.GetPropertyTextureDimension(material.shader.FindPropertyIndex(property.ShaderName));
                    if (target.Texture != null && target.Texture.dimension != dimension)
                        throw new InvalidOperationException(TemplateText.Get("textureDimensionMismatch"));
                    material.SetTexture(property.ShaderName, target.Texture); break;
                case MenuMaterialPropertyKind.Keyword:
                    if (target.Maximum != 0) material.EnableKeyword(property.ShaderName); else material.DisableKeyword(property.ShaderName); break;
                case MenuMaterialPropertyKind.Shader:
                    if (target.Shader == null) throw new InvalidOperationException(TemplateText.Get("missingMaterialProperty"));
                    material.shader = target.Shader; break;
                case MenuMaterialPropertyKind.RenderQueue:
                    if (target.Maximum < -1 || target.Maximum > 5000) throw new InvalidOperationException(TemplateText.Get("invalidMaterialValue"));
                    material.renderQueue = (int)target.Maximum; break;
                case MenuMaterialPropertyKind.Instancing: material.enableInstancing = target.Maximum != 0; break;
                case MenuMaterialPropertyKind.DoubleSidedGi: material.doubleSidedGI = target.Maximum != 0; break;
                case MenuMaterialPropertyKind.GiFlags:
                    if (target.Maximum < 0 || target.Maximum > 7) throw new InvalidOperationException(TemplateText.Get("invalidMaterialValue"));
                    material.globalIlluminationFlags = (MaterialGlobalIlluminationFlags)(int)target.Maximum; break;
                case MenuMaterialPropertyKind.Integer: material.SetInteger(property.ShaderName, (int)target.Maximum); break;
                case MenuMaterialPropertyKind.Float: material.SetFloat(property.ShaderName, target.Maximum); break;
                case MenuMaterialPropertyKind.Color: material.SetColor(property.ShaderName, target.MaximumVector); break;
                case MenuMaterialPropertyKind.Vector: material.SetVector(property.ShaderName, target.MaximumVector); break;
                case MenuMaterialPropertyKind.Scale: material.SetTextureScale(property.ShaderName, target.MaximumVector); break;
                case MenuMaterialPropertyKind.Offset: material.SetTextureOffset(property.ShaderName, target.MaximumVector); break;
            }
        }

        private static void FinishPrepared(AvatarEditingContext context, PreparedAction prepared, List<PreparedAction> clips, bool continuous)
        {
            var bindings = Bindings(prepared.on);
            if (bindings.Length == 0)
            {
                clips.Remove(prepared);
                DestroyTemporaryClips(new[] { prepared });
            }
            else if (!continuous) prepared.off = CopyClip(context, null, bindings, "OFF");
        }

        internal static Transform ResolveTransform(AvatarEditingContext context, string path) =>
            path == null || context.Root == null ? null : path.Length == 0 ? context.Root.transform : context.Root.transform.Find(path);

        internal static void InitializeTransform(MenuTransformTarget target, Transform transform)
        {
            target.MinimumPosition = target.MaximumPosition = transform != null ? transform.localPosition : Vector3.zero;
            target.MinimumRotation = target.MaximumRotation = transform != null ? transform.localEulerAngles : Vector3.zero;
            target.MinimumScale = target.MaximumScale = transform != null ? transform.localScale : Vector3.one;
        }

        private static IEnumerable<(string property, Vector3 min, Vector3 max, Vector3 current)> TransformProperties(MenuTransformTarget target, Transform transform)
        {
            if (target.Position) yield return ("m_LocalPosition", target.MinimumPosition, target.MaximumPosition, transform.localPosition);
            if (target.Rotation) yield return ("localEulerAnglesRaw", target.MinimumRotation, target.MaximumRotation, transform.localEulerAngles);
            if (target.Scale) yield return ("m_LocalScale", target.MinimumScale, target.MaximumScale, transform.localScale);
        }

        private void PrepareTransformActions(AvatarEditingContext context, ModularAvatarMenuItem item,
            List<PreparedAction> clips, HashSet<(string, Type, string)> used)
        {
            var continuous = IsContinuous(item);
            var puppet = EffectiveMode(item) == MenuBehaviorMode.Puppet;
            foreach (var action in Transforms)
            {
                if (EffectiveMode(item) == MenuBehaviorMode.Button) throw new InvalidOperationException(TemplateText.Get("incompatibleMode"));
                if (puppet && (action.SourceAxis < 0 || action.SourceAxis > 1 || action.HorizontalAxis < 0 || action.HorizontalAxis > 2 ||
                    AxisCount(item) == 4 && (action.VerticalAxis < 0 || action.VerticalAxis > 2 || action.VerticalAxis == action.HorizontalAxis)))
                    throw new InvalidOperationException(TemplateText.Get("invalidTransformAxis"));
                var prepared = new PreparedAction(new AnimationClip { name = "Transform", frameRate = 60 }, null, action.Synced, action.SourceAxis);
                if (puppet) prepared.transform = action;
                clips.Add(prepared);
                foreach (var target in action.Targets)
                {
                    if (target.Path == null) continue;
                    var transform = ResolveTransform(context, target.Path);
                    if (transform == null) throw new InvalidOperationException(TemplateText.Get("missingTransform"));
                    ExpressionMenuTemplateModel.Reference(context, transform.gameObject);
                    foreach (var property in TransformProperties(target, transform))
                        for (var axis = 0; axis < 3; axis++)
                        {
                            ValidateMaterialValue(property.max[axis]);
                            if (continuous) { ValidateMaterialValue(property.min[axis]); ValidateMaterialValue(property.max[axis] - property.min[axis]); }
                            var binding = EditorCurveBinding.FloatCurve(target.Path, typeof(Transform), property.property + "." + "xyz"[axis]);
                            RegisterBinding(context, prepared.on, binding, used);
                            AnimationUtility.SetEditorCurve(prepared.on, binding, continuous && !puppet ?
                                AnimationCurve.Linear(0, property.min[axis], 1, property.max[axis]) :
                                AnimationCurve.Constant(0, 1, puppet ? property.current[axis] : property.max[axis]));
                        }
                }
                FinishPrepared(context, prepared, clips, continuous);
            }
        }

        private Motion BuildTransformMotion(AvatarEditingContext context, ModularAvatarMenuItem item, MenuTransformAction action)
        {
            AnimationClip Pose(float horizontal, float vertical)
            {
                var clip = new AnimationClip { name = "Transform " + horizontal + " " + vertical, frameRate = 60 };
                foreach (var target in action.Targets.Where(target => target.Path != null))
                {
                    var transform = ResolveTransform(context, target.Path);
                    foreach (var property in TransformProperties(target, transform))
                    {
                        var value = property.current;
                        value[action.HorizontalAxis] = Mathf.Lerp(property.min[action.HorizontalAxis], property.max[action.HorizontalAxis], (horizontal + 1) / 2);
                        if (AxisCount(item) == 4)
                            value[action.VerticalAxis] = Mathf.Lerp(property.min[action.VerticalAxis], property.max[action.VerticalAxis], (vertical + 1) / 2);
                        for (var axis = 0; axis < 3; axis++)
                            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(target.Path, typeof(Transform),
                                property.property + "." + "xyz"[axis]), AnimationCurve.Constant(0, 1, value[axis]));
                    }
                }
                AddAsset(clip);
                return clip;
            }
            BlendTree Tree(string parameter, params ChildMotion[] children)
            {
                var tree = new BlendTree { name = "Transform " + parameter, blendType = BlendTreeType.Simple1D,
                    blendParameter = parameter, useAutomaticThresholds = false, children = children };
                AddAsset(tree);
                return tree;
            }
            ChildMotion Child(Motion motion, float threshold) => new ChildMotion { motion = motion, threshold = threshold, timeScale = 1 };
            if (AxisCount(item) == 2)
                return Tree(AxisParameter(item, action.SourceAxis), Child(Pose(-1, 0), -1), Child(Pose(0, 0), 0), Child(Pose(1, 0), 1));
            Motion Horizontal(float vertical) =>
                Tree(AxisParameter(item, 3), Child(Tree(AxisParameter(item, 1),
                    Child(Pose(0, vertical), 0), Child(Pose(1, vertical), 1)), 0), Child(Pose(-1, vertical), 1));
            return Tree(AxisParameter(item, 2), Child(Tree(AxisParameter(item, 0),
                Child(Horizontal(0), 0), Child(Horizontal(1), 1)), 0), Child(Horizontal(-1), 1));
        }

        internal static Component[] EnabledComponents(GameObject target)
        {
            if (target == null) return Array.Empty<Component>();
            var bindings = AnimationUtility.GetAnimatableBindings(target, target);
            return target.GetComponents<Component>().Where(component => component != null && !(component is Transform) &&
                (component is Renderer || component is Collider || component is Animator || component is Light ||
                 component is Camera || component is AudioSource || component is Cloth || component is Animation ||
                 component is UnityEngine.Animations.IConstraint ||
                 (component.GetType().Namespace ?? "").StartsWith("VRC.SDK3.Dynamics.", StringComparison.Ordinal) ||
                 (component.GetType().Namespace ?? "").StartsWith("VRC.SDK3.", StringComparison.Ordinal) &&
                 new[] { "VRCSpatialAudioSource", "VRCHeadChop", "VRCStation" }.Contains(component.GetType().Name)) &&
                (!(component is Animator) || target.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>() == null) &&
                component.GetType().Assembly != typeof(ModularAvatarMenuItem).Assembly &&
                !(component.GetType().Namespace ?? "").StartsWith("Ee4v", StringComparison.Ordinal) &&
                component.GetType().FullName != "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor" &&
                target.GetComponents(component.GetType()).Length == 1 &&
                bindings.Any(binding => binding.type == component.GetType() && binding.propertyName == "m_Enabled") &&
                AnimationUtility.GetFloatValue(target, EditorCurveBinding.FloatCurve("", component.GetType(), "m_Enabled"), out _)).ToArray();
        }

        internal static Component ResolveComponent(AvatarEditingContext context, MenuComponentTarget target) =>
            EnabledComponents(ResolveTransform(context, target.Path)?.gameObject)
                .FirstOrDefault(component => component.GetType().AssemblyQualifiedName == target.Type);

        internal static bool ComponentCurrentValue(AvatarEditingContext context, MenuComponentTarget target) =>
            ResolveComponent(context, target) != null && AnimationUtility.GetFloatValue(context.Root,
                EditorCurveBinding.FloatCurve(target.Path, Type.GetType(target.Type), "m_Enabled"), out var value) && value != 0;

        private void PrepareComponentActions(AvatarEditingContext context, ModularAvatarMenuItem item,
            List<PreparedAction> clips, HashSet<(string, Type, string)> used)
        {
            foreach (var action in Components)
            {
                if (EffectiveMode(item) != MenuBehaviorMode.Toggle) throw new InvalidOperationException(TemplateText.Get("incompatibleMode"));
                var prepared = new PreparedAction(new AnimationClip { name = "Components", frameRate = 60 }, null, action.Synced, 0);
                clips.Add(prepared);
                foreach (var target in action.Targets)
                {
                    if (string.IsNullOrEmpty(target.Type)) continue;
                    var component = ResolveComponent(context, target);
                    if (component == null) throw new InvalidOperationException(TemplateText.Get("missingComponent"));
                    var binding = EditorCurveBinding.FloatCurve(target.Path, component.GetType(), "m_Enabled");
                    RegisterBinding(context, prepared.on, binding, used);
                    AnimationUtility.SetEditorCurve(prepared.on, binding, AnimationCurve.Constant(0, 1f / 60f, target.Enabled ? 1 : 0));
                }
                FinishPrepared(context, prepared, clips, false);
            }
        }
    }
}
