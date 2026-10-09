using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarInfo
{
    internal sealed class AvatarInfoWindow : AvatarPrefabEditorWindow
    {
        [SerializeField] private bool _mobile;
        protected override string TitleKey => "avatarEditor.infoTitle";
        protected override bool UsesBodyPartSelector => false;
        protected override bool ShowsSaveButton => true;
        protected override bool ShowsRevertButton => false;

        [MenuItem("ee4v/Window/Avatar/Avatar Info", false, 202)]
        private static void ShowWindow() => GetWindow<AvatarInfoWindow>().Show();

        protected override void CreateFeature()
        {
            AvatarPlayModePerformanceCache.Changed += RefreshPerformance;
            AvatarBuildSizeCache.Changed += RefreshPerformance;
        }
        protected override void DisposeFeature()
        {
            AvatarPlayModePerformanceCache.Changed -= RefreshPerformance;
            AvatarBuildSizeCache.Changed -= RefreshPerformance;
        }
        protected override void ClearFeatureData() { }

        protected override void RenderFeature()
        {
            var avatar = Context.Root;
            Context.ControlsHost.Add(new AvatarInfoView(avatar.name,
                AvatarInfoAnalysis.GetBlueprintId(avatar),
                AvatarInfoAnalysis.FindAttachmentWarnings(avatar),
                AvatarPlayModePerformanceCache.Get(avatar), _mobile,
                AvatarInfoAnalysis.HasAaoComponents(avatar),
                mobile => { _mobile = mobile; Render(); },
                target =>
                {
                    if (target == null) return;
                    Selection.activeGameObject = target;
                    EditorGUIUtility.PingObject(target);
                    EditorUtility.OpenPropertyEditor(target);
                }, AvatarInfoEditing.CreateOptions(Context),
                AvatarInfoSdk.Provider?.ReadParameterMemory(avatar),
                AvatarBuildSizeCache.Get(avatar, _mobile),
                () =>
                {
                    if (Context.Edits.FlushChanges()) AvatarPlayModePerformanceCache.Bake(avatar);
                }, AvatarPlayModePerformanceCache.CanBake(avatar)));
        }

        private void RefreshPerformance()
        {
            if (Context?.Root != null) Render();
        }
    }
}
