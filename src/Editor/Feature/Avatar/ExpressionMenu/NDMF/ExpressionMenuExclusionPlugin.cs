#if EE4V_NDMF
using Ee4v.Core.AvatarEvaluation;
using nadena.dev.ndmf;

[assembly: ExportsPlugin(typeof(Ee4v.ExpressionMenu.Ndmf.ExpressionMenuExclusionPlugin))]

namespace Ee4v.ExpressionMenu.Ndmf
{
    public sealed class ExpressionMenuExclusionPlugin : Plugin<ExpressionMenuExclusionPlugin>
    {
        public override string QualifiedName => "dev.4of.ee4v.expression-menu.exclusions";
        public override string DisplayName => "ee4v Expression Menu Exclusions";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).BeforePlugin("nadena.dev.modular-avatar")
                .Run("Track excluded menu entries", context =>
                    context.GetState<AvatarMenuExclusionUtility.BuildFilter>().Prepare(context.AvatarRootObject,
                        asset => context.AssetSaver.SaveAsset(asset)));
            InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar")
                .BeforePlugin("nadena.dev.modular-avatar.late-transform-stages")
                .Run("Remove excluded menu entries", context =>
                    context.GetState<AvatarMenuExclusionUtility.BuildFilter>().Apply(context.AvatarRootObject));
        }
    }
}
#endif
