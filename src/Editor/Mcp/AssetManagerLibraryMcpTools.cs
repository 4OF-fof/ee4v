using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal static class AssetManagerLibraryMcpTools
    {
        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition("ee4v_asset_get_thumbnail", "Returns the Item thumbnail through the same local/datasource provider as the UI; does not render or import Prefabs.",
                McpSchemas.Object(new JObject { ["itemId"] = McpSchemas.String() }, "itemId"), args => Thumbnail(args, CancellationToken.None), true,
                openWorld: true, invokeWithCancellation: Thumbnail));
            Add("ee4v_asset_list_tags", "Reads datasource and local tag candidates.", McpSchemas.Object(),
                args => AssetManagerMcpTools.Manager().GetTags(), true);
            Add("ee4v_asset_reorder_collections", "Reorders saved collections using the UI API. Supply all collection IDs in the desired order.",
                McpSchemas.Object(new JObject { ["collectionIds"] = McpSchemas.Array(McpSchemas.String()) }, "collectionIds"), args =>
                {
                    if (!(args["collectionIds"] is JArray values) || values.Any(value => value.Type != JTokenType.String))
                        throw new McpToolException("invalid_request", "collectionIds must be an array of strings.");
                    AssetManagerMcpTools.Manager().ReorderCollections(values.Values<string>().ToArray());
                    return AssetManagerMcpTools.Manager().GetCollections();
                }, false);
            Add("ee4v_asset_create_item", "Creates a locally owned library Item. Does not create or import Unity assets.",
                McpSchemas.Object(new JObject { ["name"] = McpSchemas.String(), ["description"] = McpSchemas.String() }, "name"),
                args => AssetManagerMcpTools.Manager().CreateItem(new CreateAssetItemRequest
                { Name = McpJson.RequireString(args, "name"), Description = (string)args["description"] ?? string.Empty }), false, idempotent: false);
            Add("ee4v_asset_delete_items", "Deletes archived Items through the library API, with the same ownership and archive validation as the UI. Returns prior metadata.",
                Ids("itemIds"), args =>
                {
                    var ids = AssetManagerMcpTools.RequiredStrings(args, "itemIds");
                    var manager = AssetManagerMcpTools.Manager();
                    var before = ids.Select(manager.GetItem).ToArray();
                    manager.DeleteItem(ids);
                    return before;
                }, false, true, false);
            Add("ee4v_asset_inspect_files", "Reads selected registered Files, or unassigned Files when fileIds is omitted. Does not ingest or import content.",
                McpSchemas.Object(new JObject { ["fileIds"] = McpSchemas.Array(McpSchemas.String()), ["includeArchived"] = McpSchemas.Boolean() }),
                args => args["fileIds"] == null ? (object)AssetManagerMcpTools.Manager().GetUnassignedFiles((bool?)args["includeArchived"] ?? false) :
                    AssetManagerMcpTools.RequiredStrings(args, "fileIds").Select(AssetManagerMcpTools.Manager().GetFile).ToArray(), true);
            Add("ee4v_asset_register_file", "Registers a local File under an Item using the library API; does not import it into Unity.",
                McpSchemas.Object(new JObject { ["itemId"] = McpSchemas.String("Empty or omitted registers an unassigned File."), ["libraryPath"] = McpSchemas.String(),
                    ["filePath"] = McpSchemas.String(), ["fileName"] = McpSchemas.String() }, "libraryPath", "filePath"),
                args => AssetManagerMcpTools.Manager().RegisterFile((string)args["itemId"], new RegisterFileRequest
                { LibraryPath = McpJson.RequireString(args, "libraryPath"), FilePath = McpJson.RequireString(args, "filePath"), FileName = (string)args["fileName"] }), false, idempotent: false, openWorld: true);
            var edit = new JObject { ["fileIds"] = McpSchemas.Array(McpSchemas.String()), ["itemId"] = McpSchemas.String("Empty detaches Files from their Item."), ["archived"] = McpSchemas.Boolean() };
            var editSchema = McpSchemas.Object(edit, "fileIds");
            editSchema["oneOf"] = new JArray(
                McpSchemas.Object(new JObject { ["fileIds"] = edit["fileIds"].DeepClone(), ["itemId"] = edit["itemId"].DeepClone() }, "fileIds", "itemId"),
                McpSchemas.Object(new JObject { ["fileIds"] = edit["fileIds"].DeepClone(), ["archived"] = edit["archived"].DeepClone() }, "fileIds", "archived"));
            Add("ee4v_asset_edit_files", "Reassigns or archives selected Files with UI validation. Empty itemId detaches them. Choose one operation per call.", editSchema,
                args => args["itemId"] != null ? (object)AssetManagerMcpTools.Manager().SetFileItem(AssetManagerMcpTools.RequiredStrings(args, "fileIds"), (string)args["itemId"]) :
                    AssetManagerMcpTools.Manager().SetFileArchived(AssetManagerMcpTools.RequiredStrings(args, "fileIds"), (bool)args["archived"]), false, destructive: true);
            Add("ee4v_asset_delete_files", "Deletes archived registered Files using the UI API. Returns prior metadata; never performs an implicit Unity import.", Ids("fileIds"), args =>
            {
                var ids = AssetManagerMcpTools.RequiredStrings(args, "fileIds");
                var manager = AssetManagerMcpTools.Manager();
                var before = ids.Select(manager.GetFile).ToArray();
                manager.DeleteFile(ids);
                return before;
            }, false, true, false);
            Add("ee4v_asset_sync_source", "Explicitly synchronizes one configured datasource into the shared library using the UI datasource API. Does not import Unity assets or change the UI's selected datasource.",
                McpSchemas.Object(new JObject { ["kind"] = McpSchemas.Enum("Eagle", "BoothLibraryManager", "Ee4v"),
                    ["libraryPath"] = McpSchemas.String(), ["databasePath"] = McpSchemas.String(), ["targetRoot"] = McpSchemas.String() }, "kind", "libraryPath"), args =>
                ((IAssetDatasourceManager)AssetManagerMcpTools.Manager()).SyncDatasource(new AssetDatasourceRequest
                { Kind = (AssetDatasourceKind)Enum.Parse(typeof(AssetDatasourceKind), (string)args["kind"]), LibraryPath = McpJson.RequireString(args, "libraryPath"),
                    DatabasePath = (string)args["databasePath"], TargetRoot = (string)args["targetRoot"] ?? "VRCAsset" }), false, true, false, true);
            Add("ee4v_asset_ingest_file", "Adds a local file to the ee4v library, creating its Item and tags through the UI API. Does not import it into Unity.",
                McpSchemas.Object(new JObject { ["libraryPath"] = McpSchemas.String(), ["filePath"] = McpSchemas.String(), ["name"] = McpSchemas.String(),
                    ["description"] = McpSchemas.String(), ["tags"] = McpSchemas.Array(McpSchemas.String()) }, "libraryPath", "filePath"), args =>
                AssetManagerMcpTools.Manager().ImportEe4vFile(new ImportEe4vFileRequest(McpJson.RequireString(args, "libraryPath"), McpJson.RequireString(args, "filePath"),
                    (string)args["name"], (string)args["description"], McpJson.To<string[]>(args["tags"]))), false, idempotent: false, openWorld: true);
        }

        private static JObject Ids(string key)
        {
            var array = McpSchemas.Array(McpSchemas.String()); array["minItems"] = 1;
            return McpSchemas.Object(new JObject { [key] = array }, key);
        }
        private static async Task<McpToolResult> Thumbnail(JObject args, CancellationToken cancellationToken)
        {
            var id = McpJson.RequireString(args, "itemId");
            var manager = AssetManagerMcpTools.Manager(); manager.GetItem(id);
            var thumbnail = await manager.GetThumbnail(id, cancellationToken);
            return thumbnail.Found ? McpToolResult.MediaImage(new JObject { ["itemId"] = id, ["path"] = thumbnail.Path, ["sourceUrl"] = thumbnail.SourceUrl }, thumbnail.Data) :
                McpToolResult.Error("thumbnail_unavailable", thumbnail.MissingReason);
        }
        private static void Add(string name, string description, JObject schema, Func<JObject, object> operation,
            bool readOnly, bool destructive = false, bool idempotent = true, bool openWorld = false)
        {
            McpToolRegistry.Register(new McpToolDefinition(name, description, schema,
                args => AssetManagerMcpTools.AssetResult(() =>
                {
                    var result = operation(args);
                    return result is AssetSyncResult sync && sync.ErrorCount > 0 ? McpToolResult.Error("asset_sync_failed", string.Join("\n", sync.ErrorMessages), McpJson.From(sync)) :
                        AssetManagerMcpTools.Success(result);
                }), readOnly, destructive, idempotent, openWorld));
        }
    }
}
