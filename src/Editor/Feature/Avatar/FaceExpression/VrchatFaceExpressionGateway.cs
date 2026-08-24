using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class VrchatFaceExpressionGateway
    {
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
            Undo.RegisterCreatedObjectUndo(
                controller,
                "Create Face Expression Controller");
            return controller;
        }
    }
}
