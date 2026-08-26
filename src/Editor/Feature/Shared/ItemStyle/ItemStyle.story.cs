using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.ItemStyle
{
    internal sealed class ItemStyleStoryProvider : IUiStoryProvider
    {
        public int Order => 170;

        public IReadOnlyList<UiStory> GetStories()
        {
            var styles = new[]
            {
                "Editor/UI/Components/Content/Icon/icon.uss",
                "Editor/UI/Components/Inputs/ui-button.uss",
                "Editor/Feature/Shared/ItemStyle/item-style-window.uss"
            };
            return new[]
            {
                new UiStory(
                    "project-style-window",
                    "Domain/ProjectStyle",
                    "Project Style Window",
                    "Project のフォルダーへ色とアイコンを設定する画面です。",
                    "ProjectStyle と HierarchyStyle が共有する ItemStyleEditor を使用します。",
                    parent => Build(parent, false),
                    dependencies: new[]
                    {
                        "CustomPopup",
                        "ItemStyleEditor",
                        "UiButton"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Project/ProjectStyle/UI/ProjectStyleWindow.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleWindow.cs"
                    },
                    styleSheetPaths: styles),
                new UiStory(
                    "hierarchy-style-window",
                    "Domain/HierarchyStyle",
                    "Hierarchy Style Window",
                    "Hierarchy の項目へ背景色とアイコンを設定する画面です。",
                    "共有の ItemStyleEditor に非表示操作を追加した状態を表示します。",
                    parent => Build(parent, true),
                    dependencies: new[]
                    {
                        "CustomPopup",
                        "ItemStyleEditor",
                        "UiButton"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Hierarchy/HierarchyStyle/UI/HierarchyStyleWindow.cs",
                        "Editor/Feature/Shared/ItemStyle/ItemStyleWindow.cs"
                    },
                    styleSheetPaths: styles)
            };
        }

        private static void Build(
            VisualElement parent,
            bool hierarchy)
        {
            var title = hierarchy ? "Environment" : "Scenes";
            var targetTooltip = hierarchy
                ? "Environment"
                : "Assets/Scenes";
            var text = new ItemStyleEditorText
            {
                ColorTitle = hierarchy ? "Background" : "Color",
                ColorTooltip = "Choose a color",
                CustomColorLabel = "Custom color",
                ClearColorLabel = "Clear color",
                IconTitle = "Icon",
                IconTooltip = "Choose an icon",
                RecentIconsLabel = "Recently used",
                ChooseIconLabel = "Choose icon",
                ClearIconLabel = "Clear icon",
                ActionLabel = hierarchy ? "Hide from Hierarchy" : null,
                ActionTooltip = hierarchy
                    ? "Hide this object from Hierarchy"
                    : null
            };
            var view = new ItemStyleEditor(
                text,
                hierarchy ? new Action(() => { }) : null);
            var defaultIcon = UiBuiltinIconResolver.LoadTexture(
                hierarchy ? UiBuiltinIcon.GameObject : UiBuiltinIcon.Folder);
            view.SetState(new ItemStyleEditorState
            {
                Color = new Color(
                    0.35f,
                    0.65f,
                    0.95f,
                    hierarchy ? 0.32f : 0.7f),
                DefaultIcon = defaultIcon,
                ColorPresets = new[]
                {
                    new Color(0.95f, 0.3f, 0.3f, 0.7f),
                    new Color(0.95f, 0.65f, 0.2f, 0.7f),
                    new Color(0.35f, 0.65f, 0.95f, 0.7f),
                    new Color(0.65f, 0.35f, 0.95f, 0.7f)
                },
                RecentIcons = new[]
                {
                    new ItemStyleIconCandidate
                    {
                        Texture = UiBuiltinIconResolver.LoadTexture(
                            UiBuiltinIcon.Folder),
                        Tooltip = "Folder icon"
                    },
                    new ItemStyleIconCandidate
                    {
                        Texture = UiBuiltinIconResolver.LoadTexture(
                            UiBuiltinIcon.ModelFile),
                        Tooltip = "Prefab icon"
                    }
                },
                PreviewColorAsBackground = hierarchy,
                IconType = hierarchy ? typeof(Texture2D) : typeof(Texture)
            });
            var popup = new CustomPopup(
                title,
                titleTooltip: targetTooltip,
                closeTooltip: "Close");
            popup.style.width = 360f;
            popup.style.height = hierarchy ? 318f : 268f;
            popup.style.flexGrow = 0f;
            popup.HeaderLeading.Add(view.HeaderPreview);
            popup.Content.Add(view);
            parent.Add(popup);
        }
    }
}
