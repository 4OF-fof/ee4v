using Ee4v.Core.Background;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class BackgroundActivityTests
    {
        [Test]
        public void BeginAndDispose_TracksLatestActiveOperation()
        {
            var tracker = new BackgroundActivityTracker();
            var first = tracker.Begin("first");
            var second = tracker.Begin("second");

            var active = tracker.GetState();
            Assert.That(active.IsActive, Is.True);
            Assert.That(active.ActivityCount, Is.EqualTo(2));
            Assert.That(active.Message, Is.EqualTo("second"));

            second.Dispose();
            active = tracker.GetState();
            Assert.That(active.ActivityCount, Is.EqualTo(1));
            Assert.That(active.Message, Is.EqualTo("first"));

            first.Dispose();
            Assert.That(tracker.GetState().IsActive, Is.False);
        }
    }
}
