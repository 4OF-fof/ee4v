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
        internal static void Register()
        {
            RegisterSearch();
            RegisterGetItem();
            RegisterEditItems();
            RegisterAnalyzeFile();
            RegisterImport();
            RegisterSetDependencies();
            RegisterCollections();
            AssetManagerPrefabMcpTools.Register();
            AssetManagerVariantMcpTools.Register();
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
                    var itemId = McpJson.RequireString(arguments, "itemId");
                    var item = manager.GetItem(itemId);
                    var files = manager.GetFiles(itemId, true);
                    var importedAssetGuids =
                        manager.GetItemImportedAssetGuids(itemId);
                    var prefabCandidates =
                        AssetManagerPrefabMcpTools.FindCandidates(
                            itemId,
                            importedAssetGuids);
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
                        ImportedAssetGuids = importedAssetGuids,
                        PrefabCandidates = prefabCandidates.Candidates,
                        PrefabCandidateResolution = new
                        {
                            prefabCandidates.ImportedAssetGuidCount,
                            prefabCandidates.NonPrefabAssetCount,
                            prefabCandidates.UnresolvedImportedAssets,
                            prefabCandidates.EmptyReason
                        }
                    });
                }),
                readOnly: true));
        }

        private static void RegisterEditItems()
        {
            var schema = McpSchemas.Object(new JObject
            {
                ["itemIds"] = McpSchemas.Array(McpSchemas.String()),
                ["name"] = McpSchemas.String(),
                ["description"] = McpSchemas.String(),
                ["tags"] = McpSchemas.Array(McpSchemas.String()),
                ["archived"] = McpSchemas.Boolean(),
                ["targets"] = McpSchemas.Array(TargetSchema())
            }, "itemIds");
            schema["properties"]["itemIds"]["minItems"] = 1;
            schema["anyOf"] = new JArray(new[] { "name", "description", "tags", "archived", "targets" }
                .Select(field => new JObject { ["required"] = new JArray(field) }));
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_edit_items",
                "Partially edits item names, descriptions, locally managed tags, archive state, and import targets. Unspecified fields are preserved. itemIds supports bulk edits; targets requires one item. Empty tags or targets clears them. Eagle owns its names, descriptions, and source tags. On failure, appliedChanges reports completed changes; the combined edit is not a transaction.",
                schema,
                arguments => AssetResult(() => EditItems(arguments)),
                readOnly: false));
        }

        private static McpToolResult EditItems(JObject arguments)
        {
            var fields = new[] { "name", "description", "tags", "archived", "targets" }
                .Where(field => arguments.Property(field) != null).ToArray();
            if (fields.Length == 0)
            {
                throw new McpToolException("invalid_request", "Provide at least one item field to edit.");
            }
            foreach (var field in fields)
            {
                var expected = field == "archived" ? JTokenType.Boolean :
                    field == "tags" || field == "targets" ? JTokenType.Array : JTokenType.String;
                if (arguments[field].Type != expected)
                {
                    throw new McpToolException("invalid_request", field + " has an invalid type.");
                }
            }
            var ids = RequiredStrings(arguments, "itemIds");
            if (arguments.Property("targets") != null && ids.Count != 1)
            {
                throw new McpToolException("invalid_request", "targets requires exactly one item.");
            }
            var name = arguments.Property("name") == null ? null : McpJson.RequireString(arguments, "name");
            var metadata = name != null || arguments.Property("description") != null;
            var manager = Manager();
            var originals = ids.Select(manager.GetItem).ToArray();
            if (metadata && originals.Any(item => item.SourceType == AssetSourceType.Eagle))
            {
                throw new McpToolException("invalid_request", "Eagle item names and descriptions must be edited in Eagle.");
            }
            var applied = new List<object>();
            try
            {
                if (arguments.Property("targets") != null)
                {
                    var targets = McpJson.To<List<AssetFileTarget>>(arguments["targets"]);
                    manager.SetItemTargets(ids[0], targets);
                    applied.Add(new { itemIds = ids, fields = new[] { "targets" } });
                    foreach (var target in targets.Where(target => target.GroupName != null))
                    {
                        manager.SetItemTargetGroup(ids[0], target.FileId, target.TargetPath, target.GroupName);
                        applied.Add(new { itemIds = ids, fields = new[] { "targetGroup" }, target.FileId, target.TargetPath });
                    }
                }
                if (arguments.Property("tags") != null)
                {
                    manager.SetItemTags(ids, McpJson.To<List<string>>(arguments["tags"]));
                    applied.Add(new { itemIds = ids, fields = new[] { "tags" } });
                }
                if (metadata)
                {
                    foreach (var original in originals)
                    {
                        manager.UpdateItem(original.Id, new UpdateAssetItemRequest
                        {
                            Name = name ?? original.Name,
                            Description = (string)arguments["description"] ?? original.Description
                        });
                        applied.Add(new { itemIds = new[] { original.Id }, fields = fields.Where(field => field == "name" || field == "description").ToArray() });
                    }
                }
                if (arguments.Property("archived") != null)
                {
                    manager.SetItemArchived(ids, (bool)arguments["archived"]);
                    applied.Add(new { itemIds = ids, fields = new[] { "archived" } });
                }
                return Success(new
                {
                    Items = ids.Select(manager.GetItem).ToArray(),
                    Targets = ids.Count == 1 ? manager.GetItemTargets(ids[0]) : null,
                    AppliedChanges = applied
                });
            }
            catch (Exception exception)
            {
                return McpToolResult.Error(
                    exception is AssetManagerException assetException
                        ? "asset_manager_" + ToSnakeCase(assetException.Code.ToString())
                        : "asset_edit_failed",
                    exception.Message,
                    McpJson.From(new { appliedChanges = applied }));
            }
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
                    McpJson.RequireString(arguments, "fileId")))),
                readOnly: true,
                openWorld: true));
        }

        private static JObject TargetSchema()
        {
            return McpSchemas.Object(new JObject
            {
                ["fileId"] = McpSchemas.String(),
                ["targetPath"] = McpSchemas.String(),
                ["groupName"] = McpSchemas.String()
            }, "fileId", "targetPath");
        }

        private static void RegisterImport()
        {
            var schema = McpSchemas.Object(new JObject
            {
                ["itemId"] = McpSchemas.String(),
                ["selectedTargets"] = McpSchemas.Array(TargetSchema()),
                ["fileId"] = McpSchemas.String(),
                ["paths"] = McpSchemas.Array(McpSchemas.String(
                    "Internal entry paths from file analysis. An empty string imports the whole non-ZIP file."))
            });
            schema["properties"]["paths"]["minItems"] = 1;
            schema["oneOf"] = new JArray(
                McpSchemas.Object(new JObject
                {
                    ["itemId"] = schema["properties"]["itemId"].DeepClone(),
                    ["selectedTargets"] = schema["properties"]["selectedTargets"].DeepClone()
                }, "itemId"),
                McpSchemas.Object(new JObject
                {
                    ["fileId"] = schema["properties"]["fileId"].DeepClone(),
                    ["paths"] = schema["properties"]["paths"].DeepClone()
                }, "fileId", "paths"));
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_import",
                "Imports registered AssetManager content into the current Unity Project. Supply itemId and optional selectedTargets (one choice per named group) to import saved targets and dependencies, or fileId and nonempty paths to import selected file entries. Never supply both modes. ZIP targets must be internal non-ZIP entries. Returns imported file IDs, Unity asset GUIDs, and failure or cancellation details. Existing Project assets may be overwritten.",
                schema, Import,
                readOnly: false,
                destructive: true,
                idempotent: false,
                openWorld: true));
        }

        private static async Task<McpToolResult> Import(JObject arguments)
        {
            try
            {
                var itemMode = arguments.Property("itemId") != null;
                if (itemMode == (arguments.Property("fileId") != null) ||
                    (itemMode && arguments.Property("paths") != null) ||
                    (!itemMode && arguments.Property("selectedTargets") != null))
                {
                    throw new McpToolException("invalid_request", "Specify either itemId with selectedTargets, or fileId with paths.");
                }
                AssetImportResult result;
                if (itemMode)
                {
                    if (arguments.Property("selectedTargets") != null && arguments["selectedTargets"].Type != JTokenType.Array)
                    {
                        throw new McpToolException("invalid_request", "selectedTargets must be an array.");
                    }
                    result = await Manager().ImportItemTargets(McpJson.RequireString(arguments, "itemId"),
                        McpJson.To<List<AssetFileTarget>>(arguments["selectedTargets"]) ?? new List<AssetFileTarget>());
                }
                else
                {
                    if (!(arguments["paths"] is JArray paths) || paths.Count == 0 || paths.Any(path => path.Type != JTokenType.String))
                    {
                        throw new McpToolException("invalid_request", "paths must contain at least one string.");
                    }
                    result = await Manager().ImportFileEntries(McpJson.RequireString(arguments, "fileId"), paths.Values<string>().ToArray());
                }
                return result.Succeeded ? Success(result) : McpToolResult.Error(
                    result.Canceled ? "asset_import_canceled" : "asset_import_failed",
                    result.ErrorMessage, McpJson.From(result));
            }
            catch (AssetManagerException exception)
            {
                return McpToolResult.Error("asset_manager_" + ToSnakeCase(exception.Code.ToString()), exception.Message);
            }
        }

        private static void RegisterSetDependencies()
        {
            var target = TargetSchema();
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_set_dependencies",
                "Replaces dependencies from selected file contents to selected target contents.",
                McpSchemas.Object(new JObject
                {
                    ["dependentTargets"] = McpSchemas.Array(target),
                    ["dependencyTargets"] = McpSchemas.Array(target)
                }, "dependentTargets", "dependencyTargets"),
                arguments => AssetResult(() => Success(Manager().SetFileDependencies(
                    McpJson.To<List<AssetFileTarget>>(arguments["dependentTargets"]) ??
                    new List<AssetFileTarget>(),
                    McpJson.To<List<AssetFileTarget>>(arguments["dependencyTargets"]) ??
                    new List<AssetFileTarget>()))),
                readOnly: false));
        }

        private static void RegisterCollections()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_list_collections",
                "Lists saved AssetManager smart collections and their filter trees.",
                McpSchemas.Object(),
                _ => AssetResult(() => Success(Manager().GetCollections())),
                readOnly: true));

            var properties = new JObject
            {
                ["collectionId"] = McpSchemas.String(),
                ["name"] = McpSchemas.String(),
                ["icon"] = McpSchemas.Enum(Enum.GetNames(typeof(AssetCollectionIcon))),
                ["filter"] = new JObject { ["type"] = "object" }
            };
            var schema = McpSchemas.Object(properties);
            var create = McpSchemas.Object(new JObject
            {
                ["name"] = properties["name"].DeepClone(),
                ["icon"] = properties["icon"].DeepClone(),
                ["filter"] = properties["filter"].DeepClone()
            }, "name", "filter");
            var update = McpSchemas.Object((JObject)properties.DeepClone(), "collectionId");
            update["anyOf"] = new JArray(new[] { "name", "icon", "filter" }
                .Select(field => new JObject { ["required"] = new JArray(field) }));
            schema["oneOf"] = new JArray(create, update);
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_upsert_collection",
                "Creates a smart collection when collectionId is omitted, or partially updates an existing collection when provided. Creation requires name and filter. Updates require at least one of name, icon, filter and preserve omitted fields. Filter types are And, Or, Not, Condition; condition types are NameContains, DescriptionContains, HasTag, HasFileExtension.",
                schema,
                arguments => AssetResult(() =>
                {
                    var manager = Manager();
                    var existing = arguments.Property("collectionId") == null
                        ? null : manager.GetCollection(McpJson.RequireString(arguments, "collectionId"));
                    if (existing != null && !new[] { "name", "icon", "filter" }.Any(field => arguments.Property(field) != null))
                    {
                        throw new McpToolException("invalid_request", "Provide at least one collection field to edit.");
                    }
                    if ((existing == null || arguments.Property("filter") != null) && !(arguments["filter"] is JObject))
                    {
                        throw new McpToolException("invalid_request", "filter must be an object.");
                    }
                    var name = arguments.Property("name") == null && existing != null
                        ? existing.Name : McpJson.RequireString(arguments, "name");
                    var filter = arguments.Property("filter") == null && existing != null
                        ? existing.Root : McpJson.To<AssetFilterNode>(arguments["filter"]);
                    var icon = CollectionIcon(arguments);
                    return Success(existing == null
                        ? manager.CreateCollection(new CreateAssetCollectionRequest
                        {
                            Name = name, Icon = icon ?? AssetCollectionIcon.Folder, Root = filter
                        })
                        : manager.UpdateCollection(existing.Id, new UpdateAssetCollectionRequest
                        {
                            Name = name, Icon = icon, Root = filter
                        }));
                }),
                readOnly: false,
                idempotent: false));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_delete_collection",
                "Deletes one AssetManager smart collection by ID and returns its former name and filter tree.",
                McpSchemas.Object(new JObject
                {
                    ["collectionId"] = McpSchemas.String()
                }, "collectionId"),
                arguments => AssetResult(() =>
                {
                    var manager = Manager();
                    var id = McpJson.RequireString(arguments, "collectionId");
                    var collection = manager.GetCollection(id);
                    manager.DeleteCollection(id);
                    return Success(collection);
                }),
                readOnly: false,
                destructive: true,
                idempotent: false));
        }

        private static AssetCollectionIcon? CollectionIcon(JObject arguments)
        {
            var value = arguments["icon"];
            if (value == null)
            {
                return null;
            }
            if (value.Type == JTokenType.String &&
                Enum.TryParse<AssetCollectionIcon>((string)value, true, out var icon) &&
                Enum.IsDefined(typeof(AssetCollectionIcon), icon))
            {
                return icon;
            }
            throw new AssetManagerException(
                AssetManagerErrorCode.InvalidRequest,
                "Collection icon is invalid.");
        }

        internal static IAssetManager Manager()
        {
            var root = GlobalDataSettings.RootDirectory;
            var path = Path.Combine(root, "asset-manager-v1.db");
            Directory.CreateDirectory(root);
            return AssetManagerFactory.OpenSession(path);
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
