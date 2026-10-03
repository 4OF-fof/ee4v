using Ee4v.Core.I18n;

namespace Ee4v.UI
{
    internal static class UiLocalization
    {
        internal static string Get(string key)
        {
            return CoreLocalization.Current.ForScope("UI").Get(key);
        }
    }
}
