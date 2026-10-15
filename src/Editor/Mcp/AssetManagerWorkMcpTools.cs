using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.UI;
using Ee4v.Core.Settings;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.Mcp
{
    internal static class AssetManagerWorkMcpTools
    {
        internal static IAssetVariantManager Manager() => AssetManagerFactory.OpenVariantSession(
            Path.Combine(GlobalDataSettings.RootDirectory, "asset-manager-v1.db"), AssetManagerMcpTools.Manager());

        internal static object List() => new { variants = Manager().GetVariants().Select(work => work.Id)
            .Concat(DerivedAssetCreator.FindAll().Select(work => work.VariantId)).Distinct(StringComparer.Ordinal)
            .Select(id => new { variantId = id, variant = Work(id) }).ToArray() };

        internal static void Register()
        {
            Add("ee4v_asset_create_work", "Creates a new owned Item and work, optionally based on an imported Prefab; no source creates a regular composition Prefab. Opens its working Scene, without saving a history revision.",
                McpSchemas.Object(new JObject { ["name"] = McpSchemas.String(), ["description"] = McpSchemas.String(), ["sourcePrefabGuid"] = McpSchemas.String() }, "name"), args =>
                {
                    GameObject source = null;
                    if (args["sourcePrefabGuid"] != null)
                    {
                        var guid = McpJson.RequireString(args, "sourcePrefabGuid");
                        source = AssetManagerPrefabMcpTools.Resolve(guid, null).Asset;
                        var imported = AssetManagerMcpTools.Manager().GetImportedAssetAssociations(new[] { guid }).Any();
                        var owned = DerivedAssetCreator.FindAll().Any(work => !work.IsArchived && work.Prefab == source &&
                            AssetManagerMcpTools.Manager().GetItem(work.ParentItemId).IsOriginal);
                        if (!imported && !owned) throw new McpToolException("source_not_imported", "Use a library-imported Prefab or an original Item's work.");
                    }
                    var result = DerivedAssetCreator.CreateOwnedWork(AssetManagerMcpTools.Manager(), McpJson.RequireString(args, "name"), (string)args["description"], source);
                    var root = DerivedAssetCreator.OpenWorkingScene(result.AssetPath);
                    Selection.activeGameObject = root;
                    return Done(result);
                }, false, idempotent: false);
            Add("ee4v_asset_get_variant", "Reads work metadata, history, current editing revision, asset changes, gallery identities and working Scene state. variantId also identifies a saved work absent from this Project.",
                Id(), args => Done(Inspect(McpJson.RequireString(args, "variantId"))), true);
            var edit = IdProperties(); edit["name"] = McpSchemas.String(); edit["description"] = McpSchemas.String();
            edit["tags"] = McpSchemas.Array(McpSchemas.String()); edit["archived"] = McpSchemas.Boolean();
            var editSchema = McpSchemas.Object(edit, "variantId");
            editSchema["anyOf"] = new JArray(new[] { "name", "description", "tags", "archived" }.Select(field => new JObject { ["required"] = new JArray(field) }));
            Add("ee4v_asset_edit_variant", "Patches work metadata and archive state through the UI history manager, including unsaved works. Unspecified fields are retained. Tags apply only to works without a base.", editSchema, async args =>
            {
                var id = McpJson.RequireString(args, "variantId"); var current = Work(id); var manager = Manager();
                var applied = new List<string>();
                try
                {
                    if (new[] { "name", "description", "tags" }.Any(field => args[field] != null))
                    {
                        await manager.UpdateMetadata(id, new UpdateAssetVariantRequest { Name = args["name"] == null ? current.Name : McpJson.RequireString(args, "name"),
                            Description = (string)args["description"] ?? current.Description, Tags = McpJson.To<string[]>(args["tags"]) });
                        applied.Add("metadata");
                    }
                    if (args["archived"] != null) { await manager.SetArchived(id, (bool)args["archived"]); applied.Add("archived"); }
                    return McpToolResult.Success(McpJson.From(Inspect(id)));
                }
                catch (Exception exception) { return McpToolResult.Error("variant_edit_failed", exception.Message, McpJson.From(new { appliedChanges = applied })); }
            }, false);
            var restore = IdProperties(); restore["revisionId"] = McpSchemas.String(); restore["discardWorkingChanges"] = McpSchemas.Boolean();
            Add("ee4v_asset_restore_variant", "Restores an explicit saved revision through AssetManager, including its dependencies. Replaces work assets and removes work-local files absent from the revision. Dirty working Scenes require discardWorkingChanges=true.",
                McpSchemas.Object(restore, "variantId", "revisionId"), args => Restore(args, false), false, true, false, true);
            Add("ee4v_asset_discard_variant_changes", "Discards local changes by restoring the current editing revision through AssetManager. Requires a known saved editing revision and discardWorkingChanges=true for a dirty Scene.",
                McpSchemas.Object(new JObject { ["variantId"] = McpSchemas.String(), ["discardWorkingChanges"] = McpSchemas.Boolean() }, "variantId"),
                args => Restore(args, true), false, true, false, true);
            Add("ee4v_asset_delete_variant", "Deletes an archived work using the UI history manager. Does not delete imported source assets. Dirty working Scenes require discardWorkingChanges=true.",
                McpSchemas.Object(new JObject { ["variantId"] = McpSchemas.String(), ["discardWorkingChanges"] = McpSchemas.Boolean() }, "variantId"), async args =>
                {
                    var work = Work(McpJson.RequireString(args, "variantId"));
                    if (!work.IsArchived) throw new McpToolException("archive_required", "Archive the work before deleting it.");
                    CloseWorkingScene(work.RootAssetPath, (bool?)args["discardWorkingChanges"] ?? false);
                    await Manager().Delete(work.Id); return McpToolResult.Success(McpJson.From(new { deleted = work }));
                }, false, true, false);
            var fitting = IdProperties(); fitting["avatarPrefabGuid"] = McpSchemas.String("Empty removes the fitting Avatar.");
            Add("ee4v_asset_set_fitting_avatar", "Sets or clears the fitting Avatar for a non-avatar composition using the UI hierarchy, cycle validation and Undo. Fitting environment is excluded from work Prefab saving.",
                McpSchemas.Object(fitting, "variantId", "avatarPrefabGuid"), args =>
                {
                    var work = Work(McpJson.RequireString(args, "variantId")); var guid = (string)args["avatarPrefabGuid"];
                    var root = DerivedAssetCreator.OpenWorkingScene(work.RootAssetPath);
                    var prefab = string.IsNullOrWhiteSpace(guid) ? null : AssetManagerPrefabMcpTools.Resolve(guid, null).Asset;
                    DerivedAssetCreator.SetFittingAvatar(root, prefab);
                    return Done(new { variantId = work.Id, fittingAvatarPrefabGuid = prefab == null ? string.Empty : guid });
                }, false);
            var image = IdProperties(); image["imageId"] = McpSchemas.String(); image["revisionId"] = McpSchemas.String();
            var imageSchema = McpSchemas.Object(image, "variantId");
            imageSchema["oneOf"] = new JArray(
                McpSchemas.Object(new JObject { ["variantId"] = image["variantId"].DeepClone(), ["imageId"] = image["imageId"].DeepClone() }, "variantId", "imageId"),
                McpSchemas.Object(new JObject { ["variantId"] = image["variantId"].DeepClone(), ["revisionId"] = image["revisionId"].DeepClone() }, "variantId", "revisionId"));
            Add("ee4v_asset_get_variant_image", "Returns a gallery image or saved revision thumbnail. Select imageId or revisionId from variant inspection.", imageSchema, async args =>
            {
                var id = McpJson.RequireString(args, "variantId"); Work(id);
                var thumbnail = args["imageId"] != null ? await Manager().GetGalleryImage(id, McpJson.RequireString(args, "imageId")) :
                    await Manager().GetRevisionThumbnail(id, McpJson.RequireString(args, "revisionId"));
                if (!thumbnail.Found) return McpToolResult.Error("image_unavailable", thumbnail.MissingReason);
                return McpToolResult.MediaImage(new JObject { ["variantId"] = id }, thumbnail.Data);
            }, true);
            var upload = IdProperties(); upload["paths"] = McpSchemas.Array(McpSchemas.String());
            Add("ee4v_asset_add_variant_images", "Adds explicit local PNG/JPEG images to a work gallery with UI image validation, size limit, deduplication and history persistence.",
                McpSchemas.Object(upload, "variantId", "paths"), async args =>
                {
                    var work = Work(McpJson.RequireString(args, "variantId"));
                    var paths = AssetManagerMcpTools.RequiredStrings(args, "paths");
                    var uploads = paths.Select(path =>
                    {
                        var info = new FileInfo(path);
                        if (!info.Exists || info.Length > 16 * 1024 * 1024) throw new McpToolException("invalid_image", "Gallery images must exist and be at most 16 MiB.");
                        return new AssetVariantGalleryUpload { FileName = info.Name, Data = File.ReadAllBytes(info.FullName) };
                    }).ToArray();
                    await Manager().AddGalleryImages(work.Id, work.ParentItemId, uploads);
                    return McpToolResult.Success(McpJson.From(Manager().GetGalleryImages(work.Id)));
                }, false, idempotent: false, openWorld: true);
            var gallery = McpSchemas.Object(new JObject { ["variantId"] = McpSchemas.String(), ["imageId"] = McpSchemas.String() }, "variantId", "imageId");
            Add("ee4v_asset_delete_variant_image", "Deletes an explicitly added gallery image. Generated revision thumbnails are retained.", gallery,
                async args => { await Manager().RemoveGalleryImage(GalleryWork(args), McpJson.RequireString(args, "imageId")); return McpToolResult.Success(new JObject { ["ok"] = true }); }, false, true, false);
            Add("ee4v_asset_move_variant_image_to_front", "Moves one explicitly added gallery image to the front and persists the UI's gallery order.", (JObject)gallery.DeepClone(),
                async args => { await Manager().MoveGalleryImageToFront(GalleryWork(args), McpJson.RequireString(args, "imageId")); return McpToolResult.Success(new JObject { ["ok"] = true }); }, false);
        }

        internal static AssetVariant Work(string id)
        {
            var stored = Manager().GetVariants().FirstOrDefault(work => work.Id == id);
            var local = DerivedAssetCreator.FindAll().FirstOrDefault(work => work.VariantId == id);
            if (local != null) return new AssetVariant { Id = id, ParentItemId = local.ParentItemId, Name = local.Name, Description = local.Description,
                Tags = local.Tags, RootAssetPath = local.AssetPath, SourcePrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(local.Prefab))),
                HeadRevisionId = stored?.HeadRevisionId, IsArchived = local.IsArchived, CreatedAt = local.CreatedAt, UpdatedAt = local.UpdatedAt };
            return stored ?? throw new McpToolException("variant_not_found", "variantId must identify an owned work or saved history.");
        }
        private static string GalleryWork(JObject args)
        {
            var id = McpJson.RequireString(args, "variantId"); Work(id);
            var imageId = McpJson.RequireString(args, "imageId");
            if (!Manager().GetGalleryImages(id).Any(image => image.Id == imageId))
                throw new McpToolException("image_not_found", "imageId must identify an explicitly added gallery image from work inspection.");
            return id;
        }
        private static object Inspect(string id)
        {
            var work = Work(id); var manager = Manager(); var scenePath = DerivedAssetCreator.GetWorkingScenePath(work.RootAssetPath);
            var scene = SceneManager.GetSceneByPath(scenePath);
            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(work.RootAssetPath) != null;
            var dirtyAssets = exists ? UnsavedAssets(work.RootAssetPath) : Array.Empty<string>();
            return new { variant = work, projectAssetAvailable = exists, workingScenePath = scenePath, sceneDirty = scene.IsValid() && scene.isDirty,
                currentRevisionId = manager.GetCurrentRevisionId(id), revisions = manager.GetRevisions(id).Select(revision => new { revision, details = manager.GetRevisionDetails(id, revision.Id) }).ToArray(),
                changes = exists && dirtyAssets.Length == 0 ? manager.GetChangeDetails(work.RootAssetPath) : null,
                unsavedAssetPaths = dirtyAssets, changesUnavailableReason = dirtyAssets.Length > 0 ? "Project assets have unsaved changes. Save them explicitly before inspecting disk changes." : null,
                gallery = manager.GetGalleryImages(id) };
        }
        private static string[] UnsavedAssets(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            return AssetDatabase.GetDependencies(assetPath, true)
                .Concat(AssetDatabase.FindAssets(string.Empty, new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
                .Distinct(StringComparer.Ordinal).Where(path => AssetDatabase.IsMainAssetAtPathLoaded(path) &&
                    AssetDatabase.LoadAllAssetsAtPath(path).Any(asset => asset != null && EditorUtility.IsDirty(asset))).ToArray();
        }
        private static async Task<McpToolResult> Restore(JObject args, bool discard)
        {
            var work = Work(McpJson.RequireString(args, "variantId")); var manager = Manager();
            var revision = discard ? manager.GetCurrentRevisionId(work.Id) : McpJson.RequireString(args, "revisionId");
            if (string.IsNullOrEmpty(revision)) throw new McpToolException("revision_required", "This work has no saved editing revision to restore.");
            manager.GetRevisionDetails(work.Id, revision);
            CloseWorkingScene(work.RootAssetPath, (bool?)args["discardWorkingChanges"] ?? false);
            var path = await manager.Restore(work.Id, revision);
            return McpToolResult.Success(McpJson.From(new { variantId = work.Id, revisionId = revision, restoredPath = path }));
        }
        private static void CloseWorkingScene(string assetPath, bool discard)
        {
            var scene = SceneManager.GetSceneByPath(DerivedAssetCreator.GetWorkingScenePath(assetPath));
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (scene.isDirty && !discard) throw new McpToolException("working_scene_dirty", "Inspect the work and explicitly set discardWorkingChanges=true to discard Scene changes.");
            if (!EditorSceneManager.CloseScene(scene, true)) throw new McpToolException("scene_close_failed", "The working Scene could not be closed.");
        }
        private static JObject IdProperties() => new JObject { ["variantId"] = McpSchemas.String("Stable work GUID from creation/listing; works missing from this Project remain addressable by saved ID.") };
        private static JObject Id() => McpSchemas.Object(IdProperties(), "variantId");
        private static Task<McpToolResult> Done(object value) => Task.FromResult(McpToolResult.Success(McpJson.From(value)));
        private static void Add(string name, string description, JObject schema, Func<JObject, Task<McpToolResult>> invoke,
            bool readOnly, bool destructive = false, bool idempotent = true, bool openWorld = false) =>
            McpToolRegistry.Register(new McpToolDefinition(name, description, schema, invoke, readOnly, destructive, idempotent, openWorld));
    }
}
