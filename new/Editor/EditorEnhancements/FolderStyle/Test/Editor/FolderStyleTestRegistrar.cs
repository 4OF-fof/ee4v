using Ee4v.Testing.Contracts;

namespace Ee4v.FolderStyle.Tests
{
    public sealed class FolderStyleTestRegistrar
        : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "FolderStyle",
                "FolderStyle",
                "Ee4v.FolderStyle.Tests.Editor",
                "Project folderの色とアイコン装飾を確認します。",
                order: 320);
        }
    }
}
