using System.Collections.Generic;

namespace Ee4v.HiddenObjects
{
    internal interface IHiddenObjectRepository
    {
        IReadOnlyList<HiddenObjectSnapshotItem> Load();

        int Reveal(
            IReadOnlyCollection<int> instanceIds,
            string undoOperationName);
    }

    internal interface IHiddenObjectNavigator
    {
        void Focus(int instanceId);
    }
}
