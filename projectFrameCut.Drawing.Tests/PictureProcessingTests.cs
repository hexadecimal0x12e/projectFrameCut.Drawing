using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Processing.Converting;
using projectFrameCut.Drawing.Processing.Cropping;
using projectFrameCut.Drawing.Processing.Resizing;

namespace projectFrameCut.Drawing.Tests;

[TestClass]
public sealed class PictureProcessingTests
{
    [TestMethod]
    public void Crop_ExtractsExpectedPixels()
    {
        var source = TestPictureFactory.CreatePicture8(
            3,
            3,
            [0, 1, 2, 3, 4, 5, 6, 7, 8],
            [10, 11, 12, 13, 14, 15, 16, 17, 18],
            [20, 21, 22, 23, 24, 25, 26, 27, 28]);

        var cropper = new CPUPictureCropper();
        var result = (Picture8bpp)cropper.Crop(source, 1, 1, 2, 2);

        CollectionAssert.AreEqual(new byte[] { 4, 5, 7, 8 }, result.r);
        CollectionAssert.AreEqual(new byte[] { 14, 15, 17, 18 }, result.g);
        CollectionAssert.AreEqual(new byte[] { 24, 25, 27, 28 }, result.b);
    }

    [TestMethod]
    public void Crop_AdjustsOutOfBoundsRequest()
    {
        var source = TestPictureFactory.CreatePicture8(3, 3, [0, 1, 2, 3, 4, 5, 6, 7, 8]);
        var cropper = new CPUPictureCropper();

        var result = (Picture8bpp)cropper.Crop(source, -1, -1, 3, 3);

        Assert.AreEqual(2, result.Width);
        Assert.AreEqual(2, result.Height);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 3, 4 }, result.r);
    }

    [TestMethod]
    public void Crop_ThrowsWhenNoOverlap()
    {
        var source = TestPictureFactory.CreatePicture8(3, 3, [0, 1, 2, 3, 4, 5, 6, 7, 8]);
        var cropper = new CPUPictureCropper();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => cropper.Crop(source, 5, 5, 2, 2));
    }

    [TestMethod]
    public void Resize_ReturnsSourceWhenSizeUnchanged()
    {
        var source = TestPictureFactory.CreatePicture8(2, 2, [1, 2, 3, 4]);
        var resizer = new CPUBilinearPictureResizer();

        var result = resizer.Resize(source, 2, 2, preserveAspect: false);

        Assert.AreSame(source, result);
    }

    [TestMethod]
    public void Resize_ThrowsOnInvalidTargetDimensions()
    {
        var source = TestPictureFactory.CreatePicture8(2, 2, [1, 2, 3, 4]);
        var resizer = new CPUBilinearPictureResizer();

        Assert.ThrowsExactly<ArgumentException>(() => resizer.Resize(source, 0, 2, preserveAspect: false));
    }

    [TestMethod]
    public void Resize_PreserveAspectChangesOutputSize()
    {
        var source = TestPictureFactory.CreatePicture8(4, 2, [1, 2, 3, 4, 5, 6, 7, 8]);
        var resizer = new CPUBilinearPictureResizer();

        var result = (Picture8bpp)resizer.Resize(source, 2, 2, preserveAspect: true);

        Assert.AreEqual(2, result.Width);
        Assert.AreEqual(1, result.Height);
    }

    [TestMethod]
    public void ToHDR_FromSDR_SetsBrightnessAndMaximumBrightness()
    {
        var source = TestPictureFactory.CreatePicture16(2, 1, [100, 200], [300, 400], [500, 600], alpha: [0.3f, 0.7f]);

        var hdr = (HDRPicture16bpp)HDRPictureConverter.Default.ToHDR(source, maximumBrightness: 1200f, defaultBrightness: 0.25f);

        Assert.AreEqual(1200f, hdr.MaximumBrightness, 0.001f);
        CollectionAssert.AreEqual(new float[] { 0.25f, 0.25f }, hdr.Brightness);
        CollectionAssert.AreEqual(source.r, hdr.r);
        CollectionAssert.AreEqual(source.a!, hdr.a!);
    }

    [TestMethod]
    public void DegradeToSDR_DiscardBrightnessChannel_KeepsRgb()
    {
        var source = TestPictureFactory.CreateHdrPicture16(
            2,
            1,
            [1000, 5000],
            [2000, 6000],
            [3000, 7000],
            brightness: [0.1f, 0.9f],
            maximumBrightness: 1500f);

        var sdr = HDRPictureConverter.Default.ToSDR(source, HDRImageDegradeToSDRMode.DiscardBrightnessChannel);

        CollectionAssert.AreEqual(source.r, sdr.r);
        CollectionAssert.AreEqual(source.g, sdr.g);
        CollectionAssert.AreEqual(source.b, sdr.b);
    }

    [TestMethod]
    public void DegradeToSDR_OverlayMaskFromBrightness_AppliesMask()
    {
        var source = TestPictureFactory.CreateHdrPicture16(
            2,
            1,
            [10000, 50000],
            [20000, 40000],
            [30000, 30000],
            brightness: [0.5f, 1.0f]);

        var sdr = HDRPictureConverter.Default.ToSDR(source, HDRImageDegradeToSDRMode.OverlayMaskFromBrightness);

        Assert.AreEqual((ushort)5000, sdr.r[0]);
        Assert.AreEqual((ushort)10000, sdr.g[0]);
        Assert.AreEqual((ushort)15000, sdr.b[0]);
        Assert.AreEqual((ushort)50000, sdr.r[1]);
    }

    [TestMethod]
    public void DegradeToSDR_DisallowDowngrade_Throws()
    {
        var source = TestPictureFactory.CreateHdrPicture16(1, 1, [1000], brightness: [1.0f]);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => HDRPictureConverter.Default.ToSDR(source, HDRImageDegradeToSDRMode.DisallowDowngrade));
    }
}
