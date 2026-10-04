namespace Ee4v.Core.Background
{
    public static class CoreBackgroundActivities
    {
        private static readonly IBackgroundTaskManager _current =
            new BackgroundActivityTracker();

        public static IBackgroundTaskManager Current => _current;

    }
}
