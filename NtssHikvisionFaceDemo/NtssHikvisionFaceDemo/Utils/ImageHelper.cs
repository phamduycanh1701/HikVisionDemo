using SkiaSharp;

namespace NtssHikvisionFaceDemo.Utils;
public static class ImageHelper
{
    public static void ResizeJpeg(
        string inputPath,
        string outputPath,
        int width = 704,
        int height = 864,
        int quality = 80)
    {
        using var stream = File.OpenRead(inputPath);

        using var codec = SKCodec.Create(stream);

        if (codec == null)
            throw new Exception("Không đọc được ảnh.");

        var origin = codec.EncodedOrigin;

        stream.Position = 0;

        using var decoded = SKBitmap.Decode(stream);

        if (decoded == null)
            throw new Exception("Không decode được ảnh.");

        using var bitmap = ApplyOrientation(decoded, origin);

        float srcRatio = (float)bitmap.Width / bitmap.Height;
        float dstRatio = (float)width / height;

        SKRectI srcRect;

        if (srcRatio > dstRatio)
        {
            int cropWidth = (int)(bitmap.Height * dstRatio);

            int x = (bitmap.Width - cropWidth) / 2;

            srcRect = new SKRectI(
                x,
                0,
                x + cropWidth,
                bitmap.Height);
        }
        else
        {
            int cropHeight = (int)(bitmap.Width / dstRatio);

            int y = (bitmap.Height - cropHeight) / 2;

            srcRect = new SKRectI(
                0,
                y,
                bitmap.Width,
                y + cropHeight);
        }

        var info = new SKImageInfo(width, height);

        using var surface = SKSurface.Create(info);

        var canvas = surface.Canvas;

        canvas.Clear(SKColors.White);

        canvas.DrawBitmap(
            bitmap,
            srcRect,
            new SKRect(0, 0, width, height));

        canvas.Flush();

        using var image = surface.Snapshot();

        using var data = image.Encode(
            SKEncodedImageFormat.Jpeg,
            quality);

        using var output = File.Create(outputPath);

        data.SaveTo(output);
    }

    private static SKBitmap ApplyOrientation(
        SKBitmap bitmap,
        SKEncodedOrigin origin)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopLeft:
                return bitmap.Copy();

            case SKEncodedOrigin.RightTop:
                return Rotate90(bitmap);

            case SKEncodedOrigin.BottomRight:
                return Rotate180(bitmap);

            case SKEncodedOrigin.LeftBottom:
                return Rotate270(bitmap);

            case SKEncodedOrigin.TopRight:
                return FlipHorizontal(bitmap);

            case SKEncodedOrigin.BottomLeft:
                return FlipVertical(bitmap);

            default:
                return bitmap.Copy();
        }
    }

    private static SKBitmap Rotate90(SKBitmap source)
    {
        var dest = new SKBitmap(source.Height, source.Width);

        using var canvas = new SKCanvas(dest);

        canvas.Translate(dest.Width, 0);

        canvas.RotateDegrees(90);

        canvas.DrawBitmap(source, 0, 0);

        return dest;
    }

    private static SKBitmap Rotate180(SKBitmap source)
    {
        var dest = new SKBitmap(source.Width, source.Height);

        using var canvas = new SKCanvas(dest);

        canvas.Translate(dest.Width, dest.Height);

        canvas.RotateDegrees(180);

        canvas.DrawBitmap(source, 0, 0);

        return dest;
    }

    private static SKBitmap Rotate270(SKBitmap source)
    {
        var dest = new SKBitmap(source.Height, source.Width);

        using var canvas = new SKCanvas(dest);

        canvas.Translate(0, dest.Height);

        canvas.RotateDegrees(270);

        canvas.DrawBitmap(source, 0, 0);

        return dest;
    }

    private static SKBitmap FlipHorizontal(SKBitmap source)
    {
        var dest = new SKBitmap(source.Width, source.Height);

        using var canvas = new SKCanvas(dest);

        canvas.Scale(-1, 1);

        canvas.Translate(-source.Width, 0);

        canvas.DrawBitmap(source, 0, 0);

        return dest;
    }

    private static SKBitmap FlipVertical(SKBitmap source)
    {
        var dest = new SKBitmap(source.Width, source.Height);

        using var canvas = new SKCanvas(dest);

        canvas.Scale(1, -1);

        canvas.Translate(0, -source.Height);

        canvas.DrawBitmap(source, 0, 0);

        return dest;
    }
}