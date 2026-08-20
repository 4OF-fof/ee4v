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

        [Test]
        public void FluentUiIcons_LoadsSelectedRuntimeIcons()
        {
            var iconFileNames = new[]
            {
                "add.png",
                "archive.png",
                "arrow_clockwise.png",
                "arrow_left.png",
                "arrow_right.png",
                "arrow_sort.png",
                "chevron_down.png",
                "chevron_right.png",
                "code.png",
                "cube.png",
                "dismiss.png",
                "document.png",
                "eye_off.png",
                "folder.png",
                "folder_zip.png",
                "image.png",
                "info.png",
                "library.png",
                "music_note_2.png",
                "pin.png",
                "search.png",
                "star.png",
                "subtract.png",
                "tag.png",
                "video.png"
            };

            foreach (var iconFileName in iconFileNames)
            {
                Assert.That(
                    FluentUiIcons.LoadTexture(iconFileName),
                    Is.Not.Null,
                    iconFileName);
            }
        }

    }
}
