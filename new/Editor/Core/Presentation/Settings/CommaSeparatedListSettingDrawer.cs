using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    internal static class CommaSeparatedListSettingDrawer
    {
        public static void Register(SettingDefinition<string> definition)
        {
            SettingDrawerRegistry.Register(
                definition,
                CreateField);
        }

        private static VisualElement CreateField(
            SettingDrawerContext<string> context)
        {
            var field = new CommaSeparatedListField(
                new CommaSeparatedListFieldState(
                    ParseItems(context.Value),
                    context.Tooltip,
                    I18N.Get("settings.listInput.itemPlaceholder")));
            field.ValuesChanged += values =>
                context.NotifyValueChanged(SerializeItems(values));
            return field;
        }

        internal static IReadOnlyList<string> ParseItems(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(
                    new[] { ',', ';', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
        }

        internal static string SerializeItems(
            IEnumerable<string> values)
        {
            return string.Join(
                ",",
                (values ?? Array.Empty<string>())
                    .SelectMany(ParseItems));
        }
    }
}
