namespace Ee4v.Core.Background
{
    public static class CoreBackgroundActivities
    {
        private static IBackgroundTaskManager _current =
            new BackgroundActivityTracker();

        public static IBackgroundTaskManager Current => _current;

        internal static void ResetForTests()
        {
            _current = new BackgroundActivityTracker();
        }
    }
}
