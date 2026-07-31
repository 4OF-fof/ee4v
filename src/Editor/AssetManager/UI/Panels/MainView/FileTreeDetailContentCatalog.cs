using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class FileTreeDetailContentDefinition
    {
        private readonly Func<FileTreeDetailState, VisualElement>
            _contentFactory;

        public FileTreeDetailContentDefinition(
            string id,
            IEnumerable<string> extensions,
            Func<FileTreeDetailState, VisualElement> contentFactory)
        {
            Id = id ?? string.Empty;
            Extensions = (extensions ?? Array.Empty<string>())
                .Select(FileExtensionUtility.Normalize)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _contentFactory = contentFactory ??
                              throw new ArgumentNullException(
                                  nameof(contentFactory));
        }

        public string Id { get; }

        public IReadOnlyList<string> Extensions { get; }

        public bool Matches(string extension)
        {
            var normalized =
                FileExtensionUtility.Normalize(extension);
            return Extensions.Contains(
                normalized,
                StringComparer.OrdinalIgnoreCase);
        }

        public VisualElement CreateContent(
            FileTreeDetailState state)
        {
            return _contentFactory(
                state ??
                new FileTreeDetailState(
                    string.Empty,
                    string.Empty));
        }
    }

    internal static class FileTreeDetailContentCatalog
    {
        private const string ContentClassName =
            "ee4v-asset-manager-file-detail__content";
        private const string NameClassName =
            "ee4v-asset-manager-file-detail__name";

        private static readonly FileTreeDetailContentDefinition[]
            RegisteredDefinitions =
        {
            new FileTreeDetailContentDefinition(
                "zip",
                new[] { "zip" },
                CreateArchiveDetail),
            new FileTreeDetailContentDefinition(
                "unitypackage",
                new[] { "unitypackage" },
                CreateArchiveDetail)
        };

        private static readonly FileTreeDetailContentDefinition
            FallbackDefinition =
                new FileTreeDetailContentDefinition(
                    "fallback",
                    null,
                    CreateFallback);

        private static readonly IReadOnlyList<
                FileTreeDetailContentDefinition>
            ReadOnlyDefinitions =
                Array.AsReadOnly(RegisteredDefinitions);

        public static IReadOnlyList<FileTreeDetailContentDefinition>
            Definitions
        {
            get { return ReadOnlyDefinitions; }
        }

        public static FileTreeDetailContentDefinition Resolve(
            string extension)
        {
            for (var i = 0;
                 i < RegisteredDefinitions.Length;
                 i++)
            {
                if (RegisteredDefinitions[i].Matches(extension))
                {
                    return RegisteredDefinitions[i];
                }
            }

            return FallbackDefinition;
        }

        private static VisualElement CreateFallback(
            FileTreeDetailState state)
        {
            var content = new VisualElement();
            content.AddToClassList(ContentClassName);
            var name = UiTextFactory.Create(
                state.Name,
                NameClassName,
                UiClassNames.FileTreeDetailName);
            name.SetWhiteSpace(WhiteSpace.Normal);
            content.Add(name);
            return content;
        }

        private static VisualElement CreateArchiveDetail(
            FileTreeDetailState state)
        {
            var view = new ArchiveFileDetailView();
            view.SetState(state);
            return view;
        }
    }
}
