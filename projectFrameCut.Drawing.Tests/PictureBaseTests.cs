using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;

namespace projectFrameCut.Drawing.Tests;

[TestClass]
public sealed class PictureBaseTests
{
    [TestMethod]
    public void Clone_CreatesDeepCopy_ForPicture8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(2, 2, [1, 2, 3, 4], alpha: [0.1f, 0.2f, 0.3f, 0.4f]);

        var cloned = (Picture8bpp)source.Clone();
        cloned.r[0] = 99;
        cloned.a![1] = 0.9f;

        Assert.AreNotSame(source, cloned);
        Assert.AreEqual((byte)1, source.r[0]);
        Assert.AreEqual(0.2f, source.a![1], 0.0001f);
        Assert.AreEqual(2, cloned.Width);
        Assert.AreEqual(2, cloned.Height);
    }

    [TestMethod]
    public void TryFromXYToArrayIndex_And_GetPixel_HandleBounds()
    {
        var source = TestPictureFactory.CreatePicture8(2, 2, [10, 20, 30, 40]);

        bool ok = PictureExtensions.TryFromXYToArrayIndex(1, 1, 2, 2, out int index);
        bool bad = PictureExtensions.TryFromXYToArrayIndex(-1, 0, 2, 2, out int badIndex);

        Assert.IsTrue(ok);
        Assert.AreEqual(3, index);
        Assert.IsFalse(bad);
        Assert.AreEqual(-1, badIndex);

        var pixel = source.GetPixel(1, 0);
        Assert.AreEqual((byte)20, pixel.r);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.GetPixel(-1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.GetPixel(0, 9));
    }

    [TestMethod]
    public void ToBitPerPixel_ConvertsBetween8And16bpp()
    {
        var source8 = TestPictureFactory.CreatePicture8(2, 1, [0, 255], [10, 20], [30, 40]);

        var converted16 = (Picture16bpp)source8.ToBitPerPixel(IPicture.PicturePixelMode.UShortPicture);
        Assert.AreEqual((ushort)0, converted16.r[0]);
        Assert.AreEqual((ushort)65535, converted16.r[1]);
        Assert.AreEqual((ushort)(10 * 257), converted16.g[0]);

        var source16 = TestPictureFactory.CreatePicture16(2, 1, [257, 65535], [514, 0], [771, 65535]);
        var converted8 = (Picture8bpp)source16.ToBitPerPixel(IPicture.PicturePixelMode.BytePicture);
        Assert.AreEqual((byte)1, converted8.r[0]);
        Assert.AreEqual((byte)255, converted8.r[1]);
        Assert.AreEqual((byte)2, converted8.g[0]);
    }

    [TestMethod]
    public void ToBitPerPixel_ThrowsWhenDowngradeDisallowed()
    {
        var source16 = TestPictureFactory.CreatePicture16(1, 1, [1000]);
        bool original = IPicture.AllowPixelModeDowngrade;

        try
        {
            IPicture.AllowPixelModeDowngrade = false;
            Assert.ThrowsExactly<InvalidOperationException>(
                () => source16.ToBitPerPixel(IPicture.PicturePixelMode.BytePicture));
        }
        finally
        {
            IPicture.AllowPixelModeDowngrade = original;
        }
    }
}
