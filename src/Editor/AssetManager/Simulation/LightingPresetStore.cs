using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEngine;

namespace Ee4v.AssetManager.Simulation
{
    [Serializable]
    public sealed class LightingPreset
    {
        public string Name = string.Empty;
        public int Pattern;
        public Color AmbientColor = new Color(0.22f, 0.24f, 0.28f);
        public float AmbientIntensity = 1;
        public float ReflectionIntensity = 0.5f;
        public Color LightColor = Color.white;
        public float LightIntensity = 1;
        public float Pitch = 35;
        public float Yaw = 160;
        public Color VolumeColor = Color.white;
        [NonSerialized] public string BuiltinKey;
        [NonSerialized] public bool Modified;

        public LightingPreset Copy() => (LightingPreset)MemberwiseClone();
        public bool Matches(LightingPreset other) => other != null && Pattern == other.Pattern &&
            AmbientColor == other.AmbientColor && AmbientIntensity == other.AmbientIntensity &&
            ReflectionIntensity == other.ReflectionIntensity && LightColor == other.LightColor &&
            LightIntensity == other.LightIntensity && Pitch == other.Pitch && Yaw == other.Yaw && VolumeColor == other.VolumeColor;
        public bool IsValid => !string.IsNullOrWhiteSpace(Name) && ValidSettings;
        public bool ValidSettings => Pattern >= 1 && Pattern <= 6 && ValidColor(AmbientColor) &&
            ValidColor(LightColor) && ValidColor(VolumeColor) && Finite(AmbientIntensity) && AmbientIntensity >= 0 &&
            Finite(ReflectionIntensity) && ReflectionIntensity >= 0 && Finite(LightIntensity) && LightIntensity >= 0 &&
            Finite(Pitch) && Finite(Yaw);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidColor(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b) && Finite(value.a);
    }

    public static class LightingPresetStore
    {
        public static readonly IReadOnlyList<string> BuiltinKeys = Array.AsReadOnly(new[] { "day", "night", "warm", "cold", "backlight", "lv" });
        public static bool VolumesAvailable => AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            assembly.GetType("VRCLightVolumes.LightVolumeManager", false) != null);
        public static event Action Changed;
        private static string PresetPath => Path.Combine(GlobalDataSettings.RootDirectory, "preset", "lighting.json");
        [Serializable] private sealed class PresetFile
        {
            public int Version = 1;
            public List<LightingPreset> Presets = new List<LightingPreset>();
        }

        public static LightingPreset CreateBuiltin(int pattern)
        {
            if (pattern < 1 || pattern > 6) throw new ArgumentOutOfRangeException(nameof(pattern));
            var preset = new LightingPreset { Pattern = pattern, BuiltinKey = BuiltinKeys[pattern - 1] };
            if (pattern == 2)
            {
                preset.AmbientColor = new Color(0.025f, 0.035f, 0.08f);
                preset.ReflectionIntensity = 0.1f;
                preset.LightColor = new Color(0.45f, 0.6f, 1);
                preset.LightIntensity = 0.15f;
            }
            if (pattern == 3) preset.LightColor = new Color(1, 0.65f, 0.35f);
            if (pattern == 4) preset.LightColor = new Color(0.45f, 0.65f, 1);
            if (pattern == 5) preset.Yaw = 0;
            if (pattern == 6)
            {
                preset.AmbientColor = new Color(0.18f, 0.18f, 0.18f);
                preset.ReflectionIntensity = 0.2f;
                preset.LightIntensity = 2;
            }
            return preset;
        }

        public static IReadOnlyList<LightingPreset> Read()
        {
            if (!File.Exists(PresetPath)) return Array.Empty<LightingPreset>();
            var file = JsonUtility.FromJson<PresetFile>(File.ReadAllText(PresetPath));
            if (file == null || file.Version != 1 || file.Presets == null ||
                file.Presets.Any(preset => preset == null || !preset.IsValid) ||
                file.Presets.GroupBy(preset => preset.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
                throw new InvalidDataException("Invalid lighting preset file; it will not be overwritten.");
            return file.Presets;
        }

        public static void Upsert(LightingPreset preset)
        {
            if (preset == null || !preset.IsValid) throw new ArgumentException("A name and valid lighting settings are required.");
            preset = preset.Copy();
            preset.Name = preset.Name.Trim();
            if (BuiltinKeys.Any(key => string.Equals(key, preset.Name, StringComparison.OrdinalIgnoreCase) ||
                Ee4v.Core.I18n.I18N.Get("workflow.lighting." + key) == preset.Name))
                throw new ArgumentException("Built-in lighting preset names are reserved.");
            preset.BuiltinKey = null;
            preset.Modified = false;
            var presets = Read().Where(value => value.Name != preset.Name).ToList();
            presets.Add(preset);
            Persist(presets);
        }

        public static void Delete(string name)
        {
            var presets = Read().ToList();
            if (presets.RemoveAll(preset => preset.Name == name) == 0) throw new ArgumentException("Lighting preset not found.");
            Persist(presets);
        }

        private static void Persist(List<LightingPreset> presets)
        {
            var path = PresetPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(new PresetFile { Presets = presets }, true));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Changed?.Invoke();
        }
    }
}
