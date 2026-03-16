using System.Collections.Generic;
using Ee4v.Core.Settings;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class CoreStoreTests
    {
        [SetUp]
        public void SetUp()
        {
            Ee4vCoreTestReset.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            Ee4vCoreTestReset.ResetAll();
            Ee4vCoreTestReset.RecoverEditorState();
        }

        [Test]
        public void ProjectFileSettingStore_LoadAll_ReturnsEmpty_WhenFileDoesNotExist()
        {
            var fileSystem = new FakeFileSystem();
            var store = new ProjectFileSettingStore("ProjectSettings/ee4v.settings.json", fileSystem);

            var values = store.LoadAll();

            Assert.That(values, Is.Empty);
        }

        [Test]
        public void ProjectFileSettingStore_SaveAll_CreatesDirectory_AndPersistsJson()
        {
            var fileSystem = new FakeFileSystem();
            var store = new ProjectFileSettingStore("ProjectSettings/ee4v.settings.json", fileSystem);
            var expected = new Dictionary<string, string>
            {
                { "core.sample", "\"value\"" }
            };

            store.SaveAll(expected);
            var loaded = store.LoadAll();

            Assert.That(fileSystem.CreatedDirectories, Contains.Item("ProjectSettings"));
            Assert.That(fileSystem.WrittenFiles.ContainsKey("ProjectSettings/ee4v.settings.json"), Is.True);
            Assert.That(loaded, Is.EquivalentTo(expected));
        }

        [Test]
        public void EditorPrefsSettingStore_LoadAll_ReturnsEmpty_WhenKeyDoesNotExist()
        {
            var editorPrefs = new FakeEditorPrefsFacade();
            var store = new EditorPrefsSettingStore("ee4v.tests.user", editorPrefs);

            var values = store.LoadAll();

            Assert.That(values, Is.Empty);
        }

        [Test]
        public void EditorPrefsSettingStore_SaveAll_RoundTripsValues()
        {
            var editorPrefs = new FakeEditorPrefsFacade();
            var store = new EditorPrefsSettingStore("ee4v.tests.user", editorPrefs);
            var expected = new Dictionary<string, string>
            {
                { "core.sample", "\"value\"" }
            };

            store.SaveAll(expected);
            var loaded = store.LoadAll();

            Assert.That(editorPrefs.Values.ContainsKey("ee4v.tests.user"), Is.True);
            Assert.That(loaded, Is.EquivalentTo(expected));
        }

        private sealed class FakeFileSystem : IFileSystem
        {
            public Dictionary<string, string> WrittenFiles { get; } = new Dictionary<string, string>();

            public List<string> CreatedDirectories { get; } = new List<string>();

            public bool FileExists(string path)
            {
                return WrittenFiles.ContainsKey(path);
            }

            public string ReadAllText(string path)
            {
                return WrittenFiles[path];
            }

            public void WriteAllText(string path, string content)
            {
                WrittenFiles[path] = content;
            }

            public bool DirectoryExists(string path)
            {
                return CreatedDirectories.Contains(path);
            }

            public void CreateDirectory(string path)
            {
                if (!CreatedDirectories.Contains(path))
                {
                    CreatedDirectories.Add(path);
                }
            }
        }

        private sealed class FakeEditorPrefsFacade : IEditorPrefsFacade
        {
            public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

            public bool HasKey(string key)
            {
                return Values.ContainsKey(key);
            }

            public string GetString(string key, string defaultValue)
            {
                return Values.TryGetValue(key, out var value) ? value : defaultValue;
            }

            public void SetString(string key, string value)
            {
                Values[key] = value;
            }
        }
    }
}
