using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    public sealed partial class AvatarLightingView : AvatarInspectionView
    {
        private readonly List<LightingPreset> _previews = new List<LightingPreset>();
        private AvatarLightingRenderer _renderer;
        private UiTextElement _selection;
        private bool _volumesAvailable;
        private readonly string[] _patternNames = { "day", "night", "warm", "cold", "backlight", "lv" };

        protected override GameObject VisibleAvatar => GestureManagerIntegration.VisibleAvatar(Avatar);

        public AvatarLightingView(GameObject avatar, Action repaint)
            : base(avatar, repaint, Text("title"))
        {
            _volumesAvailable = LightingPresetStore.VolumesAvailable;
            _previews.Add(CreateBuiltin(1));
            _addPreview = new UiButton(Text("addPreview"), AddPreview,
                icon: FluentUiIcons.CreateState("add.png", UiSizeTokens.Size12));
            PreviewActions.Add(_addPreview);
            Controls.Add(UiTextFactory.Create(Text("settings"), UiClassNames.SectionTitle));
            _selection = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            Controls.Add(_selection);
            BuildPresetControls();
            BuildPreviewSettings();
            Controls.Add(UiTextFactory.Create(Text("compareHint"), UiClassNames.SecondaryText));
            SetPreviewCount(_previews.Count);
            RefreshSelection();
            if (avatar == null || !EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode)
            { Status.SetText(Text("ready")); Controls.SetEnabled(false); return; }
            _renderer = new AvatarLightingRenderer(avatar);
        }

        protected override void ActivePreviewChanged()
        {
            if (_presetName != null) { _presetName.SetValueWithoutNotify(_previews[ActivePreview].Name); }
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_selection == null) { return; }
            for (var i = 0; i < PreviewCount; i++)
            { SetPreviewTitle(i, (i + 1) + " · " + PresetLabel(_previews[i])); }
            _selection.SetText(I18N.Get("workflow.lighting.selected", ActivePreview + 1));
            _addPreview.SetEnabled(PreviewCount < MaxPreviewCount);
            RefreshPresetControls();
            foreach (var refresh in _refreshPreviewSettings) { refresh(); }
            var pattern = _previews[ActivePreview].Pattern;
            _directionalSettings.style.display = pattern < 6 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_volumeSettings != null)
            { _volumeSettings.style.display = pattern == 6 ? DisplayStyle.Flex : DisplayStyle.None; }
        }

        protected override void RenderPreview(Camera camera, int index)
        {
            if (_renderer == null) camera.Render();
            else _renderer.Render(camera, _previews[index]);
        }

        public override void Dispose()
        {
            try
            {
                Ee4v.Core.Settings.GlobalDataSettings.PathChanged -= ReloadPresets;
                LightingPresetStore.Changed -= ReloadPresets;
                _renderer?.Dispose();
                _renderer = null;
            }
            finally { base.Dispose(); }
        }
        private static string Text(string key) => I18N.Get("workflow.lighting." + key);
    }
}
