using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    public static class CommaSeparatedListSettingDrawer
    {
        public static void Register(SettingDefinition<string> definition)
        {
            SettingDrawerApi.Register(
                definition,
                CreateField);
        }

        private static VisualElement CreateField(
            SettingDrawerContext<string> context)
        {
            var field = new ListField<string>(
                new ListFieldState<string>(
                    ParseItems(context.Value),
                    (value, notifyValueChanged) =>
                    {
                        var item = new InputField(
                            new InputFieldState(
                                value,
                                placeholder: I18N.Get(
                                    "settings.listInput.itemPlaceholder")));
                        item.ValueChanged += notifyValueChanged;
                        return item;
                    },
                    () => string.Empty,
                    string.IsNullOrEmpty,
                    item => ((InputField)item).FocusInput(),
                    context.Tooltip,
                    I18N.Get("settings.listInput.addItem"),
                    I18N.Get("settings.listInput.removeItem")));
            field.ValuesChanged += values =>
                context.NotifyValueChanged(SerializeItems(values));
            return field;
        }

        public static IReadOnlyList<string> ParseItems(string value)
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
