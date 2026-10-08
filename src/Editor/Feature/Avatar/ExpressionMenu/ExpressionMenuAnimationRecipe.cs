using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AvatarEditing;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal enum MenuBehaviorMode { Toggle = 1, Radial = 2, Button = 3, Puppet = 4 }

    [Serializable]
    internal sealed class MenuMaterialValueTarget
    {
        public string Path;
        public string Property;
        public float Minimum;
        public float Maximum = 1;
        public Vector4 MinimumVector;
        public Vector4 MaximumVector;
        public Texture Texture;
        public Shader Shader;
    }

    [Serializable]
    internal sealed class MenuMaterialValueAction
    {
        public bool Synced = true;
        public List<MenuMaterialValueTarget> Targets = new List<MenuMaterialValueTarget>();
    }

    [Serializable]
    internal sealed class MenuParameterAction
    {
        public bool Synced = true;
        public string Parameter;
        public AnimatorControllerParameterType NumericType = AnimatorControllerParameterType.Float;
        public AnimatorControllerParameterType ButtonType = AnimatorControllerParameterType.Trigger;
        public int Axis;
        public float Off;
        public float On = 1;
    }

    [Serializable]
    internal sealed class MenuClipAction
    {
        public bool Synced = true;
        public AnimationClip On;
        public AnimationClip Off;
    }

    [Serializable]
    internal sealed class MenuRadialShapeTarget
    {
        public string Path;
        public string Shape;
        public float Minimum;
        public float Maximum = 100;
    }

    [Serializable]
    internal sealed class MenuRadialShapeAction
    {
        public bool Synced = true;
        public int Axis;
        public List<MenuRadialShapeTarget> Targets = new List<MenuRadialShapeTarget>();
    }

    [Serializable]
    internal sealed class MenuReactiveAction
    {
        public MenuTemplateKind Kind;
        public bool Synced = true;
        public bool Inverted;
        public AvatarObjectReference Root;
        public List<ToggledObject> Objects = new List<ToggledObject>();
        public List<MatSwap> Swaps = new List<MatSwap>();
        public List<ChangedShape> Shapes = new List<ChangedShape>();
    }

    // Editor-only authoring data. The prefab references only the generated controller at build time.
    internal sealed partial class ExpressionMenuAnimationRecipe : ScriptableObject
    {
        [SerializeField] internal List<MenuClipAction> Actions = new List<MenuClipAction>();
        [SerializeField] internal List<MenuRadialShapeAction> RadialShapes = new List<MenuRadialShapeAction>();
        [SerializeField] internal List<MenuReactiveAction> ReactiveActions = new List<MenuReactiveAction>();
        [SerializeField] internal List<MenuParameterAction> ParameterActions = new List<MenuParameterAction>();
        [SerializeField] internal List<MenuMaterialValueAction> MaterialValues = new List<MenuMaterialValueAction>();
        [SerializeField] internal List<MenuTransformAction> Transforms = new List<MenuTransformAction>();
        [SerializeField] internal List<MenuComponentAction> Components = new List<MenuComponentAction>();
        [SerializeField] internal MenuBehaviorMode Mode = MenuBehaviorMode.Toggle;
        [SerializeField] internal float InitialValue;
        [SerializeField] private string _sharedParameter;
        [SerializeField] private AnimatorController _controller;

        internal ExpressionMenuParameterCatalog.Entry[] ParameterChoices(AvatarEditingContext context, ModularAvatarMenuItem item) =>
            ExpressionMenuParameterCatalog.Entries(context, _controller).Where(entry =>
                (EffectiveMode(item) == MenuBehaviorMode.Button ? entry.Type != AnimatorControllerParameterType.Trigger || !entry.Expression :
                    IsContinuous(item) ? entry.Type == AnimatorControllerParameterType.Float || entry.Type == AnimatorControllerParameterType.Int :
                    entry.Type == AnimatorControllerParameterType.Bool) &&
                !InputParameters(item).Contains(entry.Name) && entry.Name != Parameter(item) && entry.Name != Parameter(item) + "/CopyClock").ToArray();

        internal static AnimatorControllerParameterType ParameterType(ModularAvatarMenuItem item, MenuParameterAction action) =>
            EffectiveMode(item) == MenuBehaviorMode.Button ? action.ButtonType :
                IsContinuous(item) ? action.NumericType : AnimatorControllerParameterType.Bool;

        internal static bool IsContinuous(ModularAvatarMenuItem item) =>
            EffectiveMode(item) == MenuBehaviorMode.Radial || EffectiveMode(item) == MenuBehaviorMode.Puppet;

        internal static int AxisCount(ModularAvatarMenuItem item) =>
            EffectiveMode(item) == MenuBehaviorMode.Puppet ? (item.PortableControl.Type == PortableControlType.FourAxisPuppet ? 4 : 2) : 1;

        private static readonly string[] TwoAxes = { "Horizontal", "Vertical" };
        private static readonly string[] FourAxes = { "Up", "Right", "Down", "Left" };

        internal static string[] AxisNames(ModularAvatarMenuItem item) => AxisCount(item) == 4 ? FourAxes : TwoAxes;

        internal static float SourceMinimum(ModularAvatarMenuItem item) =>
            EffectiveMode(item) == MenuBehaviorMode.Puppet && AxisCount(item) == 2 ? -1 : 0;

        private static bool InitiallyEnabled(ModularAvatarMenuItem item) => EffectiveMode(item) == MenuBehaviorMode.Toggle && item.isDefault;

        private static string[] InputParameters(ModularAvatarMenuItem item) => EffectiveMode(item) == MenuBehaviorMode.Puppet ?
            AxisNames(item).Select(axis => Parameter(item) + "/" + axis).ToArray() : new[] { Parameter(item) };

        private static string AxisParameter(ModularAvatarMenuItem item, int axis) => InputParameters(item)[axis];

        internal static MenuBehaviorMode EffectiveMode(ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            if (recipe != null && Enum.IsDefined(typeof(MenuBehaviorMode), recipe.Mode)) return recipe.Mode;
            switch (item.PortableControl.Type)
            {
                case PortableControlType.RadialPuppet: return MenuBehaviorMode.Radial;
                case PortableControlType.Button: return MenuBehaviorMode.Button;
                case PortableControlType.TwoAxisPuppet:
                case PortableControlType.FourAxisPuppet: return MenuBehaviorMode.Puppet;
                default: return MenuBehaviorMode.Toggle;
            }
        }

        private static string Parameter(ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            if (!string.IsNullOrEmpty(recipe?._sharedParameter)) return recipe._sharedParameter;
            return !string.IsNullOrEmpty(item.PortableControl.Parameter) ? item.PortableControl.Parameter :
                item.Control.subParameters?.FirstOrDefault()?.name ?? "";
        }

        internal static bool HasSettingsToDiscard(ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            return ExpressionMenuTemplateModel.Effects(item).Length > 0 ||
                recipe != null && (recipe.Actions.Count > 0 || recipe.RadialShapes.Count > 0 ||
                    recipe.ReactiveActions.Count > 0 || recipe.ParameterActions.Count > 0 || recipe.MaterialValues.Count > 0 ||
                    recipe.Transforms.Count > 0 || recipe.Components.Count > 0) ||
                (EffectiveMode(item) == MenuBehaviorMode.Radial ? recipe != null && recipe.InitialValue != 0 : InitiallyEnabled(item));
        }

        internal static void SetModeDiscardingSettings(AvatarEditingContext context, ModularAvatarMenuItem item, MenuBehaviorMode mode, int puppetAxes = 2)
        {
            var recipe = Find(item);
            if (recipe == null || !ExpressionMenuTemplateModel.IsOwned(item.gameObject))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            if (!Enum.IsDefined(typeof(MenuBehaviorMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (puppetAxes != 2 && puppetAxes != 4) throw new ArgumentOutOfRangeException(nameof(puppetAxes));
            if (EffectiveMode(item) == mode && (mode != MenuBehaviorMode.Puppet || AxisCount(item) == puppetAxes)) return;
            Change(context, item, () =>
            {
                var parameter = Parameter(item);
                recipe._sharedParameter = parameter;
                Undo.RecordObject(item, "Change behavior type");
                recipe.Actions.Clear();
                recipe.RadialShapes.Clear();
                recipe.ReactiveActions.Clear();
                recipe.ParameterActions.Clear();
                recipe.MaterialValues.Clear();
                recipe.Transforms.Clear();
                recipe.Components.Clear();
                recipe.InitialValue = 0;
                item.isDefault = false;
                recipe.Mode = mode;
                item.PortableControl.Type = mode == MenuBehaviorMode.Radial ? PortableControlType.RadialPuppet :
                    mode == MenuBehaviorMode.Button ? PortableControlType.Button : mode == MenuBehaviorMode.Puppet ?
                        (puppetAxes == 2 ? PortableControlType.TwoAxisPuppet : PortableControlType.FourAxisPuppet) : PortableControlType.Toggle;
                item.PortableControl.Parameter = IsContinuous(item) ? "" : parameter;
                item.Control.subParameters = IsContinuous(item) ? InputParameters(item).Select(name =>
                    new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter { name = name }).ToArray() :
                    Array.Empty<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter>();
                item.Control.labels = mode == MenuBehaviorMode.Puppet ? FourAxes.Select(axis =>
                    new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Label { name = TemplateText.Get("axis" + axis) }).ToArray() :
                    Array.Empty<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Label>();
                item.PortableControl.Value = IsContinuous(item) ? 0 : 1;
                item.isSaved = mode != MenuBehaviorMode.Button;
                EditorUtility.SetDirty(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                foreach (var effect in ExpressionMenuTemplateModel.Effects(item)) Undo.DestroyObjectImmediate(effect);
            });
        }

        private static string Folder(ModularAvatarMenuItem item) =>
            Path.GetDirectoryName(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject)).Replace('\\', '/');

        internal static ExpressionMenuAnimationRecipe Find(ModularAvatarMenuItem item) =>
            item != null && ExpressionMenuTemplateModel.IsOwned(item.gameObject) ?
                AssetDatabase.LoadAssetAtPath<ExpressionMenuAnimationRecipe>(Folder(item) + "/Gimmick.asset") : null;

        internal static ExpressionMenuAnimationRecipe Ensure(ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            if (recipe != null) return recipe;
            if (item == null || !ExpressionMenuTemplateModel.IsOwned(item.gameObject))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            recipe = CreateInstance<ExpressionMenuAnimationRecipe>();
            recipe.name = "Gimmick";
            AssetDatabase.CreateAsset(recipe, Folder(item) + "/Gimmick.asset");
            Undo.RegisterCreatedObjectUndo(recipe, "Create gimmick actions");
            return recipe;
        }

        internal static void Change(AvatarEditingContext context, ModularAvatarMenuItem item, Action change)
        {
            var recipe = Find(item);
            if (!CanEdit(context, item))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Edit gimmick actions");
            try
            {
                Undo.RecordObject(recipe, "Edit gimmick actions");
                change();
                recipe.ImportEffects(context, item);
                recipe.Build(context, item);
                EditorUtility.SetDirty(recipe);
                AssetDatabase.SaveAssetIfDirty(recipe);
                ExpressionMenuInstaller.SaveGeneratedPrefab(item.gameObject);
                Undo.CollapseUndoOperations(group);
                context.Edits.WorkingSceneDirty = true;
                context.Edits.Changed();
            }
            catch
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        internal static void SetClip(AvatarEditingContext context, ModularAvatarMenuItem item, int index,
            AnimationClip clip, bool on)
        {
            if (!CanEdit(context, item))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var recipe = Find(item);
            var current = recipe.Actions[index];
            var proposed = new MenuClipAction
            {
                Synced = current.Synced,
                On = on ? clip : current.On,
                Off = on ? current.Off : clip
            };
            // Reject incompatible clips before starting an Undo transaction or changing saved authoring data.
            ValidateClip(context, clip);
            var prepared = recipe.PrepareClips(context, item, index, proposed);
            DestroyTemporaryClips(prepared);
            Change(context, item, () =>
            {
                if (on) recipe.Actions[index].On = clip;
                else recipe.Actions[index].Off = clip;
            });
        }

        internal static bool CanEdit(AvatarEditingContext context, ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            return ExpressionMenuTemplateModel.CanEdit(context, item) && ExpressionMenuModel.CanWrite(recipe) &&
                (recipe._controller == null || ExpressionMenuModel.CanWrite(recipe._controller)) &&
                ExpressionMenuTemplateModel.Effects(item).All(effect => ExpressionMenuTemplateModel.CanEdit(context, effect)) &&
                (item.GetComponent<ModularAvatarParameters>() == null ||
                 ExpressionMenuTemplateModel.CanEdit(context, item.GetComponent<ModularAvatarParameters>())) &&
                item.GetComponents<ModularAvatarMergeAnimator>().Where(component =>
                    recipe._controller != null && component.animator == recipe._controller)
                    .All(component => ExpressionMenuTemplateModel.CanEdit(context, component));
        }

        internal static bool CanWriteBacking(ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            return recipe == null || ExpressionMenuModel.CanWrite(recipe) &&
                (recipe._controller == null || ExpressionMenuModel.CanWrite(recipe._controller));
        }

        internal static void SyncInitial(ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            if (recipe == null) return;
            recipe.ConfigureParameters(item);
            if (recipe._controller == null) return;
            if (!ExpressionMenuModel.CanWrite(recipe._controller))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var controller = recipe._controller;
            Undo.RegisterCompleteObjectUndo(controller, "Edit initial state");
            controller.parameters = recipe.ControllerParameters(item);
            if (!IsContinuous(item)) foreach (var layer in controller.layers)
            {
                var machine = layer.stateMachine;
                Undo.RecordObject(machine, "Edit initial state");
                machine.defaultState = machine.states.Select(state => state.state).FirstOrDefault(state => state.name == "LocalOnly") ??
                    machine.states.Select(state => state.state)
                    .FirstOrDefault(state => state.name == (InitiallyEnabled(item) ? "ON" : "OFF"));
                EditorUtility.SetDirty(machine);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
        }

        private AnimatorControllerParameter ControllerParameter(ModularAvatarMenuItem item) => new AnimatorControllerParameter
        {
            name = Parameter(item),
            type = EffectiveMode(item) == MenuBehaviorMode.Radial ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Bool,
            defaultBool = InitiallyEnabled(item),
            defaultFloat = InitialValue / 100f
        };

        private AnimatorControllerParameter[] ControllerParameters(ModularAvatarMenuItem item, AvatarEditingContext context = null)
        {
            var parameters = EffectiveMode(item) == MenuBehaviorMode.Puppet ? InputParameters(item).Select(name =>
                new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float }).ToList() :
                new List<AnimatorControllerParameter> { ControllerParameter(item) };
            parameters.Add(new AnimatorControllerParameter { name = "IsLocal", type = AnimatorControllerParameterType.Bool });
            foreach (var action in ParameterActions.Where(action => !string.IsNullOrEmpty(action.Parameter)))
            {
                var declaration = context != null ? ExpressionMenuParameterCatalog.Find(context, _controller, action.Parameter)?.Declaration :
                    _controller?.parameters.FirstOrDefault(parameter => parameter.name == action.Parameter);
                parameters.Add(new AnimatorControllerParameter
                {
                    name = action.Parameter, type = ParameterType(item, action), defaultBool = declaration?.defaultBool ?? false,
                    defaultInt = declaration?.defaultInt ?? 0, defaultFloat = declaration?.defaultFloat ?? 0
                });
            }
            if (IsContinuous(item) && ParameterActions.Any(action => !string.IsNullOrEmpty(action.Parameter)))
                parameters.Add(new AnimatorControllerParameter { name = Parameter(item) + "/CopyClock", type = AnimatorControllerParameterType.Float });
            return parameters.ToArray();
        }

        private void ImportEffects(AvatarEditingContext context, ModularAvatarMenuItem item, bool removeOriginal = true)
        {
            foreach (var effect in ExpressionMenuTemplateModel.Effects(item))
            {
                var action = new MenuReactiveAction { Synced = item.isSynced, Inverted = effect.Inverted };
                if (effect is ModularAvatarObjectToggle toggle)
                {
                    action.Kind = MenuTemplateKind.ObjectToggle;
                    action.Objects = toggle.Objects.Select(target => new ToggledObject
                    {
                        Object = target.Object?.Get(effect) is GameObject go ? ExpressionMenuTemplateModel.Reference(context, go, false) :
                            new AvatarObjectReference { referencePath = target.Object?.referencePath }, Active = target.Active
                    }).ToList();
                }
                else if (effect is ModularAvatarMaterialSwap swap)
                {
                    if (swap.QuickSwapMode != QuickSwapMode.None) throw new InvalidOperationException(TemplateText.Get("unsupportedAction"));
                    action.Kind = MenuTemplateKind.MaterialSwap;
                    var root = swap.Root?.Get(effect);
                    if (root == null && !string.IsNullOrEmpty(swap.Root?.referencePath))
                        throw new InvalidOperationException(TemplateText.Get("missingTarget"));
                    action.Root = ExpressionMenuTemplateModel.Reference(context, root ?? context.Root);
                    action.Swaps = swap.Swaps.Select(target => target.Clone()).ToList();
                }
                else if (effect is ModularAvatarShapeChanger shape)
                {
                    if (shape.Shapes.Any(target => target.ChangeType != ShapeChangeType.Set))
                        throw new InvalidOperationException(TemplateText.Get("unsupportedAction"));
                    action.Kind = MenuTemplateKind.ShapeChanger;
                    action.Shapes = shape.Shapes.Select(target => new ChangedShape
                    {
                        Object = target.Object?.Get(effect) is GameObject go ? ExpressionMenuTemplateModel.Reference(context, go) :
                            new AvatarObjectReference { referencePath = target.Object?.referencePath },
                        ShapeName = target.ShapeName, ChangeType = target.ChangeType, Value = target.Value
                    }).ToList();
                }
                ReactiveActions.Add(action);
                if (removeOriginal) Undo.DestroyObjectImmediate(effect);
            }
        }

        internal static void SetEffectSynced(AvatarEditingContext context, ModularAvatarMenuItem item, ReactiveComponent effect, bool synced)
        {
            if (!ExpressionMenuTemplateModel.CanEdit(context, item) || !ExpressionMenuTemplateModel.CanEdit(context, effect))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var recipe = Ensure(item);
            var index = recipe.ReactiveActions.Count + Array.IndexOf(ExpressionMenuTemplateModel.Effects(item), effect);
            Change(context, item, () =>
            {
                recipe.ImportEffects(context, item);
                recipe.ReactiveActions[index].Synced = synced;
            });
        }

        private void ConfigureParameters(ModularAvatarMenuItem item)
        {
            var parameter = Parameter(item);
            var component = item.GetComponent<ModularAvatarParameters>();
            if (component != null && !ExpressionMenuModel.CanWrite(component))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var radial = IsContinuous(item);
            var synced = Actions.Any(action => action.Synced) || RadialShapes.Any(action => action.Synced) ||
                ReactiveActions.Any(action => action.Synced) || ParameterActions.Any(action => action.Synced) ||
                MaterialValues.Any(action => action.Synced) ||
                Transforms.Any(action => action.Synced) || Components.Any(action => action.Synced) ||
                ExpressionMenuTemplateModel.Effects(item).Length > 0 && item.isSynced;
            Undo.RecordObject(item, "Edit action synchronization");
            item.isSynced = synced;
            EditorUtility.SetDirty(item);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            if (!radial && component == null) return;
            if (component == null) component = Undo.AddComponent<ModularAvatarParameters>(item.gameObject);
            Undo.RecordObject(component, "Edit behavior parameter");
            var generated = new[] { parameter }.Concat(TwoAxes.Concat(FourAxes).Select(axis => parameter + "/" + axis)).ToArray();
            component.parameters.RemoveAll(config => !config.isPrefix && generated.Contains(config.nameOrPrefix));
            if (radial) for (var axis = 0; axis < AxisCount(item); axis++)
            {
                var axisSynced = EffectiveMode(item) != MenuBehaviorMode.Puppet ? item.isSynced :
                    RadialShapes.Any(action => action.Axis == axis && action.Synced) || ParameterActions.Any(action => action.Axis == axis && action.Synced) ||
                    Transforms.Any(action => action.Synced && (AxisCount(item) == 4 || action.SourceAxis == axis));
                component.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = AxisParameter(item, axis), syncType = ParameterSyncType.Float, localOnly = !axisSynced,
                    saved = item.isSaved, hasExplicitDefaultValue = true,
                    defaultValue = EffectiveMode(item) == MenuBehaviorMode.Radial ? InitialValue / 100f : 0
                });
            }
            if (component.parameters.Count == 0) Undo.DestroyObjectImmediate(component);
            else
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        private List<PreparedAction> PrepareClips(
            AvatarEditingContext context, ModularAvatarMenuItem item, int replacedIndex = -1, MenuClipAction replacement = null)
        {
            var clips = new List<PreparedAction>();
            var used = new HashSet<(string, Type, string)>();
            var radial = IsContinuous(item);
            try
            {
                for (var index = 0; index < Actions.Count; index++)
                {
                    var action = index == replacedIndex ? replacement : Actions[index];
                    if (action.On == null) continue;
                    ValidateClip(context, action.On);
                    if (!radial) ValidateClip(context, action.Off);
                    var bindings = Bindings(action.On).Concat(radial ? Array.Empty<EditorCurveBinding>() : Bindings(action.Off)).Distinct().ToArray();
                    foreach (var binding in bindings)
                    {
                        if (!used.Add((binding.path, binding.type, binding.propertyName)))
                            throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
                    }
                    var on = CopyClip(context, action.On, bindings, "ON");
                    clips.Add(new PreparedAction(on, null, action.Synced, 0));
                    if (radial)
                    {
                        var settings = AnimationUtility.GetAnimationClipSettings(on);
                        settings.loopTime = false;
                        AnimationUtility.SetAnimationClipSettings(on, settings);
                        continue;
                    }
                    var off = CopyClip(context, action.Off, bindings, "OFF");
                    clips[clips.Count - 1].off = off;
                }
                if (radial) foreach (var action in RadialShapes)
                {
                    ValidateAxis(item, action.Axis);
                    var clip = new AnimationClip { name = "BlendShapes", frameRate = 60 };
                    clips.Add(new PreparedAction(clip, null, action.Synced, action.Axis));
                    foreach (var target in action.Targets)
                    {
                        if (string.IsNullOrEmpty(target.Shape)) continue;
                        var renderer = ResolveRenderer(context, target);
                        if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(target.Shape) < 0)
                            throw new InvalidOperationException(TemplateText.Get("missingShape"));
                        ValidatePercent(target.Minimum);
                        ValidatePercent(target.Maximum);
                        ExpressionMenuTemplateModel.Reference(context, renderer.gameObject);
                        var property = "blendShape." + target.Shape;
                        if (!used.Add((target.Path, typeof(SkinnedMeshRenderer), property)))
                            throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(target.Path, typeof(SkinnedMeshRenderer), property),
                            AnimationCurve.Linear(0, target.Minimum, 1, target.Maximum));
                    }
                    if (AnimationUtility.GetCurveBindings(clip).Length == 0)
                    {
                        clips.RemoveAt(clips.Count - 1);
                        DestroyImmediate(clip);
                    }
                }
                PrepareMaterialActions(context, item, clips, used);
                PrepareTransformActions(context, item, clips, used);
                PrepareComponentActions(context, item, clips, used);
                foreach (var action in ReactiveActions)
                {
                    var on = BuildReactiveClip(context, item, action);
                    clips.Add(new PreparedAction(on, null, action.Synced, 0));
                    var bindings = Bindings(on);
                    foreach (var binding in bindings)
                        if (!used.Add((binding.path, binding.type, binding.propertyName)))
                            throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
                    var off = CopyClip(context, null, bindings, "OFF");
                    clips[clips.Count - 1].on = action.Inverted ? off : on;
                    clips[clips.Count - 1].off = action.Inverted ? on : off;
                }
                return clips;
            }
            catch
            {
                DestroyTemporaryClips(clips);
                throw;
            }
        }

        private void Build(AvatarEditingContext context, ModularAvatarMenuItem item)
        {
            ValidateParameters(context, item);
            var clips = PrepareClips(context, item);
            var parameters = ParameterActions.Where(action => !string.IsNullOrEmpty(action.Parameter)).ToArray();
            var radial = IsContinuous(item);
            try
            {
                ConfigureParameters(item);
                var merge = item.GetComponents<ModularAvatarMergeAnimator>().FirstOrDefault(component =>
                    _controller != null && component.animator == _controller);
                if (merge != null && !ExpressionMenuTemplateModel.CanEdit(context, merge))
                    throw new InvalidOperationException(TemplateText.Get("readOnly"));
                if (clips.Count == 0 && parameters.Length == 0 && _controller == null) return;
                if (_controller == null)
                {
                    _controller = new AnimatorController { name = "Gimmick" };
                    AssetDatabase.CreateAsset(_controller, Folder(item) + "/Gimmick.controller");
                    Undo.RegisterCreatedObjectUndo(_controller, "Create gimmick animation");
                }
                Undo.RegisterCompleteObjectUndo(_controller, "Edit gimmick animation");
                foreach (var child in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_controller)))
                    if (child != _controller) Undo.DestroyObjectImmediate(child);
                _controller.layers = Array.Empty<AnimatorControllerLayer>();
                _controller.parameters = ControllerParameters(item, context);
                for (var index = 0; index < clips.Count; index++)
                {
                    var pair = clips[index];
                    pair.on.name = "Action " + (index + 1) + (radial ? " Value" : " ON");
                    foreach (var material in pair.materials) AddAsset(material);
                    if (pair.off != null) pair.off.name = "Action " + (index + 1) + " OFF";
                    if (EffectiveMode(item) != MenuBehaviorMode.Puppet) AddAsset(pair.on);
                    if (pair.off != null) AddAsset(pair.off);
                    var machine = new AnimatorStateMachine { name = "Action " + (index + 1) };
                    AddAsset(machine);
                    AnimatorState local = null;
                    if (!pair.synced)
                    {
                        local = machine.AddState("LocalOnly");
                        local.writeDefaultValues = false;
                        Undo.RegisterCreatedObjectUndo(local, "Create local action gate");
                        EditorUtility.SetDirty(local);
                    }
                    if (radial)
                    {
                        var value = machine.AddState("Value");
                        value.motion = EffectiveMode(item) == MenuBehaviorMode.Puppet ?
                            pair.transform != null ? BuildTransformMotion(context, item, pair.transform) : BuildAxisMotion(item, pair.on, pair.axis) : pair.on;
                        value.writeDefaultValues = false;
                        value.timeParameter = Parameter(item);
                        value.timeParameterActive = EffectiveMode(item) == MenuBehaviorMode.Radial;
                        value.speed = EffectiveMode(item) == MenuBehaviorMode.Radial ? 0 : 1;
                        machine.defaultState = value;
                        if (local != null) LocalTransition(local, value);
                        Undo.RegisterCreatedObjectUndo(value, "Create gimmick animation");
                        EditorUtility.SetDirty(value);
                    }
                    else
                    {
                        var off = machine.AddState("OFF");
                        var on = machine.AddState("ON");
                        off.motion = pair.off;
                        on.motion = pair.on;
                        off.writeDefaultValues = on.writeDefaultValues = false;
                        machine.defaultState = InitiallyEnabled(item) ? on : off;
                        var enable = off.AddTransition(on);
                        var disable = on.AddTransition(off);
                        foreach (var transition in new[] { enable, disable })
                        {
                            transition.hasExitTime = false;
                            transition.duration = 0;
                        }
                        enable.AddCondition(AnimatorConditionMode.If, 0, Parameter(item));
                        disable.AddCondition(AnimatorConditionMode.IfNot, 0, Parameter(item));
                        if (local != null)
                        {
                            LocalTransition(local, on).AddCondition(AnimatorConditionMode.If, 0, Parameter(item));
                            LocalTransition(local, off).AddCondition(AnimatorConditionMode.IfNot, 0, Parameter(item));
                        }
                        foreach (var child in new Object[] { off, on, enable, disable })
                        {
                            Undo.RegisterCreatedObjectUndo(child, "Create gimmick animation");
                            EditorUtility.SetDirty(child);
                        }
                    }
                    if (local != null) machine.defaultState = local;
                    EditorUtility.SetDirty(machine);
                    _controller.AddLayer(new AnimatorControllerLayer
                    {
                        name = machine.name, stateMachine = machine, defaultWeight = 1
                    });
                }
                for (var index = 0; index < parameters.Length; index++) BuildParameterLayer(item, parameters[index], index);
                if (clips.Count == 0 && parameters.Length == 0)
                {
                    if (merge != null) Undo.DestroyObjectImmediate(merge);
                }
                else
                {
                    if (merge == null) merge = Undo.AddComponent<ModularAvatarMergeAnimator>(item.gameObject);
                    Undo.RecordObject(merge, "Edit gimmick animation");
                    merge.animator = _controller;
                    merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                    merge.pathMode = MergeAnimatorPathMode.Absolute;
                    merge.matchAvatarWriteDefaults = false;
                    merge.deleteAttachedAnimator = true;
                    merge.mergeAnimatorMode = MergeAnimatorMode.Append;
                    EditorUtility.SetDirty(merge);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(merge);
                }
                EditorUtility.SetDirty(_controller);
                AssetDatabase.SaveAssetIfDirty(_controller);
            }
            finally
            {
                DestroyTemporaryClips(clips);
            }
        }

        internal static void ValidateMaterialValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException(TemplateText.Get("invalidMaterialValue"));
        }

        internal static Renderer ResolveMaterialRenderer(AvatarEditingContext context, MenuMaterialValueTarget target)
        {
            if (target.Path == null || context.Root == null) return null;
            var transform = target.Path.Length == 0 ? context.Root.transform : context.Root.transform.Find(target.Path);
            var renderer = transform != null ? transform.GetComponent<Renderer>() : null;
            return renderer != null ? renderer : null;
        }


        private static void ValidateAxis(ModularAvatarMenuItem item, int axis)
        {
            if (axis < 0 || axis >= AxisCount(item)) throw new InvalidOperationException(TemplateText.Get("incompatibleMode"));
        }

        private Motion BuildAxisMotion(ModularAvatarMenuItem item, AnimationClip source, int axis)
        {
            var tree = new BlendTree
            {
                name = source.name + " Axis", blendType = BlendTreeType.Simple1D,
                blendParameter = AxisParameter(item, axis), useAutomaticThresholds = false
            };
            AddAsset(tree);
            AnimationClip Endpoint(float time)
            {
                var clip = new AnimationClip { name = source.name + (time == 0 ? " Minimum" : " Maximum"), frameRate = 60 };
                foreach (var binding in AnimationUtility.GetCurveBindings(source))
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1,
                        AnimationUtility.GetEditorCurve(source, binding).Evaluate(time * source.length)));
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                    var value = keys.LastOrDefault(key => key.time <= time * source.length).value;
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] { new ObjectReferenceKeyframe { time = 0, value = value } });
                }
                AddAsset(clip);
                return clip;
            }
            tree.children = new[]
            {
                new ChildMotion { motion = Endpoint(0), threshold = SourceMinimum(item), timeScale = 1 },
                new ChildMotion { motion = Endpoint(1), threshold = 1, timeScale = 1 }
            };
            EditorUtility.SetDirty(tree);
            return tree;
        }

        private void ValidateParameters(AvatarEditingContext context, ModularAvatarMenuItem item)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in ParameterActions)
            {
                var type = ParameterType(item, action);
                ValidateAxis(item, action.Axis);
                if (IsContinuous(item) && type != AnimatorControllerParameterType.Float && type != AnimatorControllerParameterType.Int)
                    throw new InvalidOperationException(TemplateText.Get("parameterTypeMismatch"));
                var hasName = !string.IsNullOrEmpty(action.Parameter);
                if (hasName && (string.IsNullOrWhiteSpace(action.Parameter) || action.Parameter.Trim() != action.Parameter ||
                    ExpressionMenuParameterCatalog.IsBuiltIn(action.Parameter) || InputParameters(item).Contains(action.Parameter) || action.Parameter == Parameter(item) ||
                    action.Parameter == Parameter(item) + "/CopyClock"))
                    throw new InvalidOperationException(TemplateText.Get("invalidParameter"));
                if (hasName && !used.Add(action.Parameter)) throw new InvalidOperationException(TemplateText.Get("overlappingParameters"));
                var known = hasName ? ExpressionMenuParameterCatalog.Find(context, _controller, action.Parameter) : null;
                if (known != null && known.Type != type)
                    throw new InvalidOperationException(TemplateText.Get("parameterTypeMismatch"));
                if (type == AnimatorControllerParameterType.Trigger)
                {
                    if (known != null && known.Expression)
                        throw new InvalidOperationException(TemplateText.Get("parameterTypeMismatch"));
                    continue;
                }
                foreach (var value in EffectiveMode(item) == MenuBehaviorMode.Button ? new[] { action.On } : new[] { action.Off, action.On })
                {
                    if (float.IsNaN(value) || float.IsInfinity(value) ||
                        type != AnimatorControllerParameterType.Bool && type != AnimatorControllerParameterType.Int && type != AnimatorControllerParameterType.Float ||
                        type == AnimatorControllerParameterType.Bool && value != 0 && value != 1 ||
                        type == AnimatorControllerParameterType.Int && (value != Math.Floor(value) || value < int.MinValue || value >= 2147483648d) ||
                        known != null && known.Expression && (type == AnimatorControllerParameterType.Float && (value < -1 || value > 1) ||
                            type == AnimatorControllerParameterType.Int && (value < 0 || value > 255)))
                        throw new InvalidOperationException(TemplateText.Get("invalidParameterValue"));
                }
            }
        }

        private void BuildParameterLayer(ModularAvatarMenuItem item, MenuParameterAction action, int index)
        {
            var radial = IsContinuous(item);
            var button = EffectiveMode(item) == MenuBehaviorMode.Button;
            var trigger = ParameterType(item, action) == AnimatorControllerParameterType.Trigger;
            var machine = new AnimatorStateMachine { name = "Parameter " + (index + 1) + " " + action.Parameter };
            AddAsset(machine);
            var off = machine.AddState(radial ? "Value" : "OFF");
            var on = machine.AddState(radial ? "Refresh" : "ON");
            off.writeDefaultValues = on.writeDefaultValues = false;
            AnimatorState local = null;
            if (!action.Synced)
            {
                local = machine.AddState("LocalOnly");
                local.writeDefaultValues = false;
                Undo.RegisterCreatedObjectUndo(local, "Create local parameter gate");
                EditorUtility.SetDirty(local);
            }
            AnimationClip clock = null;
            if (radial)
            {
                // Drivers run on state entry. Alternate timed states to copy the latest input continuously.
                // Keep each state active for at least 0.02s, as recommended by the VRChat SDK.
                clock = new AnimationClip { name = machine.name + " Clock", frameRate = 60 };
                AnimationUtility.SetEditorCurve(clock, EditorCurveBinding.FloatCurve("", typeof(Animator), Parameter(item) + "/CopyClock"),
                    AnimationCurve.Linear(0, 0, 0.02f, 1));
                AddAsset(clock);
            }
            foreach (var state in new[] { off, on })
            {
                state.motion = clock;
                if (!button || state == on)
                {
                    var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    driver.localOnly = !action.Synced;
                    driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
                    {
                        name = action.Parameter,
                        type = radial ? VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy : VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        value = trigger ? 1 : state == on ? action.On : action.Off,
                        source = radial ? AxisParameter(item, action.Axis) : "",
                        convertRange = radial, sourceMin = SourceMinimum(item), sourceMax = 1, destMin = action.Off, destMax = action.On
                    });
                    Undo.RegisterCreatedObjectUndo(driver, "Create parameter driver");
                    EditorUtility.SetDirty(driver);
                }
                Undo.RegisterCreatedObjectUndo(state, "Create parameter action");
                EditorUtility.SetDirty(state);
            }
            var enable = off.AddTransition(on);
            var disable = on.AddTransition(off);
            foreach (var transition in new[] { enable, disable })
            {
                transition.hasExitTime = radial;
                transition.exitTime = 1;
                transition.duration = 0;
                Undo.RegisterCreatedObjectUndo(transition, "Create parameter transition");
                EditorUtility.SetDirty(transition);
            }
            if (!radial)
            {
                enable.AddCondition(AnimatorConditionMode.If, 0, Parameter(item));
                disable.AddCondition(AnimatorConditionMode.IfNot, 0, Parameter(item));
            }
            if (local != null)
            {
                var enter = LocalTransition(local, off);
                if (!radial)
                {
                    enter.AddCondition(AnimatorConditionMode.IfNot, 0, Parameter(item));
                    LocalTransition(local, on).AddCondition(AnimatorConditionMode.If, 0, Parameter(item));
                }
            }
            machine.defaultState = local ?? (!radial && InitiallyEnabled(item) ? on : off);
            EditorUtility.SetDirty(machine);
            _controller.AddLayer(new AnimatorControllerLayer { name = machine.name, stateMachine = machine, defaultWeight = 1 });
        }

        private static void DestroyTemporaryClips(IEnumerable<PreparedAction> clips)
        {
            foreach (var pair in clips)
            {
                if (pair.on != null && !EditorUtility.IsPersistent(pair.on)) DestroyImmediate(pair.on);
                if (pair.off != null && !EditorUtility.IsPersistent(pair.off)) DestroyImmediate(pair.off);
                foreach (var material in pair.materials)
                    if (material != null && !EditorUtility.IsPersistent(material)) DestroyImmediate(material);
            }
        }

        private static AnimatorStateTransition LocalTransition(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            Undo.RegisterCreatedObjectUndo(transition, "Create local action gate");
            EditorUtility.SetDirty(transition);
            return transition;
        }

        private static AnimationClip BuildReactiveClip(AvatarEditingContext context, ModularAvatarMenuItem item, MenuReactiveAction action)
        {
            var clip = new AnimationClip { name = "Action", frameRate = 60 };
            try
            {
                string PathOf(GameObject target, bool rootAllowed = true)
                {
                    if (target == null) throw new InvalidOperationException(TemplateText.Get("missingTarget"));
                    ExpressionMenuTemplateModel.Reference(context, target, rootAllowed);
                    return AnimationUtility.CalculateTransformPath(target.transform, context.Root.transform);
                }
                void Float(GameObject target, Type type, string property, float value, bool rootAllowed = true) =>
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(PathOf(target, rootAllowed), type, property),
                        AnimationCurve.Constant(0, 1f / 60f, value));
                if (action.Kind == MenuTemplateKind.ObjectToggle)
                {
                    foreach (var target in action.Objects)
                    {
                        if (string.IsNullOrEmpty(target.Object?.referencePath)) continue;
                        Float(target.Object.Get(item), typeof(GameObject), "m_IsActive", target.Active ? 1 : 0, false);
                    }
                }
                else if (action.Kind == MenuTemplateKind.MaterialSwap)
                {
                    var root = action.Root?.Get(item);
                    if (root == null) throw new InvalidOperationException(TemplateText.Get("missingTarget"));
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (var index = 0; index < materials.Length; index++)
                        {
                            var swap = action.Swaps.LastOrDefault(entry => entry.From != null && entry.To != null && entry.From == materials[index]);
                            if (swap.To == null) continue;
                            AnimationUtility.SetObjectReferenceCurve(clip,
                                EditorCurveBinding.PPtrCurve(PathOf(renderer.gameObject), renderer.GetType(), "m_Materials.Array.data[" + index + "]"),
                                new[] { new ObjectReferenceKeyframe { time = 0, value = swap.To } });
                        }
                    }
                }
                else foreach (var target in action.Shapes)
                {
                    if (string.IsNullOrEmpty(target.Object?.referencePath) || string.IsNullOrEmpty(target.ShapeName)) continue;
                    var gameObject = target.Object?.Get(item);
                    var renderer = gameObject != null ? gameObject.GetComponent<SkinnedMeshRenderer>() : null;
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(target.ShapeName) < 0)
                        throw new InvalidOperationException(TemplateText.Get("missingShape"));
                    ValidatePercent(target.Value);
                    Float(gameObject, typeof(SkinnedMeshRenderer), "blendShape." + target.ShapeName, target.Value);
                }
                return clip;
            }
            catch { DestroyImmediate(clip); throw; }
        }

        internal static SkinnedMeshRenderer ResolveRenderer(AvatarEditingContext context, MenuRadialShapeTarget target)
        {
            if (target.Path == null) return null;
            var transform = target.Path.Length == 0 ? context.Root.transform : context.Root.transform.Find(target.Path);
            if (transform == null) return null;
            var renderer = transform.GetComponent<SkinnedMeshRenderer>();
            // Unity can return a missing-component wrapper; return an actual null for absent renderers.
            return renderer != null ? renderer : null;
        }

        internal static void ValidatePercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 100)
                throw new InvalidOperationException(TemplateText.Get("invalidValue"));
        }

        private void AddAsset(Object asset)
        {
            AssetDatabase.AddObjectToAsset(asset, _controller);
            Undo.RegisterCreatedObjectUndo(asset, "Create gimmick animation");
            EditorUtility.SetDirty(asset);
        }

        private static EditorCurveBinding[] Bindings(AnimationClip clip) => clip == null ? Array.Empty<EditorCurveBinding>() :
            AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).ToArray();

        private static void ValidateClip(AvatarEditingContext context, AnimationClip clip)
        {
            if (clip == null) return;
            if (clip.legacy || clip.isHumanMotion)
                throw new InvalidOperationException(TemplateText.Get("unsupportedClip"));
            foreach (var binding in Bindings(clip)) ValidateBinding(context, binding, clip.name);
        }

        private static void ValidateBinding(AvatarEditingContext context, EditorCurveBinding binding, string clipName)
        {
            var path = binding.path ?? "";
            var transform = path.Length == 0 ? context.Root.transform : context.Root.transform.Find(path);
            var error = TemplateText.Get("invalidClipTarget") + " " + clipName + ": " +
                (path.Length == 0 ? context.Root.name : path) + " [" + binding.type?.Name + "] " + binding.propertyName;
            if (transform == null || binding.type == null ||
                path.Length == 0 && binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                throw new InvalidOperationException(error);
            ExpressionMenuTemplateModel.Reference(context, transform.gameObject);
            // Native animation access can return a missing-component wrapper. Resolve the component first.
            Object target = binding.type == typeof(GameObject) ? transform.gameObject :
                typeof(Component).IsAssignableFrom(binding.type) ? transform.GetComponent(binding.type) : null;
            if (target == null) throw new InvalidOperationException(error);
            if (target is SkinnedMeshRenderer renderer && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                if (renderer.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11)) < 0)
                    throw new InvalidOperationException(error);
            var readable = binding.isPPtrCurve
                ? AnimationUtility.GetObjectReferenceValue(context.Root, binding, out _)
                : AnimationUtility.GetFloatValue(context.Root, binding, out _);
            if (!readable) throw new InvalidOperationException(error);
        }

        private static AnimationClip CopyClip(AvatarEditingContext context, AnimationClip source,
            EditorCurveBinding[] bindings, string name)
        {
            if (source != null && (source.legacy || source.isHumanMotion))
                throw new InvalidOperationException(TemplateText.Get("unsupportedClip"));
            var clip = source != null ? Instantiate(source) : new AnimationClip();
            clip.name = name;
            try
            {
                foreach (var binding in bindings)
                {
                    ValidateBinding(context, binding, source != null ? source.name : name);
                    if (binding.isPPtrCurve)
                    {
                        if (AnimationUtility.GetObjectReferenceCurve(clip, binding)?.Length > 0) continue;
                        if (!AnimationUtility.GetObjectReferenceValue(context.Root, binding, out var value))
                            throw new InvalidOperationException(TemplateText.Get("invalidClipTarget"));
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
                        {
                            new ObjectReferenceKeyframe { time = 0, value = value }
                        });
                    }
                    else
                    {
                        if (AnimationUtility.GetEditorCurve(clip, binding) != null) continue;
                        if (!AnimationUtility.GetFloatValue(context.Root, binding, out var value))
                            throw new InvalidOperationException(TemplateText.Get("invalidClipTarget"));
                        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1f / 60f, value));
                    }
                }
                return clip;
            }
            catch { DestroyImmediate(clip); throw; }
        }
    }
}
