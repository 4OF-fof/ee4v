using System;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AvatarParts;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal static class AvatarAuthoringMcpTools
    {
        internal static void Register()
        {
            var avatar = new JObject { ["avatarRef"] = McpSchemas.String(), ["scopePath"] = McpSchemas.String("Optional direct child Prefab path; empty means the entire Avatar.") };
            McpToolRegistry.Register(new McpToolDefinition("ee4v_inspect_avatar_authoring",
                "Reads source-relative body scale targets and ratios, Avatar height/ViewPosition, body BlendShape groups and synchronization sources, and structural attachment plans with existing connections, proposals, state, reviewReasons and canApply.",
                McpSchemas.Object(avatar, "avatarRef"), args => Task.FromResult(McpToolResult.Success(McpJson.From(
                    AvatarPartsEditor.InspectAuthoring(UnityObjectReference.ResolveGameObject(McpJson.RequireString(args, "avatarRef")), (string)args["scopePath"])))), true));
            var properties = (JObject)avatar.DeepClone(); properties["action"] = McpSchemas.Enum("setScale", "setHeight", "setBlendshape", "setBlendshapeGroup", "prepareBlendshapeSync");
            properties["part"] = McpSchemas.String("Scale part from authoring inspection: wholeBody/head/chest/waist/shoulders/arms/hands/legs/feet.");
            properties["multipliers"] = McpSchemas.Array(McpSchemas.Number("Ratio relative to source Prefab; 1=100%."));
            properties["value"] = McpSchemas.Number("Height in metres or BlendShape weight 0–100, depending on action.");
            properties["rendererPath"] = McpSchemas.String(); properties["shapeName"] = McpSchemas.String(); properties["category"] = McpSchemas.String("Exact category from inspection.");
            properties["dryRun"] = McpSchemas.Boolean();
            var schema = McpSchemas.Object(properties, "avatarRef", "action");
            schema["oneOf"] = new JArray(
                Branch(properties, "setScale", "part", "multipliers"), Branch(properties, "setHeight", "value"),
                Branch(properties, "setBlendshape", "rendererPath", "shapeName", "category", "value"),
                Branch(properties, "setBlendshapeGroup", "shapeName", "category", "value"), Branch(properties, "prepareBlendshapeSync"));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_edit_avatar_shape",
                "Edits body scale/height or individual/grouped body BlendShapes using the UI domain logic, including ViewPosition and MA BlendshapeSync/remap. prepareBlendshapeSync performs UI save preparation. Scene changes use Undo and are saved by the host. dryRun validates without changes.", schema,
                args =>
                {
                    var result = AvatarPartsEditor.EditAuthoring(UnityObjectReference.ResolveWritableAvatar(McpJson.RequireString(args, "avatarRef")),
                        McpJson.To<AvatarShapeEditRequest>(args), (bool?)args["dryRun"] ?? false);
                    return Task.FromResult(McpToolResult.Success(new JObject { ["dryRun"] = (bool?)args["dryRun"] ?? false,
                        ["request"] = args.DeepClone(), ["authoring"] = McpJson.From(result) }));
                }, false));
            var attachment = new JObject { ["avatarRef"] = McpSchemas.String(), ["partPath"] = McpSchemas.String("Direct child Prefab path from authoring inspection."),
                ["kind"] = McpSchemas.Enum("auto", "MergeArmature", "BoneProxy"), ["ownerPath"] = McpSchemas.String("Avatar-relative source path. auto without ownerPath applies all unambiguous proposed connections; with ownerPath applies that proposal."),
                ["targetPath"] = McpSchemas.String("Avatar-relative destination outside this part; omitted uses the UI recommendation."),
                ["mode"] = McpSchemas.String("BoneProxyAttachmentMode name from inspection or the MA enum."), ["matchScale"] = McpSchemas.Boolean(), ["dryRun"] = McpSchemas.Boolean() };
            McpToolRegistry.Register(new McpToolDefinition("ee4v_edit_avatar_attachment",
                "Adds or edits MA Merge Armature/Bone Proxy through the shared structural attachment plan. auto requires canApply and applies all proposed regions in one Undo group; ambiguous structures require explicit ownerPath, targetPath and kind. Existing settings and merge bone-name settings are preserved. New Bone Proxy keeps world pose. dryRun validates without changes.",
                McpSchemas.Object(attachment, "avatarRef", "partPath", "kind"), args => Task.FromResult(McpToolResult.Success(new JObject
                { ["dryRun"] = (bool?)args["dryRun"] ?? false, ["request"] = args.DeepClone(), ["authoring"] = McpJson.From(AvatarPartsEditor.EditAttachment(
                    UnityObjectReference.ResolveWritableAvatar(McpJson.RequireString(args, "avatarRef")), McpJson.To<AvatarAttachmentEditRequest>(args), (bool?)args["dryRun"] ?? false)) })), false));
        }
        private static JObject Branch(JObject properties, string action, params string[] fields)
        {
            var selected = new JObject { ["action"] = McpSchemas.Enum(action) };
            foreach (var field in new[] { "avatarRef", "scopePath", "dryRun" }.Concat(fields)) selected[field] = properties[field].DeepClone();
            return McpSchemas.Object(selected, new[] { "avatarRef", "action" }.Concat(fields).ToArray());
        }
    }
}
