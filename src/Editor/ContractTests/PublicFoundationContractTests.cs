using System.Collections.Generic;
using Ee4v.Core.Background;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.Settings;
using Ee4v.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.ContractTests
{
    public sealed class PublicFoundationContractTests
    {
        [Test]
        public void IndependentFeatureAssembly_ComposesPublicFoundation()
        {
            var definition = new SettingDefinition<bool>(
                "sample.enabled",
                SettingScope.User,
                "Sample",
                "settings.section",
                "settings.enabled",
                "settings.enabled.description",
                true);
            var root = new VisualElement();
            UiComposition.Prepare(root);
            root.Add(UiTextFactory.CreateButton("Sample"));
            IUiStoryProvider stories = new SampleStories();

            Assert.That(definition.DefaultValue, Is.True);
            Assert.That(root.childCount, Is.EqualTo(1));
            Assert.That(stories.GetStories().Count, Is.EqualTo(1));
            Assert.That(typeof(ProjectBrowserApi).IsPublic, Is.True);
            Assert.That(typeof(IBackgroundTaskManager).IsPublic, Is.True);
        }

        private sealed class SampleStories : IUiStoryProvider
        {
            public int Order => 0;

            public IReadOnlyList<UiStory> GetStories()
            {
                return new[]
                {
                    new UiStory(
                        "sample",
                        "Feature/Sample",
                        "Sample",
                        string.Empty,
                        string.Empty,
                        _ => { },
                        usageLocations: new[]
                        {
                            "Editor/ContractTests/PublicFoundationContractTests.cs"
                        })
                };
            }
        }
    }
}
