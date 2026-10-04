using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    public sealed partial class AvatarLightingView : AvatarInspectionView
    {
        private readonly List<LightingPreset> _previews = new List<LightingPreset>();
        private readonly List<Light> _lights = new List<Light>();
        private Light _previewLight;
        private UiTextElement _selection;
        private bool _volumesAvailable;
        private readonly string[] _patternNames = { "day", "night", "warm", "cold", "backlight", "lv" };

        protected override GameObject VisibleAvatar => GestureManagerIntegration.GetVisibleAvatar(
            GestureManagerIntegration.FindManager(Avatar)?.Module as BlackStartX.GestureManager.Editor.Modules.Vrc3.ModuleVrc3) ?? Avatar;

        public AvatarLightingView(GameObject avatar, Action repaint)
            : base(avatar, repaint, Text("title"))
        {
            _volumesAvailable = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                assembly.GetType("VRCLightVolumes.LightVolumeManager", false) != null);
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
            if (avatar == null || !EditorApplication.isPlaying)
            { Status.SetText(Text("ready")); Controls.SetEnabled(false); return; }
            var lightObject = new GameObject("ee4v Preview Light") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, avatar.scene);
            _previewLight = lightObject.AddComponent<Light>();
            _previewLight.type = LightType.Directional;
            _previewLight.enabled = false;
            _previewLight.shadows = LightShadows.None;
            _lights.AddRange(avatar.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Light>(true)).Where(light => light != _previewLight));
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
            var settings = _previews[index];
            var pattern = settings.Pattern;
            if (_previewLight == null) { camera.Render(); return; }
            var mode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            var intensity = RenderSettings.ambientIntensity;
            var reflection = RenderSettings.reflectionIntensity;
            var probe = RenderSettings.ambientProbe;
            var enabled = _lights.Select(light => light != null && light.enabled).ToArray();
            using (var volumes = new LightVolumePreviewScope())
            {
                try
                {
                    for (var i = 0; i < _lights.Count; i++) { if (_lights[i] != null) { _lights[i].enabled = false; } }
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = settings.AmbientColor;
                    RenderSettings.ambientIntensity = settings.AmbientIntensity;
                    RenderSettings.reflectionIntensity = settings.ReflectionIntensity;
                    var previewProbe = new SphericalHarmonicsL2();
                    previewProbe.AddAmbientLight(settings.AmbientColor.linear * settings.AmbientIntensity);
                    RenderSettings.ambientProbe = previewProbe;
                    _previewLight.color = settings.LightColor;
                    _previewLight.intensity = settings.LightIntensity;
                    _previewLight.transform.rotation = (Avatar != null ? Avatar.transform.rotation : Quaternion.identity) *
                        Quaternion.Euler(settings.Pitch, settings.Yaw, 0);
                    _previewLight.enabled = pattern < 6;
                    if (pattern < 6) { volumes.Disable(); }
                    else { volumes.Apply(settings, VisibleAvatar.transform, GetVolumeAtlas()); }
                    camera.Render();
                }
                finally
                {
                    _previewLight.enabled = false;
                    for (var i = 0; i < _lights.Count; i++) { if (_lights[i] != null) { _lights[i].enabled = enabled[i]; } }
                    RenderSettings.ambientMode = mode;
                    RenderSettings.ambientLight = ambient;
                    RenderSettings.ambientIntensity = intensity;
                    RenderSettings.reflectionIntensity = reflection;
                    RenderSettings.ambientProbe = probe;
                }
            }
        }

        public override void Dispose()
        {
            try
            {
                Ee4v.Core.Settings.GlobalDataSettings.PathChanged -= ReloadPresets;
                if (_previewLight != null) { UnityEngine.Object.DestroyImmediate(_previewLight.gameObject); }
                _previewLight = null;
                if (_volumeAtlas != null) { UnityEngine.Object.DestroyImmediate(_volumeAtlas); }
                _volumeAtlas = null;
            }
            finally { base.Dispose(); }
        }
        private static string Text(string key) => I18N.Get("workflow.lighting." + key);
    }
}
