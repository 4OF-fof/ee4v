using Ee4v.AvatarModify.UI;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class AvatarModifyCatalogStory
    {
        private sealed class Registrar :
            CatalogWindow.ICatalogRegistrar
        {
            public int Order => 135;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/Button/ui-button.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/InputField/input-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/AvatarModify/UI/avatar-variant-popup.uss");
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        "avatar-variant-creation-popup",
                        "Domain/AvatarModify/Components",
                        "Avatar Variant Creation Popup",
                        CatalogCoveragePreview.ComponentDescription(
                            "Avatar Variant Creation Popup"),
                        CatalogCoveragePreview.ComponentDetails(
                            "Avatar Variant Creation Popup"),
                        new[] { "UiButton" },
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        Build));
            }
        }

        private static void Build(
            CatalogWindow window,
            VisualElement parent)
        {
            var view = new AvatarVariantCreationView(
                AvatarVariantCreationPopup.CreateText());
            view.SetState(
                new AvatarVariantCreationViewState
                {
                    Prefabs = new[]
                    {
                        new AvatarVariantOption(
                            "prefab-a",
                            "SampleAvatar",
                            "Assets/Avatars  •  Avatar Descriptor",
                            "Assets/Avatars/SampleAvatar.prefab"),
                        new AvatarVariantOption(
                            "prefab-b",
                            "SampleAvatarLite",
                            "Assets/Avatars",
                            "Assets/Avatars/SampleAvatarLite.prefab")
                    },
                    PrefabGuid = "prefab-a",
                    VariantName = string.Empty,
                    CanCreate = true
                });
            var surface =
                CatalogCoveragePreview.CreateSurface(
                    window,
                    parent,
                    310f);
            surface.AddToClassList(
                "ee4v-avatar-variant-popup");
            surface.AddToClassList(
                UiClassNames.PopupSurface);
            surface.Add(view);
        }
    }
}
