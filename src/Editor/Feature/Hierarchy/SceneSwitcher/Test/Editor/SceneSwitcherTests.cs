using System;
using System.Linq;
using NUnit.Framework;

namespace Ee4v.SceneSwitcher.Tests
{
    public sealed class SceneSwitcherTests
    {
        [Test]
        public void View_OrdersOpenFavoriteThenOtherAndAllowsNewName()
        {
            var records = new[]
            {
                new SceneSwitcherRecord("Assets/Other.unity"),
                new SceneSwitcherRecord(
                    "Assets/Favorite.unity",
                    isFavorite: true),
                new SceneSwitcherRecord("Assets/Open.unity")
            };

            var state = SceneSwitcherPolicy.BuildView(
                records,
                new[] { "Assets/Open.unity" },
                string.Empty);
            var createState = SceneSwitcherPolicy.BuildView(
                records,
                Array.Empty<string>(),
                "New Scene");

            Assert.That(
                state.Items.Select(item => item.Name),
                Is.EqualTo(new[] { "Open", "Favorite", "Other" }));
            Assert.That(createState.CanCreate, Is.True);
        }
    }
}
