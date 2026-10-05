using Ee4v.UI;

namespace Ee4v.Core.Settings
{
    public static class PathSettingDrawer
    {
        public static void Register(SettingDefinition<string> definition,
            PathFieldKind kind = PathFieldKind.Folder, string extension = "")
        {
            SettingDrawerApi.Register(definition, context =>
            {
                var field = new PathField(kind, context.Value, extension)
                {
                    tooltip = context.Tooltip
                };
                field.ValueChanged += context.NotifyValueChanged;
                return field;
            });
        }
    }
}
