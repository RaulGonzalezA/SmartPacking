namespace SmartPacking.Mobile;

internal static class JpegExifOrientationReader
{
    public static int Read(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return 1;
        }

        var position = 2;
        while (position + 3 < jpeg.Length)
        {
            if (jpeg[position] != 0xFF)
            {
                position++;
                continue;
            }

            while (position < jpeg.Length && jpeg[position] == 0xFF)
            {
                position++;
            }

            if (position >= jpeg.Length)
            {
                break;
            }

            var marker = jpeg[position++];
            if (marker is 0xD9 or 0xDA)
            {
                break;
            }

            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            if (position + 2 > jpeg.Length)
            {
                break;
            }

            var segmentLength = (jpeg[position] << 8) | jpeg[position + 1];
            position += 2;
            if (segmentLength < 2)
            {
                break;
            }

            var payloadLength = segmentLength - 2;
            if (position + payloadLength > jpeg.Length)
            {
                break;
            }

            if (marker == 0xE1 && TryReadExifOrientation(jpeg.Slice(position, payloadLength), out var orientation))
            {
                return orientation;
            }

            position += payloadLength;
        }

        return 1;
    }

    private static bool TryReadExifOrientation(ReadOnlySpan<byte> payload, out int orientation)
    {
        orientation = 1;
        if (payload.Length < 14 ||
            payload[0] != (byte)'E' ||
            payload[1] != (byte)'x' ||
            payload[2] != (byte)'i' ||
            payload[3] != (byte)'f' ||
            payload[4] != 0 ||
            payload[5] != 0)
        {
            return false;
        }

        var tiff = payload[6..];
        var littleEndian = tiff[0] == (byte)'I' && tiff[1] == (byte)'I';
        var bigEndian = tiff[0] == (byte)'M' && tiff[1] == (byte)'M';
        if ((!littleEndian && !bigEndian) || ReadUInt16(tiff, 2, littleEndian) != 42)
        {
            return false;
        }

        var ifdOffsetValue = ReadUInt32(tiff, 4, littleEndian);
        if (ifdOffsetValue > int.MaxValue)
        {
            return false;
        }

        var ifdOffset = (int)ifdOffsetValue;
        if (ifdOffset < 0 || ifdOffset + 2 > tiff.Length)
        {
            return false;
        }

        var entryCount = ReadUInt16(tiff, ifdOffset, littleEndian);
        for (var index = 0; index < entryCount; index++)
        {
            var entryOffset = ifdOffset + 2 + (index * 12);
            if (entryOffset + 12 > tiff.Length)
            {
                return false;
            }

            var tag = ReadUInt16(tiff, entryOffset, littleEndian);
            if (tag != 0x0112)
            {
                continue;
            }

            var type = ReadUInt16(tiff, entryOffset + 2, littleEndian);
            var count = ReadUInt32(tiff, entryOffset + 4, littleEndian);
            if (type != 3 || count == 0)
            {
                return false;
            }

            var value = ReadUInt16(tiff, entryOffset + 8, littleEndian);
            if (value is >= 1 and <= 8)
            {
                orientation = value;
                return true;
            }

            return false;
        }

        return false;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian) =>
        littleEndian
            ? (ushort)(data[offset] | (data[offset + 1] << 8))
            : (ushort)((data[offset] << 8) | data[offset + 1]);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian) =>
        littleEndian
            ? (uint)data[offset] |
              ((uint)data[offset + 1] << 8) |
              ((uint)data[offset + 2] << 16) |
              ((uint)data[offset + 3] << 24)
            : ((uint)data[offset] << 24) |
              ((uint)data[offset + 1] << 16) |
              ((uint)data[offset + 2] << 8) |
              data[offset + 3];
}
