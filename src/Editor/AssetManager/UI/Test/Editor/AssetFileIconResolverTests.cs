using NUnit.Framework;

namespace Ee4v.AssetManager.UI.Tests
{
    internal sealed class AssetFileIconResolverTests
    {
        [TestCase("preview.PNG", null, "image.png")]
        [TestCase("ambient.wav", null, "music_note_2.png")]
        [TestCase("trailer.mp4", null, "video.png")]
        [TestCase("avatar.unitypackage", null, "folder_zip.png")]
        [TestCase("AvatarController.cs", null, "code.png")]
        [TestCase("character.fbx", null, "cube.png")]
        [TestCase("license.pdf", null, "document.png")]
        [TestCase("Avatar package", "library/avatar.zip", "folder_zip.png")]
        public void GetIconFileName_MapsFileExtension(
            string fileName,
            string sourcePath,
            string expected)
        {
            Assert.That(
                AssetFileIconResolver.GetIconFileName(
                    fileName,
                    sourcePath),
                Is.EqualTo(expected));
        }
    }
}
