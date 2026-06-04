using projectFrameCut.Drawing.Base;
using projectFrameCut.Drawing.Base.Picture;
using projectFrameCut.Drawing.Effect;
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
        var resizer = new BilinearPictureResizer();

        var result = resizer.Resize(source, 2, 2, preserveAspect: false);

        Assert.AreSame(source, result);
    }

    [TestMethod]
    public void Resize_ThrowsOnInvalidTargetDimensions()
    {
        var source = TestPictureFactory.CreatePicture8(2, 2, [1, 2, 3, 4]);
        var resizer = new BilinearPictureResizer();

        Assert.ThrowsExactly<ArgumentException>(() => resizer.Resize(source, 0, 2, preserveAspect: false));
    }

    [TestMethod]
    public void Resize_PreserveAspectChangesOutputSize()
    {
        var source = TestPictureFactory.CreatePicture8(4, 2, [1, 2, 3, 4, 5, 6, 7, 8]);
        var resizer = new BilinearPictureResizer();

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

    // ===== InvertEffect Tests =====

    [TestMethod]
    public void Invert_InvertsAllChannels_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(2, 2, [0, 128, 255, 10]);
        var result = (Picture8bpp)InvertEffect.Process(source);

        Assert.AreEqual(255, result.r[0]);
        Assert.AreEqual(127, result.r[1]);
        Assert.AreEqual(0, result.r[2]);
        Assert.AreEqual(245, result.r[3]);
    }

    [TestMethod]
    public void Invert_InvertsAllChannels_16bpp()
    {
        var source = TestPictureFactory.CreatePicture16(1, 1, [0], [65535], [12345]);
        var result = (Picture16bpp)InvertEffect.Process(source);

        Assert.AreEqual(65535, result.r[0]);
        Assert.AreEqual(0, result.g[0]);
        Assert.AreEqual((ushort)(65535 - 12345), result.b[0]);
    }

    [TestMethod]
    public void Invert_PreservesAlpha_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [100], alpha: [0.5f]);
        var result = (Picture8bpp)InvertEffect.Process(source);

        Assert.AreEqual(0.5f, result.a![0], 0.001f);
    }

    [TestMethod]
    public void Invert_HDR_PreservesBrightness()
    {
        var source = TestPictureFactory.CreateHdrPicture16(1, 1, [1000], brightness: [0.75f]);
        var result = (HDRPicture16bpp)InvertEffect.Process(source);

        Assert.AreEqual(65535 - 1000, result.r[0]);
        Assert.AreEqual(0.75f, result.Brightness[0], 0.001f);
    }

    // ===== GrayscaleEffect Tests =====

    [TestMethod]
    public void Grayscale_AllChannelsEqual_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [100], [150], [200]);
        var result = (Picture8bpp)GrayscaleEffect.Process(source);

        Assert.AreEqual(result.r[0], result.g[0]);
        Assert.AreEqual(result.g[0], result.b[0]);
    }

    [TestMethod]
    public void Grayscale_UsesBT709Weights()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [255], [0], [0]);
        var result = (Picture8bpp)GrayscaleEffect.Process(source);

        int expected = (int)(255 * 0.2126f + 0.5f);
        Assert.AreEqual(expected, result.r[0]);
    }

    // ===== BrightnessEffect Tests =====

    [TestMethod]
    public void Brightness_FactorZero_ReturnsIdentity_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(2, 1, [10, 200]);
        var result = (Picture8bpp)BrightnessEffect.Process(source, 0f);

        CollectionAssert.AreEqual(source.r, result.r);
        CollectionAssert.AreEqual(source.g, result.g);
        CollectionAssert.AreEqual(source.b, result.b);
    }

    [TestMethod]
    public void Brightness_PositiveLightens_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [100]);
        var result = (Picture8bpp)BrightnessEffect.Process(source, 0.5f);

        Assert.IsGreaterThan((byte)100, result.r[0]);
    }

    [TestMethod]
    public void Brightness_NegativeDarkens_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [200]);
        var result = (Picture8bpp)BrightnessEffect.Process(source, -0.5f);

        Assert.AreEqual(100, result.r[0]);
    }

    [TestMethod]
    public void Brightness_HDR_AffectsBrightnessChannel()
    {
        var source = TestPictureFactory.CreateHdrPicture16(1, 1, [1000], brightness: [0.5f]);
        var result = (HDRPicture16bpp)BrightnessEffect.Process(source, -0.5f);

        Assert.AreEqual(0.25f, result.Brightness[0], 0.001f);
    }

    // ===== ContrastEffect Tests =====

    [TestMethod]
    public void Contrast_FactorOne_ReturnsIdentity_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(2, 1, [50, 200]);
        var result = (Picture8bpp)ContrastEffect.Process(source, 1f);

        CollectionAssert.AreEqual(source.r, result.r);
        CollectionAssert.AreEqual(source.g, result.g);
        CollectionAssert.AreEqual(source.b, result.b);
    }

    [TestMethod]
    public void Contrast_FactorZero_ProducesMidGray_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [100]);
        var result = (Picture8bpp)ContrastEffect.Process(source, 0f);

        Assert.AreEqual(128, result.r[0]);
    }

    [TestMethod]
    public void Contrast_ClampsCorrectly_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [0], [255]);
        var result = (Picture8bpp)ContrastEffect.Process(source, 2f);

        Assert.AreEqual(0, result.r[0]);
        Assert.AreEqual(255, result.g[0]);
    }

    // ===== SaturationEffect Tests =====

    [TestMethod]
    public void Saturation_FactorOne_ReturnsIdentity_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [50], [100], [200]);
        var result = (Picture8bpp)SaturationEffect.Process(source, 1f);

        Assert.AreEqual(source.r[0], result.r[0]);
        Assert.AreEqual(source.g[0], result.g[0]);
        Assert.AreEqual(source.b[0], result.b[0]);
    }

    [TestMethod]
    public void Saturation_FactorZero_ProducesGray_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [50], [100], [200]);
        var result = (Picture8bpp)SaturationEffect.Process(source, 0f);

        Assert.AreEqual(result.r[0], result.g[0]);
        Assert.AreEqual(result.g[0], result.b[0]);
    }

    // ===== GammaEffect Tests =====

    [TestMethod]
    public void Gamma_FactorOne_ReturnsIdentity_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(2, 1, [0, 255]);
        var result = (Picture8bpp)GammaEffect.Process(source, 1f);

        CollectionAssert.AreEqual(source.r, result.r);
        CollectionAssert.AreEqual(source.g, result.g);
        CollectionAssert.AreEqual(source.b, result.b);
    }

    [TestMethod]
    public void Gamma_InvalidGamma_ReturnsSource()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [100]);
        var result = GammaEffect.Process(source, -1f);

        Assert.AreSame(source, result);
    }

    [TestMethod]
    public void Gamma_HDR_AffectsBrightnessChannel()
    {
        var source = TestPictureFactory.CreateHdrPicture16(1, 1, [1000], brightness: [0.25f]);
        var result = (HDRPicture16bpp)GammaEffect.Process(source, 2f);

        Assert.AreEqual(MathF.Pow(0.25f, 0.5f), result.Brightness[0], 0.001f);
    }

    // ===== ThresholdEffect Tests =====

    [TestMethod]
    public void Threshold_DefaultParams_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(2, 1, [0, 255]);
        var result = (Picture8bpp)ThresholdEffect.Process(source);

        Assert.AreEqual(0, result.r[0]);
        Assert.AreEqual(0, result.g[0]);
        Assert.AreEqual(0, result.b[0]);
        Assert.AreEqual(255, result.r[1]);
    }

    [TestMethod]
    public void Threshold_CustomValues_8bpp()
    {
        var source = TestPictureFactory.CreatePicture8(1, 1, [100]);
        var result = (Picture8bpp)ThresholdEffect.Process(source, threshold: 0.5f, lowValue: 0.2f, highValue: 0.8f);

        byte expectedLow = (byte)Math.Clamp((int)(0.2f * 255f + 0.5f), 0, 255);
        Assert.AreEqual(expectedLow, result.r[0]);
    }

    // ===== Fluent API Tests =====

    [TestMethod]
    public void Fluent_Chaining_MultipleEffects_8bpp()
    {
        IPicture<byte> source = TestPictureFactory.CreatePicture8(1, 1, [100]);
        var result = source.EnterProcessContext()
            .Invert()
            .Grayscale()
            .Brightness(0.5f)
            .Result;

        Assert.IsInstanceOfType(result, typeof(IPicture<byte>));
    }

    [TestMethod]
    public void Fluent_InvertExtension_8bpp()
    {
        IPicture<byte> source = TestPictureFactory.CreatePicture8(1, 1, [0]);
        var result = source.EnterProcessContext().Invert().Result;

        Assert.AreEqual(255, ((Picture8bpp)result).r[0]);
    }
}
