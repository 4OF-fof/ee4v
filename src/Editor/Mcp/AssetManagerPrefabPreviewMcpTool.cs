using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ee4v.Core.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ee4v.Mcp
{
    internal static class AssetManagerPrefabPreviewMcpTool
    {
        private const string CacheDirectoryName = "asset-preview";
        private const string RenderProfileVersion = "v1";
        private const int DefaultSize = 1024;
        private const int MinimumSize = 128;
        private const int MaximumSize = 2048;

        private static readonly string[] TurntableViews =
        {
            "front",
            "frontRight",
            "right",
            "backRight",
            "back",
            "backLeft",
            "left",
            "frontLeft"
        };

        private static readonly HashSet<string> ViewPresets =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "front",
                "back",
                "left",
                "right",
                "threeQuarter",
                "turntable"
            };

        private static readonly HashSet<string> Backgrounds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "neutral",
                "light",
                "dark",
                "transparent"
            };

        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_render_prefab_preview",
                "Loads an already imported Project Prefab into an isolated Unity Preview scene and returns a reproducible PNG. It never uses AssetPreview thumbnails, enters Play Mode, places the Prefab in a user Scene, or saves Project assets.",
                McpSchemas.Object(new JObject
                {
                    ["prefabGuid"] = McpSchemas.String(
                        "GUID of an already imported Project Prefab."),
                    ["prefabPath"] = McpSchemas.String(
                        "Optional Assets/ or Packages/ path. Paths outside the Project are rejected."),
                    ["viewPreset"] = McpSchemas.Enum(
                        "front",
                        "back",
                        "left",
                        "right",
                        "threeQuarter",
                        "turntable"),
                    ["width"] = McpSchemas.Integer(),
                    ["height"] = McpSchemas.Integer(),
                    ["background"] = McpSchemas.Enum(
                        "neutral",
                        "light",
                        "dark",
                        "transparent"),
                    ["forceRefresh"] = McpSchemas.Boolean()
                }),
                Render,
                readOnly: true));
        }

        private static Task<McpToolResult> Render(JObject arguments)
        {
            var resolved = AssetManagerPrefabMcpTools.Resolve(
                (string)arguments["prefabGuid"],
                (string)arguments["prefabPath"]);
            var view = ((string)arguments["viewPreset"] ?? "threeQuarter")
                .Trim();
            var background = ((string)arguments["background"] ?? "neutral")
                .Trim();
            RequireChoice(view, "viewPreset", ViewPresets);
            RequireChoice(background, "background", Backgrounds);
            var width = Mathf.Clamp(
                (int?)arguments["width"] ?? DefaultSize,
                MinimumSize,
                MaximumSize);
            var height = Mathf.Clamp(
                (int?)arguments["height"] ?? DefaultSize,
                MinimumSize,
                MaximumSize);
            var forceRefresh = (bool?)arguments["forceRefresh"] ?? false;
            var cache = CachePaths(
                resolved,
                view,
                width,
                height,
                background);

            if (!forceRefresh && File.Exists(cache.ImagePath))
            {
                var cached = File.ReadAllBytes(cache.ImagePath);
                if (cached.Length > 0)
                {
                    return Task.FromResult(McpToolResult.Image(
                        ResultMetadata(
                            resolved,
                            view,
                            width,
                            height,
                            background,
                            cache.ImagePath,
                            true,
                            File.GetLastWriteTimeUtc(cache.ImagePath)),
                        cached));
                }
            }

            Directory.CreateDirectory(cache.DirectoryPath);
            try
            {
                AssetManagerPrefabMcpTools.EnsurePreviewable(resolved);
                byte[] bytes;
                using (var renderer = new PrefabPreviewRenderer(
                           resolved.Asset,
                           BackgroundColor(background)))
                {
                    bytes = renderer.Render(view, width, height);
                }

                if (bytes == null || bytes.Length == 0)
                {
                    throw new McpToolException(
                        "prefab_preview_render_failed",
                        "Unity did not produce a PNG for the Prefab preview.");
                }

                File.WriteAllBytes(cache.ImagePath, bytes);
                var createdAt = DateTime.UtcNow;
                File.WriteAllText(
                    cache.MetadataPath,
                    ResultMetadata(
                            resolved,
                            view,
                            width,
                            height,
                            background,
                            cache.ImagePath,
                            false,
                            createdAt,
                            string.Empty)
                        .ToString(Formatting.Indented));
                if (File.Exists(cache.ErrorPath))
                {
                    File.Delete(cache.ErrorPath);
                }

                return Task.FromResult(McpToolResult.Image(
                    ResultMetadata(
                        resolved,
                        view,
                        width,
                        height,
                        background,
                        cache.ImagePath,
                        false,
                        createdAt),
                    bytes));
            }
            catch (Exception exception)
            {
                WriteErrorMetadata(
                    cache.ErrorPath,
                    resolved,
                    view,
                    width,
                    height,
                    background,
                    cache.ImagePath,
                    exception);
                throw;
            }
        }

        private static JObject ResultMetadata(
            AssetManagerPrefabMcpTools.ResolvedPrefab resolved,
            string view,
            int width,
            int height,
            string background,
            string imagePath,
            bool cacheHit,
            DateTime createdAt,
            string error = null)
        {
            var pipeline = RenderPipelineName();
            return new JObject
            {
                ["ok"] = string.IsNullOrEmpty(error),
                ["prefabGuid"] = resolved.Guid,
                ["assetPath"] = resolved.Path,
                ["prefabType"] = resolved.AssetType.ToString(),
                ["dependencyHash"] = resolved.DependencyHash,
                ["renderProfileVersion"] = RenderProfileVersion,
                ["view"] = view,
                ["turntableViews"] = view == "turntable"
                    ? new JArray(TurntableViews)
                    : new JArray(),
                ["imagePath"] = imagePath,
                ["width"] = width,
                ["height"] = height,
                ["background"] = background,
                ["cacheHit"] = cacheHit,
                ["unityVersion"] = Application.unityVersion,
                ["renderPipeline"] = pipeline,
                ["createdAtUtc"] = createdAt.ToString("O"),
                ["error"] = error ?? string.Empty
            };
        }

        private static void WriteErrorMetadata(
            string errorPath,
            AssetManagerPrefabMcpTools.ResolvedPrefab resolved,
            string view,
            int width,
            int height,
            string background,
            string imagePath,
            Exception exception)
        {
            try
            {
                var metadata = ResultMetadata(
                    resolved,
                    view,
                    width,
                    height,
                    background,
                    imagePath,
                    false,
                    DateTime.UtcNow,
                    exception.Message);
                metadata["errorType"] = exception.GetType().FullName;
                File.WriteAllText(
                    errorPath,
                    metadata.ToString(Formatting.Indented));
            }
            catch (Exception cacheException)
            {
                Debug.LogWarning(
                    "ee4v could not write Prefab preview error metadata: " +
                    cacheException.Message);
            }
        }

        private static CacheLocation CachePaths(
            AssetManagerPrefabMcpTools.ResolvedPrefab resolved,
            string view,
            int width,
            int height,
            string background)
        {
            var root = CacheRoot();
            var directory = Path.Combine(
                root,
                CacheDirectoryName,
                SafeSegment(resolved.Guid),
                SafeSegment(resolved.DependencyHash));
            var stem = RenderProfileVersion + "-" +
                       SafeSegment(view) + "-" +
                       width + "x" + height + "-" +
                       SafeSegment(background);
            return new CacheLocation
            {
                DirectoryPath = directory,
                ImagePath = Path.Combine(directory, stem + ".png"),
                MetadataPath = Path.Combine(directory, stem + ".json"),
                ErrorPath = Path.Combine(directory, stem + ".error.json")
            };
        }

        private static string CacheRoot()
        {
            var configured = Path.GetFullPath(GlobalDataSettings.RootDirectory);
            var assets = Path.GetFullPath(Application.dataPath);
            if (!IsWithin(configured, assets))
            {
                return configured;
            }

            var project = Directory.GetParent(assets)?.FullName ?? assets;
            return Path.Combine(project, "Library", "ee4v-cache");
        }

        private static bool IsWithin(string path, string parent)
        {
            var normalizedPath = path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedParent = parent.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return normalizedPath.StartsWith(
                normalizedParent,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeSegment(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? string.Empty)
                .Where(character => !invalid.Contains(character) &&
                                    character != '/' &&
                                    character != '\\')
                .ToArray());
        }

        private static string RenderPipelineName()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline == null
                ? "Built-in Render Pipeline"
                : pipeline.GetType().FullName + " (" + pipeline.name + ")";
        }

        private static void RequireChoice(
            string value,
            string inputName,
            ISet<string> allowed)
        {
            if (allowed.Contains(value))
            {
                return;
            }

            throw new McpToolException(
                "invalid_request",
                inputName + " must be one of: " +
                string.Join(", ", allowed.OrderBy(item => item)) + ".");
        }

        private static Color BackgroundColor(string background)
        {
            switch (background)
            {
                case "light":
                    return new Color32(186, 191, 201, 255);
                case "dark":
                    return new Color32(30, 32, 36, 255);
                case "transparent":
                    return new Color(0f, 0f, 0f, 0f);
                default:
                    return new Color32(82, 86, 95, 255);
            }
        }

        private sealed class CacheLocation
        {
            internal string DirectoryPath { get; set; }
            internal string ImagePath { get; set; }
            internal string MetadataPath { get; set; }
            internal string ErrorPath { get; set; }
        }

        private sealed class PrefabPreviewRenderer : IDisposable
        {
            private const float NormalizedMaximumSize = 2f;
            private const float CameraFieldOfView = 30f;
            private readonly PreviewRenderUtility _utility;
            private readonly GameObject _instance;
            private readonly Color _background;
            private Bounds _bounds;

            internal PrefabPreviewRenderer(
                GameObject prefab,
                Color background)
            {
                _background = background;
                _utility = new PreviewRenderUtility();
                try
                {
                    _instance = PrefabUtility.InstantiatePrefab(
                        prefab,
                        PreviewScene(_utility)) as GameObject;
                    if (_instance == null)
                    {
                        throw new McpToolException(
                            "prefab_preview_instantiate_failed",
                            "Unity could not instantiate the Prefab in the isolated Preview scene.");
                    }

                    _instance.name = prefab.name + " (ee4v MCP Preview)";
                    SetHideFlags(_instance.transform);
                    DisableBehaviours(_instance);
                    foreach (var renderer in _instance
                                 .GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        renderer.forceMatrixRecalculationPerRender = true;
                    }

                    _bounds = CalculateBounds(_instance);
                    NormalizeScale();
                    ConfigureUtility();
                }
                catch
                {
                    _utility.Cleanup();
                    throw;
                }
            }

            internal byte[] Render(string view, int width, int height)
            {
                if (view == "turntable")
                {
                    return RenderTurntable(width, height);
                }

                var texture = RenderView(
                    ViewDirection(view),
                    width,
                    height);
                try
                {
                    return texture.EncodeToPNG();
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }

            public void Dispose()
            {
                if (_instance != null)
                {
                    Object.DestroyImmediate(_instance);
                }

                _utility.Cleanup();
            }

            private void ConfigureUtility()
            {
                _utility.cameraFieldOfView = CameraFieldOfView;
                _utility.camera.clearFlags = CameraClearFlags.Color;
                _utility.camera.backgroundColor = _background;
                _utility.camera.allowHDR = false;
                _utility.camera.allowMSAA = true;
                _utility.lights[0].intensity = 1.1f;
                _utility.lights[0].color = new Color(1f, 0.97f, 0.92f);
                _utility.lights[0].transform.rotation =
                    Quaternion.Euler(35f, 35f, 0f);
                _utility.lights[1].intensity = 0.65f;
                _utility.lights[1].color = new Color(0.82f, 0.9f, 1f);
                _utility.lights[1].transform.rotation =
                    Quaternion.Euler(340f, 215f, 0f);
            }

            private Texture2D RenderView(
                Vector3 direction,
                int width,
                int height)
            {
                ConfigureCamera(direction, width, height);
                _utility.BeginStaticPreview(new Rect(0f, 0f, width, height));
                _utility.camera.Render();
                var texture = _utility.EndStaticPreview();
                if (texture == null)
                {
                    throw new McpToolException(
                        "prefab_preview_render_failed",
                        "Unity returned no Texture for the Prefab preview.");
                }

                return texture;
            }

            private byte[] RenderTurntable(int width, int height)
            {
                var columns = 4;
                var rows = 2;
                var cellWidth = Math.Max(1, width / columns);
                var cellHeight = Math.Max(1, height / rows);
                var sheet = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                var fill = Enumerable.Repeat(
                        (Color32)_background,
                        width * height)
                    .ToArray();
                sheet.SetPixels32(fill);

                try
                {
                    for (var index = 0; index < TurntableViews.Length; index++)
                    {
                        var texture = RenderView(
                            TurntableDirection(index),
                            cellWidth,
                            cellHeight);
                        try
                        {
                            var column = index % columns;
                            var rowFromTop = index / columns;
                            var x = column * cellWidth;
                            var y = height - ((rowFromTop + 1) * cellHeight);
                            sheet.SetPixels(
                                x,
                                Math.Max(0, y),
                                cellWidth,
                                cellHeight,
                                texture.GetPixels());
                        }
                        finally
                        {
                            Object.DestroyImmediate(texture);
                        }
                    }

                    sheet.Apply(false, false);
                    return sheet.EncodeToPNG();
                }
                finally
                {
                    Object.DestroyImmediate(sheet);
                }
            }

            private void ConfigureCamera(
                Vector3 direction,
                int width,
                int height)
            {
                var aspect = Math.Max(0.01f, width / (float)height);
                var verticalHalf = CameraFieldOfView * 0.5f * Mathf.Deg2Rad;
                var horizontalHalf = Mathf.Atan(Mathf.Tan(verticalHalf) * aspect);
                var fitHalfAngle = Mathf.Min(verticalHalf, horizontalHalf);
                var radius = Mathf.Max(0.05f, _bounds.extents.magnitude);
                var distance = radius / Mathf.Sin(fitHalfAngle) * 1.08f;
                var normalizedDirection = direction.sqrMagnitude < 0.001f
                    ? Vector3.forward
                    : direction.normalized;
                var camera = _utility.camera;
                camera.aspect = aspect;
                camera.transform.position =
                    _bounds.center + normalizedDirection * distance;
                camera.transform.rotation = Quaternion.LookRotation(
                    _bounds.center - camera.transform.position,
                    Vector3.up);
                camera.nearClipPlane = Mathf.Max(
                    0.001f,
                    distance - radius * 1.5f);
                camera.farClipPlane = distance + radius * 3f;
            }

            private void NormalizeScale()
            {
                var maximum = Mathf.Max(
                    _bounds.size.x,
                    Mathf.Max(_bounds.size.y, _bounds.size.z));
                if (maximum <= 0.0001f ||
                    float.IsNaN(maximum) ||
                    float.IsInfinity(maximum))
                {
                    throw new McpToolException(
                        "prefab_preview_invalid_bounds",
                        "The Prefab has no finite Renderer bounds.");
                }

                var factor = NormalizedMaximumSize / maximum;
                _instance.transform.localScale *= factor;
                _bounds = CalculateBounds(_instance);
            }

            private static Bounds CalculateBounds(GameObject instance)
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled &&
                                       renderer.gameObject.activeInHierarchy)
                    .ToArray();
                if (renderers.Length == 0)
                {
                    throw new McpToolException(
                        "prefab_preview_invalid_bounds",
                        "The Preview instance has no active Renderer bounds.");
                }

                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }

                if (!IsFinite(bounds.center) || !IsFinite(bounds.size))
                {
                    throw new McpToolException(
                        "prefab_preview_invalid_bounds",
                        "The Prefab Renderer bounds contain non-finite values.");
                }

                return bounds;
            }

            private static void DisableBehaviours(GameObject instance)
            {
                foreach (var behaviour in instance
                             .GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour != null)
                    {
                        behaviour.enabled = false;
                    }
                }

                foreach (var particle in instance
                             .GetComponentsInChildren<ParticleSystem>(true))
                {
                    particle.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            private static void SetHideFlags(Transform transform)
            {
                transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
                for (var index = 0; index < transform.childCount; index++)
                {
                    SetHideFlags(transform.GetChild(index));
                }
            }

            private static Scene PreviewScene(PreviewRenderUtility utility)
            {
                var previewSceneProperty = typeof(PreviewRenderUtility)
                    .GetProperty(
                        "previewScene",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                var previewScene = previewSceneProperty?.GetValue(utility);
                var sceneProperty = previewScene?.GetType().GetProperty(
                    "scene",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (!(sceneProperty?.GetValue(previewScene) is Scene scene) ||
                    !scene.IsValid())
                {
                    throw new McpToolException(
                        "prefab_preview_scene_unavailable",
                        "Unity did not expose an isolated Preview scene for safe Prefab instantiation.");
                }

                return scene;
            }

            private static Vector3 ViewDirection(string view)
            {
                switch (view)
                {
                    case "front":
                        return Vector3.forward;
                    case "back":
                        return Vector3.back;
                    case "left":
                        return Vector3.left;
                    case "right":
                        return Vector3.right;
                    default:
                        return new Vector3(1f, 0.15f, 1f).normalized;
                }
            }

            private static Vector3 TurntableDirection(int index)
            {
                var angle = index * 45f * Mathf.Deg2Rad;
                return new Vector3(
                    Mathf.Sin(angle),
                    0.1f,
                    Mathf.Cos(angle)).normalized;
            }

            private static bool IsFinite(Vector3 value)
            {
                return !float.IsNaN(value.x) &&
                       !float.IsNaN(value.y) &&
                       !float.IsNaN(value.z) &&
                       !float.IsInfinity(value.x) &&
                       !float.IsInfinity(value.y) &&
                       !float.IsInfinity(value.z);
            }
        }
    }
}
