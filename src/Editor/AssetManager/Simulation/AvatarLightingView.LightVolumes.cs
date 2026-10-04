using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.AssetManager.Simulation
{
    public sealed partial class AvatarLightingView
    {
        private Texture3D _volumeAtlas;

        private Texture3D GetVolumeAtlas()
        {
            if (_volumeAtlas != null) { return _volumeAtlas; }
            _volumeAtlas = new Texture3D(6, 2, 2, TextureFormat.RGBAHalf, false)
            {
                name = "ee4v Preview Light Volume", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[24];
            for (var z = 0; z < 2; z++)
            for (var y = 0; y < 2; y++)
            for (var x = 0; x < 6; x++)
            {
                pixels[x + 6 * (y + 2 * z)] = x < 2 ? new Color(0.55f, 0.55f, 0.55f, 0.22f)
                    : x < 4 ? new Color(0, 0, 0, 0.22f) : new Color(0.1f, 0.1f, 0.1f, 0.22f);
            }
            _volumeAtlas.SetPixels(pixels);
            _volumeAtlas.Apply(false, true);
            return _volumeAtlas;
        }

        private sealed class LightVolumePreviewScope : IDisposable
        {
            private static readonly string[] FloatNames =
            {
                "_UdonLightVolumeEnabled", "_UdonLightVolumeVersion", "_UdonLightVolumeCount",
                "_UdonLightVolumeAdditiveCount", "_UdonLightVolumeOcclusionCount", "_UdonLightVolumeProbesBlend",
                "_UdonLightVolumeSharpBounds", "_UdonLightVolumeAdditiveMaxOverdraw", "_UdonPointLightVolumeCount"
            };
            private static readonly (string Name, int Count)[] VectorNames =
            {
                ("_UdonLightVolumeInvLocalEdgeSmooth", 32), ("_UdonLightVolumeColor", 32),
                ("_UdonLightVolumeRotation", 64), ("_UdonLightVolumeRotationQuaternion", 32),
                ("_UdonLightVolumeUvw", 192), ("_UdonLightVolumeUvwScale", 96), ("_UdonLightVolumeOcclusionUvw", 32)
            };
            private readonly float[] _floats = new float[FloatNames.Length];
            private readonly Dictionary<string, Vector4[]> _vectors = new Dictionary<string, Vector4[]>();
            private readonly Matrix4x4[] _matrices;
            private readonly Texture _atlas;

            internal LightVolumePreviewScope()
            {
                for (var i = 0; i < FloatNames.Length; i++) { _floats[i] = Shader.GetGlobalFloat(FloatNames[i]); }
                foreach (var entry in VectorNames)
                {
                    var values = Shader.GetGlobalVectorArray(entry.Name);
                    _vectors.Add(entry.Name, values != null && values.Length > 0 ? values : new Vector4[entry.Count]);
                }
                var matrices = Shader.GetGlobalMatrixArray("_UdonLightVolumeInvWorldMatrix");
                _matrices = matrices != null && matrices.Length > 0 ? matrices : new Matrix4x4[32];
                _atlas = Shader.GetGlobalTexture("_UdonLightVolume");
            }

            internal void Disable() { Shader.SetGlobalFloat("_UdonLightVolumeEnabled", 0); }

            internal void Apply(Color volumeColor, Vector3 origin, Quaternion rotation, Texture3D atlas)
            {
                Shader.SetGlobalFloat("_UdonLightVolumeEnabled", 1);
                Shader.SetGlobalFloat("_UdonLightVolumeVersion", 2);
                Shader.SetGlobalFloat("_UdonLightVolumeCount", 1);
                Shader.SetGlobalFloat("_UdonLightVolumeAdditiveCount", 0);
                Shader.SetGlobalFloat("_UdonLightVolumeOcclusionCount", 0);
                Shader.SetGlobalFloat("_UdonLightVolumeProbesBlend", 0);
                Shader.SetGlobalFloat("_UdonLightVolumeSharpBounds", 1);
                Shader.SetGlobalFloat("_UdonLightVolumeAdditiveMaxOverdraw", 4);
                Shader.SetGlobalFloat("_UdonPointLightVolumeCount", 0);
                Shader.SetGlobalTexture("_UdonLightVolume", atlas);
                var matrices = new Matrix4x4[32];
                matrices[0] = Matrix4x4.TRS(origin + rotation * Vector3.up, rotation, Vector3.one * 20).inverse;
                Shader.SetGlobalMatrixArray("_UdonLightVolumeInvWorldMatrix", matrices);
                var linearColor = volumeColor.linear;
                var color = new Vector4(linearColor.r, linearColor.g, linearColor.b, 0);
                SetVectors("_UdonLightVolumeColor", 32, color);
                SetVectors("_UdonLightVolumeInvLocalEdgeSmooth", 32, Vector4.one * 100);
                SetVectors("_UdonLightVolumeOcclusionUvw", 32, -Vector4.one);
                SetVectors("_UdonLightVolumeRotationQuaternion", 32, new Vector4(0, 0, 0, 1));
                var uvw = new Vector4[192];
                var scale = new Vector4[96];
                for (var i = 0; i < 3; i++)
                {
                    uvw[i * 2] = new Vector4((i * 2 + 0.5f) / 6, 0.25f, 0.25f, 0);
                    uvw[i * 2 + 1] = new Vector4((i * 2 + 1.5f) / 6, 0.75f, 0.75f, 0);
                    scale[i] = uvw[i * 2];
                    scale[i].w = i == 0 ? 1f / 6 : 0.5f;
                }
                Shader.SetGlobalVectorArray("_UdonLightVolumeUvw", uvw);
                Shader.SetGlobalVectorArray("_UdonLightVolumeUvwScale", scale);
            }

            private static void SetVectors(string name, int count, params Vector4[] values)
            {
                var data = new Vector4[count];
                Array.Copy(values, data, values.Length);
                Shader.SetGlobalVectorArray(name, data);
            }

            public void Dispose()
            {
                for (var i = 0; i < FloatNames.Length; i++) { Shader.SetGlobalFloat(FloatNames[i], _floats[i]); }
                foreach (var entry in _vectors) { Shader.SetGlobalVectorArray(entry.Key, entry.Value); }
                Shader.SetGlobalMatrixArray("_UdonLightVolumeInvWorldMatrix", _matrices);
                Shader.SetGlobalTexture("_UdonLightVolume", _atlas);
            }
        }
    }
}
