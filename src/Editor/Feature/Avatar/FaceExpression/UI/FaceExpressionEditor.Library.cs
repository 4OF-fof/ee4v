using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetLibrary;
using Ee4v.Core.I18n;
using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed partial class FaceExpressionEditor
    {
        private IExpressionLibrary _expressionLibrary;
        private IReadOnlyList<ExpressionLibraryEntry> _sharedEntries = Array.Empty<ExpressionLibraryEntry>();
        private ExpressionLibraryEntry _sharedCandidateEntry;
        private Dictionary<string, ExpressionProjectLocation> _projectLocations = new Dictionary<string, ExpressionProjectLocation>();
        private bool _showOtherLibraryTargets;
        private readonly HashSet<AnimationClip> _currentLibraryClips = new HashSet<AnimationClip>();
        private string _sharedFolder = string.Empty;
        private CancellationTokenSource _libraryOperation = new CancellationTokenSource();
        private readonly Dictionary<string, Texture2D> _sharedPreviews = new Dictionary<string, Texture2D>();
        private readonly Queue<AnimationClip> _localRegistrations = new Queue<AnimationClip>();
        private readonly Dictionary<string, Hash128> _localRegistrationFailures = new Dictionary<string, Hash128>();
        private bool _localRegistrationRenderPending;
        private bool _localRegistrationPreviewPrepared;
        private double _libraryInteractionAt = double.NegativeInfinity;

        private void EnsureSharedLibrary()
        {
            if (_expressionLibrary != null) return;
            try
            {
                _expressionLibrary = ExpressionLibrary.Open();
            }
            catch (Exception exception) { ReportLibraryFailure(exception); }
        }

        private bool SynchronizeSharedLibrary()
        {
            try
            {
                EnsureSharedLibrary();
                _expressionLibrary.Synchronize();
                _sharedEntries = _expressionLibrary.List();
                _projectLocations = _expressionLibrary.GetProjectLocations().ToDictionary(value => value.RootGuid);
                PruneSharedPreviews();
                return true;
            }
            catch (Exception exception) { ReportLibraryFailure(exception); return false; }
        }

        private void QueueLocalRegistrations(bool libraryReady)
        {
            _localRegistrations.Clear();
            _localRegistrationRenderPending = false;
            _localRegistrationPreviewPrepared = false;
            if (!libraryReady || _avatar == null || _expressionLibrary == null || _disposed) return;
            var sharedGuids = _sharedEntries.SelectMany(entry => entry.Assets.Select(asset => asset.Guid)).ToHashSet();
            foreach (var clip in _libraryClips)
            {
                if (!_currentLibraryClips.Contains(clip)) continue;
                var path = AssetDatabase.GetAssetPath(clip);
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) || sharedGuids.Contains(guid) ||
                    HasPendingLibraryEdits(clip, path)) continue;
                if (_localRegistrationFailures.TryGetValue(guid, out var failedHash) &&
                    failedHash == AssetDatabase.GetAssetDependencyHash(path)) continue;
                _localRegistrations.Enqueue(clip);
            }
        }

        private void RegisterNextLocalExpression()
        {
            if (_disposed || _avatar == null || _expressionLibrary == null || _root.panel == null ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (_localRegistrations.Count > 0)
            {
                var clip = _localRegistrations.Dequeue();
                if (clip != null)
                {
                    var path = AssetDatabase.GetAssetPath(clip);
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (IsAtOrBelow(GetAssetDirectory(path), _libraryRootFolder) &&
                        !HasPendingLibraryEdits(clip, path) &&
                        !_expressionLibrary.List().Any(entry => entry.Assets.Any(asset => asset.Guid == guid)))
                    {
                        try
                        {
                            FaceExpressionLibraryApi.Publish(_avatar, clip, GetLocalLibraryFolder(clip), null,
                                _expressionLibrary, () => RenderLocalRegistrationPreview(clip));
                            _localRegistrationFailures.Remove(guid);
                            _localRegistrationRenderPending = true;
                        }
                        catch (Exception exception)
                        {
                            _localRegistrationFailures[guid] = AssetDatabase.GetAssetDependencyHash(path);
                            ReportLibraryFailure(new InvalidOperationException(clip.name + ": " + exception.Message, exception));
                        }
                    }
                }
            }
            // Keep the clicked card alive throughout the double-click interval.
            if (_localRegistrations.Count > 0 || !_localRegistrationRenderPending ||
                EditorApplication.timeSinceStartup - _libraryInteractionAt < 0.6d) return;
            _localRegistrationRenderPending = false;
            _sharedEntries = _expressionLibrary.List();
            _projectLocations = _expressionLibrary.GetProjectLocations().ToDictionary(value => value.RootGuid);
            if (_assignmentCandidateClip != null && _sharedCandidateEntry == null)
                _sharedCandidateEntry = _expressionLibrary.FindByRootGuid(
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_assignmentCandidateClip)));
            PruneSharedPreviews();
            RenderLibrary();
        }

        private byte[] RenderLocalRegistrationPreview(AnimationClip clip)
        {
            if (!_localRegistrationPreviewPrepared)
            {
                _preview.PrepareThumbnailSource(FaceExpressionPreview.GetThumbnailSourceSignature(_avatar));
                _localRegistrationPreviewPrepared = true;
            }
            var channels = FaceExpressionClipEditor.Read(_avatar, clip, FaceExpressionSettings.GetSeparators(_settings),
                _previewRendererPaths);
            var texture = _preview.RenderThumbnail(channels, 256, 256);
            if (texture == null) return Array.Empty<byte>();
            try { return texture.EncodeToPNG(); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private bool HasPendingLibraryEdits(AnimationClip clip, string path) => _editedClips.Contains(clip) ||
            AssetDatabase.GetDependencies(path, true).Where(dependency => dependency.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .Any(asset => EditorUtility.IsDirty(asset) || asset is AnimationClip dependencyClip && _editedClips.Contains(dependencyClip));

        private void OnLibraryPathChanged()
        {
            CancelLibraryOperation();
            _expressionLibrary = null;
            _localRegistrations.Clear();
            _localRegistrationFailures.Clear();
            _sharedEntries = Array.Empty<ExpressionLibraryEntry>();
            _projectLocations.Clear();
            _sharedCandidateEntry = null;
            _showOtherLibraryTargets = false;
            _sharedFolder = string.Empty;
            _assignmentCandidateClip = null;
            _assignmentPicker?.SetCandidate(null);
            _view?.SetLibrarySelection(null);
            ClearSharedPreviews();
            RefreshLibrary();
        }

        private bool RenderSharedLibrary()
        {
            if (_view == null) return false;
            var guids = _avatar != null
                ? _avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer => renderer.sharedMesh != null)
                    .Select(renderer => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMesh))).ToHashSet()
                : new HashSet<string>();
            var entries = _sharedEntries.Where(entry => guids.Contains(entry.PrimaryFbxGuid) != _showOtherLibraryTargets).ToArray();
            var sharedGuids = _sharedEntries.SelectMany(entry => entry.Assets.Select(asset => asset.Guid)).ToHashSet();
            var localClips = _libraryClips.Where(clip => clip != null &&
                !sharedGuids.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip)))).ToArray();
            var clips = localClips.Where(clip => _currentLibraryClips.Contains(clip) != _showOtherLibraryTargets).ToArray();
            var prefix = _sharedFolder.Length == 0 ? string.Empty : _sharedFolder + "/";
            var folders = entries.Select(GetSharedLibraryFolder).Concat(clips.Select(GetLocalLibraryFolder))
                .Where(folder => folder.StartsWith(prefix, StringComparison.Ordinal) && folder != _sharedFolder)
                .Select(folder => "shared:/" + prefix + folder.Substring(prefix.Length).Split('/')[0])
                .Distinct().OrderBy(folder => folder, StringComparer.Ordinal).ToArray();
            _view.SetSharedLibrary(entries.Where(entry => GetSharedLibraryFolder(entry) == _sharedFolder).ToArray(),
                _sharedCandidateEntry?.Id, DrawSharedThumbnail);
            var hasOtherAvatars = _sharedEntries.Any(entry => !guids.Contains(entry.PrimaryFbxGuid)) ||
                localClips.Any(clip => !_currentLibraryClips.Contains(clip));
            _view.SetLibrary(folders, clips.Where(clip => GetLocalLibraryFolder(clip) == _sharedFolder).ToArray(),
                _sharedFolder.Length > 0 || _showOtherLibraryTargets, DrawLibraryThumbnail,
                !_showOtherLibraryTargets && _sharedFolder.Length == 0 && hasOtherAvatars);
            return true;
        }

        private string GetLocalLibraryFolder(AnimationClip clip)
        {
            var folder = NormalizeAssetPath(GetAssetDirectory(AssetDatabase.GetAssetPath(clip)));
            return folder == _libraryRootFolder ? string.Empty : folder.Substring(_libraryRootFolder.Length + 1);
        }

        private string GetSharedLibraryFolder(ExpressionLibraryEntry entry)
        {
            if (!_projectLocations.TryGetValue(entry.RootGuid, out var location)) return entry.Folder;
            var folder = NormalizeAssetPath(GetAssetDirectory(location.AssetPath));
            if (string.Equals(folder, location.OriginalFolder, AssetPathComparison) || !IsAtOrBelow(folder, _libraryRootFolder))
                return entry.Folder;
            return folder == _libraryRootFolder ? string.Empty : folder.Substring(_libraryRootFolder.Length + 1);
        }

        private void ShowLibraryFolderInProject()
        {
            var folder = _libraryFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                folder = _sharedEntries.Where(entry => GetSharedLibraryFolder(entry) == _sharedFolder &&
                        _projectLocations.ContainsKey(entry.RootGuid))
                    .Select(entry => GetAssetDirectory(_projectLocations[entry.RootGuid].AssetPath))
                    .FirstOrDefault(AssetDatabase.IsValidFolder);
                if (folder == null)
                {
                    folder = _libraryFolder;
                    while (!AssetDatabase.IsValidFolder(folder) && IsAtOrBelow(folder, _libraryRootFolder))
                        folder = GetAssetDirectory(folder);
                    if (!IsAtOrBelow(folder, _libraryRootFolder))
                        folder = Core.Settings.ProjectAssetSettings.EnsureAssetFolder("Animation/Facial", _settings);
                }
            }
            AssetPicker.ShowProject(_view, typeof(AnimationClip), folder);
        }

        private void SelectSharedCandidate(ExpressionLibraryEntry entry)
        {
            _libraryInteractionAt = EditorApplication.timeSinceStartup;
            var clip = FaceExpressionLibraryApi.GetProjectClip(entry);
            if (_sharedCandidateEntry?.Id == entry.Id && _sharedCandidateEntry.Revision == entry.Revision &&
                _assignmentCandidateClip == clip) return;
            CancelLibraryOperation();
            _sharedCandidateEntry = entry;
            _assignmentCandidateClip = clip;
            _assignmentPicker?.SetCandidate(clip);
            _view?.SetSharedSelection(entry.Id);
        }

        private async Task<AnimationClip> PrepareLibraryCandidate(AnimationClip candidate)
        {
            var entry = _sharedCandidateEntry;
            if (entry == null && candidate != null)
                entry = _expressionLibrary?.FindByRootGuid(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(candidate)));
            if (entry == null) return candidate;
            if (FaceExpressionLibraryApi.GetProjectClip(entry) == null)
                throw new InvalidOperationException(I18N.Get("library.importRequired"));
            var avatar = _avatar;
            var token = _libraryOperation.Token;
            _view?.SetValidation(new MessagePanelState(I18N.Get("library.importing")));
            var clip = await FaceExpressionLibraryApi.Import(avatar, entry, _expressionLibrary, token);
            token.ThrowIfCancellationRequested();
            if (_disposed || _avatar != avatar || _assignmentCandidateClip != candidate) throw new OperationCanceledException();
            _assignmentCandidateClip = clip;
            _view?.SetLibrarySelection(clip);
            _view?.SetValidation(null);
            return clip;
        }

        private async void EditSharedClip(ExpressionLibraryEntry entry, bool copy)
        {
            if (FaceExpressionLibraryApi.GetProjectClip(entry) == null) return;
            SelectSharedCandidate(entry);
            try
            {
                var needsConversion = false;
                try { FaceExpressionLibraryApi.ValidateTarget(_avatar, entry); }
                catch (InvalidOperationException) { needsConversion = true; }
                if (needsConversion)
                {
                    var avatar = _avatar;
                    var library = _expressionLibrary;
                    var token = _libraryOperation.Token;
                    _view?.SetValidation(new MessagePanelState(I18N.Get("library.importing")));
                    var path = await library.EnsureImported(entry.Id, entry.Revision,
                        Core.Settings.ProjectAssetSettings.GetAssetFolder("Animation/Facial/" + entry.Id, _settings), token);
                    token.ThrowIfCancellationRequested();
                    if (_disposed || _avatar != avatar || _expressionLibrary != library || _sharedCandidateEntry?.Id != entry.Id) return;
                    var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (source == null) throw new InvalidOperationException("The expression Clip could not be loaded for conversion.");
                    _view?.SetValidation(null);
                    ShowClipConversion(source);
                    return;
                }
                var clip = await PrepareLibraryCandidate(_assignmentCandidateClip);
                if (copy) CopyClip(clip);
                else SetClip(clip);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportLibraryFailure(exception); }
        }

        private async void AddSharedClipToProject(ExpressionLibraryEntry entry)
        {
            if (_disposed) return;
            CancelLibraryOperation();
            var token = _libraryOperation.Token;
            var avatar = _avatar;
            var library = _expressionLibrary;
            _view?.SetEnabled(false);
            _view?.SetValidation(new MessagePanelState(I18N.Get("library.importing")));
            try
            {
                EnsureSharedLibrary();
                library = _expressionLibrary;
                await library.EnsureImported(entry.Id, entry.Revision,
                    Core.Settings.ProjectAssetSettings.GetAssetFolder("Animation/Facial/" + entry.Id, _settings), token);
                token.ThrowIfCancellationRequested();
                if (_disposed || _avatar != avatar || _expressionLibrary != library) return;
                RefreshLibrary();
                if (_sharedCandidateEntry?.Id == entry.Id) SelectSharedCandidate(entry);
                _view?.SetValidation(null);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportLibraryFailure(exception); }
            finally { if (!_disposed) _view?.SetEnabled(true); }
        }

        private void DeleteLibraryClip(AnimationClip clip) => DeleteLibraryClip(clip, null);

        private async void DeleteLibraryClip(AnimationClip clip, ExpressionLibraryEntry entry)
        {
            if (_disposed || (clip == null && entry == null)) return;
            var avatar = _avatar;
            var configuration = _assignments.CreateConfiguration();
            var wasEdited = clip != null && _editedClips.Contains(clip);
            CancelLibraryOperation();
            var token = _libraryOperation.Token;
            _view?.SetEnabled(false);
            try
            {
                EnsureSharedLibrary();
                if (entry == null) entry = _expressionLibrary.FindByRootGuid(
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip)));
                if (clip != null)
                {
                    ClearClipAssignments(clip);
                    if (!TryApplyAssignments()) throw new InvalidOperationException("The expression assignments could not be cleared.");
                }
                if (entry != null) await _expressionLibrary.Delete(entry.Id, entry.Revision, token);
                else
                {
                    var path = AssetDatabase.GetAssetPath(clip);
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                        !AssetDatabase.DeleteAsset(path)) throw new IOException("The project expression Clip could not be deleted.");
                }
                _editedClips.Remove(clip);
                if (!ReferenceEquals(clip, null) && _clipSnapshots.TryGetValue(clip, out var snapshot))
                {
                    snapshot.Dispose();
                    _clipSnapshots.Remove(clip);
                }
                if (_disposed || _avatar != avatar) return;
                _sharedCandidateEntry = null;
                _assignmentCandidateClip = null;
                _assignmentPicker?.SetCandidate(null);
                _view?.SetLibrarySelection(null);
                SetClip(null);
                _saveFailed = false;
                RefreshLibrary();
                RefreshSaveNotification();
            }
            catch (Exception exception)
            {
                if (!_disposed && _avatar == avatar)
                {
                    _assignments.SetConfiguration(configuration);
                    _assignmentApplyAt = EditorApplication.timeSinceStartup;
                    TryApplyAssignments();
                    if (wasEdited && clip != null) _editedClips.Add(clip);
                    ReportLibraryFailure(exception);
                }
            }
            finally { if (!_disposed) _view?.SetEnabled(true); }
        }

        private bool TryPublishLibraryClip(AnimationClip clip)
        {
            try
            {
                EnsureSharedLibrary();
                var existing = _expressionLibrary.FindByRootGuid(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip)));
                var entry = FaceExpressionLibraryApi.Publish(_avatar, clip, existing != null ? GetSharedLibraryFolder(existing) : _sharedFolder,
                    existing?.PrimaryFbxGuid, _expressionLibrary);
                _sharedEntries = _expressionLibrary.List();
                _projectLocations = _expressionLibrary.GetProjectLocations().ToDictionary(value => value.RootGuid);
                if (_assignmentCandidateClip == clip || _sharedCandidateEntry?.RootGuid == entry.RootGuid)
                    _sharedCandidateEntry = entry;
                _saveFailed = false;
                PruneSharedPreviews();
                RenderLibrary();
                RefreshSaveNotification();
                return true;
            }
            catch (Exception exception)
            {
                _saveFailed = true;
                ReportLibraryFailure(exception);
                RefreshSaveNotification();
                return false;
            }
        }

        private void DrawSharedThumbnail(ExpressionLibraryEntry entry, Rect rect, bool imported)
        {
            var key = entry.Id + ":" + entry.PreviewHash + (imported ? ":project" : ":library");
            if (!_sharedPreviews.TryGetValue(key, out var texture))
            {
                var path = _expressionLibrary?.GetPreviewPath(entry.Id);
                if (path != null)
                {
                    texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                    if (!texture.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(texture); texture = null; }
                    if (!imported && texture != null)
                    {
                        var pixels = texture.GetPixels32();
                        for (var index = 0; index < pixels.Length; index++)
                        {
                            var pixel = pixels[index];
                            var gray = (byte)Mathf.RoundToInt(pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f);
                            pixels[index] = new Color32(gray, gray, gray, pixel.a);
                        }
                        texture.SetPixels32(pixels);
                        texture.Apply(false, false);
                    }
                }
                _sharedPreviews[key] = texture;
            }
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            else GUI.DrawTexture(rect, EditorGUIUtility.IconContent("AnimationClip Icon").image, ScaleMode.ScaleToFit, true);
        }

        private void ReportLibraryFailure(Exception exception)
        {
            _view?.SetValidation(new MessagePanelState(I18N.Get("library.failed"), exception.Message, MessageSeverity.Error));
            Debug.LogError(I18N.Get("library.failed") + " " + exception.Message);
        }

        private void CancelLibraryOperation()
        {
            _libraryOperation.Cancel();
            _libraryOperation.Dispose();
            _libraryOperation = new CancellationTokenSource();
        }

        private void ClearSharedPreviews()
        {
            foreach (var texture in _sharedPreviews.Values) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            _sharedPreviews.Clear();
        }

        private void PruneSharedPreviews()
        {
            var keys = _sharedEntries.SelectMany(entry => new[] { entry.Id + ":" + entry.PreviewHash + ":project",
                entry.Id + ":" + entry.PreviewHash + ":library" }).ToHashSet();
            foreach (var key in _sharedPreviews.Keys.Where(key => !keys.Contains(key)).ToArray())
            {
                if (_sharedPreviews[key] != null) UnityEngine.Object.DestroyImmediate(_sharedPreviews[key]);
                _sharedPreviews.Remove(key);
            }
        }

        private void DisposeSharedLibrary()
        {
            _localRegistrations.Clear();
            _localRegistrationFailures.Clear();
            _libraryOperation.Cancel();
            _libraryOperation.Dispose();
            ClearSharedPreviews();
        }
    }
}
