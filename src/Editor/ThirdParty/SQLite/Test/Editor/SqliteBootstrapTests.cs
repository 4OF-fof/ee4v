using System;
using System.Reflection;
using NUnit.Framework;
#if UNITY_EDITOR_WIN
using SQLite;
#endif

namespace Ee4v.SQLite.Tests
{
    public sealed class SqliteBootstrapTests
    {
        [SetUp]
        public void SetUp()
        {
            ReflectionReset.SetStaticField(typeof(SqliteBootstrap), "_initialized", false);
        }

        [Test]
        public void SqliteBootstrap_EnsureInitialized_IsIdempotent()
        {
            SqliteBootstrap.EnsureInitialized();
            SqliteBootstrap.EnsureInitialized();

            Assert.That((bool)ReflectionReset.GetStaticField(typeof(SqliteBootstrap), "_initialized"), Is.True);
        }

#if UNITY_EDITOR_WIN
        [Test]
        public void SqliteBootstrap_Provider_AllowsInMemoryRoundTrip()
        {
            SqliteBootstrap.EnsureInitialized();

            using (var connection = new SQLiteConnection(
                       ":memory:",
                       SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex))
            {
                connection.Execute("create table smoke_items (id integer primary key, name text not null)");
                connection.Execute("insert into smoke_items (name) values (?)", "sqlite");

                var count = connection.ExecuteScalar<int>("select count(*) from smoke_items");
                var name = connection.ExecuteScalar<string>("select name from smoke_items limit 1");

                Assert.That(count, Is.EqualTo(1));
                Assert.That(name, Is.EqualTo("sqlite"));
            }
        }
#else
        [Test]
        public void SqliteBootstrap_NonWindows_Initialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(SqliteBootstrap.EnsureInitialized);
        }
#endif
    }

    internal static class ReflectionReset
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        public static object GetStaticField(Type type, string fieldName)
        {
            return GetField(type, fieldName).GetValue(null);
        }

        public static void SetStaticField(Type type, string fieldName, object value)
        {
            GetField(type, fieldName).SetValue(null, value);
        }

        private static FieldInfo GetField(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, Flags);
            if (field == null)
            {
                throw new InvalidOperationException(
                    "Field '" + fieldName + "' was not found on '" + type.FullName + "'.");
            }

            return field;
        }
    }
}
