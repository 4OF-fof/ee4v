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
                    "囲み枠とフォーカス表示を担当します。グループ内の各行はFormInputで構成し、各FormInputのラベルは省略できます。",
                    new[] { "FormInput" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildInputGroupStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    }));
            }
        }

        private void BuildInputGroupStory(VisualElement parent)
        {
            var size = InputGroupStorySize.Multiple;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "FormInputの数を変更します。複数表示ではラベルなしの入力も確認できます。");
            var sizeField = AddEnumField(
                controls.Content,
                "入力数",
                size,
                value =>
                {
                    size = value;
                    refresh();
                });

            var preview = CreatePreviewSection(parent);
            var groupHost = new VisualElement();
            preview.Body.Add(groupHost);

            refresh = () =>
            {
                sizeField.SetValueWithoutNotify(size);
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

                groupHost.Add(group);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
