using System.Collections.Generic;

namespace Ee4v.SceneSwitcher
{
    internal interface ISceneSwitcherRepository
    {
        IReadOnlyList<SceneSwitcherRecord> Load();

        void Save(IReadOnlyList<SceneSwitcherRecord> records);
    }
}
