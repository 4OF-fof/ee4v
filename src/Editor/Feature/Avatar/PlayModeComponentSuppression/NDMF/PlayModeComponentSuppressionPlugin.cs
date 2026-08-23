#if EE4V_NDMF
using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Ee4v.PlayModeComponentSuppression.Ndmf.PlayModeComponentSuppressionPlugin))]

namespace Ee4v.PlayModeComponentSuppression.Ndmf
{
    public sealed class PlayModeComponentSuppressionPlugin :
        Plugin<PlayModeComponentSuppressionPlugin>
    {
        public override string QualifiedName =>
            "dev.4of.ee4v.play-mode-component-suppression";

        public override string DisplayName =>
            "ee4v Play Mode Component Suppression";

        protected override void Configure()
        {
            InPhase(BuildPhase.FirstChance).Run(
                "Suppress configured components in Play Mode",
                context =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        return;
                    }

                    var suppressedTypes = new HashSet<Type>(
                        PlayModeComponentSuppressionSettings
                            .GetSuppressedTypes());
                    if (suppressedTypes.Count == 0)
                    {
                        return;
                    }

                    var components = context.AvatarRootObject
                        .GetComponentsInChildren<Component>(true);
                    foreach (var component in components)
                    {
                        if (component != null &&
                            suppressedTypes.Contains(component.GetType()))
                        {
                            UnityEngine.Object.DestroyImmediate(component);
                        }
                    }
                });
        }
    }
}
#endif
