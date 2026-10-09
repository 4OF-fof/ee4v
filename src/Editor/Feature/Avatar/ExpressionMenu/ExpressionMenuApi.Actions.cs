using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace Ee4v.ExpressionMenu
{
    public static partial class ExpressionMenuApi
    {
        [Serializable] public sealed class AssetReference
        {
            public string Path;
            public string LocalId;
        }
        [Serializable] public sealed class ActionData
        {
            public string Kind;
            public bool Synced = true, Inverted;
            public int Axis, HorizontalAxis, VerticalAxis = 1;
            public string RootPath = "", Parameter, ParameterType = "Float";
            public float Off, On = 1;
            public AssetReference OnClip, OffClip;
            public List<Target> Targets = new List<Target>();
        }
        [Serializable] public sealed class Target
        {
            public string Path, Shape, Property, ComponentType;
            public bool Active = true, Enabled = true, Position = true, Rotation, Scale;
            public float Minimum, Maximum = 100;
            public float[] MinimumVector, MaximumVector;
            public float[] MinimumPosition, MaximumPosition, MinimumRotation, MaximumRotation, MinimumScale, MaximumScale;
            public AssetReference FromMaterial, ToMaterial, Texture, Shader;
        }

        public static Snapshot ReplaceActions(GameObject avatar, string path, IReadOnlyList<ActionData> actions, string expectedRevision, bool dryRun = false)
        {
            CheckRevision(avatar, expectedRevision);
            var context = EditableContext(avatar);
            var item = OwnedItem(EntryAt(ExpressionMenuModel.Read(avatar, out _), path));
            if (!ExpressionMenuTemplateModel.CanEdit(context, item)) throw new InvalidOperationException("Menu item is read-only.");
            if (actions == null || actions.Any(action => action == null || action.Targets == null || action.Targets.Any(target => target == null)))
                throw new ArgumentException("A complete action list with non-null targets is required.");
            var existingClips = ExpressionMenuAnimationRecipe.Find(item)?.Actions.Count ?? 0;
            if (actions.Count(action => action.Kind == "Clip") > existingClips)
                throw new InvalidOperationException("New Clip actions are not supported; existing Clip actions may be edited or removed.");
            var available = ExpressionMenuTemplateModel.AvailableActions(item).Select(kind => kind.ToString()).ToArray();
            var existing = ReadActions(context, item, ExpressionMenuAnimationRecipe.Find(item)).Select(action => action.Kind).ToArray();
            if (actions.Any(action => action.Kind != "Clip" && !available.Contains(action.Kind) && !existing.Contains(action.Kind)))
                throw new ArgumentException("Action is incompatible with this control type.");
            Action<ExpressionMenuAnimationRecipe> update = recipe => ApplyActions(context, item, recipe, actions);
            ExpressionMenuAnimationRecipe.ValidateProposal(context, item, update);
            if (!dryRun)
            {
                ExpressionMenuAnimationRecipe.ApplyProposal(context, item, update);
                Notify(avatar);
            }
            return Inspect(avatar);
        }

        private static List<ActionData> ReadActions(AvatarEditingContext context, ModularAvatarMenuItem item, ExpressionMenuAnimationRecipe recipe)
        {
            var result = new List<ActionData>();
            if (item == null) return result;
            string Path(AvatarObjectReference reference)
            {
                var obj = reference?.Get(item);
                return obj == null ? null : AnimationUtility.CalculateTransformPath(obj.transform, context.Root.transform);
            }
            string ObjectPath(GameObject obj) => obj == null ? null : obj.transform.IsChildOf(context.Root.transform) ?
                AnimationUtility.CalculateTransformPath(obj.transform, context.Root.transform) : "#outside:" + GlobalObjectId.GetGlobalObjectIdSlow(obj);
            ActionData Reactive(MenuReactiveAction action) => new ActionData
            {
                Kind = action.Kind.ToString(), Synced = action.Synced, Inverted = action.Inverted, RootPath = Path(action.Root),
                Targets = action.Kind == MenuTemplateKind.ObjectToggle ? action.Objects.Select(value => new Target { Path = Path(value.Object), Active = value.Active }).ToList() :
                    action.Kind == MenuTemplateKind.MaterialSwap ? action.Swaps.Select(value => new Target { FromMaterial = Ref(value.From), ToMaterial = Ref(value.To) }).ToList() :
                    action.Shapes.Select(value => new Target { Path = Path(value.Object), Shape = value.ShapeName, Maximum = value.Value }).ToList()
            };
            if (recipe != null)
            {
                result.AddRange(recipe.ReactiveActions.Select(Reactive));
                result.AddRange(recipe.RadialShapes.Select(action => new ActionData { Kind = "ShapeChanger", Synced = action.Synced, Axis = action.Axis,
                    Targets = action.Targets.Select(target => new Target { Path = target.Path, Shape = target.Shape, Minimum = target.Minimum, Maximum = target.Maximum }).ToList() }));
                result.AddRange(recipe.ParameterActions.Select(action => new ActionData { Kind = "ParameterValue", Synced = action.Synced,
                    Axis = action.Axis, Parameter = action.Parameter, ParameterType = ExpressionMenuAnimationRecipe.ParameterType(item, action).ToString(), Off = action.Off, On = action.On }));
                result.AddRange(recipe.MaterialValues.Select(action => new ActionData { Kind = "MaterialValue", Synced = action.Synced,
                    Targets = action.Targets.Select(target => new Target { Path = target.Path, Property = target.Property, Minimum = target.Minimum, Maximum = target.Maximum,
                        MinimumVector = Floats(target.MinimumVector), MaximumVector = Floats(target.MaximumVector), Texture = Ref(target.Texture), Shader = Ref(target.Shader) }).ToList() }));
                result.AddRange(recipe.Transforms.Select(action => new ActionData { Kind = "Transform", Synced = action.Synced, Axis = action.SourceAxis,
                    HorizontalAxis = action.HorizontalAxis, VerticalAxis = action.VerticalAxis,
                    Targets = action.Targets.Select(target => new Target { Path = target.Path, Position = target.Position, Rotation = target.Rotation, Scale = target.Scale,
                        MinimumPosition = Floats(target.MinimumPosition), MaximumPosition = Floats(target.MaximumPosition), MinimumRotation = Floats(target.MinimumRotation), MaximumRotation = Floats(target.MaximumRotation),
                        MinimumScale = Floats(target.MinimumScale), MaximumScale = Floats(target.MaximumScale) }).ToList() }));
                result.AddRange(recipe.Components.Select(action => new ActionData { Kind = "Component", Synced = action.Synced,
                    Targets = action.Targets.Select(target => new Target { Path = target.Path, ComponentType = target.Type, Enabled = target.Enabled }).ToList() }));
                result.AddRange(recipe.Actions.Select(action => new ActionData { Kind = "Clip", Synced = action.Synced, OnClip = Ref(action.On), OffClip = Ref(action.Off) }));
            }
            // Existing external MA settings are readable without converting or deleting their components.
            foreach (var effect in ExpressionMenuTemplateModel.Effects(item))
            {
                if (effect is ModularAvatarObjectToggle toggle)
                    result.Add(new ActionData { Kind = "ObjectToggle", Synced = item.isSynced, Inverted = effect.Inverted,
                        Targets = toggle.Objects.Select(value => new Target { Path = ObjectPath(value.Object?.Get(effect)), Active = value.Active }).ToList() });
                else if (effect is ModularAvatarMaterialSwap swap)
                    result.Add(new ActionData { Kind = "MaterialSwap", Synced = item.isSynced, Inverted = effect.Inverted, RootPath = ObjectPath(swap.Root?.Get(effect) ?? context.Root),
                        Targets = swap.Swaps.Select(value => new Target { FromMaterial = Ref(value.From), ToMaterial = Ref(value.To) }).ToList() });
                else if (effect is ModularAvatarShapeChanger shape)
                    result.Add(new ActionData { Kind = "ShapeChanger", Synced = item.isSynced, Inverted = effect.Inverted,
                        Targets = shape.Shapes.Select(value => new Target { Path = ObjectPath(value.Object?.Get(effect)), Shape = value.ShapeName, Maximum = value.Value }).ToList() });
            }
            return result;
        }

        private static void ApplyActions(AvatarEditingContext context, ModularAvatarMenuItem item, ExpressionMenuAnimationRecipe recipe, IReadOnlyList<ActionData> actions)
        {
            recipe.Actions.Clear(); recipe.ReactiveActions.Clear(); recipe.RadialShapes.Clear(); recipe.ParameterActions.Clear();
            recipe.MaterialValues.Clear(); recipe.Transforms.Clear(); recipe.Components.Clear();
            foreach (var action in actions)
            {
                switch (action.Kind)
                {
                    case "ObjectToggle": case "MaterialSwap": case "ShapeChanger" when !ExpressionMenuAnimationRecipe.IsContinuous(item):
                        var reactive = new MenuReactiveAction { Kind = (MenuTemplateKind)Enum.Parse(typeof(MenuTemplateKind), action.Kind), Synced = action.Synced,
                            Inverted = action.Inverted, Root = Reference(context, action.RootPath ?? "") };
                        foreach (var target in action.Targets)
                        {
                            if (action.Kind == "ObjectToggle") reactive.Objects.Add(new ToggledObject { Object = Reference(context, target.Path, false), Active = target.Active });
                            else if (action.Kind == "MaterialSwap") reactive.Swaps.Add(new MatSwap { From = Asset<Material>(target.FromMaterial), To = Asset<Material>(target.ToMaterial) });
                            else reactive.Shapes.Add(new ChangedShape { Object = Reference(context, target.Path), ShapeName = target.Shape, Value = target.Maximum, ChangeType = ShapeChangeType.Set });
                        }
                        recipe.ReactiveActions.Add(reactive);
                        break;
                    case "ShapeChanger":
                        recipe.RadialShapes.Add(new MenuRadialShapeAction { Synced = action.Synced, Axis = action.Axis,
                            Targets = action.Targets.Select(target => new MenuRadialShapeTarget { Path = ValidPath(context, target.Path), Shape = target.Shape, Minimum = target.Minimum, Maximum = target.Maximum }).ToList() });
                        break;
                    case "ParameterValue":
                        if (!Enum.TryParse(action.ParameterType, false, out AnimatorControllerParameterType parameterType)) throw new ArgumentException("Invalid parameter type.");
                        recipe.ParameterActions.Add(new MenuParameterAction { Synced = action.Synced, Parameter = action.Parameter, Axis = action.Axis,
                            NumericType = parameterType, ButtonType = parameterType, Off = action.Off, On = action.On });
                        break;
                    case "MaterialValue":
                        recipe.MaterialValues.Add(new MenuMaterialValueAction { Synced = action.Synced,
                            Targets = action.Targets.Select(target => new MenuMaterialValueTarget { Path = ValidPath(context, target.Path), Property = target.Property,
                                Minimum = target.Minimum, Maximum = target.Maximum, MinimumVector = Vector4Value(target.MinimumVector), MaximumVector = Vector4Value(target.MaximumVector),
                                Texture = Asset<Texture>(target.Texture), Shader = Asset<Shader>(target.Shader) }).ToList() });
                        break;
                    case "Transform":
                        recipe.Transforms.Add(new MenuTransformAction { Synced = action.Synced, SourceAxis = action.Axis, HorizontalAxis = action.HorizontalAxis, VerticalAxis = action.VerticalAxis,
                            Targets = action.Targets.Select(target => new MenuTransformTarget { Path = ValidPath(context, target.Path), Position = target.Position, Rotation = target.Rotation, Scale = target.Scale,
                                MinimumPosition = Vector3Value(target.MinimumPosition, Vector3.zero), MaximumPosition = Vector3Value(target.MaximumPosition, Vector3.zero),
                                MinimumRotation = Vector3Value(target.MinimumRotation, Vector3.zero), MaximumRotation = Vector3Value(target.MaximumRotation, Vector3.zero),
                                MinimumScale = Vector3Value(target.MinimumScale, Vector3.one), MaximumScale = Vector3Value(target.MaximumScale, Vector3.one) }).ToList() });
                        break;
                    case "Component":
                        recipe.Components.Add(new MenuComponentAction { Synced = action.Synced,
                            Targets = action.Targets.Select(target => new MenuComponentTarget { Path = ValidPath(context, target.Path), Type = target.ComponentType, Enabled = target.Enabled }).ToList() });
                        break;
                    case "Clip":
                        recipe.Actions.Add(new MenuClipAction { Synced = action.Synced, On = Asset<AnimationClip>(action.OnClip), Off = Asset<AnimationClip>(action.OffClip) });
                        break;
                    default: throw new ArgumentException("Unknown menu action: " + action.Kind);
                }
            }
        }

        private static string ValidPath(AvatarEditingContext context, string path)
        {
            if (path == null) return null; // Preserve an unconfigured target row.
            var transform = path.Length == 0 ? context.Root.transform : context.Root.transform.Find(path);
            if (transform == null) throw new ArgumentException("Avatar target not found: " + path);
            ExpressionMenuTemplateModel.Reference(context, transform.gameObject);
            return path;
        }
        private static AvatarObjectReference Reference(AvatarEditingContext context, string path, bool allowRoot = true)
        {
            ValidPath(context, path);
            return ExpressionMenuTemplateModel.Reference(context, path == null ? null : path.Length == 0 ? context.Root : context.Root.transform.Find(path).gameObject, allowRoot);
        }
        private static AssetReference Ref(UnityEngine.Object asset)
        {
            if (asset == null) return null;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id);
            return new AssetReference { Path = AssetDatabase.GetAssetPath(asset), LocalId = id.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }
        private static T Asset<T>(AssetReference reference) where T : UnityEngine.Object
        {
            if (reference == null || string.IsNullOrEmpty(reference.Path)) return null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(reference.Path).OfType<T>().ToArray();
            if (string.IsNullOrEmpty(reference.LocalId)) return assets.Length == 1 ? assets[0] : throw new ArgumentException("Specify a local ID for ambiguous assets: " + reference.Path);
            return assets.SingleOrDefault(value => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string _, out long id); return id.ToString(System.Globalization.CultureInfo.InvariantCulture) == reference.LocalId; })
                ?? throw new ArgumentException("Asset reference not found: " + reference.Path);
        }
        private static float[] Floats(Vector3 value) => new[] { value.x, value.y, value.z };
        private static float[] Floats(Vector4 value) => new[] { value.x, value.y, value.z, value.w };
        private static Vector3 Vector3Value(float[] value, Vector3 fallback)
        {
            if (value == null) return fallback;
            ValidateVector(value, 3);
            return new Vector3(value[0], value[1], value[2]);
        }
        private static Vector4 Vector4Value(float[] value)
        {
            if (value == null) return Vector4.zero;
            ValidateVector(value, 4);
            return new Vector4(value[0], value[1], value[2], value[3]);
        }
        private static void ValidateVector(float[] value, int count)
        {
            if (value.Length != count || value.Any(number => float.IsNaN(number) || float.IsInfinity(number))) throw new ArgumentException("Invalid vector.");
        }
    }

    internal sealed partial class ExpressionMenuAnimationRecipe
    {
        internal static void ApplyProposal(AvatarEditingContext context, ModularAvatarMenuItem item, Action<ExpressionMenuAnimationRecipe> update)
        {
            var recipe = Ensure(item);
            Change(context, item, () =>
            {
                // Convert old reactive components in the same Undo transaction, then replace their imported settings.
                recipe.ImportEffects(context, item);
                update(recipe);
            });
        }

        internal static void ValidateProposal(AvatarEditingContext context, ModularAvatarMenuItem item, Action<ExpressionMenuAnimationRecipe> update)
        {
            var source = Find(item);
            var copy = source != null ? Instantiate(source) : CreateInstance<ExpressionMenuAnimationRecipe>();
            copy.Mode = EffectiveMode(item);
            try
            {
                copy.ImportEffects(context, item, removeOriginal: false);
                update(copy);
                copy.ValidateParameters(context, item);
                var clips = copy.PrepareClips(context, item);
                DestroyTemporaryClips(clips);
            }
            finally { DestroyImmediate(copy); }
        }
    }
}
