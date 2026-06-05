using System.Runtime.CompilerServices;

namespace projectFrameCut.Drawing.Text.FontHelper.Reader;

internal static class BigEndianReader
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(ReadOnlySpan<byte> data, ref int offset)
    {
        if ((uint)offset >= (uint)data.Length)
            ThrowOffsetOutOfRange(offset, data.Length);
        return data[offset++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        if ((uint)(offset + 1) >= (uint)data.Length)
            ThrowOffsetOutOfRange(offset + 1, data.Length);
        ushort value = (ushort)((data[offset] << 8) | data[offset + 1]);
        offset += 2;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16(ReadOnlySpan<byte> data, ref int offset) =>
        (short)ReadUInt16(data, ref offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(ReadOnlySpan<byte> data, ref int offset)
    {
        if ((uint)(offset + 3) >= (uint)data.Length)
            ThrowOffsetOutOfRange(offset + 3, data.Length);
        uint value = (uint)((data[offset] << 24) | (data[offset + 1] << 16)
                          | (data[offset + 2] << 8) | data[offset + 3]);
        offset += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(ReadOnlySpan<byte> data, ref int offset) =>
        (int)ReadUInt32(data, ref offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64(ReadOnlySpan<byte> data, ref int offset)
    {
        uint hi = ReadUInt32(data, ref offset);
        uint lo = ReadUInt32(data, ref offset);
        return ((ulong)hi << 32) | lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64(ReadOnlySpan<byte> data, ref int offset) =>
        (long)ReadUInt64(data, ref offset);

    public static string ReadTag(ReadOnlySpan<byte> data, ref int offset)
    {
        if ((uint)(offset + 3) >= (uint)data.Length)
            ThrowOffsetOutOfRange(offset + 3, data.Length);
        string tag = ((char)data[offset]).ToString()
                    + (char)data[offset + 1]
                    + (char)data[offset + 2]
                    + (char)data[offset + 3];
        offset += 4;
        return tag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadF2Dot14(ReadOnlySpan<byte> data, ref int offset) =>
        ReadInt16(data, ref offset) / 16384.0f;

    /// <summary>
    /// Read a 16.16 signed fixed-point number (int32 / 65536.0).
    /// Used by the 'fvar' table for axis min/default/max values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadFixed(ReadOnlySpan<byte> data, ref int offset) =>
        ReadInt32(data, ref offset) / 65536.0f;

    private static void ThrowOffsetOutOfRange(int offset, int length) =>
        throw new InvalidFontFileException(
            $"Offset {offset} is out of range (data length: {length}).");
}
