using NUnit.Framework;
#if UNITY_EDITOR_WIN
using SQLite;
#endif

namespace Ee4v.SQLite.Tests
{
    public sealed class SqliteBootstrapTests
    {
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

}
