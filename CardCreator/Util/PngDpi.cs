namespace CardCreator.Util;

/// <summary>Adds a pHYs chunk to a PNG so print software reads the real resolution (Avalonia's PNG writer
/// does not set one). 600 DPI = 23622 pixels per metre.</summary>
public static class PngDpi
{
    public static byte[] WithDpi(byte[] png, int dpi)
    {
        // signature (8) + IHDR chunk (4 length + 4 type + 13 data + 4 crc = 25) = 33
        const int afterIhdr = 33;
        if (png.Length < afterIhdr || png[0] != 0x89 || png[1] != 0x50) return png;

        int perMetre = (int)Math.Round(dpi / 0.0254);
        var data = new byte[9];
        WriteInt(data, 0, perMetre);
        WriteInt(data, 4, perMetre);
        data[8] = 1;                                   // unit: metre

        var chunk = new byte[4 + 4 + 9 + 4];
        WriteInt(chunk, 0, 9);
        chunk[4] = (byte)'p'; chunk[5] = (byte)'H'; chunk[6] = (byte)'Y'; chunk[7] = (byte)'s';
        data.CopyTo(chunk, 8);
        WriteInt(chunk, 17, (int)Crc(chunk.AsSpan(4, 13)));

        var result = new byte[png.Length + chunk.Length];
        Array.Copy(png, 0, result, 0, afterIhdr);
        chunk.CopyTo(result, afterIhdr);
        Array.Copy(png, afterIhdr, result, afterIhdr + chunk.Length, png.Length - afterIhdr);
        return result;
    }

    static void WriteInt(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    static readonly uint[] Table = Make();
    static uint[] Make()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in bytes) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }
}
