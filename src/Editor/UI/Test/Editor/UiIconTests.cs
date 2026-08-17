using System;
using NUnit.Framework;

namespace Ee4v.UI.Tests
{
    public sealed class UiIconTests
    {
        [Test]
        public void UiBuiltinIconResolver_TryResolve_AllRegisteredIcons()
        {
            foreach (UiBuiltinIcon builtinIcon in Enum.GetValues(typeof(UiBuiltinIcon)))
            {
                var resolved = UiBuiltinIconResolver.TryResolve(builtinIcon, out var texture);

                Assert.That(resolved, Is.True, builtinIcon.ToString());
                Assert.That(texture, Is.Not.Null, builtinIcon.ToString());
            }
        }

    }
}
