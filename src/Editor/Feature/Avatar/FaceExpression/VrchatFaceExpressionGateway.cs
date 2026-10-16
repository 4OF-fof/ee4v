using System;
using System.Linq;
using Ee4v.Core;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    // Prepared motions and menu icons are derived assets. Their source clips can
    // also change outside the expression window (including through the public API).
    [InitializeOnLoad]
    internal static class FaceExpressionSourceRefresh
    {
        private static readonly System.Collections.Generic.HashSet<string> PendingPaths =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        private static bool _queued;

        static FaceExpressionSourceRefresh()
        {
            AnimationUtility.onCurveWasModified += (clip, _, __) => Queue(AssetDatabase.GetAssetPath(clip));
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                PendingPaths.Clear();
                // Also repairs saved generated assets from a previous Editor session.
                RefreshLoaded(null);
            };
            EditorApplication.update += RefreshAfterReload;
        }

        private static void RefreshAfterReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= RefreshAfterReload;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) RefreshLoaded(null);
        }

        internal static void Queue(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            PendingPaths.Add(path);
            if (_queued) return;
            _queued = true;
            EditorApplication.update += Flush;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Flush()
        {
            EditorApplication.update -= Flush;
            _queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (PendingPaths.Count > 0) Queue(PendingPaths.First());
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var paths = new System.Collections.Generic.HashSet<string>(PendingPaths, StringComparer.Ordinal);
            PendingPaths.Clear();
            if (paths.Count > 0) RefreshLoaded(paths);
        }

        private static void RefreshLoaded(System.Collections.Generic.ISet<string> changedPaths)
        {
            if (EditorApplication.isPlaying) return;
            var gateway = new VrchatFaceExpressionGateway();
            foreach (var animator in Resources.FindObjectsOfTypeAll<Animator>())
            {
                if (animator == null || EditorUtility.IsPersistent(animator) ||
                    (animator.hideFlags & HideFlags.DontSaveInEditor) != 0 ||
                    !animator.gameObject.scene.IsValid() ||
                    Ee4v.Core.AvatarEvaluation.AvatarPreviewRenderer.IsPreviewScene(animator.gameObject.scene)) continue;
                var avatar = animator.gameObject;
                var paths = FaceExpressionGenerationPaths.Create(avatar);
                var controller = ModularAvatarFaceExpressionInstaller.TryGetController(avatar, paths.RootName);
                if (!GestureMatrixControllerWriter.OwnsLayer(controller) ||
                    !gateway.TryRead(avatar, out var configuration)) continue;
                var sourceClips = configuration.Assignments.Values.Select(assignment => assignment.Clip)
                    .Concat(configuration.MenuEntries.Select(entry => entry.Assignment.Clip)).Where(clip => clip != null);
                if (changedPaths != null && !sourceClips.Any(clip => changedPaths.Contains(AssetDatabase.GetAssetPath(clip))))
                    continue;
                using (new FaceExpressionGenerationUndo())
                {
                    if (!gateway.TryApply(avatar, configuration, out _, out var error, saveScene: true))
                        Debug.LogError("FacialSet source refresh failed: " + error, avatar);
                }
            }
        }
    }

    internal sealed class FaceExpressionSourcePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (var path in imported.Concat(moved))
                if (path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) FaceExpressionSourceRefresh.Queue(path);
        }
    }

    internal sealed class FaceExpressionSourceSaveProcessor : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
                if (path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) FaceExpressionSourceRefresh.Queue(path);
            return paths;
        }
    }

    internal sealed class VrchatFaceExpressionGateway
    {
        internal static void RemoveGeneratedInstallation(GameObject avatar)
        {
            if (avatar == null) { return; }
            var rootName = FaceExpressionGenerationPaths.Create(avatar).RootName;
            if (!EditorUtility.IsPersistent(avatar))
            {
                var root = avatar.transform.Find(rootName);
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                    if (avatar.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(avatar.scene);
                        EditorSceneApi.SaveAfterEdit(avatar.scene);
                    }
                }
                return;
            }
            var path = AssetDatabase.GetAssetPath(avatar);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var root = contents.transform.Find(rootName);
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public bool TryRead(
            GameObject avatar,
            out FaceExpressionConfiguration configuration)
        {
            configuration = new FaceExpressionConfiguration(null, null);
            if (!VrchatAvatarDescriptorAdapter.TryGet(avatar, out _, out var avatarFx))
            {
                return false;
            }

            var paths = FaceExpressionGenerationPaths.Create(avatar);
            var controller = ModularAvatarFaceExpressionInstaller.TryGetController(
                                 avatar,
                                 paths.RootName) ??
                             avatarFx as AnimatorController;
            if (!GestureMatrixControllerWriter.OwnsLayer(controller))
            {
                return true;
            }

            configuration = new FaceExpressionConfiguration(
                GestureMatrixControllerWriter.Read(controller),
                GestureMatrixControllerWriter.ReadMenuEntries(controller));
            return true;
        }

        public bool TryApply(
            GameObject avatar,
            FaceExpressionConfiguration configuration,
            out AnimatorController controller,
            out string error,
            bool deferSave = false,
            bool generateIcons = true,
            bool saveScene = false)
        {
            controller = null;
            error = null;
            if (!VrchatAvatarDescriptorAdapter.TryGet(avatar, out var descriptor, out _))
            {
                error = "descriptorMissing";
                return false;
            }

            if (!ModularAvatarFaceExpressionInstaller.IsAvailable ||
                !VrchatExpressionMenuWriter.IsAvailable)
            {
                error = "modularAvatarMissing";
                return false;
            }

            try
            {
                var paths = FaceExpressionGenerationPaths.Create(
                    avatar,
                    ensureFolders: true);
                controller = GetOrCreateController(paths.ControllerPath);
                FaceExpressionSettings.EnsureNamePreset(
                    avatar,
                    null,
                    BlendShapePresetStorage.Shared);
                var namingRule = FaceExpressionSettings.GetNameRule(
                    BlendShapePresetStorage.Shared);
                var avatarBindings = VrchatAvatarDescriptorAdapter.ReadBindings(
                    descriptor,
                    avatar,
                    FaceExpressionSettings.GetSeparators(),
                    namingRule);
                GestureMatrixControllerWriter.Apply(
                    controller,
                    avatar,
                    configuration?.Assignments,
                    configuration?.MenuEntries,
                    avatarBindings,
                    paths.AssetsFolder,
                    saveAssets: !deferSave);

                var entries = GestureMatrixControllerWriter.GetEffectiveMenuEntries(
                    configuration?.Assignments,
                    configuration?.MenuEntries);
                var icons = FaceExpressionMenuIconWriter.Write(avatar, entries, paths, generateIcons);
                var menu = VrchatExpressionMenuWriter.Write(
                    paths.MenuPath,
                    entries,
                    icons);
                var installationChanged = ModularAvatarFaceExpressionInstaller.Install(
                    avatar,
                    paths,
                    controller,
                    menu,
                    avatarBindings.Blink.Count > 0);
                SaveMenu(menu, deferSave);
                if (installationChanged && !EditorUtility.IsPersistent(avatar) && avatar.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(avatar.scene);
                    if (saveScene) EditorSceneApi.SaveAfterEdit(avatar.scene);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = "applyFailed";
                return false;
            }
        }

        internal bool RefreshMenuIcons(GameObject avatar, FaceExpressionConfiguration configuration, bool complete = false,
            FaceExpressionPreview sharedPreview = null)
        {
            var paths = FaceExpressionGenerationPaths.Create(avatar);
            var entries = GestureMatrixControllerWriter.GetEffectiveMenuEntries(configuration.Assignments, configuration.MenuEntries);
            var icons = FaceExpressionMenuIconWriter.Write(avatar, entries, paths, true,
                complete ? int.MaxValue : 1, out var pending, sharedPreview);
            SaveMenu(VrchatExpressionMenuWriter.Write(paths.MenuPath, entries, icons), true);
            return pending;
        }

        private static void SaveMenu(ScriptableObject menu, bool deferSave)
        {
            if (deferSave) DeferredAssetSave.Queue(menu);
            else
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(menu)))
                    AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static AnimatorController GetOrCreateController(string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (existing != null)
            {
                if (!AssetDatabase.IsOpenForEdit(existing))
                {
                    throw new InvalidOperationException(
                        "The generated Animator Controller is not editable.");
                }

                return existing;
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                throw new InvalidOperationException(
                    "Another asset already uses the generated Animator Controller path.");
            }

            var controller = new AnimatorController
            {
                name = System.IO.Path.GetFileNameWithoutExtension(assetPath)
            };
            AssetDatabase.CreateAsset(controller, assetPath);
            FaceExpressionGenerationUndo.RegisterCreatedObjectUndo(
                controller,
                "Create Face Expression Controller");
            return controller;
        }
    }

    // Automatic generation is derived from the session's undoable configuration.
    // Rebuilding assets during Undo/Redo must not add Undo entries or discard Redo.
    internal sealed class FaceExpressionGenerationUndo : IDisposable
    {
        private readonly bool _previous;
        internal static bool Enabled { get; private set; } = true;
        internal FaceExpressionGenerationUndo()
        {
            _previous = Enabled;
            Enabled = false;
        }
        public void Dispose() { Enabled = _previous; }
        internal static void RegisterCreatedObjectUndo(UnityEngine.Object target, string label)
        {
            if (Enabled) { Undo.RegisterCreatedObjectUndo(target, label); }
        }
        internal static void RegisterCompleteObjectUndo(UnityEngine.Object target, string label)
        {
            if (Enabled) { Undo.RegisterCompleteObjectUndo(target, label); }
        }
        internal static void RecordObject(UnityEngine.Object target, string label)
        {
            if (Enabled) { Undo.RecordObject(target, label); }
        }
        internal static void DestroyObjectImmediate(UnityEngine.Object target)
        {
            if (Enabled) { Undo.DestroyObjectImmediate(target); }
            else { UnityEngine.Object.DestroyImmediate(target, true); }
        }
        internal static AnimatorState AddState(AnimatorStateMachine machine, string name)
        {
            if (Enabled) { return machine.AddState(name); }
            var state = new AnimatorState { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(state, machine);
            machine.states = machine.states.Concat(new[] { new ChildAnimatorState
                { state = state, position = new Vector3(200f, machine.states.Length * 70f, 0f) } }).ToArray();
            return state;
        }
        internal static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine machine, AnimatorState state)
        {
            if (Enabled) { return machine.AddAnyStateTransition(state); }
            var transition = new AnimatorStateTransition { destinationState = state, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(transition, machine);
            machine.anyStateTransitions = machine.anyStateTransitions.Concat(new[] { transition }).ToArray();
            return transition;
        }
        internal static AnimatorStateTransition AddTransition(AnimatorState state, AnimatorState destination)
        {
            if (Enabled) { return state.AddTransition(destination); }
            var transition = new AnimatorStateTransition { destinationState = destination, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(transition, state);
            state.transitions = state.transitions.Concat(new[] { transition }).ToArray();
            return transition;
        }
        internal static void AddCondition(AnimatorStateTransition transition, AnimatorConditionMode mode, float threshold, string parameter)
        {
            if (Enabled) { transition.AddCondition(mode, threshold, parameter); return; }
            transition.conditions = transition.conditions.Concat(new[] { new AnimatorCondition
            { mode = mode, threshold = threshold, parameter = parameter } }).ToArray();
        }
        internal static void AddLayer(AnimatorController controller, AnimatorControllerLayer layer)
        {
            if (Enabled) { controller.AddLayer(layer); }
            else { controller.layers = controller.layers.Concat(new[] { layer }).ToArray(); }
        }
        internal static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (Enabled) { controller.AddParameter(name, type); }
            else { controller.parameters = controller.parameters.Concat(new[] { new AnimatorControllerParameter
            { name = name, type = type } }).ToArray(); }
        }
        internal static StateMachineBehaviour AddBehaviour(AnimatorState state, Type type)
        {
            if (Enabled) { return state.AddStateMachineBehaviour(type); }
            var behaviour = (StateMachineBehaviour)ScriptableObject.CreateInstance(type);
            behaviour.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(behaviour, state);
            state.behaviours = state.behaviours.Concat(new[] { behaviour }).ToArray();
            return behaviour;
        }
        internal static void RemoveLayer(AnimatorController controller, int index)
        {
            if (Enabled) { controller.RemoveLayer(index); return; }
            var machine = controller.layers[index].stateMachine;
            controller.layers = controller.layers.Where((_, i) => i != index).ToArray();
            DestroyGraph(machine);
        }
        private static void DestroyGraph(AnimatorStateMachine machine)
        {
            if (machine == null) { return; }
            foreach (var child in machine.stateMachines) { DestroyGraph(child.stateMachine); }
            foreach (var child in machine.states)
            {
                foreach (var transition in child.state.transitions) { DestroyObjectImmediate(transition); }
                foreach (var behaviour in child.state.behaviours) { DestroyObjectImmediate(behaviour); }
                DestroyObjectImmediate(child.state);
            }
            foreach (var transition in machine.anyStateTransitions) { DestroyObjectImmediate(transition); }
            foreach (var transition in machine.entryTransitions) { DestroyObjectImmediate(transition); }
            foreach (var behaviour in machine.behaviours) { DestroyObjectImmediate(behaviour); }
            DestroyObjectImmediate(machine);
        }
    }
}
