using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetLibrary;
using Ee4v.Core.I18n;
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
        private AnimationClip _libraryPlaceholder;
        private bool _showOtherLibraryTargets;
        private readonly HashSet<AnimationClip> _currentLibraryClips = new HashSet<AnimationClip>();
        private string _sharedFolder = string.Empty;
        private CancellationTokenSource _libraryOperation = new CancellationTokenSource();
        private readonly Dictionary<string, Texture2D> _sharedPreviews = new Dictionary<string, Texture2D>();

        private void EnsureSharedLibrary()
        {
            if (_expressionLibrary != null) return;
            try
            {
                _expressionLibrary = ExpressionLibrary.Open();
                _expressionLibrary.Synchronize();
                _sharedEntries = _expressionLibrary.List();
            }
            catch (Exception exception) { ReportLibraryFailure(exception); }
        }

        private void SynchronizeSharedLibrary()
        {
            try
            {
                EnsureSharedLibrary();
                _expressionLibrary.Synchronize();
                _sharedEntries = _expressionLibrary.List();
                ClearSharedPreviews();
            }
            catch (Exception exception) { ReportLibraryFailure(exception); }
        }

        private void OnLibraryPathChanged()
        {
            CancelLibraryOperation();
            _expressionLibrary = null;
            _sharedEntries = Array.Empty<ExpressionLibraryEntry>();
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
            var folders = entries.Select(entry => entry.Folder).Concat(clips.Select(GetLocalLibraryFolder))
                .Where(folder => folder.StartsWith(prefix, StringComparison.Ordinal) && folder != _sharedFolder)
                .Select(folder => "shared:/" + prefix + folder.Substring(prefix.Length).Split('/')[0])
                .Distinct().OrderBy(folder => folder, StringComparer.Ordinal).ToArray();
            _view.SetSharedLibrary(entries.Where(entry => entry.Folder == _sharedFolder).ToArray(),
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

        private void SelectSharedCandidate(ExpressionLibraryEntry entry)
        {
            CancelLibraryOperation();
            _sharedCandidateEntry = entry;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(entry.RootGuid));
            if (clip == null)
            {
                if (_libraryPlaceholder != null) UnityEngine.Object.DestroyImmediate(_libraryPlaceholder);
                _libraryPlaceholder = new AnimationClip { name = entry.Name, hideFlags = HideFlags.HideAndDontSave };
                clip = _libraryPlaceholder;
            }
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
            SelectSharedCandidate(entry);
            try
            {
                var clip = await PrepareLibraryCandidate(_assignmentCandidateClip);
                if (copy) CopyClip(clip);
                else SetClip(clip);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportLibraryFailure(exception); }
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
                var entry = FaceExpressionLibraryApi.Publish(_avatar, clip, existing?.Folder ?? _sharedFolder,
                    existing?.PrimaryFbxGuid, _expressionLibrary);
                _sharedEntries = _expressionLibrary.List();
                if (_assignmentCandidateClip == clip || _sharedCandidateEntry?.RootGuid == entry.RootGuid)
                    _sharedCandidateEntry = entry;
                _saveFailed = false;
                ClearSharedPreviews();
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

        private void DrawSharedThumbnail(ExpressionLibraryEntry entry, Rect rect)
        {
            var key = entry.Id + ":" + entry.PreviewHash;
            if (!_sharedPreviews.TryGetValue(key, out var texture))
            {
                var path = _expressionLibrary?.GetPreviewPath(entry.Id);
                if (path != null)
                {
                    texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                    if (!texture.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(texture); texture = null; }
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

        private void DisposeSharedLibrary()
        {
            _libraryOperation.Cancel();
            _libraryOperation.Dispose();
            if (_libraryPlaceholder != null) UnityEngine.Object.DestroyImmediate(_libraryPlaceholder);
            ClearSharedPreviews();
        }
    }
}
