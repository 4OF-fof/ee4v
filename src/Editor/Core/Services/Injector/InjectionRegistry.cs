using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.Core.Injector
{
    public sealed class InjectionRegistry : IInjectionRegistry
    {
        private readonly List<IInjectionRegistration> _registrations =
            new List<IInjectionRegistration>();

        public event EventHandler<InjectionRegistryChangedEventArgs> Changed;

        public void Register(IInjectionRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            if (string.IsNullOrWhiteSpace(registration.Id))
            {
                throw new ArgumentException(
                    "Registration id must not be empty.",
                    nameof(registration));
            }

            var index = _registrations.FindIndex(
                existing =>
                    existing.Id == registration.Id &&
                    existing.Channel == registration.Channel);
            if (index >= 0)
            {
                _registrations[index] = registration;
            }
            else
            {
                _registrations.Add(registration);
            }

            _registrations.Sort(CompareRegistrations);
            OnChanged(registration.Channel);
        }

        public bool Unregister(IInjectionRegistration registration)
        {
            if (registration == null)
            {
                return false;
            }

            var index = _registrations.FindIndex(
                existing => ReferenceEquals(existing, registration));
            if (index < 0)
            {
                return false;
            }

            _registrations.RemoveAt(index);
            OnChanged(registration.Channel);
            return true;
        }

        public IReadOnlyList<IInjectionRegistration> GetRegistrations(
            InjectionChannel channel)
        {
            return _registrations
                .Where(registration => registration.Channel == channel)
                .ToArray();
        }

        public void Clear()
        {
            var channels = _registrations
                .Select(registration => registration.Channel)
                .Distinct()
                .ToArray();
            _registrations.Clear();

            for (var i = 0; i < channels.Length; i++)
            {
                OnChanged(channels[i]);
            }
        }

        private static int CompareRegistrations(
            IInjectionRegistration left,
            IInjectionRegistration right)
        {
            var channelCompare = left.Channel.CompareTo(right.Channel);
            if (channelCompare != 0)
            {
                return channelCompare;
            }

            var priorityCompare = left.Priority.CompareTo(right.Priority);
            return priorityCompare != 0
                ? priorityCompare
                : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }

        private void OnChanged(InjectionChannel channel)
        {
            Changed?.Invoke(
                this,
                new InjectionRegistryChangedEventArgs(channel));
        }
    }
}
