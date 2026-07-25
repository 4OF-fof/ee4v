namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectRestoreState
    {
        public HiddenObjectRestoreState(
            string objectId,
            bool activeSelf,
            string tag)
        {
            ObjectId = objectId ?? string.Empty;
            ActiveSelf = activeSelf;
            Tag = tag ?? string.Empty;
        }

        public string ObjectId { get; }

        public bool ActiveSelf { get; }

        public string Tag { get; }
    }

    internal interface IHiddenObjectRestoreStateStore
    {
        HiddenObjectRestoreState Get(string objectId);

        void Put(HiddenObjectRestoreState state);

        void Save();
    }
}
