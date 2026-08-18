using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.HierarchyDecoration
{
    [InitializeOnLoad]
    internal static class HierarchyDecoration
    {
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

        static HierarchyDecoration()
        {
            var settings = CoreSettings.Current;
            settings.Register(Enabled);
            InjectorApi.Register(
                new ItemInjectionRegistration(
                    "hierarchy-decoration.renderer",
                    InjectionChannel.HierarchyItem,
                    HierarchyDecorationRenderer.Draw,
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
                        change.instanceId) as GameObject);
            }
        }

        private static void TryNormalizeCreatedObject(
            GameObject gameObject)
        {
            if (gameObject == null ||
                !DividerTarget.TryNormalizeDuplicateName(
                    gameObject.name,
                    gameObject.GetComponents<Component>(),
                    gameObject.transform.childCount,
                    gameObject.transform.parent != null,
                    out var normalizedName))
            {
                return;
            }

            gameObject.name = normalizedName;
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
    }

    internal static class HierarchyDecorationMenu
    {
        [MenuItem("GameObject/HierarchyDecoration/div", false, 10)]
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
