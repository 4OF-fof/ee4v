using Ee4v.I18n;
using Ee4v.Injector;
using Ee4v.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Phase1
{
    internal static class Phase1StubBootstrap
    {
        private static bool _registered;

        public static void RegisterAll()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;

            InjectorApi.Register(new ItemInjectionRegistration(
                "phase1.hierarchy.item.stub",
                InjectionChannel.HierarchyItem,
                DrawHierarchyItem,
                priority: 100,
                isEnabled: () => SettingApi.Get(Phase1Definitions.EnableHierarchyItemStub)));

            InjectorApi.Register(new VisualElementInjectionRegistration(
                "phase1.hierarchy.header.stub",
                InjectionChannel.HierarchyHeader,
                CreateHierarchyHeader,
                priority: 100,
                isEnabled: () => SettingApi.Get(Phase1Definitions.EnableHierarchyHeaderStub)));

            InjectorApi.Register(new ItemInjectionRegistration(
                "phase1.project.item.stub",
                InjectionChannel.ProjectItem,
                DrawProjectItem,
                priority: 100,
                isEnabled: () => SettingApi.Get(Phase1Definitions.EnableProjectItemStub)));

            InjectorApi.Register(new VisualElementInjectionRegistration(
                "phase1.project.toolbar.stub",
                InjectionChannel.ProjectToolbar,
                CreateProjectToolbar,
                priority: 100,
                isEnabled: () => SettingApi.Get(Phase1Definitions.EnableProjectToolbarStub)));

            SettingApi.Changed -= OnSettingChanged;
            SettingApi.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(SettingDefinitionBase definition, object value)
        {
            if (definition == Phase1Definitions.EnableHierarchyItemStub ||
                definition == Phase1Definitions.EnableHierarchyHeaderStub ||
                definition == Phase1Definitions.HierarchyBadgeText ||
                definition == Phase1Definitions.HierarchyHeaderText ||
                definition == Phase1Definitions.HierarchyAccentColor)
            {
                InjectorApi.Repaint(InjectionChannel.HierarchyItem);
                InjectorApi.Repaint(InjectionChannel.HierarchyHeader);
            }

            if (definition == Phase1Definitions.EnableProjectItemStub ||
                definition == Phase1Definitions.EnableProjectToolbarStub ||
                definition == Phase1Definitions.ProjectToolbarText ||
                definition == Phase1Definitions.ProjectAccentColor ||
                definition == Phase1Definitions.ToolbarButtonWidth)
            {
                InjectorApi.Repaint(InjectionChannel.ProjectItem);
                InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
            }
        }

        private static void DrawHierarchyItem(ItemInjectionContext context)
        {
            var badgeText = Phase1ContextVerification.GetHierarchyBadge(
                context,
                SettingApi.Get(Phase1Definitions.HierarchyBadgeText));
            var accent = SettingApi.Get(Phase1Definitions.HierarchyAccentColor);

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accent }
            };

            var content = new GUIContent(badgeText);
            var size = style.CalcSize(content);
            var width = size.x + 8f;
            var rect = new Rect(context.CurrentRect.xMax - width - 6f, context.SelectionRect.y + 1f, width, context.SelectionRect.height - 2f);

            EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, 0.12f));
            GUI.Label(rect, content, style);
            context.CurrentRect = new Rect(context.CurrentRect.x, context.CurrentRect.y, Mathf.Max(0f, rect.x - context.CurrentRect.x - 4f), context.CurrentRect.height);
        }

        private static VisualElement CreateHierarchyHeader(VisualHostContext context)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1f;

            var swatch = new VisualElement();
            swatch.style.width = 10f;
            swatch.style.height = 10f;
            swatch.style.marginRight = 6f;
            swatch.style.borderTopLeftRadius = 5f;
            swatch.style.borderTopRightRadius = 5f;
            swatch.style.borderBottomLeftRadius = 5f;
            swatch.style.borderBottomRightRadius = 5f;
            swatch.style.backgroundColor = SettingApi.Get(Phase1Definitions.HierarchyAccentColor);

            var label = new Label(SettingApi.Get(Phase1Definitions.HierarchyHeaderText))
            {
                tooltip = I18N.Get("stubs.hierarchyHeader.tooltip")
            };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(Color.white);

            row.Add(swatch);
            row.Add(label);
            return row;
        }

        private static void DrawProjectItem(ItemInjectionContext context)
        {
            var accent = SettingApi.Get(Phase1Definitions.ProjectAccentColor);
            var barRect = new Rect(context.SelectionRect.x + 2f, context.SelectionRect.y + 2f, 3f, Mathf.Max(0f, context.SelectionRect.height - 4f));
            EditorGUI.DrawRect(barRect, accent);

            var badgeText = Phase1ContextVerification.GetProjectBadge(context);
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accent }
            };

            var content = new GUIContent(badgeText);
            var size = style.CalcSize(content);
            var width = size.x + 8f;
            var rect = new Rect(context.CurrentRect.xMax - width - 6f, context.SelectionRect.y + 1f, width, context.SelectionRect.height - 2f);

            EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, 0.12f));
            GUI.Label(rect, content, style);
            context.CurrentRect = new Rect(
                context.CurrentRect.x,
                context.CurrentRect.y,
                Mathf.Max(0f, rect.x - context.CurrentRect.x - 4f),
                context.CurrentRect.height);
        }

        private static VisualElement CreateProjectToolbar(VisualHostContext context)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1f;

            var label = new Label(SettingApi.Get(Phase1Definitions.ProjectToolbarText));
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(Color.white);
            label.style.flexGrow = 1f;
            label.style.marginRight = 6f;

            var reloadButton = new Button(I18N.Reload)
            {
                text = I18N.Get("stubs.projectToolbar.reload")
            };
            reloadButton.style.width = SettingApi.Get(Phase1Definitions.ToolbarButtonWidth);
            reloadButton.style.marginRight = 6f;

            var settingsButton = new Button(() => SettingsService.OpenProjectSettings("Project/4OF/ee4v"))
            {
                text = I18N.Get("stubs.projectToolbar.settings")
            };
            settingsButton.style.width = SettingApi.Get(Phase1Definitions.ToolbarButtonWidth);

            row.Add(label);
            row.Add(reloadButton);
            row.Add(settingsButton);
            return row;
        }
    }
}
