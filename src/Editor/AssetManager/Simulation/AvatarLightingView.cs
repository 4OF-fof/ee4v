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
        private readonly int[] _patterns = { 1, 3, 6, 6 };
        private readonly Color[] _volumeColors = { Color.white, Color.white, Color.white, new Color(1, 0.65f, 0.35f) };
        private readonly List<UiButton> _patternButtons = new List<UiButton>();
        private readonly List<UiButton> _layoutButtons = new List<UiButton>();
        private readonly List<Light> _lights = new List<Light>();
        private Light _previewLight;
        private VisualElement _advanced;
        private UiTextElement _selection;
        private readonly VisualElement _volumeControls;
        private readonly ColorField _volumeColor;
        private readonly string[] _patternNames = { "scene", "day", "overcast", "night", "warm", "backlight",
            "lv" };

        protected override GameObject VisibleAvatar => GestureManagerIntegration.GetVisibleAvatar(
            GestureManagerIntegration.FindManager(Avatar)?.Module as BlackStartX.GestureManager.Editor.Modules.Vrc3.ModuleVrc3) ?? Avatar;

        public AvatarLightingView(GameObject avatar, Action repaint)
            : base(avatar, repaint, Text("title"))
        {
            Controls.Add(UiTextFactory.Create(Text("patterns"), UiClassNames.SectionTitle));
            _selection = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            Controls.Add(_selection);
            var patterns = new VisualElement();
            patterns.AddToClassList("ee4v-inspection__choices");
            Controls.Add(patterns);
            var volumesAvailable = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                assembly.GetType("VRCLightVolumes.LightVolumeManager", false) != null);
            if (!volumesAvailable) { _patterns[2] = 2; _patterns[3] = 5; }
            for (var i = 0; i < (volumesAvailable ? _patternNames.Length : 6); i++)
            {
                var pattern = i;
                var button = new UiButton(Text(_patternNames[i]), () =>
                { _patterns[ActivePreview] = pattern; RefreshSelection(); Repaint(); });
                button.AddToClassList("ee4v-inspection__choice");
                patterns.Add(button);
                _patternButtons.Add(button);
            }
            if (volumesAvailable)
            {
                _volumeControls = new VisualElement();
                _volumeColor = UiTextFactory.CreateColorField(Text("lvColor"));
                _volumeColor.showAlpha = false;
                _volumeColor.RegisterValueChangedCallback(evt =>
                { _volumeColors[ActivePreview] = evt.newValue; Repaint(); });
                _volumeControls.Add(_volumeColor);
                _volumeControls.Add(UiTextFactory.Create(Text("lvHint"), UiClassNames.SecondaryText));
                Controls.Add(_volumeControls);
            }
            Controls.Add(UiTextFactory.Create(Text("compare"), UiClassNames.SectionTitle));
            var layouts = new VisualElement();
            layouts.AddToClassList("ee4v-inspection__choices");
            Controls.Add(layouts);
            foreach (var count in new[] { 1, 2, 4 })
            {
                var button = new UiButton(Text("layout" + count), () =>
                { SetPreviewCount(count); RefreshSelection(); });
                button.AddToClassList("ee4v-inspection__choice");
                layouts.Add(button);
                _layoutButtons.Add(button);
            }
            Controls.Add(UiTextFactory.Create(Text("compareHint"), UiClassNames.SecondaryText));
            _advanced = AddAdvancedSettings();
            _advanced.Add(UiTextFactory.Create(Text("advancedHint"), UiClassNames.SecondaryText));
            SetPreviewCount(2);
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
            if (volumesAvailable && volumes.Length == 0) { _advanced.Add(UiTextFactory.Create(Text("noVolumes"), UiClassNames.SecondaryText)); }
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

        protected override void ActivePreviewChanged() { RefreshSelection(); }

        private void RefreshSelection()
        {
            if (_selection == null) { return; }
            for (var i = 0; i < PreviewCount; i++)
            { SetPreviewTitle(i, (i + 1) + " · " + Text(_patternNames[_patterns[i]])); }
            _selection.SetText(I18N.Get("workflow.lighting.selected", ActivePreview + 1));
            if (_volumeControls != null)
            {
                _volumeControls.style.display = _patterns[ActivePreview] == 6 ? DisplayStyle.Flex : DisplayStyle.None;
                _volumeColor.SetValueWithoutNotify(_volumeColors[ActivePreview]);
            }
            for (var i = 0; i < _patternButtons.Count; i++)
            { _patternButtons[i].EnableInClassList("ee4v-inspection__choice--selected", _patterns[ActivePreview] == i); }
            for (var i = 0; i < _layoutButtons.Count; i++)
            { _layoutButtons[i].EnableInClassList("ee4v-inspection__choice--selected", PreviewCount == (i == 2 ? 4 : i + 1)); }
        }

        protected override void RenderPreview(Camera camera, int index)
        {
            var pattern = _patterns[index];
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
                    var ambientColor = pattern == 3 ? new Color(0.025f, 0.035f, 0.08f) : new Color(0.22f, 0.24f, 0.28f);
                    if (pattern == 2) { ambientColor = new Color(0.5f, 0.52f, 0.56f); }
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = ambientColor;
                    RenderSettings.ambientIntensity = 1;
                    RenderSettings.reflectionIntensity = pattern == 3 ? 0.1f : 0.5f;
                    var previewProbe = new SphericalHarmonicsL2();
                    previewProbe.AddAmbientLight(ambientColor.linear);
                    RenderSettings.ambientProbe = previewProbe;
                    _previewLight.color = pattern == 4 ? new Color(1, 0.65f, 0.35f) : pattern == 3 ? new Color(0.45f, 0.6f, 1) : Color.white;
                    _previewLight.intensity = pattern == 2 ? 0.25f : pattern == 3 ? 0.15f : 1;
                    _previewLight.transform.rotation = (Avatar != null ? Avatar.transform.rotation : Quaternion.identity) *
                        Quaternion.Euler(35, pattern == 5 ? 0 : 160, 0);
                    _previewLight.enabled = pattern < 6;
                    if (pattern < 6) { volumes.Disable(); }
                    else { volumes.Apply(_volumeColors[index], VisibleAvatar.transform.position, VisibleAvatar.transform.rotation, GetVolumeAtlas()); }
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
