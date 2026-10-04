using System.Collections.Generic;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    internal sealed class BoneMergeSettingsStoryProvider : IUiStoryProvider
    {
        public int Order => 203;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[] { new UiStory("bone-merge-settings", "Domain/AvatarParts/Inputs", "BoneMergeSettings",
                "統合元・統合先を専用フォームで表示します。",
                "サンプルの設定だけを編集します。", Build,
                usageLocations: new[] { "Editor/Feature/Avatar/AvatarParts/AvatarPartsEditor.Attachments.cs" },
                styleSheetPaths: new[] { "Editor/Feature/Avatar/AvatarParts/avatar-parts.uss" }) };
        }

        private static void Build(VisualElement parent)
        {
            var avatar = new GameObject("Sample Avatar") { hideFlags = HideFlags.HideAndDontSave };
            var target = new GameObject("Avatar Armature") { hideFlags = HideFlags.HideAndDontSave };
            target.transform.SetParent(avatar.transform, false);
            var source = new GameObject("Accessory Armature") { hideFlags = HideFlags.HideAndDontSave };
            source.transform.SetParent(avatar.transform, false);
            var merge = source.AddComponent<ModularAvatarMergeArmature>();
            merge.mergeTarget.Set(target);
            parent.Add(new BoneMergeSettings(merge, "Accessory / Armature", selected =>
            {
                if (selected != target) { return false; }
                merge.mergeTarget.Set(selected);
                return true;
            }));
            parent.RegisterCallback<DetachFromPanelEvent>(_ => Object.DestroyImmediate(avatar));
        }
    }
}
