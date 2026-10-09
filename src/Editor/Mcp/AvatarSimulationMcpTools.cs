using System;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Simulation;
using Ee4v.AvatarInfo;
using Ee4v.PlayModeComponentSuppression;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class AvatarSimulationMcpTools
    {
        internal static void Register()
        {
            var avatar = new JObject { ["avatarRef"] = McpSchemas.String() };
            McpToolRegistry.Register(new McpToolDefinition("ee4v_get_avatar_performance",
                "Reads the last explicit NDMF/AAO measurement, PC/Quest metrics, parameter breakdown, separate SDK build sizes and timestamps. Never starts a build.",
                McpSchemas.Object(avatar, "avatarRef"), args => Task.FromResult(Performance(Avatar(args))), true));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_measure_avatar_performance",
                "Explicitly measures an isolated avatar copy through all NDMF phases in Edit Mode. Returns errors without discarding previous successful measurements; does not upload or start Play Mode.",
                McpSchemas.Object((JObject)avatar.DeepClone(), "avatarRef"), args =>
                {
                    var target = Avatar(args);
                    if (!AvatarPlayModePerformanceCache.CanBake(target)) throw new McpToolException("measurement_unavailable", "SDK/NDMF must be available and no measurement or Play Mode transition may be active.");
                    AvatarPlayModePerformanceCache.Bake(target);
                    var record = AvatarPlayModePerformanceCache.Get(target);
                    return Task.FromResult(!string.IsNullOrEmpty(record?.Error) ? McpToolResult.Error("measurement_failed", record.Error, Performance(target).StructuredContent) : Performance(target));
                }, false, idempotent: false));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_get_avatar_runtime",
                "Reads GestureManager connection, live avatar reference and all runtime parameters. Does not connect or change parameters.",
                McpSchemas.Object((JObject)avatar.DeepClone(), "avatarRef"), args => Task.FromResult(Runtime(Avatar(args))), true));
            var control = (JObject)avatar.DeepClone();
            control["action"] = McpSchemas.Enum("connect", "setParameters");
            control["parameters"] = McpSchemas.Array(McpSchemas.Object(new JObject { ["name"] = McpSchemas.String(), ["value"] = McpSchemas.Number() }, "name", "value"));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_control_avatar_runtime",
                "Connects an existing GestureManager or sets validated live parameters (including GestureLeft/Right and weights). Play Mode only; no Prefab or Scene assets are saved.",
                McpSchemas.Object(control, "avatarRef", "action"), args =>
                {
                    var target = Avatar(args);
                    switch (McpJson.RequireString(args, "action"))
                    {
                        case "connect": GestureManagerIntegration.Connect(target); break;
                        case "setParameters":
                            var values = (args["parameters"] as JArray ?? throw new McpToolException("invalid_request", "parameters is required."))
                                .Cast<JObject>().ToDictionary(value => McpJson.RequireString(value, "name"), value => RequiredFloat(value, "value"));
                            GestureManagerIntegration.SetParameters(target, values);
                            break;
                        default: throw new McpToolException("invalid_request", "Unknown runtime action.");
                    }
                    return Task.FromResult(Runtime(target));
                }, false, idempotent: false, allowDuringPlayMode: true));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_list_lighting_presets",
                "Lists built-in and saved lighting presets, including VRCLV availability. Saved unavailable VRCLV presets are retained.",
                McpSchemas.Object(), args => Task.FromResult(McpToolResult.Success(new JObject
                {
                    ["ok"] = true, ["volumesAvailable"] = LightingPresetStore.VolumesAvailable,
                    ["builtins"] = new JArray(Enumerable.Range(1, LightingPresetStore.VolumesAvailable ? 6 : 5).Select(value => PresetJson(LightingPresetStore.CreateBuiltin(value)))),
                    ["saved"] = new JArray(LightingPresetStore.Read().Select(PresetJson))
                })), true));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_upsert_lighting_preset",
                "Saves or replaces one named lighting preset through the same store as the UI. Refuses malformed preset files and built-in names.",
                McpSchemas.Object(new JObject { ["name"] = McpSchemas.String(), ["settings"] = PresetSchema() }, "name", "settings"), args =>
                {
                    var preset = ParsePreset(args["settings"] as JObject);
                    preset.Name = McpJson.RequireString(args, "name");
                    LightingPresetStore.Upsert(preset);
                    preset.BuiltinKey = null;
                    preset.Modified = false;
                    return Task.FromResult(McpToolResult.Success(new JObject { ["ok"] = true, ["preset"] = PresetJson(preset) }));
                }, false, allowDuringPlayMode: true));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_delete_lighting_preset",
                "Deletes one saved named lighting preset. Does not change lighting already applied to an inspection preview.",
                McpSchemas.Object(new JObject { ["name"] = McpSchemas.String() }, "name"), args =>
                {
                    var name = McpJson.RequireString(args, "name");
                    LightingPresetStore.Delete(name);
                    return Task.FromResult(McpToolResult.Success(new JObject { ["ok"] = true, ["deletedName"] = name }));
                }, false, destructive: true, idempotent: false, allowDuringPlayMode: true));
            var lighting = (JObject)avatar.DeepClone();
            lighting["previews"] = McpSchemas.Array(PresetSchema(), "1–6 settings objects. Start with a builtin key or saved preset name and optionally override its settings.");
            lighting["width"] = McpSchemas.Integer(); lighting["height"] = McpSchemas.Integer();
            lighting["cameraYaw"] = McpSchemas.Number(); lighting["cameraPitch"] = McpSchemas.Number(); lighting["distanceScale"] = McpSchemas.Number();
            McpToolRegistry.Register(new McpToolDefinition("ee4v_render_lighting_preview",
                "Captures 1–6 PNGs of the live Play Mode avatar under independent lighting settings with the same camera. Restores Scene lights, ambient/reflection and VRCLV shader globals after each render; saves no assets.",
                McpSchemas.Object(lighting, "avatarRef", "previews"), args =>
                {
                    var presets = (args["previews"] as JArray ?? throw new McpToolException("invalid_request", "previews is required.")).Cast<JObject>().Select(ParsePreset).ToArray();
                    var images = AvatarLightingRenderer.Capture(Avatar(args), presets, (int?)args["width"] ?? 512, (int?)args["height"] ?? 512,
                        (float?)args["cameraYaw"] ?? 0, (float?)args["cameraPitch"] ?? 0, (float?)args["distanceScale"] ?? 1);
                    return Task.FromResult(McpToolResult.Images(new JObject { ["ok"] = true, ["previews"] = new JArray(presets.Select(PresetJson)) }, images));
                }, true));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_get_playmode_suppression",
                "Reads configured component type identities (including missing types) and available concrete MonoBehaviours for Play Mode NDMF suppression.",
                McpSchemas.Object(), args => Task.FromResult(Suppression()), true));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_set_playmode_suppression",
                "Replaces Project Play Mode suppression settings. New entries must be available concrete MonoBehaviour types; existing missing identities may be retained.",
                McpSchemas.Object(new JObject { ["types"] = McpSchemas.Array(McpSchemas.String()) }, "types"), args =>
                {
                    var types = args["types"] as JArray ?? throw new McpToolException("invalid_request", "types is required.");
                    PlayModeComponentSuppressionSettings.SetConfiguredTypes(types.Select(value => value.Value<string>()));
                    return Task.FromResult(Suppression());
                }, false));
        }

        private static GameObject Avatar(JObject args) => UnityObjectReference.ResolveGameObject(McpJson.RequireString(args, "avatarRef"));
        private static McpToolResult Runtime(GameObject avatar) => McpToolResult.Success(new JObject
        {
            ["ok"] = true, ["isPlaying"] = EditorApplication.isPlaying,
            ["connected"] = GestureManagerIntegration.IsConnected(avatar),
            ["visibleAvatarRef"] = UnityObjectReference.Create(GestureManagerIntegration.VisibleAvatar(avatar)),
            ["parameters"] = McpJson.From(GestureManagerIntegration.ReadParameters(avatar))
        });
        private static McpToolResult Performance(GameObject avatar) => McpToolResult.Success(new JObject
        {
            ["ok"] = true, ["canMeasure"] = AvatarPlayModePerformanceCache.CanBake(avatar), ["isMeasuring"] = AvatarPlayModePerformanceCache.IsBaking,
            ["measurement"] = McpJson.From(AvatarPlayModePerformanceCache.Get(avatar)),
            ["currentParameters"] = McpJson.From(AvatarInfoSdk.Provider?.ReadParameterMemory(avatar)),
            ["desktopSize"] = McpJson.From(AvatarBuildSizeCache.Get(avatar, false)), ["mobileSize"] = McpJson.From(AvatarBuildSizeCache.Get(avatar, true))
        });
        private static McpToolResult Suppression() => McpToolResult.Success(new JObject
        {
            ["ok"] = true,
            ["configured"] = new JArray(PlayModeComponentSuppressionSettings.GetConfiguredTypes().Select(value => new JObject
                { ["identity"] = value, ["available"] = PlayModeComponentSuppressionSettings.IsAvailable(value) })),
            ["availableTypes"] = new JArray(PlayModeComponentSuppressionSettings.GetAvailableTypes())
        });
        private static float RequiredFloat(JObject value, string name) => value[name]?.Type == JTokenType.Integer || value[name]?.Type == JTokenType.Float
            ? (float)value[name] : throw new McpToolException("invalid_request", name + " must be a number.");
        private static JObject PresetSchema() => McpSchemas.Object(new JObject
        {
            ["preset"] = McpSchemas.String("Builtin day/night/warm/cold/backlight/lv or saved name; defaults to day."),
            ["ambientColor"] = McpSchemas.Array(McpSchemas.Number()), ["ambientIntensity"] = McpSchemas.Number(),
            ["reflectionIntensity"] = McpSchemas.Number(), ["lightColor"] = McpSchemas.Array(McpSchemas.Number()),
            ["lightIntensity"] = McpSchemas.Number(), ["pitch"] = McpSchemas.Number(), ["yaw"] = McpSchemas.Number(),
            ["volumeColor"] = McpSchemas.Array(McpSchemas.Number())
        });
        private static LightingPreset ParsePreset(JObject args)
        {
            if (args == null) throw new McpToolException("invalid_request", "Lighting settings must be an object.");
            var name = (string)args["preset"] ?? "day";
            var index = LightingPresetStore.BuiltinKeys.ToList().IndexOf(name);
            var preset = index >= 0 ? LightingPresetStore.CreateBuiltin(index + 1) :
                LightingPresetStore.Read().FirstOrDefault(value => value.Name == name)?.Copy() ?? throw new McpToolException("preset_not_found", "Lighting preset not found.");
            Color ColorValue(string key, Color fallback)
            {
                if (args[key] == null) return fallback;
                if (!(args[key] is JArray values) || values.Count != 3) throw new McpToolException("invalid_request", key + " requires 3 RGB numbers.");
                return new Color((float)values[0], (float)values[1], (float)values[2]);
            }
            preset.AmbientColor = ColorValue("ambientColor", preset.AmbientColor); preset.LightColor = ColorValue("lightColor", preset.LightColor);
            preset.VolumeColor = ColorValue("volumeColor", preset.VolumeColor);
            preset.AmbientIntensity = (float?)args["ambientIntensity"] ?? preset.AmbientIntensity;
            preset.ReflectionIntensity = (float?)args["reflectionIntensity"] ?? preset.ReflectionIntensity;
            preset.LightIntensity = (float?)args["lightIntensity"] ?? preset.LightIntensity;
            preset.Pitch = (float?)args["pitch"] ?? preset.Pitch; preset.Yaw = (float?)args["yaw"] ?? preset.Yaw;
            if (!preset.ValidSettings) throw new McpToolException("invalid_lighting_settings", "Colors, intensities and angles must be finite; intensities must be nonnegative.");
            return preset;
        }
        private static JObject PresetJson(LightingPreset preset) => new JObject
        {
            ["name"] = preset.Name, ["preset"] = preset.BuiltinKey ?? preset.Name, ["pattern"] = preset.Pattern,
            ["ambientColor"] = new JArray(preset.AmbientColor.r, preset.AmbientColor.g, preset.AmbientColor.b), ["ambientIntensity"] = preset.AmbientIntensity,
            ["reflectionIntensity"] = preset.ReflectionIntensity, ["lightColor"] = new JArray(preset.LightColor.r, preset.LightColor.g, preset.LightColor.b),
            ["lightIntensity"] = preset.LightIntensity, ["pitch"] = preset.Pitch, ["yaw"] = preset.Yaw,
            ["volumeColor"] = new JArray(preset.VolumeColor.r, preset.VolumeColor.g, preset.VolumeColor.b)
        };
    }
}
