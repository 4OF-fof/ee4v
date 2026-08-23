using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.PlayModeComponentSuppression
{
    internal static class ComponentTypeSettingDrawer
    {
        internal static void Register(
            SettingDefinition<string> definition)
        {
            SettingDrawerApi.Register(definition, Create);
        }

        private static VisualElement Create(
            SettingDrawerContext<string> context)
        {
            var identities = ComponentTypeIdentity.Parse(context.Value)
                .ToList();
            var root = new VisualElement();
            root.style.flexGrow = 1f;

            void NotifyChanged()
            {
                context.NotifyValueChanged(
                    ComponentTypeIdentity.Serialize(identities));
            }

            void Replace(int index, Type type)
            {
                identities[index] = ComponentTypeIdentity.Create(type);
                NotifyChanged();
                Rebuild();
            }

            void Add(Type type)
            {
                identities.Add(ComponentTypeIdentity.Create(type));
                NotifyChanged();
                Rebuild();
            }

            void Rebuild()
            {
                root.Clear();
                for (var index = 0; index < identities.Count; index++)
                {
                    var capturedIndex = index;
                    var identity = identities[index];
                    var type = ComponentTypeIdentity.Resolve(identity);
                    var row = CreateRow();
                    var label = UiTextFactory.Create(
                        type != null
                            ? ComponentTypePickerWindow.GetDisplayName(type)
                            : I18N.Get(
                                "settings.suppressedTypes.missing",
                                identity));
                    label.tooltip = type != null
                        ? ComponentTypeIdentity.Create(type)
                        : identity;
                    label.style.flexGrow = 1f;
                    label.style.minWidth = 0f;
                    row.Add(label);

                    var change = UiTextFactory.CreateButton(
                        I18N.Get("settings.suppressedTypes.change"));
                    change.clicked += () =>
                        ComponentTypePickerWindow.Show(
                            change,
                            identities,
                            capturedIndex,
                            selected => Replace(capturedIndex, selected));
                    row.Add(change);
                    row.Add(UiTextFactory.CreateButton(
                        I18N.Get("settings.suppressedTypes.remove"),
                        () =>
                        {
                            identities.RemoveAt(capturedIndex);
                            NotifyChanged();
                            Rebuild();
                        }));
                    root.Add(row);
                }

                var add = UiTextFactory.CreateButton(
                    I18N.Get("settings.suppressedTypes.add"));
                add.tooltip = context.Tooltip;
                add.clicked += () => ComponentTypePickerWindow.Show(
                    add,
                    identities,
                    -1,
                    Add);
                root.Add(add);
            }

            Rebuild();
            return root;
        }

        private static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = UiSpacingTokens.Xxs;
            return row;
        }
    }
}
