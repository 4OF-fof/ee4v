using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    public interface ISettingStore
    {
        Dictionary<string, string> LoadAll();

        void SaveAll(Dictionary<string, string> values);
    }
}
