using System;
using System.Collections.Generic;
using Ee4v.Core.Settings;
using Ee4v.Core.Testing;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class SettingsServiceTests
    {
        [Test]
        [FeatureTestCase(
            "設定serviceはinstanceごとに状態を分離する",
            "異なるSettingsService instanceが登録定義・cache・変更通知を共有しないことを確認します。",
            order: 2)]
        public void Instances_DoNotShareState()
        {
            var first = CreateService();
            var second = CreateService();
            var definition = CreateDefinition();

            first.Register(definition);
            first.Set(definition, 7, saveImmediately: false);
            second.Register(definition);

            Assert.That(first.Get(definition), Is.EqualTo(7));
            Assert.That(second.Get(definition), Is.EqualTo(3));
        }

        [Test]
        public void InvalidPersistedValue_FallsBackToDefault()
        {
            var userStore = new MemoryStore();
            userStore.Values["core.test.count"] = "-1";
            var service = CreateService(userStore);
            var definition = CreateDefinition();

            service.Register(definition);

            Assert.That(service.Get(definition), Is.EqualTo(3));
        }

        [Test]
        public void Changed_IsRaisedAfterSuccessfulUpdate()
        {
            var service = CreateService();
            var definition = CreateDefinition();
            SettingChangedEventArgs received = null;
            service.Changed += (_, args) => received = args;
            service.Register(definition);

            service.Set(definition, 5, saveImmediately: false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Definition, Is.SameAs(definition));
            Assert.That(received.Value, Is.EqualTo(5));
        }

        private static SettingDefinition<int> CreateDefinition()
        {
            return new SettingDefinition<int>(
                "core.test.count",
                SettingScope.User,
                "Core",
                "settings.section.localization",
                "settings.language.label",
                "settings.language.tooltip",
                3,
                validator: value => value >= 0
                    ? SettingValidationResult.Success
                    : SettingValidationResult.Error("invalid"));
        }

        private static SettingsService CreateService(MemoryStore userStore = null)
        {
            return new SettingsService(
                new Dictionary<SettingScope, ISettingStore>
                {
                    { SettingScope.User, userStore ?? new MemoryStore() },
                    { SettingScope.Project, new MemoryStore() }
                },
                new IntegerSerializer());
        }

        private sealed class MemoryStore : ISettingStore
        {
            public Dictionary<string, string> Values { get; } =
                new Dictionary<string, string>();

            public Dictionary<string, string> LoadAll()
            {
                return new Dictionary<string, string>(Values);
            }

            public void SaveAll(Dictionary<string, string> values)
            {
                Values.Clear();
                foreach (var pair in values)
                {
                    Values[pair.Key] = pair.Value;
                }
            }
        }

        private sealed class IntegerSerializer : ISettingValueSerializer
        {
            public string Serialize(Type valueType, object value)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            public bool TryDeserialize(Type valueType, string serializedValue, out object value)
            {
                if (valueType == typeof(int) &&
                    int.TryParse(serializedValue, out var integer))
                {
                    value = integer;
                    return true;
                }

                value = null;
                return false;
            }
        }
    }
}
