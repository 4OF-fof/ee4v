using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class VrchatFaceExpressionGateway
    {
        internal static void RemoveGeneratedInstallation(GameObject avatar)
        {
            if (avatar == null) { return; }
            var rootName = FaceExpressionGenerationPaths.Create(avatar).RootName;
            if (!EditorUtility.IsPersistent(avatar))
            {
                var root = avatar.transform.Find(rootName);
                if (root != null) { UnityEngine.Object.DestroyImmediate(root.gameObject); }
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
            out string error)
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
                    paths.AssetsFolder);

                var entries = GestureMatrixControllerWriter.GetEffectiveMenuEntries(
                    configuration?.Assignments,
                    configuration?.MenuEntries);
                var icons = FaceExpressionMenuIconWriter.Write(avatar, entries, paths);
                var menu = VrchatExpressionMenuWriter.Write(
                    paths.MenuPath,
                    entries,
                    icons);
                ModularAvatarFaceExpressionInstaller.Install(
                    avatar,
                    paths,
                    controller,
                    menu,
                    avatarBindings.Blink.Count > 0);
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = "applyFailed";
                return false;
            }
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
