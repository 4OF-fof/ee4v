using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetLibrary;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    public static class FaceExpressionLibraryApi
    {
        public static ExpressionLibraryEntry Publish(GameObject avatar, AnimationClip clip, string folder = "",
            string primaryFbxGuid = null, IExpressionLibrary library = null)
            => Publish(avatar, clip, folder, primaryFbxGuid, library,
                () => FaceExpressionApi.RenderPreview(avatar, clip, 256, 256));

        internal static ExpressionLibraryEntry Publish(GameObject avatar, AnimationClip clip, string folder,
            string primaryFbxGuid, IExpressionLibrary library, Func<byte[]> renderPreview)
        {
            if (avatar == null || clip == null) throw new ArgumentException("An avatar and expression Clip are required.");
            var findings = FaceExpressionApi.ValidateClip(avatar, clip);
            if (findings.Any(finding => finding.Code == "binding_not_found" || finding.Code == "non_blendshape_curve" ||
                    finding.Code == "object_reference_curves" || finding.Code == "animation_events" || finding.Severity == "error"))
                throw new InvalidOperationException("The expression contains unsupported or invalid bindings.");
            var channels = FaceExpressionClipEditor.Read(avatar, clip, FaceExpressionSettings.GetSeparators(),
                FaceExpressionClipEditor.GetRendererPaths(avatar));
            var targets = channels.Where(channel => channel.Animated && !channel.IsHeader)
                .Select(channel => new ExpressionTarget { FbxGuid = channel.SourceAssetGuid,
                    FbxName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(channel.SourceAssetGuid)),
                    MeshLocalId = channel.SourceMeshLocalId, RendererPath = channel.RendererPath, ShapeName = channel.Name }).ToArray();
            if (targets.Any(target => string.IsNullOrEmpty(target.FbxGuid)))
                throw new InvalidOperationException("Every expression channel must belong to a saved FBX mesh.");
            if (string.IsNullOrEmpty(primaryFbxGuid))
            {
                var body = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(renderer => renderer.name == "Body");
                primaryFbxGuid = body?.sharedMesh != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(body.sharedMesh)) : null;
                if (string.IsNullOrEmpty(primaryFbxGuid))
                {
                    var guids = (targets.Length > 0 ? targets.Select(target => target.FbxGuid) : channels.Select(channel => channel.SourceAssetGuid))
                        .Where(guid => !string.IsNullOrEmpty(guid)).Distinct().ToArray();
                    if (guids.Length != 1) throw new InvalidOperationException("Choose a primary FBX when the expression uses multiple FBXs without Body.");
                    primaryFbxGuid = guids[0];
                }
            }
            if (!avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.sharedMesh != null &&
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMesh)) == primaryFbxGuid))
                throw new InvalidOperationException("The primary FBX must belong to the expression's avatar.");
            library ??= ExpressionLibrary.Open();
            return library.Publish(new ExpressionPublishRequest { AssetPath = AssetDatabase.GetAssetPath(clip), Name = clip.name,
                Folder = folder, PrimaryFbxGuid = primaryFbxGuid, Targets = targets,
                ExpectedRevision = library.GetWorkingRevision(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip))),
                Preview = renderPreview() });
        }

        public static void ValidateTarget(GameObject avatar, ExpressionLibraryEntry entry)
        {
            if (avatar == null || entry == null) throw new ArgumentException("An avatar and library expression are required.");
            if (!avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.sharedMesh != null &&
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMesh)) == entry.PrimaryFbxGuid))
                throw new InvalidOperationException("The expression's primary FBX does not match this avatar.");
            foreach (var target in entry.Targets)
            {
                var transform = string.IsNullOrEmpty(target.RendererPath) ? avatar.transform : avatar.transform.Find(target.RendererPath);
                var mesh = transform?.GetComponent<SkinnedMeshRenderer>()?.sharedMesh;
                if (mesh == null || !string.Equals(Path.GetExtension(AssetDatabase.GetAssetPath(mesh)), ".fbx", StringComparison.OrdinalIgnoreCase) ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId) ||
                    guid != target.FbxGuid || localId != target.MeshLocalId || mesh.GetBlendShapeIndex(target.ShapeName) < 0)
                    throw new InvalidOperationException("The expression does not match this avatar: " + target.RendererPath + "/" + target.ShapeName);
            }
        }

        public static async Task<AnimationClip> Import(GameObject avatar, ExpressionLibraryEntry entry,
            IExpressionLibrary library = null, CancellationToken cancellationToken = default)
        {
            ValidateTarget(avatar, entry);
            library ??= ExpressionLibrary.Open();
            var path = await library.EnsureImported(entry.Id, entry.Revision,
                ProjectAssetSettings.GetAssetFolder("Animation/Facial/" + entry.Id), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateTarget(avatar, entry);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path) ??
                throw new InvalidOperationException("The expression Clip could not be loaded after import.");
        }
    }
}
