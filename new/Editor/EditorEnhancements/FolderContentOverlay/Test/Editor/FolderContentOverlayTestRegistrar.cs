using Ee4v.Testing.Contracts;

namespace Ee4v.FolderContentOverlay.Tests
{
    public sealed class FolderContentOverlayTestRegistrar
        : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "FolderContentOverlay",
                "FolderContentOverlay",
                "Ee4v.FolderContentOverlay.Tests.Editor",
                "Project folderの内容オーバーレイを確認します。",
                order: 310);
        }
    }
}
