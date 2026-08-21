using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class LabeledContentRowCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 35;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "labeled-content-row",
                    "Inputs",
                    "LabeledContentRow",
                    "ラベル、入力内容、補助操作を横に並べるフォーム行です。",
                    "ラベル、入力内容、補助操作を一行へ整理して配置します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildLabeledContentRowStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetDetailComponents.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsView.cs"
                    }));
            }
        }

        private void BuildLabeledContentRowStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var row = new LabeledContentRow("名前");
            var input = UiTextFactory.CreateTextField();
            input.SetValueWithoutNotify("Main Window");
            row.Content.Add(input);
            row.Actions.Add(UiTextFactory.CreateButton(
                "保存",
                () => { }));
            preview.Body.Add(row);
        }
    }
}
