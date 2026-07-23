using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class IconCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStory(new StoryRegistration(
                    "icon",
                    "Content",
                    "Icon",
                    "任意の texture または enum 管理された Unity 内蔵アイコンを表示するアイコンコンポーネントです。",
                    "Unity 内蔵アイコンは version 差分の影響を抑えるため enum で許可したものだけを解決します。初期状態では検索アイコンをサポートし、custom texture に切り替えれば任意 texture を表示できます。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildIconStory(parent)));
            }
        }

        private void BuildIconStory(VisualElement parent)
        {
            var sourceKind = UiIconSourceKind.Builtin;
            var builtinIcon = UiBuiltinIcon.Search;
            Texture texture = null;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "source を切り替え、texture 指定と enum 管理の Unity 内蔵アイコン指定を確認します。");

            var sourceField = AddEnumField(controls.Content, "ソース", sourceKind, value =>
            {
                sourceKind = value;
                refresh();
            });
            var builtinField = AddEnumField(controls.Content, "内蔵アイコン", builtinIcon, value =>
            {
                builtinIcon = value;
                refresh();
            });
            var textureField = AddObjectField<Texture>(controls.Content, "Texture", texture, value =>
            {
                texture = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            var icon = new Icon();
            surface.Add(icon);
            preview.Body.Add(surface);

            refresh = () =>
            {
                sourceField.SetValueWithoutNotify((Enum)(object)sourceKind);
                builtinField.SetValueWithoutNotify((Enum)(object)builtinIcon);
                textureField.SetValueWithoutNotify(texture);

                builtinField.style.display = sourceKind == UiIconSourceKind.Builtin ? DisplayStyle.Flex : DisplayStyle.None;
                textureField.style.display = sourceKind == UiIconSourceKind.Texture ? DisplayStyle.Flex : DisplayStyle.None;

                switch (sourceKind)
                {
                    case UiIconSourceKind.Texture:
                        icon.SetState(texture != null
                            ? IconState.FromTexture(texture, tooltip: texture.name)
                            : IconState.FromBuiltinIcon(builtinIcon, tooltip: "Assign a texture"));
                        break;
                    case UiIconSourceKind.Builtin:
                        icon.SetState(IconState.FromBuiltinIcon(builtinIcon, tooltip: UiBuiltinIconResolver.GetIconName(builtinIcon)));
                        break;
                }
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
