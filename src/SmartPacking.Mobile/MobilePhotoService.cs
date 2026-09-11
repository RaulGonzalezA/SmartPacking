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
    private const int MaxSourceBytes = 25 * 1024 * 1024;
    private const int CopyBufferSize = 81920;
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
        if (source.CanSeek && source.Length > MaxSourceBytes)
        {
            throw new InvalidOperationException("La imagen seleccionada es demasiado grande. El límite es 25 MB.");
        }

        using var input = new MemoryStream();
        await CopyToBufferAsync(source, input, cancellationToken);
        if (!input.TryGetBuffer(out var sourceBuffer) || sourceBuffer.Array is null)
        {
            throw new InvalidOperationException("No se ha podido preparar la imagen seleccionada.");
        }

        return await Task.Run(
            () => Optimize(sourceBuffer.Array, sourceBuffer.Offset, sourceBuffer.Count),
            cancellationToken);
    }

    private static async Task CopyToBufferAsync(Stream source, MemoryStream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                return;
            }

            if (destination.Length + read > MaxSourceBytes)
            {
                throw new InvalidOperationException("La imagen seleccionada es demasiado grande. El límite es 25 MB.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static PreparedPhoto Optimize(byte[] sourceBytes, int offset, int length)
    {
        var orientation = JpegExifOrientationReader.Read(sourceBytes.AsSpan(offset, length));
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

        Bitmap? oriented = null;
        Bitmap? resized = null;
        try
        {
            var outputBitmap = bitmap;
            if (orientation != 1)
            {
                oriented = ApplyOrientation(bitmap, orientation);
                outputBitmap = oriented;
            }

            var largestDimension = Math.Max(outputBitmap.Width, outputBitmap.Height);
            if (largestDimension > MaxDimension)
            {
                var scale = MaxDimension / (double)largestDimension;
                var width = Math.Max(1, (int)Math.Round(outputBitmap.Width * scale));
                var height = Math.Max(1, (int)Math.Round(outputBitmap.Height * scale));
                resized = Bitmap.CreateScaledBitmap(outputBitmap, width, height, true);
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
            oriented?.Dispose();
        }
    }

    private static Bitmap ApplyOrientation(Bitmap bitmap, int orientation)
    {
        using var matrix = new Matrix();
        switch (orientation)
        {
            case 2:
                matrix.SetScale(-1, 1);
                break;
            case 3:
                matrix.SetRotate(180);
                break;
            case 4:
                matrix.SetScale(1, -1);
                break;
            case 5:
                matrix.SetRotate(90);
                _ = matrix.PostScale(-1, 1);
                break;
            case 6:
                matrix.SetRotate(90);
                break;
            case 7:
                matrix.SetRotate(-90);
                _ = matrix.PostScale(-1, 1);
                break;
            case 8:
                matrix.SetRotate(-90);
                break;
            default:
                throw new InvalidOperationException("La orientación EXIF de la fotografía no es válida.");
        }

        return Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true)
            ?? throw new InvalidOperationException("No se ha podido aplicar la orientación de la fotografía.");
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
