using Ee4v.Core.Background;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class BackgroundActivityTests
    {
        [SetUp]
        public void SetUp()
        {
            BackgroundActivityApi.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            BackgroundActivityApi.Reset();
        }

        [Test]
        public void BeginAndDispose_TracksLatestActiveOperation()
        {
            var first = BackgroundActivityApi.Begin("first");
            var second = BackgroundActivityApi.Begin("second");

            var active = BackgroundActivityApi.GetState();
            Assert.That(active.IsActive, Is.True);
            Assert.That(active.ActivityCount, Is.EqualTo(2));
            Assert.That(active.Message, Is.EqualTo("second"));

            second.Dispose();
            active = BackgroundActivityApi.GetState();
            Assert.That(active.ActivityCount, Is.EqualTo(1));
            Assert.That(active.Message, Is.EqualTo("first"));

            first.Dispose();
            Assert.That(BackgroundActivityApi.GetState().IsActive, Is.False);
        }
    }
}
