using System;
using System.Collections.Generic;
using Ee4v.Core.Injector;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class InjectionRegistryTests
    {
        [Test]
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
