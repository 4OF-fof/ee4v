using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ee4v.SceneSwitcher
{
    internal sealed class UnitySceneSwitcherGateway
        : ISceneSwitcherGateway
    {
        public IReadOnlyList<string> FindScenePaths()
        {
            return AssetDatabase
                .FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();
        }

        public IReadOnlyList<string> GetOpenScenePaths()
        {
            var paths = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path))
                {
                    paths.Add(scene.path);
                }
            }

            return paths;
        }

        public SceneOperationResult SwitchScene(
            string path,
            int sourceSceneHandle)
        {
            if (string.IsNullOrEmpty(path) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.Failed,
                    path);
            }

            var sourceScene =
                FindLoadedSceneByHandle(sourceSceneHandle);
            var loadedScene = SceneManager.GetSceneByPath(path);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                if (ShouldReplaceSource(
                        sourceScene,
                        loadedScene))
                {
                    if (!EditorSceneManager
                            .SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        return new SceneOperationResult(
                            false,
                            SceneOperationFailure.None,
                            path);
                    }

                    if (!TryCloseScene(sourceScene))
                    {
                        return new SceneOperationResult(
                            false,
                            SceneOperationFailure.Failed,
                            path);
                    }
                }

                return CompleteSwitch(loadedScene, path);
            }

            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.None,
                    path);
            }

            try
            {
                var shouldReplace =
                    IsReplaceableSource(sourceScene);
                var openMode = shouldReplace
                    ? OpenSceneMode.Additive
                    : OpenSceneMode.Single;
                var openedScene = EditorSceneManager.OpenScene(
                    path,
                    openMode);
                if (shouldReplace)
                {
                    EditorSceneManager.MoveSceneBefore(
                        openedScene,
                        sourceScene);
                    if (!TryCloseScene(sourceScene))
                    {
                        TryCloseScene(openedScene);
                        return new SceneOperationResult(
                            false,
                            SceneOperationFailure.Failed,
                            path);
                    }
                }

                return CompleteSwitch(openedScene, path);
            }
            catch (Exception)
            {
                var completedTarget =
                    SceneManager.GetSceneByPath(path);
                var remainingSource =
                    FindLoadedSceneByHandle(sourceSceneHandle);
                if (completedTarget.IsValid() &&
                    completedTarget.isLoaded &&
                    (!remainingSource.IsValid() ||
                     remainingSource.handle ==
                     completedTarget.handle))
                {
                    return CompleteSwitch(
                        completedTarget,
                        path);
                }

                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.Failed,
                    path);
            }
        }

        public SceneOperationResult AddScene(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.Failed,
                    path);
            }

            var loadedScene = SceneManager.GetSceneByPath(path);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                return CompleteSwitch(loadedScene, path);
            }

            try
            {
                var openedScene = EditorSceneManager.OpenScene(
                    path,
                    OpenSceneMode.Additive);
                return CompleteSwitch(openedScene, path);
            }
            catch (Exception)
            {
                var completedTarget =
                    SceneManager.GetSceneByPath(path);
                if (completedTarget.IsValid() &&
                    completedTarget.isLoaded)
                {
                    return CompleteSwitch(
                        completedTarget,
                        path);
                }

                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.Failed,
                    path);
            }
        }

        private static bool ShouldReplaceSource(
            Scene sourceScene,
            Scene targetScene)
        {
            return IsReplaceableSource(sourceScene) &&
                   sourceScene.handle != targetScene.handle;
        }

        private static bool IsReplaceableSource(Scene sourceScene)
        {
            return SceneManager.sceneCount > 1 &&
                   sourceScene.IsValid() &&
                   sourceScene.isLoaded;
        }

        private static Scene FindLoadedSceneByHandle(int sceneHandle)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() &&
                    scene.handle == sceneHandle)
                {
                    return scene;
                }
            }

            return default(Scene);
        }

        private static bool TryCloseScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return true;
            }

            var sceneHandle = scene.handle;
            var reportedSuccess =
                EditorSceneManager.CloseScene(scene, true);
            return reportedSuccess ||
                   !FindLoadedSceneByHandle(sceneHandle).IsValid();
        }

        private static SceneOperationResult CompleteSwitch(
            Scene targetScene,
            string path)
        {
            var loadedTarget =
                FindLoadedSceneByHandle(targetScene.handle);
            if (!loadedTarget.IsValid() ||
                !loadedTarget.isLoaded)
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.Failed,
                    path);
            }

            SceneManager.SetActiveScene(loadedTarget);
            return new SceneOperationResult(true, path: path);
        }

        public SceneOperationResult CreateScene(
            string folder,
            string sceneName)
        {
            if (!SceneSwitcherPolicy.IsValidSceneName(sceneName))
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.InvalidName);
            }

            var normalizedFolder =
                SceneSwitcherPolicy.NormalizeAssetFolder(folder);
            if (string.IsNullOrEmpty(normalizedFolder))
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.InvalidFolder);
            }

            var scenePath =
                normalizedFolder + "/" + sceneName + ".unity";
            if (File.Exists(scenePath))
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.AlreadyExists,
                    scenePath);
            }

            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.None,
                    scenePath);
            }

            try
            {
                Directory.CreateDirectory(normalizedFolder);
                var templatePath =
                    normalizedFolder + "/TEMPLATE.unity";
                if (File.Exists(templatePath))
                {
                    if (!AssetDatabase.CopyAsset(
                            templatePath,
                            scenePath))
                    {
                        return new SceneOperationResult(
                            false,
                            SceneOperationFailure.Failed,
                            scenePath);
                    }

                    AssetDatabase.Refresh();
                    EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Single);
                }
                else
                {
                    var scene = EditorSceneManager.NewScene(
                        NewSceneSetup.DefaultGameObjects,
                        NewSceneMode.Single);
                    if (!EditorSceneManager.SaveScene(
                            scene,
                            scenePath))
                    {
                        return new SceneOperationResult(
                            false,
                            SceneOperationFailure.Failed,
                            scenePath);
                    }
                }

                return new SceneOperationResult(
                    true,
                    path: scenePath);
            }
            catch (Exception)
            {
                return new SceneOperationResult(
                    false,
                    SceneOperationFailure.Failed,
                    scenePath);
            }
        }
    }
}
