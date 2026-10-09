using System;
using System.Threading.Tasks;
using Ee4v.ExpressionMenu;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal static class ExpressionMenuMcpTools
    {
        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition("ee4v_inspect_expression_menu",
                "Reads the MA-composed authoring menu, index paths, source identities, edit/copy permissions, control types, initial values and complete action settings. Shared/cyclic submenus are bounded. Returns a revision required for writes.",
                McpSchemas.Object(AvatarProperties(), "avatarRef"), args => Result(ExpressionMenuApi.Inspect(Avatar(args))), true));
            var create = WriteProperties();
            create["pagePath"] = McpSchemas.String("Empty string for root; submenu index path from inspection.");
            create["name"] = McpSchemas.String(); create["submenu"] = McpSchemas.Boolean();
            McpToolRegistry.Register(new McpToolDefinition("ee4v_create_expression_menu_item",
                "Creates an ee4v-owned Toggle item or Children Submenu and registers it under the selected composed page using the UI's Prefab/Undo/save contract.",
                McpSchemas.Object(create, "avatarRef", "expectedRevision", "pagePath", "name"), args => Result(ExpressionMenuApi.Create(Avatar(args),
                    Path(args, "pagePath"), McpJson.RequireString(args, "name"), (bool?)args["submenu"] ?? false, Revision(args))), false, idempotent: false));
            var edit = WriteProperties(); edit["entryPath"] = McpSchemas.String(); edit["action"] = McpSchemas.Enum("patch", "setType");
            edit["name"] = McpSchemas.String(); edit["iconPath"] = McpSchemas.String("Empty clears the icon."); edit["initialValue"] = McpSchemas.Number("Toggle 0/1; Radial 0–100.");
            edit["type"] = McpSchemas.Enum("Toggle", "RadialPuppet", "Button", "TwoAxisPuppet", "FourAxisPuppet"); edit["discardSettings"] = McpSchemas.Boolean();
            McpToolRegistry.Register(new McpToolDefinition("ee4v_edit_expression_menu_item",
                "Patches name/icon/initial value or changes a generated item's control type. Type changes require discardSettings=true when actions/initial values would be lost; stable parameter ID and source assets are retained.",
                McpSchemas.Object(edit, "avatarRef", "expectedRevision", "entryPath", "action"), args =>
                {
                    var target = Avatar(args); var path = McpJson.RequireString(args, "entryPath"); var revision = Revision(args);
                    switch (McpJson.RequireString(args, "action"))
                    {
                        case "patch": return Result(ExpressionMenuApi.Edit(target, path, revision, (string)args["name"], (string)args["iconPath"], (float?)args["initialValue"]));
                        case "setType": return Result(ExpressionMenuApi.SetType(target, path, McpJson.RequireString(args, "type"), (bool?)args["discardSettings"] ?? false, revision));
                        default: throw new McpToolException("invalid_request", "Unknown menu edit action.");
                    }
                }, false, destructive: true));
            var actions = WriteProperties(); actions["entryPath"] = McpSchemas.String(); actions["actions"] = McpSchemas.Array(ActionSchema()); actions["dryRun"] = McpSchemas.Boolean();
            McpToolRegistry.Register(new McpToolDefinition("ee4v_replace_expression_menu_actions",
                "Replaces the complete action list of a generated item and rebuilds its controllers using UI validation/Undo/save. Supports ObjectToggle, MaterialSwap, ShapeChanger, ParameterValue, MaterialValue, Transform and Component; existing Clip actions may be updated or removed. Empty list removes all actions. dryRun validates target paths, values, mode compatibility and property conflicts without saving.",
                McpSchemas.Object(actions, "avatarRef", "expectedRevision", "entryPath", "actions"), args =>
                {
                    if (!(args["actions"] is JArray)) throw new McpToolException("invalid_request", "actions must be an array.");
                    var dryRun = (bool?)args["dryRun"] ?? false;
                    var snapshot = ExpressionMenuApi.ReplaceActions(Avatar(args), McpJson.RequireString(args, "entryPath"),
                        McpJson.To<ExpressionMenuApi.ActionData[]>(args["actions"]), Revision(args), dryRun);
                    return Task.FromResult(McpToolResult.Success(new JObject { ["ok"] = true, ["dryRun"] = dryRun, ["menu"] = McpJson.From(snapshot) }));
                }, false, destructive: true));
            var move = WriteProperties(); move["entryPath"] = McpSchemas.String(); move["destinationPage"] = McpSchemas.String(); move["relativeEntry"] = McpSchemas.String("For same-source reordering: move into the referenced entry's current index. Otherwise append to destinationPage.");
            McpToolRegistry.Register(new McpToolDefinition("ee4v_move_expression_menu_item",
                "Reorders within one editable source or relocates to another page. Rejects protected boundaries, incompatible sources, cycles, descendants and full destinations through the UI's move contract.",
                McpSchemas.Object(move, "avatarRef", "expectedRevision", "entryPath"), args =>
                {
                    if (args["relativeEntry"] == null && args["destinationPage"] == null) throw new McpToolException("invalid_request", "Supply relativeEntry or destinationPage (empty for root).");
                    return Result(ExpressionMenuApi.Move(Avatar(args), McpJson.RequireString(args, "entryPath"), (string)args["destinationPage"], (string)args["relativeEntry"], Revision(args)));
                }, false));
            var remove = WriteProperties(); remove["entryPath"] = McpSchemas.String(); remove["includeChildren"] = McpSchemas.Boolean();
            McpToolRegistry.Register(new McpToolDefinition("ee4v_remove_expression_menu_item",
                "Removes one item via the UI deletion contract; nonempty submenus require includeChildren=true. Shared submenu assets and saved source assets are retained.",
                McpSchemas.Object(remove, "avatarRef", "expectedRevision", "entryPath"), args => Result(ExpressionMenuApi.Remove(Avatar(args),
                    McpJson.RequireString(args, "entryPath"), (bool?)args["includeChildren"] ?? false, Revision(args))), false, destructive: true, idempotent: false));
            var copy = WriteProperties(); copy["pagePath"] = McpSchemas.String();
            McpToolRegistry.Register(new McpToolDefinition("ee4v_copy_expression_menu",
                "Creates editable copies of the selected SDK menu, descendants and necessary parent menus. Replaces only references on this avatar as instance overrides, preserves shared/cyclic references and rolls back on failure; uses the UI copy contract.",
                McpSchemas.Object(copy, "avatarRef", "expectedRevision", "pagePath"), args => Result(ExpressionMenuApi.CopyEditable(Avatar(args), Path(args, "pagePath"), Revision(args))), false, idempotent: false));
            var preview = AvatarProperties(); preview["entryPath"] = McpSchemas.String(); preview["values"] = McpSchemas.Array(McpSchemas.Number("Normalized input per axis: 0–1; TwoAxisPuppet −1–1."));
            preview["elapsed"] = McpSchemas.Number(); preview["width"] = McpSchemas.Integer(); preview["height"] = McpSchemas.Integer();
            McpToolRegistry.Register(new McpToolDefinition("ee4v_render_expression_menu_preview",
                "Returns a PNG and calculated parameter outputs for one MA control in Edit Mode using AvatarEvaluation/MA-aware playback on an isolated display copy. NDMF Preview plugin processing and runtime transitions/PhysBones are not included; use Play Mode for final confirmation.",
                McpSchemas.Object(preview, "avatarRef", "entryPath", "values"), args =>
                {
                    var png = ExpressionMenuApi.RenderPreview(Avatar(args), McpJson.RequireString(args, "entryPath"), McpJson.To<float[]>(args["values"]),
                        (float?)args["elapsed"] ?? 0, (int?)args["width"] ?? 512, (int?)args["height"] ?? 512, out var outputs);
                    return Task.FromResult(McpToolResult.Image(new JObject { ["ok"] = true, ["scope"] = "isolatedAuthoringPreview", ["parameterOutputs"] = outputs }, png));
                }, true));
        }

        private static UnityEngine.GameObject Avatar(JObject args) => UnityObjectReference.ResolveGameObject(McpJson.RequireString(args, "avatarRef"));
        private static string Revision(JObject args) => McpJson.RequireString(args, "expectedRevision");
        private static string Path(JObject args, string key) => args[key]?.Type == JTokenType.String ? (string)args[key] : throw new McpToolException("invalid_request", key + " must be a string (empty for root).");
        private static Task<McpToolResult> Result(ExpressionMenuApi.Snapshot value) => Task.FromResult(McpToolResult.Success(new JObject { ["ok"] = true, ["menu"] = McpJson.From(value) }));
        private static JObject AvatarProperties() => new JObject { ["avatarRef"] = McpSchemas.String() };
        private static JObject WriteProperties()
        {
            var result = AvatarProperties(); result["expectedRevision"] = McpSchemas.String("Revision from ee4v_inspect_expression_menu; required to avoid stale index paths."); return result;
        }
        private static JObject AssetSchema() => McpSchemas.Object(new JObject { ["path"] = McpSchemas.String(), ["localId"] = McpSchemas.String("Decimal local ID; required when the path contains multiple matching sub-assets.") }, "path");
        private static JObject ActionSchema()
        {
            var target = new JObject
            {
                ["path"] = McpSchemas.String("Avatar-relative object path; empty is root."), ["shape"] = McpSchemas.String(), ["property"] = McpSchemas.String(), ["componentType"] = McpSchemas.String("Assembly-qualified enabled component type."),
                ["active"] = McpSchemas.Boolean(), ["enabled"] = McpSchemas.Boolean(), ["position"] = McpSchemas.Boolean(), ["rotation"] = McpSchemas.Boolean(), ["scale"] = McpSchemas.Boolean(),
                ["minimum"] = McpSchemas.Number(), ["maximum"] = McpSchemas.Number(), ["fromMaterial"] = AssetSchema(), ["toMaterial"] = AssetSchema(), ["texture"] = AssetSchema(), ["shader"] = AssetSchema()
            };
            foreach (var key in new[] { "minimumVector", "maximumVector", "minimumPosition", "maximumPosition", "minimumRotation", "maximumRotation", "minimumScale", "maximumScale" }) target[key] = McpSchemas.Array(McpSchemas.Number());
            return McpSchemas.Object(new JObject
            {
                ["kind"] = McpSchemas.Enum("ObjectToggle", "MaterialSwap", "ShapeChanger", "ParameterValue", "MaterialValue", "Transform", "Component", "Clip"),
                ["synced"] = McpSchemas.Boolean(), ["inverted"] = McpSchemas.Boolean(), ["axis"] = McpSchemas.Integer(), ["horizontalAxis"] = McpSchemas.Integer(), ["verticalAxis"] = McpSchemas.Integer(),
                ["rootPath"] = McpSchemas.String(), ["parameter"] = McpSchemas.String(), ["parameterType"] = McpSchemas.Enum("Bool", "Int", "Float", "Trigger"),
                ["off"] = McpSchemas.Number(), ["on"] = McpSchemas.Number(), ["onClip"] = AssetSchema(), ["offClip"] = AssetSchema(), ["targets"] = McpSchemas.Array(McpSchemas.Object(target))
            }, "kind");
        }
    }
}
