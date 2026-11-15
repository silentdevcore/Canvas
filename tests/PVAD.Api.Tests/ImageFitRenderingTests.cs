using System.Text;
using PVAD.Core.Contracts;
using PVAD.Pdf;
using PVAD.WebApi.Infrastructure;
using SkiaSharp;

namespace PVAD.Api.Tests;

public sealed class ImageFitRenderingTests
{
    [Theory]
    [InlineData("contain", 50, 50, 20, 35, 80, 40)]
    [InlineData("contain", 0, 0, 20, 30, 80, 40)]
    [InlineData("contain", 100, 100, 20, 40, 80, 40)]
    [InlineData("cover", 0, 0, 20, 30, 100, 50)]
    [InlineData("cover", 50, 50, 10, 30, 100, 50)]
    [InlineData("cover", 100, 100, 0, 30, 100, 50)]
    [InlineData("fill", 100, 0, 20, 30, 80, 50)]
    [InlineData("none", 0, 0, 20, 30, 120, 60)]
    [InlineData("none", 50, 50, 0, 25, 120, 60)]
    [InlineData("none", 100, 100, -20, 20, 120, 60)]
    [InlineData("unknown", 50, 50, 20, 35, 80, 40)]
    public void MapperUsesBrowserFitAndFocus(string fit, double focalX, double focalY,
        double x, double top, double width, double height)
    {
        var document = Render(Image(fit, focalX, focalY));
        var image = Assert.Single(document.CreateDesignSnapshot().Pages[0].Elements, e => e.Kind == "image");
        Assert.Equal(x, image.X, 6);
        Assert.Equal(top, 200 - image.Y - image.Height, 6);
        Assert.Equal(width, image.Width, 6);
        Assert.Equal(height, image.Height, 6);
        Assert.Contains("20 120 80 50 re W n", Encoding.Latin1.GetString(document.ToBytes()));
    }

    [Theory]
    [InlineData(0, 0, 20, 30)]
    [InlineData(50, 50, 40, 47)]
    [InlineData(100, 100, 60, 64)]
    public void NonePreservesSmallNaturalImageSize(double focusX, double focusY, double x, double top)
    {
        var element = Image("none", focusX, focusY);
        element.Content = DataUrl(40, 16);
        var image = Assert.Single(Render(element).CreateDesignSnapshot().Pages[0].Elements);
        Assert.Equal(x, image.X);
        Assert.Equal(top, 200 - image.Y - image.Height);
        Assert.Equal(40, image.Width);
        Assert.Equal(16, image.Height);
    }

    [Fact]
    public void PreserveAspectRatioOverridesCoverAndInvalidFocusIsNormalized()
    {
        var element = Image("cover", -100, 150);
        element.PreserveAspectRatio = true;
        var image = Assert.Single(Render(element).CreateDesignSnapshot().Pages[0].Elements);
        Assert.Equal(80, image.Width);
        Assert.Equal(40, image.Height);
        Assert.Equal(20, image.X);
        Assert.Equal(120, image.Y);
    }

    [Fact]
    public void LocalizedImageGeometryIsUsedWithoutChangingTheSource()
    {
        var element = Image("fill", 50, 50);
        element.LangOverrides = new() { ["fr"] = new() { X = 0, Y = 0, Width = 40, Height = 20 } };
        var image = Assert.Single(Render(element, "fr").CreateDesignSnapshot().Pages[0].Elements);
        Assert.Equal(0, image.X);
        Assert.Equal(180, image.Y);
        Assert.Equal(40, image.Width);
        Assert.Equal(20, image.Height);
        Assert.Equal(20, element.X);
    }

    [Fact]
    public void AlphaChannelAndClipArePreservedAndFollowingContentIsNotClipped()
    {
        var document = Render(Image("cover", 100, 100));
        document.Pages[0].DrawText("After image", 150, 180);
        var pdf = Encoding.Latin1.GetString(document.ToBytes());
        Assert.Contains("/SMask", pdf);
        Assert.Contains(" re W n", pdf);
        Assert.Matches(@"Do\s+Q\s+q", pdf);
    }

    [Theory]
    [InlineData("data:image/png;base64,invalid")]
    [InlineData("data:image/png;base64")]
    [InlineData("")]
    public void InvalidOrMissingImagesUseTheLocalPlaceholder(string content)
    {
        var element = Image("cover", 50, 50);
        element.Content = content;
        var snapshot = Render(element).CreateDesignSnapshot();
        Assert.DoesNotContain(snapshot.Pages[0].Elements, e => e.Kind == "image");
        Assert.Contains(snapshot.Pages[0].Elements, e => e.Text == "[Image]");
    }

    [Fact]
    public void NativeFittedImagesValidateBoundsAndFallbackToCenteredFiniteFocus()
    {
        var bytes = Convert.FromBase64String(DataUrl(120, 60).Split(',')[1]);
        var page = new PdfDocument().AddPage(200, 200);
        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawImageFitted(bytes, 0, 0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawImageFitted(bytes, 0, 0, double.NaN, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawImageFitted(bytes, double.PositiveInfinity, 0, 10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawImageFitted(bytes, 0, 0, 10, 10, opacity: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawImageFitted(bytes, 0, 0, 10, 10, (PdfImageFitMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawImageFitted(bytes, 0, 0, double.MaxValue, double.MaxValue, PdfImageFitMode.Cover));
        var result = page.DrawImageFitted(bytes, 20, 120, 80, 50, PdfImageFitMode.Cover, double.NaN, double.PositiveInfinity);
        Assert.Equal(10, result.X);
        Assert.Equal(100, result.Width);
    }

    [Fact]
    public void JpegKeepsItsEncodedImageAndUsesTheSameGeometry()
    {
        using var bitmap = new SKBitmap(120, 60);
        bitmap.Erase(SKColors.Coral);
        using var source = SKImage.FromBitmap(bitmap);
        using var jpeg = source.Encode(SKEncodedImageFormat.Jpeg, 90);
        var element = Image("cover", 100, 0);
        element.Content = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg.ToArray());
        var document = Render(element);
        var image = Assert.Single(document.CreateDesignSnapshot().Pages[0].Elements);
        Assert.Equal(0, image.X);
        Assert.Equal(100, image.Width);
        Assert.Contains("/DCTDecode", Encoding.Latin1.GetString(document.ToBytes()));
    }

    private static ElementDto Image(string fit, double x, double y) => new()
    {
        Id = "image", Type = "image", X = 20, Y = 30, Width = 80, Height = 50,
        Content = DataUrl(120, 60), FitMode = fit, FocalX = x, FocalY = y
    };

    private static PdfDocument Render(ElementDto element, string? language = null) => DesignJsonMapper.MapToPdfDocument(new()
    {
        PageSettings = new() { Width = 200, Height = 200, SystemLanguage = "en" },
        Pages = [new() { Id = "page", Elements = [element] }]
    }, targetLanguage: language);

    private static string DataUrl(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(200, 40, 20, 128));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(encoded.ToArray());
    }
}
