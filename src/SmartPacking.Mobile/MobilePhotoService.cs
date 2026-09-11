using Android.Graphics;

namespace SmartPacking.Mobile;

public sealed record PreparedPhoto(byte[] Content, byte[] ThumbnailContent, string FileName, int Width, int Height)
{
    public int SizeKilobytes => (int)Math.Ceiling(Content.Length / 1024d);
    public int ThumbnailSizeKilobytes => (int)Math.Ceiling(ThumbnailContent.Length / 1024d);
}

public interface IMobilePhotoService
{
    bool CanCapturePhoto { get; }
    Task<PreparedPhoto?> CaptureAsync(CancellationToken cancellationToken);
    Task<PreparedPhoto?> PickAsync(CancellationToken cancellationToken);
}

public sealed class MobilePhotoService : IMobilePhotoService
{
    private const int MaxDimension = 1280;
    private const int ThumbnailDimension = 320;
    private const int JpegQuality = 82;
    private const int ThumbnailJpegQuality = 76;

    public bool CanCapturePhoto => MediaPicker.Default.IsCaptureSupported;

    public async Task<PreparedPhoto?> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await MediaPicker.Default.CapturePhotoAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return file is null ? null : await PrepareAsync(file, cancellationToken);
    }

    public async Task<PreparedPhoto?> PickAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Selecciona una prenda" });
        cancellationToken.ThrowIfCancellationRequested();
        var file = files.FirstOrDefault();
        return file is null ? null : await PrepareAsync(file, cancellationToken);
    }

    private static async Task<PreparedPhoto> PrepareAsync(FileResult file, CancellationToken cancellationToken)
    {
        await using var source = await file.OpenReadAsync();
        using var input = new MemoryStream();
        await source.CopyToAsync(input, cancellationToken);
        if (!input.TryGetBuffer(out var sourceBuffer) || sourceBuffer.Array is null)
        {
            throw new InvalidOperationException("No se ha podido preparar la imagen seleccionada.");
        }

        return await Task.Run(
            () => Optimize(sourceBuffer.Array, sourceBuffer.Offset, sourceBuffer.Count),
            cancellationToken);
    }

    private static PreparedPhoto Optimize(byte[] sourceBytes, int offset, int length)
    {
        using var boundsOptions = new BitmapFactory.Options { InJustDecodeBounds = true };
        _ = BitmapFactory.DecodeByteArray(sourceBytes, offset, length, boundsOptions);
        if (boundsOptions.OutWidth <= 0 || boundsOptions.OutHeight <= 0)
        {
            throw new InvalidOperationException("No se han podido obtener las dimensiones de la imagen seleccionada.");
        }

        using var decodeOptions = new BitmapFactory.Options
        {
            InSampleSize = CalculateInSampleSize(boundsOptions.OutWidth, boundsOptions.OutHeight)
        };
        using var bitmap = BitmapFactory.DecodeByteArray(sourceBytes, offset, length, decodeOptions)
            ?? throw new InvalidOperationException("No se ha podido leer la imagen seleccionada.");

        Bitmap? resized = null;
        try
        {
            var outputBitmap = bitmap;
            var largestDimension = Math.Max(bitmap.Width, bitmap.Height);
            if (largestDimension > MaxDimension)
            {
                var scale = MaxDimension / (double)largestDimension;
                var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
                var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
                resized = Bitmap.CreateScaledBitmap(bitmap, width, height, true);
                outputBitmap = resized;
            }

            return new PreparedPhoto(
                Compress(outputBitmap, JpegQuality),
                CreateThumbnail(outputBitmap),
                $"garment-{Guid.NewGuid():N}.jpg",
                outputBitmap.Width,
                outputBitmap.Height);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static byte[] CreateThumbnail(Bitmap bitmap)
    {
        Bitmap? thumbnail = null;
        try
        {
            var outputBitmap = bitmap;
            var largestDimension = Math.Max(bitmap.Width, bitmap.Height);
            if (largestDimension > ThumbnailDimension)
            {
                var scale = ThumbnailDimension / (double)largestDimension;
                var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
                var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
                thumbnail = Bitmap.CreateScaledBitmap(bitmap, width, height, true);
                outputBitmap = thumbnail;
            }

            return Compress(outputBitmap, ThumbnailJpegQuality);
        }
        finally
        {
            thumbnail?.Dispose();
        }
    }

    private static byte[] Compress(Bitmap bitmap, int quality)
    {
        var jpegFormat = Bitmap.CompressFormat.Jpeg
            ?? throw new InvalidOperationException("El dispositivo no dispone del formato JPEG requerido.");
        using var output = new MemoryStream();
        if (!bitmap.Compress(jpegFormat, quality, output))
        {
            throw new InvalidOperationException("No se ha podido optimizar la fotografía.");
        }

        return output.ToArray();
    }

    private static int CalculateInSampleSize(int width, int height)
    {
        var sampleSize = 1;
        while (width / (sampleSize * 2) >= MaxDimension || height / (sampleSize * 2) >= MaxDimension)
        {
            sampleSize *= 2;
        }

        return sampleSize;
    }
}
