using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Ee4v.AvatarInfo
{
    public sealed class AvatarInfoEditOptions
    {
        public bool Editable;
        public bool SdkAvailable;
        public Func<string, string> Rename;
        public Func<string, bool> SelectBlueprint;
        public Func<bool> IsLoggedIn;
        public Func<CancellationToken, Task<IReadOnlyList<AvatarInfoSdkAvatar>>> FetchAvatars;
    }

    public static class AvatarInfoEditing
    {
        public static AvatarInfoEditOptions CreateOptions(AvatarEditingContext context)
        {
            var avatar = context.Root;
            bool CanEdit() => avatar != null && context.Root == avatar &&
                !EditorApplication.isPlayingOrWillChangePlaymode && context.Edits.CanEditPrefab();
            void Changed()
            {
                EditorSceneManager.MarkSceneDirty(avatar.scene);
                context.Edits.WorkingSceneDirty = true;
                context.Edits.Changed();
            }
            return new AvatarInfoEditOptions
            {
                Editable = CanEdit(),
                SdkAvailable = AvatarInfoSdk.Provider != null,
                IsLoggedIn = () => AvatarInfoSdk.Provider?.IsLoggedIn == true,
                FetchAvatars = token => AvatarInfoSdk.Provider.GetOwnAvatars(token),
                Rename = value =>
                {
                    value = value?.Trim();
                    if (!CanEdit() || string.IsNullOrEmpty(value) || avatar.name == value) return avatar.name;
                    Undo.RecordObject(avatar, "Rename Avatar");
                    avatar.name = value;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(avatar);
                    Changed();
                    return avatar.name;
                },
                SelectBlueprint = id =>
                {
                    if (!CanEdit() || AvatarInfoSdk.Provider == null || !context.Edits.FlushChanges()) return false;
                    AvatarInfoSdk.Provider.SetBlueprintId(avatar, id);
                    Changed();
                    return true;
                }
            };
        }
    }
}
