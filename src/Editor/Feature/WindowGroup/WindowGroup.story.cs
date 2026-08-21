using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.WindowGroup
{
    internal sealed class WindowGroupStoryProvider : IUiStoryProvider
    {
        public int Order => 190;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "window-group-settings",
                    "Domain/WindowGroup",
                    "Window Groups",
                    "EditorWindow のグループと追従設定を管理する画面です。",
                    "実際の管理画面を、保存を行わないサンプル設定で表示します。",
                    Build,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "UiComposition"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/WindowGroup/UI/WindowGroupSettingsWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/Feature/WindowGroup/UI/window-group-settings.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var configuration =
                new WindowGroupConfiguration(new StoryStore());
            var options = configuration.Groups
                .SelectMany(group => group.WindowTypeIds)
                .Distinct(StringComparer.Ordinal)
                .Select(typeId => new WindowTypeOption(
                    typeId,
                    WindowTypeIdentity.GetDisplayName(typeId),
                    null))
                .ToArray();
            var view = new WindowGroupSettingsView(
                configuration,
                () => options);
            view.style.width = 900f;
            view.style.height = 540f;
            parent.Add(view);
        }

        private sealed class StoryStore : IWindowGroupStore
        {
            public WindowGroupDocument Load()
            {
                var document = new WindowGroupDocument();
                var avatar = new WindowGroupDefinition(
                    "avatar-tools",
                    "Avatar Tools");
                avatar.WindowTypeIds.Add(
                    "Ee4v.FaceExpression.FaceExpressionWindow, Ee4v.FaceExpression.Editor");
                avatar.WindowTypeIds.Add(
                    "UnityEditor.SceneView, UnityEditor");
                avatar.FollowerWindowTypeIds.Add(
                    "UnityEditor.SceneView, UnityEditor");

                var assetManagement = new WindowGroupDefinition(
                    "asset-management",
                    "Asset Management");
                assetManagement.WindowTypeIds.Add(
                    "Ee4v.AssetManager.UI.AssetManagerMainWindow, Ee4v.AssetManager.UI.Editor");
                assetManagement.WindowTypeIds.Add(
                    "UnityEditor.ProjectBrowser, UnityEditor");

                document.Groups.Add(avatar);
                document.Groups.Add(assetManagement);
                return document;
            }

            public void Save(WindowGroupDocument document)
            {
            }
        }
    }
}
