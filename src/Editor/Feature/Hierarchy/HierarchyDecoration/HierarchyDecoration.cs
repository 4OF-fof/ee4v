using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.HierarchyDecoration
{
    [InitializeOnLoad]
    internal static class HierarchyDecoration
    {
        private const string RegistrationId =
            "hierarchy-decoration.renderer";
        private const float DividerLeftInset = 32f;

        private static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "hierarchyDecoration.enabled",
                SettingScope.User,
                "HierarchyDecoration",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 20,
                keywords: new[]
                {
                    "hierarchy",
                    "decoration",
                    "separator"
                });

        internal static readonly HierarchyDecorationTarget DividerTarget =
            new HierarchyDecorationTarget(
                namePrefix: "---",
                isEmpty: true,
                hasChildren: false,
                hasParent: false);

        private static readonly IReadOnlyList<HierarchyDecorationDefinition>
            Definitions = new[]
            {
                new HierarchyDecorationDefinition(
                    "divider",
                    DividerTarget,
                    new HierarchyDecorationStyle(DrawDivider))
            };

        private static readonly List<Component> ComponentBuffer =
            new List<Component>();
        private static GUIStyle _dividerTextStyle;

        static HierarchyDecoration()
        {
            var settings = CoreSettings.Current;
            settings.Register(Enabled);
            InjectorApi.Register(
                new ItemInjectionRegistration(
                    RegistrationId,
                    InjectionChannel.HierarchyItem,
                    Draw,
                    priority: 100,
                    isEnabled: () => settings.Get(Enabled)));
            ObjectChangeEvents.changesPublished += HandleChanges;
            settings.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(args.Definition, Enabled))
            {
                InjectorApi.Repaint(InjectionChannel.HierarchyItem);
            }
        }

        private static void Draw(ItemInjectionContext context)
        {
            if (context == null ||
                !context.IsHierarchyGameObject ||
                !(context.Target is GameObject gameObject) ||
                Event.current == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            ComponentBuffer.Clear();
            gameObject.GetComponents(ComponentBuffer);
            if (!TryFindDefinition(
                    gameObject,
                    ComponentBuffer,
                    out var definition,
                    out var styleName))
            {
                return;
            }

            definition.Style.Draw(
                context.SelectionRect,
                EditorGUIUtility.currentViewWidth,
                styleName);
        }

        private static void DrawDivider(
            Rect selectionRect,
            float viewWidth,
            string text)
        {
            var backgroundRect = new Rect(
                DividerLeftInset,
                selectionRect.y,
                Mathf.Max(0f, viewWidth - DividerLeftInset),
                selectionRect.height);
            DrawRect(
                backgroundRect,
                UiColorTokens.HierarchyDecorationBackground);

            if (string.IsNullOrWhiteSpace(text))
            {
                DrawRect(
                    GetHorizontalLine(backgroundRect),
                    UiColorTokens.HierarchyDecorationGuide);
                return;
            }

            var content = UiTextFactory.CreateGuiContent(text);
            var textStyle = GetDividerTextStyle();
            var textWidth = Mathf.Min(
                Mathf.Ceil(textStyle.CalcSize(content).x),
                Mathf.Max(
                    0f,
                    backgroundRect.width -
                    UiSpacingTokens.Medium));
            var textRect = new Rect(
                backgroundRect.center.x - textWidth * 0.5f,
                backgroundRect.y,
                textWidth,
                backgroundRect.height);
            GetDividerLineRects(
                backgroundRect,
                textRect,
                out var leftLine,
                out var rightLine);
            DrawRect(
                leftLine,
                UiColorTokens.HierarchyDecorationGuide);
            DrawRect(
                rightLine,
                UiColorTokens.HierarchyDecorationGuide);
            GUI.Label(textRect, content, textStyle);
        }

        private static void GetDividerLineRects(
            Rect backgroundRect,
            Rect textRect,
            out Rect leftLine,
            out Rect rightLine)
        {
            var lineY = Mathf.Floor(
                backgroundRect.y +
                (backgroundRect.height - UiBorderTokens.Hairline) *
                0.5f);
            var leftEnd = Mathf.Clamp(
                textRect.x - UiSpacingTokens.Xs,
                backgroundRect.x,
                backgroundRect.xMax);
            var rightStart = Mathf.Clamp(
                textRect.xMax + UiSpacingTokens.Xs,
                backgroundRect.x,
                backgroundRect.xMax);
            leftLine = new Rect(
                backgroundRect.x,
                lineY,
                leftEnd - backgroundRect.x,
                UiBorderTokens.Hairline);
            rightLine = new Rect(
                rightStart,
                lineY,
                backgroundRect.xMax - rightStart,
                UiBorderTokens.Hairline);
        }

        private static Rect GetHorizontalLine(Rect rect)
        {
            return new Rect(
                rect.x,
                Mathf.Floor(
                    rect.y +
                    (rect.height - UiBorderTokens.Hairline) * 0.5f),
                rect.width,
                UiBorderTokens.Hairline);
        }

        private static GUIStyle GetDividerTextStyle()
        {
            if (_dividerTextStyle == null)
            {
                _dividerTextStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fontSize = UiTypographyTokens.SmallFontSize,
                    richText = false,
                    wordWrap = false
                };
            }

            _dividerTextStyle.normal.textColor =
                UiColorTokens.HierarchyDecorationText;
            return _dividerTextStyle;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            if (rect.width > 0f && rect.height > 0f)
            {
                EditorGUI.DrawRect(rect, color);
            }
        }

        private static bool TryFindDefinition(
            GameObject gameObject,
            IReadOnlyList<Component> components,
            out HierarchyDecorationDefinition matchedDefinition,
            out string styleName)
        {
            for (var i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition.Target.Matches(
                        gameObject.name,
                        components,
                        gameObject.transform.childCount,
                        gameObject.transform.parent != null,
                        out styleName))
                {
                    matchedDefinition = definition;
                    return true;
                }
            }

            matchedDefinition = null;
            styleName = gameObject.name;
            return false;
        }

        private static void HandleChanges(
            ref ObjectChangeEventStream stream)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (var i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) !=
                    ObjectChangeKind.CreateGameObjectHierarchy)
                {
                    continue;
                }

                stream.GetCreateGameObjectHierarchyEvent(
                    i,
                    out var change);
                TryNormalizeCreatedObject(
                    EditorUtility.InstanceIDToObject(
                        change.instanceId) as GameObject,
                    Definitions);
            }
        }

        private static bool TryNormalizeCreatedObject(
            GameObject gameObject,
            IReadOnlyList<HierarchyDecorationDefinition> definitions)
        {
            if (gameObject == null || definitions == null)
            {
                return false;
            }

            var components = gameObject.GetComponents<Component>();
            for (var i = 0; i < definitions.Count; i++)
            {
                if (!definitions[i].Target.TryNormalizeDuplicateName(
                        gameObject.name,
                        components,
                        gameObject.transform.childCount,
                        gameObject.transform.parent != null,
                        out var normalizedName))
                {
                    continue;
                }

                gameObject.name = normalizedName;
                if (gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(
                        gameObject.scene);
                }

                return true;
            }

            return false;
        }
    }

    internal sealed class HierarchyDecorationDefinition
    {
        public HierarchyDecorationDefinition(
            string id,
            HierarchyDecorationTarget target,
            HierarchyDecorationStyle style)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Decoration id is required.",
                    nameof(id));
            }

            Id = id;
            Target = target ??
                throw new ArgumentNullException(nameof(target));
            Style = style ??
                throw new ArgumentNullException(nameof(style));
        }

        public string Id { get; }

        public HierarchyDecorationTarget Target { get; }

        public HierarchyDecorationStyle Style { get; }
    }

    internal sealed class HierarchyDecorationTarget
    {
        public HierarchyDecorationTarget(
            string namePrefix = null,
            Type requiredComponentType = null,
            bool? isEmpty = null,
            bool? hasChildren = null,
            bool? hasParent = null)
        {
            if (namePrefix != null &&
                string.IsNullOrWhiteSpace(namePrefix))
            {
                throw new ArgumentException(
                    "Name prefix must not be empty.",
                    nameof(namePrefix));
            }

            if (requiredComponentType != null &&
                !typeof(Component).IsAssignableFrom(
                    requiredComponentType))
            {
                throw new ArgumentException(
                    "Required component type must derive from Component.",
                    nameof(requiredComponentType));
            }

            if (namePrefix == null &&
                requiredComponentType == null &&
                !isEmpty.HasValue &&
                !hasChildren.HasValue &&
                !hasParent.HasValue)
            {
                throw new ArgumentException(
                    "At least one target condition is required.");
            }

            NamePrefix = namePrefix;
            RequiredComponentType = requiredComponentType;
            IsEmpty = isEmpty;
            HasChildren = hasChildren;
            HasParent = hasParent;
        }

        public string NamePrefix { get; }

        public Type RequiredComponentType { get; }

        public bool? IsEmpty { get; }

        public bool? HasChildren { get; }

        public bool? HasParent { get; }

        public bool Matches(
            string objectName,
            IReadOnlyList<Component> components,
            int childCount,
            bool hasParent,
            out string styleName)
        {
            styleName = objectName;
            if (NamePrefix != null)
            {
                if (objectName == null ||
                    !objectName.StartsWith(
                        NamePrefix,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                styleName = objectName.Substring(NamePrefix.Length);
            }

            if (RequiredComponentType != null &&
                !ContainsRequiredComponent(components))
            {
                return false;
            }

            if (IsEmpty.HasValue &&
                (components == null ||
                 IsEmpty.Value != IsEmptyGameObject(components)))
            {
                return false;
            }

            return (!HasChildren.HasValue ||
                    HasChildren.Value == (childCount > 0)) &&
                (!HasParent.HasValue ||
                 HasParent.Value == hasParent);
        }

        public bool TryNormalizeDuplicateName(
            string objectName,
            IReadOnlyList<Component> components,
            int childCount,
            bool hasParent,
            out string normalizedName)
        {
            normalizedName = objectName;
            if (!Matches(
                    objectName,
                    components,
                    childCount,
                    hasParent,
                    out _) ||
                string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            var suffixStart = objectName.LastIndexOf(
                " (",
                StringComparison.Ordinal);
            if (suffixStart < 0 ||
                objectName[objectName.Length - 1] != ')')
            {
                return false;
            }

            var numberText = objectName.Substring(
                suffixStart + 2,
                objectName.Length - suffixStart - 3);
            if (!int.TryParse(
                    numberText,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var duplicateIndex) ||
                duplicateIndex < 1)
            {
                return false;
            }

            normalizedName = objectName.Substring(0, suffixStart);
            return true;
        }

        private bool ContainsRequiredComponent(
            IReadOnlyList<Component> components)
        {
            if (components == null)
            {
                return false;
            }

            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component != null &&
                    RequiredComponentType.IsInstanceOfType(component))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEmptyGameObject(
            IReadOnlyList<Component> components)
        {
            return components.Count == 1 &&
                components[0] != null &&
                components[0].GetType() == typeof(Transform);
        }
    }

    internal sealed class HierarchyDecorationStyle
    {
        private readonly Action<Rect, float, string> _style;

        public HierarchyDecorationStyle(
            Action<Rect, float, string> style)
        {
            _style = style ??
                throw new ArgumentNullException(nameof(style));
        }

        public void Draw(
            Rect selectionRect,
            float viewWidth,
            string styleName)
        {
            _style(selectionRect, viewWidth, styleName);
        }
    }

    internal static class HierarchyDecorationMenu
    {
        private const string MenuPath =
            "GameObject/HierarchyDecoration/div";

        [MenuItem(MenuPath, false, 10)]
        private static void CreateDivider()
        {
            var gameObject = new GameObject(
                HierarchyDecoration.DividerTarget.NamePrefix);
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                I18N.Get("undo.createDivider"));
            Selection.activeObject = gameObject;
        }
    }
}
