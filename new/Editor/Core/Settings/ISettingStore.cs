using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    internal interface ISettingStore
    {
        Dictionary<string, string> LoadAll();

        void SaveAll(Dictionary<string, string> values);
    }
}
