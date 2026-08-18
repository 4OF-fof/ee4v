using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.ItemStyle;
using UnityEditor;
using UnityEngine;

namespace Ee4v.HierarchyStyle
{
    internal static class HierarchyStyleWindow
    {
        private const float ColorAlpha = 0.32f;

        private static readonly Color[] DarkColors =
        {
            new Color(0.85f, 0.18f, 0.18f, ColorAlpha),
            new Color(0.9f, 0.48f, 0.16f, ColorAlpha),
            new Color(0.9f, 0.78f, 0.16f, ColorAlpha),
            new Color(0.52f, 0.8f, 0.18f, ColorAlpha),
            new Color(0.2f, 0.75f, 0.3f, ColorAlpha),
            new Color(0.16f, 0.76f, 0.58f, ColorAlpha),
            new Color(0.16f, 0.72f, 0.78f, ColorAlpha),
            new Color(0.18f, 0.5f, 0.88f, ColorAlpha),
            new Color(0.3f, 0.3f, 0.9f, ColorAlpha),
            new Color(0.55f, 0.26f, 0.88f, ColorAlpha),
            new Color(0.8f, 0.22f, 0.78f, ColorAlpha),
            new Color(0.88f, 0.2f, 0.5f, ColorAlpha)
        };

        private static readonly Color[] LightColors =
        {
            new Color(0.95f, 0.16f, 0.16f, ColorAlpha),
            new Color(0.95f, 0.45f, 0.12f, ColorAlpha),
            new Color(0.88f, 0.72f, 0.08f, ColorAlpha),
            new Color(0.45f, 0.72f, 0.1f, ColorAlpha),
            new Color(0.12f, 0.68f, 0.22f, ColorAlpha),
            new Color(0.08f, 0.66f, 0.48f, ColorAlpha),
            new Color(0.08f, 0.62f, 0.72f, ColorAlpha),
            new Color(0.12f, 0.42f, 0.82f, ColorAlpha),
            new Color(0.22f, 0.22f, 0.85f, ColorAlpha),
            new Color(0.5f, 0.18f, 0.82f, ColorAlpha),
            new Color(0.75f, 0.14f, 0.72f, ColorAlpha),
            new Color(0.85f, 0.14f, 0.44f, ColorAlpha)
        };

        public static void ShowAt(
            IReadOnlyList<GameObject> targets,
            Vector2 screenPosition,
            ItemStyleService service,
            HierarchyObjectIdentity identity,
            HierarchyStyleIconApplier iconApplier)
        {
            if (targets == null ||
                service == null ||
                identity == null ||
                iconApplier == null)
            {
                return;
            }

            var validTargets = targets
                .Where(target =>
                    target != null && target.scene.IsValid())
                .Distinct()
                .ToArray();
            var identities = validTargets
                .Select(identity.Get)
                .Where(value => !string.IsNullOrEmpty(value))
                .ToArray();
            if (validTargets.Length == 0 || identities.Length == 0)
            {
                return;
            }

            var request = new ItemStyleWindowRequest(
                service,
                identities,
                screenPosition,
                validTargets.Length > 1
                    ? I18N.Get(
                        "window.multipleTitle",
                        validTargets.Length)
                    : validTargets[0].name,
                validTargets.Length > 1
                    ? I18N.Get("window.multipleSubtitle")
                    : I18N.Get("window.singleSubtitle"),
                CreateTargetTooltip(validTargets),
                GetColors(),
                typeof(Texture2D),
                EditorGUIUtility.ObjectContent(
                    validTargets[0],
                    typeof(GameObject)).image,
                () => InjectorApi.Repaint(
                    InjectionChannel.HierarchyItem))
            {
                ColorLabel = I18N.Get("editor.color.label"),
                ClearColorLabel = I18N.Get(
                    "editor.color.clearLabel"),
                IconLabel = I18N.Get("editor.icon.label"),
                ClearIconLabel = I18N.Get(
                    "editor.icon.clearLabel"),
                RecentIconsLabel = I18N.Get(
                    "editor.icon.recentLabel"),
                ActionLabel = validTargets.Length > 1
                    ? I18N.Get(
                        "editor.hide.multipleLabel",
                        validTargets.Length)
                    : I18N.Get("editor.hide.singleLabel"),
                ActionTooltip = I18N.Get("editor.hide.tooltip"),
                Action = () => Hide(
                    validTargets,
                    I18N.Get("editor.hide.undo")),
                IconApplied = texture => ApplyIcons(
                    validTargets,
                    texture,
                    iconApplier)
            };
            ItemStyleWindow.ShowAt(request);
        }

        private static void Hide(
            IReadOnlyList<GameObject> targets,
            string undoOperationName)
        {
            HierarchyStyleApi.Hide(
                targets
                    .Select(target => target.GetInstanceID())
                    .ToArray(),
                undoOperationName);
        }

        private static IReadOnlyList<Color> GetColors()
        {
            return EditorGUIUtility.isProSkin
                ? DarkColors
                : LightColors;
        }

        private static void ApplyIcons(
            IReadOnlyList<GameObject> targets,
            Texture texture,
            HierarchyStyleIconApplier iconApplier)
        {
            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            var iconGuid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            for (var i = 0; i < targets.Count; i++)
            {
                iconApplier.Apply(targets[i], iconGuid);
            }
        }

        private static string CreateTargetTooltip(
            IReadOnlyList<GameObject> targets)
        {
            var paths = new List<string>(targets.Count);
            for (var i = 0; i < targets.Count; i++)
            {
                paths.Add(GetHierarchyPath(targets[i].transform));
            }

            return string.Join("\n", paths);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }
    }
}
