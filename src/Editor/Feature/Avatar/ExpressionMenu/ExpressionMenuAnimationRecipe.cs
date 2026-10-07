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
    internal enum MenuBehaviorMode { Unselected, Toggle, Radial }

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
    internal sealed class ExpressionMenuAnimationRecipe : ScriptableObject
    {
        [SerializeField] internal List<MenuClipAction> Actions = new List<MenuClipAction>();
        [SerializeField] internal List<MenuRadialShapeAction> RadialShapes = new List<MenuRadialShapeAction>();
        [SerializeField] internal List<MenuReactiveAction> ReactiveActions = new List<MenuReactiveAction>();
        [SerializeField] internal MenuBehaviorMode Mode;
        [SerializeField] internal float InitialValue;
        [SerializeField] private AnimatorController _controller;

        internal static MenuBehaviorMode EffectiveMode(ModularAvatarMenuItem item)
        {
            var recipe = Find(item);
            if (recipe != null && recipe.Mode != MenuBehaviorMode.Unselected) return recipe.Mode;
            if (item.PortableControl.Type == PortableControlType.RadialPuppet) return MenuBehaviorMode.Radial;
            return ExpressionMenuTemplateModel.Effects(item).Length > 0 || recipe != null && recipe.Actions.Count + recipe.ReactiveActions.Count > 0 ?
                MenuBehaviorMode.Toggle : MenuBehaviorMode.Unselected;
        }

        private static string Parameter(ModularAvatarMenuItem item) =>
            !string.IsNullOrEmpty(item.PortableControl.Parameter) ? item.PortableControl.Parameter :
                item.Control.subParameters?.FirstOrDefault()?.name ?? "";

        internal static void SetMode(AvatarEditingContext context, ModularAvatarMenuItem item, MenuBehaviorMode mode)
        {
            var recipe = Find(item);
            if (recipe == null || !ExpressionMenuTemplateModel.IsOwned(item.gameObject))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            if (mode == MenuBehaviorMode.Radial && (ExpressionMenuTemplateModel.Effects(item).Length > 0 || recipe.ReactiveActions.Count > 0) ||
                mode != MenuBehaviorMode.Radial && recipe.RadialShapes.Count > 0 ||
                mode == MenuBehaviorMode.Unselected && (recipe.Actions.Count + recipe.ReactiveActions.Count > 0 || ExpressionMenuTemplateModel.Effects(item).Length > 0))
                throw new InvalidOperationException(TemplateText.Get("incompatibleMode"));
            Change(context, item, () =>
            {
                var parameter = Parameter(item);
                Undo.RecordObject(item, "Change behavior type");
                recipe.Mode = mode;
                item.PortableControl.Type = mode == MenuBehaviorMode.Radial ? PortableControlType.RadialPuppet : PortableControlType.Toggle;
                item.PortableControl.Parameter = mode == MenuBehaviorMode.Radial ? "" : parameter;
                item.Control.subParameters = mode == MenuBehaviorMode.Radial ? new[]
                {
                    new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter { name = parameter }
                } : Array.Empty<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter>();
                item.PortableControl.Value = mode == MenuBehaviorMode.Radial ? 0 : 1;
                EditorUtility.SetDirty(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
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
            if (EffectiveMode(item) != MenuBehaviorMode.Radial) foreach (var layer in controller.layers)
            {
                var machine = layer.stateMachine;
                Undo.RecordObject(machine, "Edit initial state");
                machine.defaultState = machine.states.Select(state => state.state).FirstOrDefault(state => state.name == "LocalOnly") ??
                    machine.states.Select(state => state.state)
                    .FirstOrDefault(state => state.name == (item.isDefault ? "ON" : "OFF"));
                EditorUtility.SetDirty(machine);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
        }

        private AnimatorControllerParameter ControllerParameter(ModularAvatarMenuItem item) => new AnimatorControllerParameter
        {
            name = Parameter(item),
            type = EffectiveMode(item) == MenuBehaviorMode.Radial ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Bool,
            defaultBool = item.isDefault,
            defaultFloat = InitialValue / 100f
        };

        private AnimatorControllerParameter[] ControllerParameters(ModularAvatarMenuItem item) =>
            new[] { ControllerParameter(item), new AnimatorControllerParameter { name = "IsLocal", type = AnimatorControllerParameterType.Bool } };

        private void ImportEffects(AvatarEditingContext context, ModularAvatarMenuItem item)
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
                Undo.DestroyObjectImmediate(effect);
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
            var radial = EffectiveMode(item) == MenuBehaviorMode.Radial;
            var synced = Actions.Any(action => action.Synced) || RadialShapes.Any(action => action.Synced) ||
                ReactiveActions.Any(action => action.Synced) || ExpressionMenuTemplateModel.Effects(item).Length > 0 && item.isSynced;
            Undo.RecordObject(item, "Edit action synchronization");
            item.isSynced = synced;
            EditorUtility.SetDirty(item);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            if (!radial && component == null) return;
            if (component == null) component = Undo.AddComponent<ModularAvatarParameters>(item.gameObject);
            Undo.RecordObject(component, "Edit behavior parameter");
            component.parameters.RemoveAll(config => !config.isPrefix && config.nameOrPrefix == parameter);
            if (radial) component.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = parameter, syncType = ParameterSyncType.Float, localOnly = !item.isSynced,
                saved = item.isSaved, hasExplicitDefaultValue = true, defaultValue = InitialValue / 100f
            });
            if (component.parameters.Count == 0) Undo.DestroyObjectImmediate(component);
            else
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        private void Build(AvatarEditingContext context, ModularAvatarMenuItem item)
        {
            var clips = new List<(AnimationClip on, AnimationClip off, bool synced)>();
            var used = new HashSet<(string, Type, string)>();
            var radial = EffectiveMode(item) == MenuBehaviorMode.Radial;
            try
            {
                foreach (var action in Actions)
                {
                    if (action.On == null) continue;
                    var bindings = Bindings(action.On).Concat(radial ? Array.Empty<EditorCurveBinding>() : Bindings(action.Off)).Distinct().ToArray();
                    foreach (var binding in bindings)
                    {
                        if (!used.Add((binding.path, binding.type, binding.propertyName)))
                            throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
                        var target = AnimationUtility.GetAnimatedObject(context.Root, binding);
                        if (target == null ||
                            binding.path == "" && binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                            throw new InvalidOperationException(TemplateText.Get("invalidClipTarget") + " " + binding.path + "/" + binding.propertyName);
                        var gameObject = target is GameObject go ? go : (target as Component)?.gameObject;
                        if (gameObject != null) ExpressionMenuTemplateModel.Reference(context, gameObject);
                    }
                    var on = CopyClip(context, action.On, bindings, "ON");
                    clips.Add((on, null, action.Synced));
                    if (radial)
                    {
                        var settings = AnimationUtility.GetAnimationClipSettings(on);
                        settings.loopTime = false;
                        AnimationUtility.SetAnimationClipSettings(on, settings);
                        continue;
                    }
                    var off = CopyClip(context, action.Off, bindings, "OFF");
                    clips[clips.Count - 1] = (on, off, action.Synced);
                }
                if (radial) foreach (var action in RadialShapes)
                {
                    var clip = new AnimationClip { name = "BlendShapes", frameRate = 60 };
                    clips.Add((clip, null, action.Synced));
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
                foreach (var action in ReactiveActions)
                {
                    var on = BuildReactiveClip(context, item, action);
                    clips.Add((on, null, action.Synced));
                    var bindings = Bindings(on);
                    foreach (var binding in bindings)
                        if (!used.Add((binding.path, binding.type, binding.propertyName)))
                            throw new InvalidOperationException(TemplateText.Get("overlappingClips"));
                    var off = CopyClip(context, null, bindings, "OFF");
                    clips[clips.Count - 1] = action.Inverted ? (off, on, action.Synced) : (on, off, action.Synced);
                }
                ConfigureParameters(item);
                var merge = item.GetComponents<ModularAvatarMergeAnimator>().FirstOrDefault(component =>
                    _controller != null && component.animator == _controller);
                if (merge != null && !ExpressionMenuTemplateModel.CanEdit(context, merge))
                    throw new InvalidOperationException(TemplateText.Get("readOnly"));
                if (clips.Count == 0 && _controller == null) return;
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
                _controller.parameters = ControllerParameters(item);
                for (var index = 0; index < clips.Count; index++)
                {
                    var pair = clips[index];
                    pair.on.name = "Action " + (index + 1) + (radial ? " Value" : " ON");
                    if (pair.off != null) pair.off.name = "Action " + (index + 1) + " OFF";
                    AddAsset(pair.on);
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
                        value.motion = pair.on;
                        value.writeDefaultValues = false;
                        value.timeParameter = Parameter(item);
                        value.timeParameterActive = true;
                        value.speed = 0;
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
                        machine.defaultState = item.isDefault ? on : off;
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
                if (clips.Count == 0)
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
                foreach (var pair in clips)
                {
                    if (pair.on != null && !EditorUtility.IsPersistent(pair.on)) DestroyImmediate(pair.on);
                    if (pair.off != null && !EditorUtility.IsPersistent(pair.off)) DestroyImmediate(pair.off);
                }
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
                    var gameObject = target.Object?.Get(item);
                    var renderer = gameObject?.GetComponent<SkinnedMeshRenderer>();
                    if (renderer?.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(target.ShapeName) < 0)
                        throw new InvalidOperationException(TemplateText.Get("missingShape"));
                    ValidatePercent(target.Value);
                    Float(gameObject, typeof(SkinnedMeshRenderer), "blendShape." + target.ShapeName, target.Value);
                }
                return clip;
            }
            catch { DestroyImmediate(clip); throw; }
        }

        internal static SkinnedMeshRenderer ResolveRenderer(AvatarEditingContext context, MenuRadialShapeTarget target) =>
            target.Path == null ? null : (target.Path.Length == 0 ? context.Root.transform :
                context.Root.transform.Find(target.Path))?.GetComponent<SkinnedMeshRenderer>();

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
