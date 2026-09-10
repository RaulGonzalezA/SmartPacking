using Android.Graphics;

namespace SmartPacking.Mobile;

public sealed record PreparedPhoto(byte[] Content, string FileName, int Width, int Height)
{
    public int SizeKilobytes => (int)Math.Ceiling(Content.Length / 1024d);
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
    private const int JpegQuality = 82;

    public bool CanCapturePhoto => MediaPicker.Default.IsCaptureSupported;

    public async Task<PreparedPhoto?> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await MediaPicker.Default.CapturePhotoAsync();
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
        var sourceBytes = input.ToArray();
        return await Task.Run(() => Optimize(sourceBytes), cancellationToken);
    }

    private static PreparedPhoto Optimize(byte[] sourceBytes)
    {
        using var boundsOptions = new BitmapFactory.Options { InJustDecodeBounds = true };
        _ = BitmapFactory.DecodeByteArray(sourceBytes, 0, sourceBytes.Length, boundsOptions);
        if (boundsOptions.OutWidth <= 0 || boundsOptions.OutHeight <= 0)
        {
            throw new InvalidOperationException("No se han podido obtener las dimensiones de la imagen seleccionada.");
        }

        using var decodeOptions = new BitmapFactory.Options
        {
            InSampleSize = CalculateInSampleSize(boundsOptions.OutWidth, boundsOptions.OutHeight)
        };
        using var bitmap = BitmapFactory.DecodeByteArray(sourceBytes, 0, sourceBytes.Length, decodeOptions)
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

            var jpegFormat = Bitmap.CompressFormat.Jpeg
                ?? throw new InvalidOperationException("El dispositivo no dispone del formato JPEG requerido.");
            using var output = new MemoryStream();
            if (!outputBitmap.Compress(jpegFormat, JpegQuality, output))
            {
                throw new InvalidOperationException("No se ha podido optimizar la fotografía.");
            }

            return new PreparedPhoto(
                output.ToArray(),
                $"garment-{Guid.NewGuid():N}.jpg",
                outputBitmap.Width,
                outputBitmap.Height);
        }
        finally
        {
            resized?.Dispose();
        }
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
