using Ee4v.AssetManager.Contracts;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.Application.Tests
{
    public sealed class AssetItemContextActionRegistryTests
    {
        [Test]
        [FeatureTestCase(
            "Context actionの登録寿命を管理する",
            "登録中だけitem actionを公開し、registration破棄後は公開しないことを確認します。",
            order: 5)]
        public void Registration_ExposesActionUntilDisposed()
        {
            var registry = new AssetItemContextActionRegistry();
            var registration = registry.Register(
                new Provider());
            var request = new AssetItemContextActionRequest(
                "item-1",
                10f,
                20f);

            Assert.That(
                registry.CreateActions(request),
                Has.Count.EqualTo(1));

            registration.Dispose();

            Assert.That(
                registry.CreateActions(request),
                Is.Empty);
        }

        private sealed class Provider :
            IAssetItemContextActionProvider
        {
            public bool TryCreate(
                AssetItemContextActionRequest request,
                out AssetItemContextAction action)
            {
                action = new AssetItemContextAction(
                    "create-variant",
                    "Create Variant",
                    () => { });
                return true;
            }
        }
    }
}
