using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetLibrary;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.Core.Settings;
using Ee4v.FaceExpression;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static partial class FaceExpressionMcpTools
    {
        private static void RegisterExpressionLibrary()
        {
            McpToolRegistry.Register(new McpToolDefinition("ee4v_list_expression_library",
                "Lists shared ee4v expressions, including expressions not imported into this project. Optional avatarRef filters by the current avatar's FBXs. Returns libraryId, library revision and current clipPath; use import_expression before editing or assigning an absent Clip.",
                McpSchemas.Object(new JObject { ["avatarRef"] = McpSchemas.String() }),
                args =>
                {
                    var library = ExpressionLibrary.Open(); library.Synchronize();
                    var entries = library.List().AsEnumerable();
                    if (!string.IsNullOrEmpty((string)args["avatarRef"]))
                    {
                        var guids = Avatar(args).GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Where(renderer => renderer.sharedMesh != null)
                            .Select(renderer => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMesh))).ToHashSet();
                        entries = entries.Where(entry => guids.Contains(entry.PrimaryFbxGuid));
                    }
                    return Task.FromResult(McpToolResult.Success(McpJson.From(new { expressions = entries.Select(LibraryValue).ToArray() })));
                }, readOnly: true));
            McpToolRegistry.Register(new McpToolDefinition("ee4v_import_expression",
                "Validates and imports a shared expression and its dependencies into the configured Animation/Facial/<libraryId> folder, preserving GUIDs. Requires the revision from list_expression_library. dryRun validates the avatar and revision without importing. Returns the actual clipPath for existing editing and assignment tools.",
                McpSchemas.Object(new JObject { ["avatarRef"] = McpSchemas.String(), ["libraryId"] = McpSchemas.String(),
                    ["expectedRevision"] = McpSchemas.String(), ["dryRun"] = McpSchemas.Boolean() }, "avatarRef", "libraryId", "expectedRevision"),
                args => ImportLibraryExpression(args, CancellationToken.None), readOnly: false,
                invokeWithCancellation: ImportLibraryExpression));
        }

        private static async Task<McpToolResult> ImportLibraryExpression(JObject args, CancellationToken cancellationToken)
        {
            var library = ExpressionLibrary.Open(); library.Synchronize();
            var entry = library.List().FirstOrDefault(value => value.Id == McpJson.RequireString(args, "libraryId"));
            if (entry == null) throw new McpToolException("expression_not_found", "The shared expression was not found.");
            if (entry.Revision != McpJson.RequireString(args, "expectedRevision"))
                throw new McpToolException("expression_revision_conflict", "The shared expression changed. List the library again.");
            var avatar = Avatar(args);
            if ((bool?)args["dryRun"] == true)
            {
                FaceExpressionLibraryApi.ValidateTarget(avatar, entry);
                return McpToolResult.Success(McpJson.From(new { dryRun = true, library = LibraryValue(entry) }));
            }
            var clip = await FaceExpressionLibraryApi.Import(avatar, entry, library, cancellationToken);
            return McpToolResult.Success(McpJson.From(new { clipPath = AssetDatabase.GetAssetPath(clip), library = LibraryValue(entry) }));
        }

        private static object LibraryValue(ExpressionLibraryEntry entry) => new
        {
            libraryId = entry.Id, entry.Name, entry.Folder, entry.PrimaryFbxGuid,
            revision = entry.Revision, clipGuid = entry.RootGuid,
            clipPath = ExistingExpressionPath(entry.RootGuid),
            targets = entry.Targets.Select(target => new { target.FbxGuid, target.FbxName,
                meshLocalId = target.MeshLocalId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                target.RendererPath, target.ShapeName }).ToArray()
        };

        private static string ExistingExpressionPath(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null ? path : string.Empty;
        }

        private static object InspectLibraryClip(AnimationClip clip)
        {
            var library = ExpressionLibrary.Open(); library.Synchronize();
            var entry = library.FindByRootGuid(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip)));
            return entry == null ? null : LibraryValue(entry);
        }

        private static JObject PrepareExpressionDestination(JObject args, string pathField, bool create)
        {
            var prepared = (JObject)args.DeepClone();
            var root = ProjectAssetSettings.GetAssetFolder("Animation/Facial").Replace('\\', '/').TrimEnd('/');
            var folder = ValidateLibraryFolder((string)args["libraryFolder"] ?? string.Empty);
            var path = ((string)args[pathField] ?? string.Empty).Trim().Replace('\\', '/');
            if (path.Length == 0)
            {
                if (!create) throw new McpToolException("invalid_request", pathField + " is required when updating an existing expression.");
                var name = McpJson.RequireString(args, "name");
                if (name.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
                if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new McpToolException("invalid_request", "name must be a valid expression file name without a directory.");
                path = root + "/" + (folder.Length > 0 ? folder + "/" : string.Empty) + name + ".anim";
            }
            ValidateExpressionPath(path);
            if (create && !path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                throw new McpToolException("invalid_expression_destination", "Create expressions within " + root + ", or omit the destination path and supply name.");
            if (args["libraryFolder"] == null && path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                var relative = path.Substring(root.Length + 1);
                var separator = relative.LastIndexOf('/');
                folder = separator < 0 ? string.Empty : relative.Substring(0, separator);
            }
            prepared[pathField] = path;
            prepared["libraryFolder"] = folder;
            return prepared;
        }

        private static string ValidateLibraryFolder(string folder)
        {
            if (folder.Length > 0 && folder.Split('/').Any(part => string.IsNullOrWhiteSpace(part) ||
                    part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new McpToolException("invalid_request", "libraryFolder must be a relative expression folder.");
            return folder;
        }

        private static void ValidateExpressionPath(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                path.Split('/').Any(part => part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new McpToolException("invalid_expression_destination", "The expression must be a standalone .anim asset under Assets.");
        }

        private static McpToolResult SaveExpressionWrite(JObject args, Func<McpToolResult> write, string pathField,
            string avatarField = "avatarRef")
        {
            var path = McpJson.RequireString(args, pathField).Replace('\\', '/');
            ValidateExpressionPath(path);
            var avatar = UnityObjectReference.ResolveGameObject(McpJson.RequireString(args, avatarField));
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new McpToolException("invalid_expression_destination", "The destination is occupied by another asset.");
            var dryRun = (bool?)args["dryRun"] ?? false;
            if (existing != null && AssetDatabase.LoadAllAssetsAtPath(path).Any(EditorUtility.IsDirty))
                throw new McpToolException("expression_local_changes", "Save or discard the expression's pending UI changes before editing through MCP.");
            var library = ExpressionLibrary.Open(); library.Synchronize();
            var guid = AssetDatabase.AssetPathToGUID(path);
            var previous = library.FindByRootGuid(guid);
            if (previous != null && previous.Revision != library.GetWorkingRevision(guid))
                throw new McpToolException("expression_revision_conflict", "Import the current shared expression before editing it.");
            if (existing != null)
            {
                if (AssetManagerFactory.Open(Path.Combine(GlobalDataSettings.RootDirectory, "asset-manager-v1.db"))
                        .GetImportedAssetAssociations(new[] { guid }).Count > 0)
                    throw new McpToolException("expression_source_protected", "Copy the source Clip before editing it as a library expression.");
                if (previous != null) FaceExpressionLibraryApi.ValidateTarget(avatar, previous);
            }
            var folder = previous?.Folder ?? ValidateLibraryFolder((string)args["libraryFolder"] ?? string.Empty);
            if (dryRun) return write();
            var absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), path));
            var oldBody = existing == null ? null : File.ReadAllBytes(absolute);
            var oldMeta = existing == null ? null : File.ReadAllBytes(absolute + ".meta");
            try
            {
                var result = write();
                if (result.IsError) return result;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var entry = FaceExpressionLibraryApi.Publish(avatar, clip, folder,
                    previous?.PrimaryFbxGuid ?? (string)args["primaryFbxGuid"], library);
                var value = (JObject)result.StructuredContent.DeepClone();
                value["library"] = McpJson.From(LibraryValue(entry));
                return McpToolResult.Success(value);
            }
            catch
            {
                if (existing == null)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
                }
                else
                {
                    File.WriteAllBytes(absolute, oldBody);
                    File.WriteAllBytes(absolute + ".meta", oldMeta);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
                throw;
            }
        }
    }
}
