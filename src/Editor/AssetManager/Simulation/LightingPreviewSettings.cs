using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Simulation
{
    [FilePath("UserSettings/ee4v.lighting.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class LightingPreviewSettings : ScriptableSingleton<LightingPreviewSettings>
    {
        [Serializable]
        private sealed class PreviewState
        {
            public LightingPreset Preset;
            public string BuiltinKey;
            public bool Modified;
        }

        [SerializeField] private List<PreviewState> _previews = new List<PreviewState>();
        [SerializeField] private int _selected;

        internal int Selected => _selected;

        internal List<LightingPreset> Read(int maximumCount, bool volumesAvailable)
        {
            var result = new List<LightingPreset>();
            if (_previews != null)
            {
                foreach (var state in _previews)
                {
                    if (result.Count >= maximumCount) { break; }
                    if (state?.Preset == null || !state.Preset.ValidSettings) { continue; }
                    var preset = state.Preset.Copy();
                    preset.BuiltinKey = state.BuiltinKey;
                    preset.Modified = state.Modified;
                    result.Add(preset.Pattern == 6 && !volumesAvailable
                        ? LightingPresetStore.CreateBuiltin(1) : preset);
                }
            }
            if (result.Count == 0)
            {
                result.Add(LightingPresetStore.CreateBuiltin(1));
                result.Add(LightingPresetStore.CreateBuiltin(2));
            }
            return result;
        }

        internal void Write(IReadOnlyList<LightingPreset> previews, int selected)
        {
            var previous = JsonUtility.ToJson(this);
            _previews = new List<PreviewState>();
            foreach (var preset in previews)
            {
                _previews.Add(new PreviewState
                {
                    Preset = preset.Copy(), BuiltinKey = preset.BuiltinKey, Modified = preset.Modified
                });
            }
            _selected = selected;
            if (JsonUtility.ToJson(this) != previous) { Save(true); }
        }
    }
}
