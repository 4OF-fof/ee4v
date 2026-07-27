using System;

namespace Ee4v.Core.Settings
{
    public interface ISettingValueSerializer
    {
        string Serialize(Type valueType, object value);

        bool TryDeserialize(Type valueType, string serializedValue, out object value);
    }
}
