using System;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        private BodyPartSelector BuildBodyPartSelector()
        {
            return new BodyPartSelector(_avatarContext.SelectedBodyPart, HasAvailableBodyPart, part =>
            {
                if (_avatarContext.SelectedBodyPart == part)
                {
                    _avatarContext.Preview?.FocusBodyPart(part);
                    return;
                }
                _parts.EndBodyScaleDrag();
                _avatarContext.SelectedBodyPart = part;
                _avatarContext.SelectedPartKey = null;
                _avatarContext.SelectedMaterial = null;
                ShowCategory(_currentCategory, false);
                _avatarContext.Preview?.FocusBodyPart(part);
            });
        }

        private bool HasAvailableBodyPart(BodyPartCategory part)
        {
            if (_bodyPartAvailabilityCache.TryGetValue(part, out var available))
            {
                return available;
            }
            available = PrefabScenePreview.HasFocusBone(
                _avatarContext.Root, part, IsInSelectedPrefabScope);
            if (available)
            {
                _meshBodyPartCategoriesCache = _meshBodyPartCategoriesCache ??
                    GetMeshBodyPartCategories(_avatarContext.Root, IsInSelectedPrefabScope);
                available = _meshBodyPartCategoriesCache.Any(category => MatchesBodyPartGroup(part, category));
            }
            _bodyPartAvailabilityCache[part] = available;
            return available;
        }

        private bool CommitWorkingScene()
        {
            if (!IsEditableWorkflowPrefab())
            {
                return false;
            }
            try
            {
                Undo.FlushUndoRecordObjects();
                var saved = DerivedAssetCreator.ApplyWorkingScene(
                    GetWorkingAssetPath());
                Undo.FlushUndoRecordObjects();
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "The Variant Prefab is unavailable after applying the Scene.");
                }
                _avatarContext.PrefabAsset = saved;
                if (_workingAsset != null)
                {
                    _workingAsset.Prefab = saved;
                }
                _workingSceneDirty = false;
                InvalidateVariantSaveStatus();
                return true;
            }
            catch (Exception exception)
            {
                _parts.ReportBodyScaleFailure(exception, true);
                return false;
            }
        }

        private bool IsEditableWorkflowPrefab()
        {
            if (EditorApplication.isPlaying || _avatarContext.Root == null || _avatarContext.PrefabAsset == null)
            {
                return false;
            }

            var path = GetWorkingAssetPath();
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(
                       DerivedAssetCreator.VariantRoot + "/",
                       StringComparison.OrdinalIgnoreCase) &&
                   PrefabUtility.IsPartOfPrefabAsset(_avatarContext.PrefabAsset);
        }

        private string GetWorkingAssetPath()
        {
            return _avatarContext.PrefabAsset == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(_avatarContext.PrefabAsset);
        }

        private GameObject AcquireWorkingScene(GameObject asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException(
                    "The selected Prefab has no asset path.");
            }
            if (!WorkingScenes.TryGetValue(path, out var session) ||
                session.Root == null)
            {
                session = WorkingScenes.Values.FirstOrDefault(candidate =>
                    candidate.Root != null &&
                    AssetDatabase.GetAssetPath(
                        PrefabUtility.GetCorrespondingObjectFromSource(
                            candidate.Root)) == path);
                if (session != null)
                {
                    WorkingScenes.Remove(session.PrefabPath);
                    session.PrefabPath = path;
                }
                else
                {
                    var root = DerivedAssetCreator.OpenWorkingScene(path);
                    session = new WorkingSceneSession
                    {
                        Root = root,
                        PrefabPath = path,
                        Scene = root.scene,
                        Dirty = PrefabUtility.IsPartOfPrefabInstance(root) &&
                            PrefabUtility.HasPrefabInstanceAnyOverrides(root, false)
                    };
                }
                WorkingScenes[path] = session;
            }
            session.Users++;
            _workingSceneSession = session;
            SceneManager.SetActiveScene(session.Root.scene);
            Selection.activeGameObject = session.Root;
            SceneView.lastActiveSceneView?.FrameSelected();
            return session.Root;
        }

        private void ReleaseWorkingScene(bool discard = false)
        {
            var session = _workingSceneSession;
            if (session != null)
            {
                session.Users = Math.Max(0, session.Users - 1);
                if (discard) { session.Dirty = false; }
                if (session.Users == 0)
                {
                    WorkingScenes.Remove(session.PrefabPath);
                }
            }
            _avatarContext.Root = null;
            _avatarContext.PrefabAsset = null;
            _workingSceneSession = null;
        }
    }
}
