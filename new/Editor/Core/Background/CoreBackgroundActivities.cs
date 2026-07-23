namespace Ee4v.Core.Background
{
    public static class CoreBackgroundActivities
    {
        private static IBackgroundActivityTracker _current =
            new BackgroundActivityTracker();

        public static IBackgroundActivityTracker Current => _current;

        internal static void ResetForTests()
        {
            _current = new BackgroundActivityTracker();
        }
    }
}
