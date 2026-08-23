#if EE4V_NDMF && EE4V_VRCSDK
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(Ee4v.FaceExpression.Ndmf.FaceExpressionBlinkOverridePlugin))]

namespace Ee4v.FaceExpression.Ndmf
{
    public sealed class FaceExpressionBlinkOverridePlugin :
        Plugin<FaceExpressionBlinkOverridePlugin>
    {
        public override string QualifiedName =>
            "dev.4of.ee4v.face-expression.blink-override";

        public override string DisplayName =>
            "ee4v Face Expression Blink Override";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run(
                "Disable VRChat eyelid animation",
                context =>
                {
                    var markers = context.AvatarRootObject
                        .GetComponentsInChildren<FaceExpressionBlinkOverride>(true);
                    if (markers.Length == 0)
                    {
                        return;
                    }

                    context.AvatarDescriptor.customEyeLookSettings.eyelidType =
                        VRCAvatarDescriptor.EyelidType.None;
                    foreach (var marker in markers)
                    {
                        Object.DestroyImmediate(marker);
                    }
                });
        }
    }
}
#endif
