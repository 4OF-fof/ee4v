using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class SourcePrioritySettingDrawer
    {
        private static readonly string[] KnownSourceIds =
        {
            "ee4v",
            "eagle",
            "blm"
        };

        public static void Register(
            SettingDefinition<string> definition)
        {
            SettingDrawerRegistry.Register(
                definition,
                CreateField);
        }

        internal static IReadOnlyList<string> NormalizeOrder(
            string value)
        {
            var known = new HashSet<string>(
                KnownSourceIds,
                StringComparer.OrdinalIgnoreCase);
            var result = (value ?? string.Empty)
                .Split(',')
                .Select(item => item.Trim().ToLowerInvariant())
                .Where(item => known.Contains(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < KnownSourceIds.Length; i++)
            {
                if (!result.Contains(
                        KnownSourceIds[i],
                        StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(KnownSourceIds[i]);
                }
            }

            return result;
        }

        internal static string SerializeOrder(
            IEnumerable<string> order)
        {
            return string.Join(
                ",",
                order ?? Array.Empty<string>());
        }

        private static VisualElement CreateField(
            SettingDrawerContext<string> context)
        {
            var items = NormalizeOrder(context.Value)
                .Select(id => new ReorderableListItemState(
                    id,
                    GetSourceLabel(id)))
                .ToArray();
            var field = new ReorderableListField(
                new ReorderableListFieldState(
                    items,
                    context.Tooltip,
                    I18N.Get(
                        "settings.sourcePriority.reorderTooltip")));
            field.OrderChanged += order =>
                context.NotifyValueChanged(
                    SerializeOrder(order));
            return field;
        }

        private static string GetSourceLabel(string id)
        {
            if (string.Equals(
                    id,
                    "ee4v",
                    StringComparison.OrdinalIgnoreCase))
            {
                return I18N.Get(
                    "settings.sourcePriority.options.ee4v");
            }

            if (string.Equals(
                    id,
                    "eagle",
                    StringComparison.OrdinalIgnoreCase))
            {
                return I18N.Get(
                    "settings.sourcePriority.options.eagle");
            }

            return I18N.Get(
                "settings.sourcePriority.options.blm");
        }
    }
}
