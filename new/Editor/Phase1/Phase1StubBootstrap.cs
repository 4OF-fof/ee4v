using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Phase1
{
    internal static class Phase1StubBootstrap
    {
        private static bool _registered;
        private static ISettingsService _settings;

        public static void RegisterAll(ISettingsService settings)
        {
            if (settings == null)
            {
                throw new System.ArgumentNullException(nameof(settings));
            }

            if (_registered)
            {
                return;
            }

            _registered = true;
            _settings = settings;

            InjectorApi.Register(new ItemInjectionRegistration(
                "phase1.hierarchy.item.stub",
                InjectionChannel.HierarchyItem,
                DrawHierarchyItem,
                priority: 100,
                isEnabled: () => _settings.Get(Phase1Definitions.EnableHierarchyItemStub)));

            InjectorApi.Register(new ItemInjectionRegistration(
                "phase1.project.item.stub",
                InjectionChannel.ProjectItem,
                DrawProjectItem,
                priority: 100,
                isEnabled: () => _settings.Get(Phase1Definitions.EnableProjectItemStub)));

            InjectorApi.Register(new VisualElementInjectionRegistration(
                "phase1.project.toolbar.stub",
                InjectionChannel.ProjectToolbar,
                CreateProjectToolbar,
                priority: 100,
                isEnabled: () => _settings.Get(Phase1Definitions.EnableProjectToolbarStub)));

            _settings.Changed -= OnSettingChanged;
            _settings.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (args.Definition == Phase1Definitions.EnableHierarchyItemStub ||
                args.Definition == Phase1Definitions.HierarchyBadgeText ||
                args.Definition == Phase1Definitions.HierarchyAccentColor)
            {
                InjectorApi.Repaint(InjectionChannel.HierarchyItem);
            }

            if (args.Definition == Phase1Definitions.EnableProjectItemStub ||
                args.Definition == Phase1Definitions.EnableProjectToolbarStub ||
                args.Definition == Phase1Definitions.ProjectToolbarText ||
                args.Definition == Phase1Definitions.ProjectAccentColor ||
                args.Definition == Phase1Definitions.ToolbarButtonWidth)
            {
                InjectorApi.Repaint(InjectionChannel.ProjectItem);
                InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
            }
        }

        private static void DrawHierarchyItem(ItemInjectionContext context)
        {
            var badgeText = Phase1ContextVerification.GetHierarchyBadge(
                context,
                _settings.Get(Phase1Definitions.HierarchyBadgeText));
            var accent = _settings.Get(Phase1Definitions.HierarchyAccentColor);

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

        private static void DrawProjectItem(ItemInjectionContext context)
        {
            var accent = _settings.Get(Phase1Definitions.ProjectAccentColor);
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

            var label = UiTextFactory.Create(_settings.Get(Phase1Definitions.ProjectToolbarText), UiClassNames.Phase1StubLabel);
            label.style.flexGrow = 1f;
            label.style.marginRight = 6f;

            var reloadButton = new Button(I18N.Reload)
            {
                text = I18N.Get("stubs.projectToolbar.reload")
            };
            reloadButton.style.width = _settings.Get(Phase1Definitions.ToolbarButtonWidth);
            reloadButton.style.marginRight = 6f;

            var settingsButton = new Button(() => SettingsService.OpenProjectSettings("Project/4OF/ee4v"))
            {
                text = I18N.Get("stubs.projectToolbar.settings")
            };
            settingsButton.style.width = _settings.Get(Phase1Definitions.ToolbarButtonWidth);

            row.Add(label);
            row.Add(reloadButton);
            row.Add(settingsButton);
            return row;
        }
    }
}
