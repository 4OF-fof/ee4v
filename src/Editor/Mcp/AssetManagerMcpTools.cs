using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.Core.Settings;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal static class AssetManagerMcpTools
    {
        private static IAssetManager _manager;
        private static string _databasePath;

        internal static void Register()
        {
            RegisterSearch();
            RegisterGetItem();
            RegisterCreateItem();
            RegisterUpdateItem();
            RegisterSetTags();
            RegisterSetArchived();
            RegisterDeleteItems();
            RegisterFile();
            RegisterAnalyzeFile();
            RegisterSetTargets();
            RegisterSetDependencies();
            RegisterImport();
            RegisterCollections();
            RegisterSync();
        }

        private static void RegisterSearch()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_search",
                "Searches the ee4v AssetManager database by name/description text, tag, file extension, archive state, and pagination.",
                McpSchemas.Object(new JObject
                {
                    ["query"] = McpSchemas.String(),
                    ["tags"] = McpSchemas.Array(McpSchemas.String()),
                    ["extension"] = McpSchemas.String(),
                    ["includeArchived"] = McpSchemas.Boolean(),
                    ["offset"] = McpSchemas.Integer(),
                    ["limit"] = McpSchemas.Integer()
                }),
                arguments => AssetResult(() =>
                {
                    var filters = new List<AssetFilterNode>();
                    var query = ((string)arguments["query"] ?? string.Empty).Trim();
                    if (query.Length > 0)
                    {
                        filters.Add(AssetFilterNode.Or(
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.NameContains,
                                query),
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.DescriptionContains,
                                query)));
                    }

                    foreach (var tag in McpJson.To<List<string>>(arguments["tags"]) ??
                                 new List<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            filters.Add(AssetFilterNode.Condition(
                                AssetFilterConditionType.HasTag,
                                tag.Trim()));
                        }
                    }

                    var extension = ((string)arguments["extension"] ?? string.Empty)
                        .Trim().TrimStart('.');
                    if (extension.Length > 0)
                    {
                        filters.Add(AssetFilterNode.Condition(
                            AssetFilterConditionType.HasFileExtension,
                            extension));
                    }

                    var limit = Math.Max(1, Math.Min(200, (int?)arguments["limit"] ?? 50));
                    var result = Manager().SearchItems(new AssetItemQuery
                    {
                        Filter = filters.Count == 0
                            ? null
                            : filters.Count == 1
                                ? filters[0]
                                : AssetFilterNode.And(filters.ToArray()),
                        IncludeArchived = (bool?)arguments["includeArchived"] ?? false,
                        Offset = Math.Max(0, (int?)arguments["offset"] ?? 0),
                        Limit = limit
                    });
                    return Success(new
                    {
                        result.TotalCount,
                        result.Items,
                        Limit = limit
                    });
                }),
                readOnly: true));
        }

        private static void RegisterGetItem()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_get_item",
                "Returns one AssetManager item with files, import targets, dependencies, imported Unity asset GUIDs, and source metadata.",
                ItemIdSchema(),
                arguments => AssetResult(() =>
                {
                    var manager = Manager();
                    var itemId = Required(arguments, "itemId");
                    var item = manager.GetItem(itemId);
                    var files = manager.GetFiles(itemId, true);
                    return Success(new
                    {
                        Item = item,
                        Files = files.Select(file => new
                        {
                            File = file,
                            Dependencies = manager.GetFileDependencies(file.Id),
                            ImportedAssetGuids = manager.GetFileImportedAssetGuids(file.Id)
                        }).ToArray(),
                        Targets = manager.GetItemTargets(itemId),
                        ImportedAssetGuids = manager.GetItemImportedAssetGuids(itemId)
                    });
                }),
                readOnly: true));
        }

        private static void RegisterCreateItem()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_create_item",
                "Creates an editable AssetManager item. This does not copy files into the library.",
                McpSchemas.Object(new JObject
                {
                    ["name"] = McpSchemas.String(),
                    ["description"] = McpSchemas.String(),
                    ["tags"] = McpSchemas.Array(McpSchemas.String())
                }, "name"),
                arguments => AssetResult(() =>
                {
                    var manager = Manager();
                    var item = manager.CreateItem(new CreateAssetItemRequest
                    {
                        Name = Required(arguments, "name"),
                        Description = (string)arguments["description"] ?? string.Empty
                    });
                    var tags = McpJson.To<List<string>>(arguments["tags"]);
                    if (tags != null)
                    {
                        item = manager.SetItemTags(new[] { item.Id }, tags).Single();
                    }

                    return Success(item);
                }),
                readOnly: false));
        }

        private static void RegisterUpdateItem()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_update_item",
                "Updates an editable AssetManager item's name and description.",
                McpSchemas.Object(new JObject
                {
                    ["itemId"] = McpSchemas.String(),
                    ["name"] = McpSchemas.String(),
                    ["description"] = McpSchemas.String()
                }, "itemId", "name"),
                arguments => AssetResult(() => Success(Manager().UpdateItem(
                    Required(arguments, "itemId"),
                    new UpdateAssetItemRequest
                    {
                        Name = Required(arguments, "name"),
                        Description = (string)arguments["description"] ?? string.Empty
                    }))),
                readOnly: false));
        }

        private static void RegisterSetTags()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_set_tags",
                "Replaces tags on one or more AssetManager items.",
                McpSchemas.Object(new JObject
                {
                    ["itemIds"] = McpSchemas.Array(McpSchemas.String()),
                    ["tags"] = McpSchemas.Array(McpSchemas.String())
                }, "itemIds", "tags"),
                arguments => AssetResult(() => Success(Manager().SetItemTags(
                    RequiredStrings(arguments, "itemIds"),
                    McpJson.To<List<string>>(arguments["tags"]) ?? new List<string>()))),
                readOnly: false));
        }

        private static void RegisterSetArchived()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_set_archived",
                "Archives or restores one or more AssetManager items without deleting source files.",
                McpSchemas.Object(new JObject
                {
                    ["itemIds"] = McpSchemas.Array(McpSchemas.String()),
                    ["archived"] = McpSchemas.Boolean()
                }, "itemIds", "archived"),
                arguments => AssetResult(() => Success(Manager().SetItemArchived(
                    RequiredStrings(arguments, "itemIds"),
                    (bool)arguments["archived"]))),
                readOnly: false));
        }

        private static void RegisterDeleteItems()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_delete_items",
                "Permanently removes archived ee4v-source items from the database and ee4v library. Eagle or non-ee4v sources are rejected. Requires confirm=true.",
                McpSchemas.Object(new JObject
                {
                    ["itemIds"] = McpSchemas.Array(McpSchemas.String()),
                    ["confirm"] = McpSchemas.Boolean()
                }, "itemIds", "confirm"),
                arguments => AssetResult(() =>
                {
                    if ((bool?)arguments["confirm"] != true)
                    {
                        throw new McpToolException(
                            "confirmation_required",
                            "Set confirm=true only after verifying every item is archived and may be permanently removed.");
                    }

                    var ids = RequiredStrings(arguments, "itemIds");
                    Manager().DeleteItem(ids);
                    return Success(new { DeletedItemIds = ids });
                }),
                readOnly: false,
                destructive: true,
                idempotent: false));
        }

        private static void RegisterFile()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_register_file",
                "Copies a local file into the configured ee4v library and registers it with an item or as an unassigned file.",
                McpSchemas.Object(new JObject
                {
                    ["itemId"] = McpSchemas.String(),
                    ["filePath"] = McpSchemas.String(),
                    ["fileName"] = McpSchemas.String()
                }, "filePath"),
                arguments => AssetResult(() => Success(Manager().RegisterFile(
                    (string)arguments["itemId"],
                    new RegisterFileRequest
                    {
                        LibraryPath = GlobalDataSettings.RootDirectory,
                        FilePath = Required(arguments, "filePath"),
                        FileName = (string)arguments["fileName"]
                    }))),
                readOnly: false,
                openWorld: true));
        }

        private static void RegisterAnalyzeFile()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_analyze_file",
                "Analyzes a registered ZIP or unitypackage and lists importable contents, sizes, kinds, and Unity asset GUIDs.",
                McpSchemas.Object(new JObject
                {
                    ["fileId"] = McpSchemas.String()
                }, "fileId"),
                arguments => AssetResult(() => Success(Manager().AnalyzeFile(
                    Required(arguments, "fileId")))),
                readOnly: true,
                openWorld: true));
        }

        private static void RegisterSetTargets()
        {
            var target = McpSchemas.Object(new JObject
            {
                ["fileId"] = McpSchemas.String(),
                ["targetPath"] = McpSchemas.String(),
                ["groupName"] = McpSchemas.String()
            }, "fileId", "targetPath");
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_set_targets",
                "Replaces an item's import targets. Grouped targets require one choice from each group when importing.",
                McpSchemas.Object(new JObject
                {
                    ["itemId"] = McpSchemas.String(),
                    ["targets"] = McpSchemas.Array(target)
                }, "itemId", "targets"),
                arguments => AssetResult(() => Success(Manager().SetItemTargets(
                    Required(arguments, "itemId"),
                    McpJson.To<List<AssetFileTarget>>(arguments["targets"]) ??
                    new List<AssetFileTarget>()))),
                readOnly: false));
        }

        private static void RegisterSetDependencies()
        {
            var target = McpSchemas.Object(new JObject
            {
                ["fileId"] = McpSchemas.String(),
                ["targetPath"] = McpSchemas.String(),
                ["groupName"] = McpSchemas.String()
            }, "fileId", "targetPath");
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_set_dependencies",
                "Replaces file-level dependency targets used to order and supplement imports.",
                McpSchemas.Object(new JObject
                {
                    ["dependentFileIds"] = McpSchemas.Array(McpSchemas.String()),
                    ["dependencyTargets"] = McpSchemas.Array(target)
                }, "dependentFileIds", "dependencyTargets"),
                arguments => AssetResult(() => Success(Manager().SetFileDependencies(
                    RequiredStrings(arguments, "dependentFileIds"),
                    McpJson.To<List<AssetFileTarget>>(arguments["dependencyTargets"]) ??
                    new List<AssetFileTarget>()))),
                readOnly: false));
        }

        private static void RegisterImport()
        {
            var target = McpSchemas.Object(new JObject
            {
                ["fileId"] = McpSchemas.String(),
                ["targetPath"] = McpSchemas.String(),
                ["groupName"] = McpSchemas.String()
            }, "fileId", "targetPath");
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_import",
                "Imports either explicit archive entries from one file or configured item targets with dependencies into the Unity project.",
                McpSchemas.Object(new JObject
                {
                    ["mode"] = McpSchemas.Enum("fileEntries", "itemTargets"),
                    ["fileId"] = McpSchemas.String(),
                    ["paths"] = McpSchemas.Array(McpSchemas.String()),
                    ["itemId"] = McpSchemas.String(),
                    ["selectedTargets"] = McpSchemas.Array(target)
                }, "mode"),
                async arguments => await AssetResultAsync(async () =>
                {
                    AssetImportResult result;
                    if (string.Equals(
                            (string)arguments["mode"],
                            "fileEntries",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        result = await Manager().ImportFileEntries(
                            Required(arguments, "fileId"),
                            RequiredStrings(arguments, "paths"));
                    }
                    else if (string.Equals(
                                 (string)arguments["mode"],
                                 "itemTargets",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        result = await Manager().ImportItemTargets(
                            Required(arguments, "itemId"),
                            McpJson.To<List<AssetFileTarget>>(
                                arguments["selectedTargets"]) ??
                            new List<AssetFileTarget>());
                    }
                    else
                    {
                        throw new McpToolException(
                            "invalid_import_mode",
                            "mode must be fileEntries or itemTargets.");
                    }

                    return Success(result);
                }),
                readOnly: false,
                destructive: false,
                idempotent: true,
                openWorld: true));
        }

        private static void RegisterCollections()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_list_collections",
                "Lists saved AssetManager smart collections and their filter trees.",
                McpSchemas.Object(),
                _ => AssetResult(() => Success(Manager().GetCollections())),
                readOnly: true));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_upsert_collection",
                "Creates or updates an AssetManager smart collection. Filter nodes use type And, Or, Not, or Condition and conditionType NameContains, DescriptionContains, HasTag, or HasFileExtension.",
                McpSchemas.Object(new JObject
                {
                    ["collectionId"] = McpSchemas.String(
                        "Omit to create a collection."),
                    ["name"] = McpSchemas.String(),
                    ["filter"] = new JObject { ["type"] = "object" }
                }, "name", "filter"),
                arguments => AssetResult(() =>
                {
                    var manager = Manager();
                    var id = (string)arguments["collectionId"];
                    var name = Required(arguments, "name");
                    var filter = McpJson.To<AssetFilterNode>(arguments["filter"]);
                    return Success(string.IsNullOrWhiteSpace(id)
                        ? manager.CreateCollection(new CreateAssetCollectionRequest
                        {
                            Name = name,
                            Root = filter
                        })
                        : manager.UpdateCollection(id, new UpdateAssetCollectionRequest
                        {
                            Name = name,
                            Root = filter
                        }));
                }),
                readOnly: false));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_delete_collection",
                "Deletes a saved AssetManager smart collection. Items are not deleted.",
                McpSchemas.Object(new JObject
                {
                    ["collectionId"] = McpSchemas.String()
                }, "collectionId"),
                arguments => AssetResult(() =>
                {
                    var id = Required(arguments, "collectionId");
                    Manager().DeleteCollection(id);
                    return Success(new { DeletedCollectionId = id });
                }),
                readOnly: false,
                destructive: false));
        }

        private static void RegisterSync()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_sync_library",
                "Synchronizes the configured ee4v library metadata into AssetManager. This does not import assets into Unity.",
                McpSchemas.Object(),
                _ => AssetResult(() => Success(Manager().SyncEe4v(
                    new Ee4vSyncRequest(GlobalDataSettings.RootDirectory)))),
                readOnly: false,
                idempotent: true,
                openWorld: true));
        }

        private static IAssetManager Manager()
        {
            var root = GlobalDataSettings.RootDirectory;
            var path = Path.Combine(root, "asset-manager-v1.db");
            if (_manager != null && string.Equals(
                    _databasePath,
                    path,
                    StringComparison.OrdinalIgnoreCase))
            {
                return _manager;
            }

            Directory.CreateDirectory(root);
            _databasePath = path;
            _manager = AssetManagerFactory.Open(path);
            return _manager;
        }

        private static Task<McpToolResult> AssetResult(Func<McpToolResult> action)
        {
            try
            {
                return Task.FromResult(action());
            }
            catch (AssetManagerException exception)
            {
                return Task.FromResult(McpToolResult.Error(
                    "asset_manager_" + ToSnakeCase(exception.Code.ToString()),
                    exception.Message));
            }
        }

        private static async Task<McpToolResult> AssetResultAsync(
            Func<Task<McpToolResult>> action)
        {
            try
            {
                return await action();
            }
            catch (AssetManagerException exception)
            {
                return McpToolResult.Error(
                    "asset_manager_" + ToSnakeCase(exception.Code.ToString()),
                    exception.Message);
            }
        }

        private static McpToolResult Success(object value)
        {
            return McpToolResult.Success(McpJson.From(new
            {
                Ok = true,
                Value = value
            }));
        }

        private static JObject ItemIdSchema()
        {
            return McpSchemas.Object(new JObject
            {
                ["itemId"] = McpSchemas.String()
            }, "itemId");
        }

        private static string Required(JObject arguments, string name)
        {
            var value = ((string)arguments[name] ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                throw new McpToolException(
                    "invalid_request",
                    name + " is required.");
            }

            return value;
        }

        private static IReadOnlyList<string> RequiredStrings(
            JObject arguments,
            string name)
        {
            var values = (McpJson.To<List<string>>(arguments[name]) ??
                          new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (values.Length == 0)
            {
                throw new McpToolException(
                    "invalid_request",
                    name + " must contain at least one value.");
            }

            return values;
        }

        private static string ToSnakeCase(string value)
        {
            var result = new System.Text.StringBuilder();
            for (var index = 0; index < (value?.Length ?? 0); index++)
            {
                if (index > 0 && char.IsUpper(value[index]))
                {
                    result.Append('_');
                }

                result.Append(char.ToLowerInvariant(value[index]));
            }

            return result.ToString();
        }
    }
}
