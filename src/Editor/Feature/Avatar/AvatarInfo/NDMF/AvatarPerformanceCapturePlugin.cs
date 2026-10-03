using nadena.dev.ndmf;

[assembly: ExportsPlugin(typeof(Ee4v.AvatarInfo.AvatarPerformanceCapturePlugin))]

namespace Ee4v.AvatarInfo
{
    public sealed class AvatarPerformanceCapturePlugin : Plugin<AvatarPerformanceCapturePlugin>
    {
        public override string QualifiedName => "dev.4of.ee4v.avatar-performance-capture";
        public override string DisplayName => "ee4v Avatar Performance Capture";

        private sealed class CaptureState { internal AvatarPlayModePerformanceCache.Record Source; }

        protected override void Configure()
        {
            InPhase(BuildPhase.FirstChance).Run("Remember play mode avatar", context =>
                context.GetState<CaptureState>().Source = AvatarPlayModePerformanceCache.FindBuildSource(context.AvatarRootObject));
            InPhase(BuildPhase.Optimizing).AfterPlugin("com.anatawa12.avatar-optimizer")
                .Run("Remember play mode build", context =>
                    AvatarPlayModePerformanceCache.RememberBuild(context.GetState<CaptureState>().Source,
                        () => context.AvatarRootObject, () => context.Successful));
        }
    }
}
