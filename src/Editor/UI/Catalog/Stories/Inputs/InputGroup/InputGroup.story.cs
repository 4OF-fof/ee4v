using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private enum InputGroupStorySize
        {
            Single,
            Multiple
        }

        private sealed class InputGroupCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 36;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "input-group",
                    "Inputs",
                    "InputGroup",
                    "必須見出し付きの囲み枠へ1個以上のFormInputをまとめる入力グループです。",
                    "見出しから内容を開閉でき、閉状態は高さを詰めた薄い面で示します。枠線または見出し上だけでホバーを示し、グループ内の各行はFormInputで構成します。",
                    new[] { "FormInput", "UiButton" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildInputGroupStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/Core/Presentation/Settings/SettingsUiRenderer.cs"
                    }));
            }
        }

        private void BuildInputGroupStory(VisualElement parent)
        {
            var size = InputGroupStorySize.Multiple;
            var expanded = true;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "FormInputの数と展開状態を変更します。Previewの見出しからも開閉できます。");
            var sizeField = AddEnumField(
                controls.Content,
                "入力数",
                size,
                value =>
                {
                    size = value;
                    refresh();
                });
            Toggle expandedToggle = null;
            expandedToggle = AddToggle(
                controls.Content,
                "展開",
                expanded,
                value =>
                {
                    expanded = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var groupHost = new VisualElement();
            preview.Body.Add(groupHost);

            refresh = () =>
            {
                sizeField.SetValueWithoutNotify(size);
                expandedToggle.SetValueWithoutNotify(expanded);
                groupHost.Clear();

                var group = new InputGroup(
                    "基本情報",
                    new FormInput(
                        "名前",
                        new InputField(new InputFieldState("Main Window"))));

                if (size == InputGroupStorySize.Multiple)
                {
                    var enabled = UiTextFactory.CreateToggle();
                    enabled.SetValueWithoutNotify(true);
                    group.AddInput(new FormInput(enabled));
                    group.AddInput(new FormInput(
                        "説明",
                        new InputField(new InputFieldState(
                            "グループ内の複数行をまとめて表示します。",
                            multiline: true))));
                }

                group.SetExpanded(expanded);
                group.ExpandedChanged += value =>
                {
                    expanded = value;
                    expandedToggle.SetValueWithoutNotify(value);
                };

                groupHost.Add(group);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
