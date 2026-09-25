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
            RegisterUpdateItem();
            RegisterSetTags();
            RegisterSetArchived();
            RegisterAnalyzeFile();
            RegisterSetTargets();
            RegisterSetDependencies();
            RegisterCollections();
            AssetManagerPrefabMcpTools.Register();
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

        private static void RegisterUpdateItem()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_update_item",
                "Updates a local AssetManager item's name and description. Eagle-sourced names, descriptions, folders, and Eagle-owned tags are edited in Eagle; locally added tags, archive state, and import settings remain editable through the corresponding tools.",
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
                "Replaces locally managed tags on one or more AssetManager items. Eagle-owned tags remain read-only and are synchronized from Eagle.",
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

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_create_collection",
                "Creates an AssetManager smart collection. Filter nodes use type And, Or, Not, or Condition and conditionType NameContains, DescriptionContains, HasTag, or HasFileExtension.",
                McpSchemas.Object(new JObject
                {
                    ["name"] = McpSchemas.String(),
                    ["filter"] = new JObject { ["type"] = "object" }
                }, "name", "filter"),
                arguments => AssetResult(() => Success(Manager().CreateCollection(
                    new CreateAssetCollectionRequest
                    {
                        Name = Required(arguments, "name"),
                        Root = McpJson.To<AssetFilterNode>(arguments["filter"])
                    }))),
                readOnly: false,
                idempotent: false));

            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_asset_update_collection",
                "Updates an existing AssetManager smart collection. Filter nodes use type And, Or, Not, or Condition and conditionType NameContains, DescriptionContains, HasTag, or HasFileExtension.",
                McpSchemas.Object(new JObject
                {
                    ["collectionId"] = McpSchemas.String(),
                    ["name"] = McpSchemas.String(),
                    ["filter"] = new JObject { ["type"] = "object" }
                }, "collectionId", "name", "filter"),
                arguments => AssetResult(() =>
                {
                    var manager = Manager();
                    var id = Required(arguments, "collectionId");
                    var name = Required(arguments, "name");
                    var filter = McpJson.To<AssetFilterNode>(arguments["filter"]);
                    return Success(manager.UpdateCollection(
                        id,
                        new UpdateAssetCollectionRequest
                        {
                            Name = name,
                            Root = filter
                        }));
                }),
                readOnly: false));

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
                    var id = Required(arguments, "collectionId");
                    var collection = manager.GetCollection(id);
                    manager.DeleteCollection(id);
                    return Success(collection);
                }),
                readOnly: false,
                destructive: true,
                idempotent: false));
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
