using System.Reflection;
using SQLitePCL;
using UnityEditor;

namespace Ee4v.SQLite
{
    [InitializeOnLoad]
    internal static class SqliteBootstrap
    {
        private static bool _initialized;

        static SqliteBootstrap()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

#if UNITY_EDITOR_WIN
            var providerField = typeof(raw).GetField("_imp", BindingFlags.Static | BindingFlags.NonPublic);
            var currentProvider = providerField?.GetValue(null);
            if (currentProvider == null)
            {
                raw.SetProvider(new SQLite3Provider_e_sqlite3());
            }
#endif

            _initialized = true;
        }
    }
}
