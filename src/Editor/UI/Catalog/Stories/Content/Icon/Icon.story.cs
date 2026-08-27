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
                    "Displays",
                    "Icon",
                    "任意の texture または enum 管理された Unity 内蔵アイコンを表示するアイコンコンポーネントです。",
                    "Unity 内蔵アイコンは Unity 固有の用途で実際に使用するものだけを enum で許可します。通常の操作アイコンは Fluent UI System Icons、任意画像は custom texture を使用します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildIconStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerControls.cs",
                        "Editor/AssetManager/UI/Components/AssetItemGridCard.cs",
                        "Editor/AssetManager/UI/DerivedAssetPrefabPickerWindow.cs",
                        "Editor/AssetManager/UI/SearchableFileTree.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/Components/HiddenObjectTreeRow.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsViewState.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectsWindow.cs",
                        "Editor/Feature/Hierarchy/HierarchyStyle/HiddenObjects/UI/HiddenObjectTreeView.cs",
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherView.cs",
                        "Editor/Feature/Project/ProjectTabs/UI/ProjectTabsView.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleEditor.cs",
                        "Editor/UI/Components/Collections/SearchableTreeView/SearchableTreeView.cs",
                        "Editor/UI/Components/Content/ContentRow/ContentRow.cs",
                        "Editor/UI/Components/Content/DisclosureSection/DisclosureSection.cs",
                        "Editor/UI/Components/Content/EmptyState/EmptyState.cs",
                        "Editor/UI/Components/Content/NavigationItem/NavigationItem.cs",
                        "Editor/UI/Components/Content/TagPill/TagPill.cs",
                        "Editor/UI/Components/Feedback/InlineMessage/InlineMessage.cs",
                        "Editor/UI/Components/Inputs/SearchField/SearchField.cs",
                        "Editor/UI/Components/Inputs/UiButton.cs"
                    }));
            }
        }

        private void BuildIconStory(VisualElement parent)
        {
            var sourceKind = UiIconSourceKind.Builtin;
            var builtinIcon = UiBuiltinIcon.Folder;
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
            var surface = CreatePreviewArea(true);
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
