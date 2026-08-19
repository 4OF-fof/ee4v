using Ee4v.Core.Images;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.Core.Tests
{
    internal sealed class CachedImageTests
    {
        [Test]
        public void ImagesWithTheSameSource_ReuseDecodedTexture()
        {
            var cache = new CachedImageCache();
            var first = new CachedImage(cache);
            var second = new CachedImage(cache);
            try
            {
                cache.SetSource("sample", CreateImageData());
                first.SetSource("sample");
                second.SetSource("sample");

                Assert.That(first.DisplayedTexture, Is.Not.Null);
                Assert.That(
                    second.DisplayedTexture,
                    Is.SameAs(first.DisplayedTexture));
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                cache.Dispose();
            }
        }

        private static byte[] CreateImageData()
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[]
            {
                Color.red,
                Color.green,
                Color.blue,
                Color.white
            });
            texture.Apply();
            var data = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return data;
        }
    }
}
