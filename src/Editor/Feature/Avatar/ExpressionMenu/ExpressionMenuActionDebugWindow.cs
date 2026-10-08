using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal sealed class ExpressionMenuActionDebugWindow : EditorWindow
    {
        private Scene _scene;
        private GameObject _root;
        private AvatarEditingContext _context;
        private readonly List<Object> _resources = new List<Object>();

        [MenuItem("ee4v/Debug/Expression Menu Action Cards", false, 1120)]
        internal static void Open() => GetWindow<ExpressionMenuActionDebugWindow>().Show();

        private void CreateGUI()
        {
            titleContent = UiTextFactory.CreateGuiContent("Expression Menu Action Cards");
            minSize = new Vector2(720, 480);
            Rebuild();
        }

        private T Own<T>(T value) where T : Object
        {
            value.hideFlags = HideFlags.HideAndDontSave;
            _resources.Add(value);
            return value;
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();
            DisposeSamples();
            UiComposition.Prepare(rootVisualElement, "Editor/Feature/Avatar/ExpressionMenu/expression-menu.uss");
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = toolbar.style.paddingRight = 12;
            toolbar.style.paddingTop = toolbar.style.paddingBottom = 8;
            var caption = UiTextFactory.Create("動作カード一覧 · 表示確認専用（アバター・アセットへの保存なし）", UiClassNames.SecondaryText);
            caption.style.flexGrow = 1;
            toolbar.Add(caption);
            toolbar.Add(new UiButton("再生成", Rebuild));
            rootVisualElement.Add(toolbar);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rootVisualElement.Add(scroll);
            var grid = new VisualElement { name = "actionDebugGrid" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.alignItems = Align.FlexStart;
            grid.style.paddingLeft = grid.style.paddingRight = 6;
            scroll.Add(grid);
            try
            {
                CreateSamples();
                foreach (var type in new[] { PortableControlType.Toggle, PortableControlType.RadialPuppet,
                    PortableControlType.Button, PortableControlType.TwoAxisPuppet, PortableControlType.FourAxisPuppet })
                    AddColumn(grid, type);
            }
            catch (Exception exception)
            {
                grid.Add(UiTextFactory.CreateHelpBox(exception.GetBaseException().Message, HelpBoxMessageType.Error));
                DisposeSamples();
            }
        }

        private void CreateSamples()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _root = new GameObject("Action Card Samples") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(_root, _scene);
            var descriptor = _root.AddComponent<VRCAvatarDescriptor>();
            descriptor.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            descriptor.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            _context = new AvatarEditingContext(new AvatarEditingServices(() => false, _ => false, () => true,
                () => "", _ => { }, () => { }, _ => { }, _ => { }),
                new AvatarEditingHost(() => { }, () => { }, () => { }, Repaint, () => { })) { Root = _root };
            var controller = Own(new AnimatorController { name = "Sample Parameters" });
            controller.parameters = new[]
            {
                new AnimatorControllerParameter { name = "SampleBool", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "SampleInt", type = AnimatorControllerParameterType.Int },
                new AnimatorControllerParameter { name = "SampleFloat", type = AnimatorControllerParameterType.Float },
                new AnimatorControllerParameter { name = "SampleTrigger", type = AnimatorControllerParameterType.Trigger }
            };
            _root.AddComponent<ModularAvatarMergeAnimator>().animator = controller;
            var target = new GameObject("Target");
            target.transform.SetParent(_root.transform, false);
            var mesh = Own(new Mesh { name = "Sample BlendShapes" });
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.AddBlendShapeFrame("SampleShape", 100, new[] { Vector3.up, Vector3.up, Vector3.up }, new Vector3[3], new Vector3[3]);
            var renderer = target.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            var shader = Own(ShaderUtil.CreateShaderAsset(@"Shader ""Hidden/ee4v/ActionCardSamples"" {
                Properties {
                    _Float (""Float"", Float) = 0.25
                    _Range (""Range"", Range(0,1)) = 0.5
                    _Integer (""Integer"", Integer) = 2
                    [HDR] _Color (""Color"", Color) = (0.3,0.6,0.9,1)
                    _Vector (""Vector"", Vector) = (1,2,3,4)
                    _Tex (""Texture"", 2D) = ""white"" {}
                }
                SubShader { Pass {
                    CGPROGRAM
                    #pragma vertex vert
                    #pragma fragment frag
                    #pragma shader_feature_local _SAMPLE_KEYWORD
                    float4 vert(float4 vertex:POSITION):SV_POSITION { return vertex; }
                    float4 frag():SV_Target { return float4(1,1,1,1); }
                    ENDCG
                } }
            }", false));
            renderer.sharedMaterial = Own(new Material(shader) { name = "Sample Material" });
            target.AddComponent<Light>();
        }

        private void AddColumn(VisualElement grid, PortableControlType type)
        {
            var column = new VisualElement();
            column.style.flexBasis = 640;
            column.style.flexGrow = 1;
            column.style.minWidth = 0;
            column.style.marginLeft = column.style.marginRight = 6;
            column.style.marginBottom = 16;
            grid.Add(column);
            var title = type == PortableControlType.Toggle ? "Toggle" : type == PortableControlType.RadialPuppet ? "数値（0〜100）" :
                type == PortableControlType.Button ? "Button" : type == PortableControlType.TwoAxisPuppet ? "Puppet · 2 Axis" : "Puppet · 4 Axis";
            column.Add(new SectionHeader(title));
            var gameObject = new GameObject(title);
            gameObject.transform.SetParent(_root.transform, false);
            var item = gameObject.AddComponent<ModularAvatarMenuItem>();
            item.PortableControl.Type = type;
            item.PortableControl.Parameter = "DebugMenu";
            var recipe = Own(CreateInstance<ExpressionMenuAnimationRecipe>());
            var renderer = _root.transform.Find("Target").GetComponent<SkinnedMeshRenderer>();
            var reference = new AvatarObjectReference { referencePath = "Target" };
            var radial = type == PortableControlType.RadialPuppet;
            var puppet = type == PortableControlType.TwoAxisPuppet || type == PortableControlType.FourAxisPuppet;
            if (type == PortableControlType.Toggle)
            {
                recipe.ReactiveActions.Add(new MenuReactiveAction { Kind = MenuTemplateKind.ObjectToggle,
                    Objects = { new ToggledObject { Object = reference, Active = true } } });
                recipe.ReactiveActions.Add(new MenuReactiveAction { Kind = MenuTemplateKind.MaterialSwap,
                    Swaps = { new MatSwap { From = renderer.sharedMaterial, To = Own(new Material(renderer.sharedMaterial) { name = "Replacement Material" }) } } });
                recipe.ReactiveActions.Add(new MenuReactiveAction { Kind = MenuTemplateKind.ShapeChanger,
                    Shapes = { new ChangedShape { Object = reference, ShapeName = "SampleShape", Value = 75, ChangeType = ShapeChangeType.Set } } });
                recipe.ParameterActions.Add(new MenuParameterAction { Parameter = "SampleBool" });
                recipe.Components.Add(new MenuComponentAction { Targets = { new MenuComponentTarget
                    { Path = "Target", Type = typeof(Light).AssemblyQualifiedName, Enabled = false } } });
            }
            if (radial)
            {
                recipe.RadialShapes.Add(new MenuRadialShapeAction { Targets = { new MenuRadialShapeTarget
                    { Path = "Target", Shape = "SampleShape", Minimum = 0, Maximum = 75 } } });
                foreach (var numeric in new[] { AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int })
                    recipe.ParameterActions.Add(new MenuParameterAction { Parameter = "Sample" + numeric,
                        NumericType = numeric, Off = 0, On = numeric == AnimatorControllerParameterType.Int ? 10 : 1 });
            }
            if (type == PortableControlType.Button)
                foreach (var parameterType in new[] { AnimatorControllerParameterType.Trigger, AnimatorControllerParameterType.Bool,
                    AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float })
                    recipe.ParameterActions.Add(new MenuParameterAction { Parameter = "Sample" + parameterType,
                        ButtonType = parameterType, On = parameterType == AnimatorControllerParameterType.Int ? 10 : 1 });
            if (radial || type == PortableControlType.Toggle)
            {
                foreach (var property in ExpressionMenuAnimationRecipe.MaterialProperties(renderer)
                    .Where(property => !radial || property.Animated).GroupBy(property => property.Kind).Select(group => group.First()))
                {
                    var current = ExpressionMenuAnimationRecipe.MaterialCurrentVector(_context,
                        new MenuMaterialValueTarget { Path = "Target", Property = property.Name });
                    recipe.MaterialValues.Add(new MenuMaterialValueAction { Targets = { new MenuMaterialValueTarget
                    {
                        Path = "Target", Property = property.Name, Minimum = current.x, Maximum = property.Boolean ? 1 : current.x + 1,
                        MinimumVector = current, MaximumVector = current + Vector4.one,
                        Shader = renderer.sharedMaterial.shader, Texture = Texture2D.whiteTexture
                    } } });
                }
            }
            if (radial || puppet || type == PortableControlType.Toggle)
            {
                var transform = new MenuTransformTarget { Path = "Target", Position = true, Rotation = true, Scale = true,
                    MinimumPosition = Vector3.zero, MaximumPosition = Vector3.one,
                    MinimumRotation = Vector3.zero, MaximumRotation = new Vector3(30, 60, 90),
                    MinimumScale = Vector3.one, MaximumScale = Vector3.one * 2 };
                recipe.Transforms.Add(new MenuTransformAction { SourceAxis = 1, HorizontalAxis = 0, VerticalAxis = 2,
                    Targets = { transform } });
            }
            if (radial || type == PortableControlType.Toggle)
                recipe.Actions.Add(new MenuClipAction { On = Own(new AnimationClip { name = "Existing Animation Clip" }) });
            column.Add(new ExpressionMenuTemplateEditor(_context, item, recipe));
        }

        private void OnDisable()
        {
            rootVisualElement.Clear();
            DisposeSamples();
        }

        private void DisposeSamples()
        {
            if (_root != null) DestroyImmediate(_root);
            _root = null;
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _scene = default;
            foreach (var resource in _resources) if (resource != null) DestroyImmediate(resource);
            _resources.Clear();
            _context = null;
        }
    }

    internal sealed partial class ExpressionMenuTemplateEditor
    {
        internal ExpressionMenuTemplateEditor(AvatarEditingContext context, ModularAvatarMenuItem item, ExpressionMenuAnimationRecipe recipe)
        {
            _context = context;
            _item = item;
            _typeChanged = () => { };
            _feedback = UiTextFactory.CreateHelpBox("", HelpBoxMessageType.Error);
            _fields = new VisualElement();
            Add(_fields);
            for (var index = 0; index < recipe.ReactiveActions.Count; index++) BuildReactive(_fields, recipe, index);
            for (var index = 0; index < recipe.RadialShapes.Count; index++) BuildRadialShapes(_fields, recipe, index);
            for (var index = 0; index < recipe.ParameterActions.Count; index++) BuildParameter(_fields, recipe, index);
            for (var index = 0; index < recipe.MaterialValues.Count; index++) BuildMaterialValues(_fields, recipe, index);
            for (var index = 0; index < recipe.Transforms.Count; index++) BuildTransforms(_fields, recipe, index);
            for (var index = 0; index < recipe.Components.Count; index++) BuildComponents(_fields, recipe, index);
            for (var index = 0; index < recipe.Actions.Count; index++) BuildAnimation(_fields, recipe, index);
            foreach (var card in _fields.Children())
            {
                card.SetEnabled(true);
                if (card.childCount > 1 && card.ElementAt(1) is Toggle sync) sync.SetEnabled(true);
            }
            void Freeze(VisualElement element)
            {
                element.pickingMode = PickingMode.Ignore;
                element.focusable = false;
                element.tabIndex = -1;
                foreach (var child in element.Children()) Freeze(child);
            }
            Freeze(this);
        }
    }
}
