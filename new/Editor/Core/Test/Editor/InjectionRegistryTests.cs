using System;
using System.Collections.Generic;
using Ee4v.Core.Injector;
using Ee4v.Core.Testing;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class InjectionRegistryTests
    {
        [Test]
        [FeatureTestCase(
            "Injector登録を優先順位順に保持する",
            "Unity非依存registryがpriorityとidで安定順序を作ることを確認します。",
            order: 32,
            category: FeatureTestCategory.Standard)]
        public void GetRegistrations_ReturnsStablePriorityOrder()
        {
            var registry = new InjectionRegistry();
            registry.Register(new FakeRegistration(
                "z",
                InjectionChannel.ProjectItem,
                10));
            registry.Register(new FakeRegistration(
                "b",
                InjectionChannel.ProjectItem,
                0));
            registry.Register(new FakeRegistration(
                "a",
                InjectionChannel.ProjectItem,
                0));

            var registrations = registry.GetRegistrations(
                InjectionChannel.ProjectItem);

            Assert.That(
                new[]
                {
                    registrations[0].Id,
                    registrations[1].Id,
                    registrations[2].Id
                },
                Is.EqualTo(new[] { "a", "b", "z" }));
        }

        [Test]
        [FeatureTestCase(
            "同一Injector登録を置換する",
            "idとchannelが同じ登録は重複せず、新しいinstanceだけが解除可能であることを確認します。",
            order: 33,
            category: FeatureTestCategory.Standard)]
        public void Register_ReplacesSameIdentity()
        {
            var registry = new InjectionRegistry();
            var original = new FakeRegistration(
                "same",
                InjectionChannel.HierarchyItem,
                0);
            var replacement = new FakeRegistration(
                "same",
                InjectionChannel.HierarchyItem,
                20);

            registry.Register(original);
            registry.Register(replacement);

            Assert.That(registry.Unregister(original), Is.False);
            Assert.That(registry.Unregister(replacement), Is.True);
            Assert.That(
                registry.GetRegistrations(
                    InjectionChannel.HierarchyItem),
                Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "Injector変更channelを通知する",
            "登録と解除が影響対象channelだけをpresentationへ通知することを確認します。",
            order: 34,
            category: FeatureTestCategory.Standard)]
        public void RegistryChanges_ReportAffectedChannel()
        {
            var registry = new InjectionRegistry();
            var channels = new List<InjectionChannel>();
            registry.Changed += (_, args) => channels.Add(args.Channel);
            var registration = new FakeRegistration(
                "toolbar",
                InjectionChannel.ProjectToolbar,
                0);

            registry.Register(registration);
            registry.Unregister(registration);

            Assert.That(
                channels,
                Is.EqualTo(new[]
                {
                    InjectionChannel.ProjectToolbar,
                    InjectionChannel.ProjectToolbar
                }));
        }

        private sealed class FakeRegistration : IInjectionRegistration
        {
            public FakeRegistration(
                string id,
                InjectionChannel channel,
                int priority)
            {
                Id = id;
                Channel = channel;
                Priority = priority;
            }

            public string Id { get; }

            public InjectionChannel Channel { get; }

            public int Priority { get; }

            public bool IsEnabled()
            {
                return true;
            }
        }
    }
}
