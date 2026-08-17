using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum UiStoryImplementationKind
    {
        UiToolkit,
        Imgui
    }

    public sealed class UiStory
    {
        public UiStory(
            string id,
            string group,
            string title,
            string description,
            string details,
            Action<VisualElement> build,
            IReadOnlyList<string> dependencies = null,
            IReadOnlyList<string> usageLocations = null,
            IReadOnlyList<string> styleSheetPaths = null,
            UiStoryImplementationKind implementation =
                UiStoryImplementationKind.UiToolkit)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Story id is required.", nameof(id));
            }

            Build = build ?? throw new ArgumentNullException(nameof(build));
            Id = id;
            Group = group ?? string.Empty;
            Title = title ?? id;
            Description = description ?? string.Empty;
            Details = details ?? string.Empty;
            Dependencies = dependencies ?? Array.Empty<string>();
            UsageLocations = usageLocations ?? Array.Empty<string>();
            StyleSheetPaths = styleSheetPaths ?? Array.Empty<string>();
            Implementation = implementation;
        }

        public string Id { get; }
        public string Group { get; }
        public string Title { get; }
        public string Description { get; }
        public string Details { get; }
        public Action<VisualElement> Build { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public IReadOnlyList<string> UsageLocations { get; }
        public IReadOnlyList<string> StyleSheetPaths { get; }
        public UiStoryImplementationKind Implementation { get; }
    }

    public interface IUiStoryProvider
    {
        int Order { get; }

        IReadOnlyList<UiStory> GetStories();
    }
}
