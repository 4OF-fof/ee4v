using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    public sealed partial class AvatarLightingView : AvatarInspectionView
    {
        private readonly List<Action> _restore = new List<Action>();
        private readonly List<Action> _refreshAdvanced = new List<Action>();
        private readonly List<LightingPreset> _previews = new List<LightingPreset>();
        private readonly List<Light> _lights = new List<Light>();
        private Light _previewLight;
        private VisualElement _advanced;
        private UiTextElement _selection;
        private bool _volumesAvailable;
        private readonly string[] _patternNames = { "scene", "day", "overcast", "night", "warm", "backlight",
            "lv" };

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
            _advanced = AddAdvancedSettings();
            _advanced.Add(UiTextFactory.Create(Text("advancedHint"), UiClassNames.SecondaryText));
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
            var ambientMode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            var intensity = RenderSettings.ambientIntensity;
            var reflection = RenderSettings.reflectionIntensity;
            var ambientProbe = RenderSettings.ambientProbe;
            _restore.Add(() =>
            {
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientIntensity = intensity;
                RenderSettings.reflectionIntensity = reflection;
                RenderSettings.ambientProbe = ambientProbe;
            });
            var ambientField = UiTextFactory.CreateColorField(Text("ambient"));
            ambientField.SetValueWithoutNotify(ambient);
            _refreshAdvanced.Add(() => ambientField.SetValueWithoutNotify(ambient));
            ambientField.RegisterValueChangedCallback(evt =>
            { RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = evt.newValue; Repaint(); });
            _advanced.Add(ambientField);
            AddFloat(_advanced, Text("ambientIntensity"), intensity, value => RenderSettings.ambientIntensity = value);
            AddFloat(_advanced, Text("reflection"), reflection, value => RenderSettings.reflectionIntensity = value);
            var components = avatar.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            _lights.AddRange(components.OfType<Light>().Where(light => light != _previewLight));
            foreach (var light in _lights.Where(light => !light.transform.IsChildOf(avatar.transform)))
            {
                var originalIntensity = light.intensity;
                var originalColor = light.color;
                var originalRotation = light.transform.localRotation;
                var originalEnabled = light.enabled;
                _restore.Add(() =>
                {
                    if (light == null) { return; }
                    light.intensity = originalIntensity; light.color = originalColor;
                    light.enabled = originalEnabled; light.transform.localRotation = originalRotation;
                });
                _advanced.Add(UiTextFactory.Create(light.name, UiClassNames.SectionTitle));
                var enabled = UiTextFactory.CreateToggle(Text("enabled"));
                enabled.SetValueWithoutNotify(light.enabled);
                _refreshAdvanced.Add(() => enabled.SetValueWithoutNotify(originalEnabled));
                enabled.RegisterValueChangedCallback(evt => light.enabled = evt.newValue);
                _advanced.Add(enabled);
                AddFloat(_advanced, Text("intensity"), light.intensity, value => light.intensity = value);
                var color = UiTextFactory.CreateColorField(Text("color"));
                color.SetValueWithoutNotify(light.color);
                _refreshAdvanced.Add(() => color.SetValueWithoutNotify(originalColor));
                color.RegisterValueChangedCallback(evt => light.color = evt.newValue);
                _advanced.Add(color);
                AddFloat(_advanced, Text("pitch"), light.transform.localEulerAngles.x,
                    value => { var angles = light.transform.localEulerAngles; angles.x = value; light.transform.localEulerAngles = angles; }, false);
                AddFloat(_advanced, Text("yaw"), light.transform.localEulerAngles.y,
                    value => { var angles = light.transform.localEulerAngles; angles.y = value; light.transform.localEulerAngles = angles; }, false);
            }
            var volumes = components.Where(component => component != null &&
                (component.GetType().FullName == "VRCLightVolumes.LightVolume" ||
                 component.GetType().FullName == "VRCLightVolumes.PointLightVolume")).ToArray();
            if (_volumesAvailable && volumes.Length == 0) { _advanced.Add(UiTextFactory.Create(Text("noVolumes"), UiClassNames.SecondaryText)); }
            foreach (var volume in volumes)
            {
                _advanced.Add(UiTextFactory.Create(volume.name + " (Light Volume)", UiClassNames.SectionTitle));
                AddVolumeControls(volume);
            }
            _advanced.Add(new UiButton(Text("reset"), () =>
            {
                Restore();
                foreach (var refresh in _refreshAdvanced) { refresh(); }
                Repaint();
            }));
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
            _previewSettings.style.display = pattern != 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _directionalSettings.style.display = pattern > 0 && pattern < 6 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_volumeSettings != null)
            { _volumeSettings.style.display = pattern == 6 ? DisplayStyle.Flex : DisplayStyle.None; }
        }

        protected override void RenderPreview(Camera camera, int index)
        {
            var settings = _previews[index];
            var pattern = settings.Pattern;
            if (pattern == 0 || _previewLight == null) { camera.Render(); return; }
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
                    else { volumes.Apply(settings.VolumeColor, VisibleAvatar.transform.position, VisibleAvatar.transform.rotation, GetVolumeAtlas()); }
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

        private void AddVolumeControls(Component volume)
        {
            var type = volume.GetType();
            var intensityField = type.GetField("Intensity");
            var colorField = type.GetField("Color");
            if (intensityField?.FieldType != typeof(float) || colorField?.FieldType != typeof(Color)) { return; }
            var intensity = (float)intensityField.GetValue(volume);
            var color = (Color)colorField.GetValue(volume);
            var rotation = volume.transform.localRotation;
            _restore.Add(() =>
            {
                if (volume == null) { return; }
                intensityField.SetValue(volume, intensity); colorField.SetValue(volume, color);
                volume.transform.localRotation = rotation;
                SyncVolume(volume);
            });
            AddFloat(_advanced, Text("intensity"), intensity, value =>
            { if (volume != null) { intensityField.SetValue(volume, value); SyncVolume(volume); } });
            var field = UiTextFactory.CreateColorField(Text("color"));
            field.SetValueWithoutNotify(color);
            _refreshAdvanced.Add(() => field.SetValueWithoutNotify(color));
            field.RegisterValueChangedCallback(evt =>
            { if (volume != null) { colorField.SetValue(volume, evt.newValue); SyncVolume(volume); } });
            _advanced.Add(field);
            AddFloat(_advanced, Text("yaw"), volume.transform.localEulerAngles.y, value =>
            {
                if (volume == null) { return; }
                var angles = volume.transform.localEulerAngles; angles.y = value;
                volume.transform.localEulerAngles = angles;
                SyncVolume(volume);
            }, false);
        }

        private void SyncVolume(Component volume)
        {
            try
            {
                volume.GetType().GetMethod("SyncUdonScript", Type.EmptyTypes)?.Invoke(volume, null);
                var setup = volume.GetType().GetField("LightVolumeSetup")?.GetValue(volume) as Component;
                setup?.GetType().GetMethod("SyncUdonScript", Type.EmptyTypes)?.Invoke(setup, null);
            }
            catch (Exception exception)
            { Status.SetText(I18N.Get("workflow.lighting.volumeFailed", volume.name, exception.GetBaseException().Message)); }
        }

        private void AddFloat(VisualElement parent, string label, float initial, Action<float> changed, bool nonnegative = true)
        {
            var field = UiTextFactory.CreateFloatField(label);
            field.SetValueWithoutNotify(initial);
            _refreshAdvanced.Add(() => field.SetValueWithoutNotify(initial));
            field.RegisterValueChangedCallback(evt =>
            {
                if (float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)) { return; }
                var value = nonnegative ? Mathf.Max(0, evt.newValue) : evt.newValue;
                field.SetValueWithoutNotify(value);
                changed(value);
                Repaint();
            });
            parent.Add(field);
        }

        private void Restore()
        {
            foreach (var restore in _restore) { restore(); }
        }

        public override void Dispose()
        {
            try
            {
                Restore();
                _restore.Clear();
                Ee4v.Core.Settings.GlobalDataSettings.PathChanged -= ReloadPresets;
                _refreshAdvanced.Clear();
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
