using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ee4v.AssetManager.Simulation
{
    /// <summary>Temporary Play Mode lighting used by both the inspection UI and automation.</summary>
    public sealed partial class AvatarLightingRenderer : IDisposable
    {
        private readonly GameObject _avatar;
        private Light _light;

        public AvatarLightingRenderer(GameObject avatar)
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode || avatar == null || !avatar.scene.IsValid() ||
                EditorUtility.IsPersistent(avatar)) throw new InvalidOperationException("A live Play Mode avatar is required.");
            _avatar = avatar;
            var obj = new GameObject("ee4v Preview Light") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(obj, avatar.scene);
            _light = obj.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.enabled = false;
            _light.shadows = LightShadows.None;
        }

        public void Render(Camera camera, LightingPreset settings)
        {
            if (_light == null) throw new ObjectDisposedException(nameof(AvatarLightingRenderer));
            if (camera == null || settings == null || !settings.ValidSettings) throw new ArgumentException("Invalid lighting settings.");
            if (settings.Pattern == 6 && !LightingPresetStore.VolumesAvailable) throw new InvalidOperationException("VRC Light Volumes is unavailable.");
            var previousScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(_avatar.scene);
            var lights = _avatar.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
                .Where(light => light != _light).ToArray();
            var enabled = lights.Select(light => light.enabled).ToArray();
            var mode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            var intensity = RenderSettings.ambientIntensity;
            var reflection = RenderSettings.reflectionIntensity;
            var probe = RenderSettings.ambientProbe;
            try
            {
                using (var volumes = new LightVolumePreviewScope())
                {
                    try
                    {
                        foreach (var light in lights) light.enabled = false;
                        RenderSettings.ambientMode = AmbientMode.Flat;
                        RenderSettings.ambientLight = settings.AmbientColor;
                        RenderSettings.ambientIntensity = settings.AmbientIntensity;
                        RenderSettings.reflectionIntensity = settings.ReflectionIntensity;
                        var previewProbe = new SphericalHarmonicsL2();
                        previewProbe.AddAmbientLight(settings.AmbientColor.linear * settings.AmbientIntensity);
                        RenderSettings.ambientProbe = previewProbe;
                        _light.color = settings.LightColor;
                        _light.intensity = settings.LightIntensity;
                        _light.transform.rotation = _avatar.transform.rotation * Quaternion.Euler(settings.Pitch, settings.Yaw, 0);
                        _light.enabled = settings.Pattern < 6;
                        if (settings.Pattern < 6) volumes.Disable();
                        else volumes.Apply(settings, GestureManagerIntegration.VisibleAvatar(_avatar).transform, GetVolumeAtlas());
                        camera.Render();
                    }
                    finally
                    {
                        _light.enabled = false;
                        for (var i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = enabled[i];
                        RenderSettings.ambientMode = mode;
                        RenderSettings.ambientLight = ambient;
                        RenderSettings.ambientIntensity = intensity;
                        RenderSettings.reflectionIntensity = reflection;
                        RenderSettings.ambientProbe = probe;
                    }
                }
            }
            finally { if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene); }
        }

        public static IReadOnlyList<byte[]> Capture(GameObject avatar, IReadOnlyList<LightingPreset> settings,
            int width = 512, int height = 512, float yaw = 0, float pitch = 0, float distanceScale = 1)
        {
            if (settings == null || settings.Count < 1 || settings.Count > 6 || width < 64 || width > 1024 || height < 64 || height > 1024 ||
                float.IsNaN(yaw) || float.IsInfinity(yaw) || float.IsNaN(pitch) || float.IsInfinity(pitch) ||
                float.IsNaN(distanceScale) || float.IsInfinity(distanceScale) || distanceScale < 0.25f || distanceScale > 4)
                throw new ArgumentException("Use 1–6 previews, dimensions 64–1024, and distanceScale 0.25–4.");
            using (var renderer = new AvatarLightingRenderer(avatar))
            {
                var cameraObject = new GameObject("ee4v Lighting Capture") { hideFlags = HideFlags.HideAndDontSave };
                RenderTexture target = null;
                Texture2D pixels = null;
                var previous = RenderTexture.active;
                try
                {
                    SceneManager.MoveGameObjectToScene(cameraObject, avatar.scene);
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                    camera.fieldOfView = 35;
                    camera.nearClipPlane = 0.01f;
                    camera.farClipPlane = 1000;
                    var visible = GestureManagerIntegration.VisibleAvatar(avatar);
                    var bounds = BoundsFor(visible);
                    var radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                    var halfAngle = Mathf.Atan(Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2) * Mathf.Min(1, (float)width / height));
                    var distance = radius / Mathf.Sin(halfAngle) * distanceScale;
                    camera.transform.rotation = avatar.transform.rotation * Quaternion.Euler(pitch, 180 + yaw, 0);
                    camera.transform.position = bounds.center - camera.transform.forward * distance;
                    target = new RenderTexture(width, height, 24);
                    target.Create();
                    camera.targetTexture = target;
                    pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                    var images = new List<byte[]>();
                    foreach (var preset in settings)
                    {
                        renderer.Render(camera, preset);
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        pixels.Apply();
                        images.Add(pixels.EncodeToPNG());
                    }
                    return images;
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                    if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
        }

        private static Bounds BoundsFor(GameObject avatar)
        {
            Bounds? bounds = null;
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>().Where(value => value.enabled))
            {
                var current = renderer.bounds;
                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    var mesh = new Mesh();
                    try
                    {
                        skinned.BakeMesh(mesh);
                        var vertices = mesh.vertices;
                        if (vertices.Length > 0)
                        {
                            current = new Bounds(skinned.transform.TransformPoint(vertices[0]), Vector3.zero);
                            foreach (var vertex in vertices) current.Encapsulate(skinned.transform.TransformPoint(vertex));
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(mesh); }
                }
                if (bounds.HasValue) { var value = bounds.Value; value.Encapsulate(current); bounds = value; }
                else bounds = current;
            }
            return bounds ?? throw new InvalidOperationException("No enabled renderer is available for lighting capture.");
        }

        public void Dispose()
        {
            if (_light != null) UnityEngine.Object.DestroyImmediate(_light.gameObject);
            _light = null;
            if (_volumeAtlas != null) UnityEngine.Object.DestroyImmediate(_volumeAtlas);
            _volumeAtlas = null;
        }
    }
}
