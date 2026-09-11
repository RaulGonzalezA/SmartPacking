using Microsoft.AspNetCore.Http;

namespace SmartPacking.Api;

internal static class JpegImageValidator
{
    public static async Task<bool> IsValidAsync(
        IFormFile file,
        long maximumBytes,
        long maximumPixels,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0 ||
            file.Length > maximumBytes ||
            !string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await using var source = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await source.CopyToAsync(buffer, cancellationToken);
        if (!buffer.TryGetBuffer(out var segment) || segment.Array is null)
        {
            return false;
        }

        var data = segment.Array.AsSpan(segment.Offset, segment.Count);
        if (!TryReadDimensions(data, out var width, out var height))
        {
            return false;
        }

        return (long)width * height <= maximumPixels;
    }

    private static bool TryReadDimensions(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return false;
        }

        var position = 2;
        while (position + 3 < data.Length)
        {
            if (data[position] != 0xFF)
            {
                position++;
                continue;
            }

            while (position < data.Length && data[position] == 0xFF)
            {
                position++;
            }

            if (position >= data.Length)
            {
                return false;
            }

            var marker = data[position++];
            if (marker is 0xD9 or 0xDA)
            {
                return false;
            }

            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            if (position + 2 > data.Length)
            {
                return false;
            }

            var segmentLength = (data[position] << 8) | data[position + 1];
            position += 2;
            if (segmentLength < 2)
            {
                return false;
            }

            var payloadLength = segmentLength - 2;
            if (position + payloadLength > data.Length)
            {
                return false;
            }

            if (IsStartOfFrame(marker))
            {
                if (payloadLength < 5)
                {
                    return false;
                }

                height = (data[position + 1] << 8) | data[position + 2];
                width = (data[position + 3] << 8) | data[position + 4];
                return width > 0 && height > 0;
            }

            position += payloadLength;
        }

        return false;
    }

    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xC3 or
            >= 0xC5 and <= 0xC7 or
            >= 0xC9 and <= 0xCB or
            >= 0xCD and <= 0xCF;
}
