using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CardCreator.Util;

namespace CardCreator.Data;

/// <summary>One recolored frame: the back, and one small picture per card zone (cut from the "cuts" layer).</summary>
public sealed class TintedFrame : IDisposable
{
    public Bitmap Back { get; init; } = null!;
    public Dictionary<string, Bitmap> Pieces { get; init; } = new();
    public void Dispose()
    {
        Back.Dispose();
        foreach (var p in Pieces.Values) p.Dispose();
    }
}

/// <summary>A frame set: four renders (back and cuts, each with a white and a black base color) plus the list of
/// named colors. Any color, named or not, is rebuilt from them (see <see cref="FrameTint"/>).
/// Folder layout: set.json, back_white.png, back_black.png, cuts_white.png, cuts_black.png.</summary>
public sealed class FrameSet
{
    sealed record ColorEntry(string Name, float R, float G, float B);

    readonly Dictionary<string, ColorEntry> _colors;
    readonly byte[] _backWhite, _backBlack, _cutsWhite, _cutsBlack;
    readonly int[] _backBand, _cutsBand;
    readonly byte[] _scratch;
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<string> ColorNames { get; }
    readonly Dictionary<string, string> _legacy;

    FrameSet(string name, List<ColorEntry> colors, Dictionary<string, string> legacy, byte[] bw, byte[] bk, byte[] cw, byte[] ck, int w, int h)
    {
        Name = name;
        _colors = colors.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        ColorNames = colors.Select(c => c.Name).ToList();
        _legacy = legacy;
        (_backWhite, _backBlack, _cutsWhite, _cutsBlack, Width, Height) = (bw, bk, cw, ck, w, h);
        _backBand = FrameTint.FindBand(bw, bk);
        _cutsBand = FrameTint.FindBand(cw, ck);
        _scratch = new byte[w * h * 4];
    }

    public static FrameSet Load(string dir)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "set.json")));
        var root = doc.RootElement;
        var colors = root.GetProperty("colors").EnumerateArray()
            .Select(c => { var rgb = c.GetProperty("linearRgb"); return new ColorEntry(c.GetProperty("name").GetString()!, rgb[0].GetSingle(), rgb[1].GetSingle(), rgb[2].GetSingle()); })
            .ToList();
        var legacy = root.TryGetProperty("legacyColors", out var l)
            ? l.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>();
        var (bw, w, h) = ReadPixels(Path.Combine(dir, "back_white.png"));
        var (bk, _, _) = ReadPixels(Path.Combine(dir, "back_black.png"));
        var (cw, _, _) = ReadPixels(Path.Combine(dir, "cuts_white.png"));
        var (ck, _, _) = ReadPixels(Path.Combine(dir, "cuts_black.png"));
        return new FrameSet(root.GetProperty("name").GetString() ?? "Frames", colors, legacy, bw, bk, cw, ck, w, h);
    }

    /// <summary>The name to use for a color a card was saved with: itself if this set has it, a nearby color if it
    /// came from an older frame set (silver, gold, ...), otherwise the first color.</summary>
    public string Resolve(string? name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            if (_colors.TryGetValue(name, out var c)) return c.Name;
            if (_legacy.TryGetValue(name, out var to) && _colors.ContainsKey(to)) return _colors[to].Name;
        }
        return ColorNames[0];
    }

    /// <summary>Paint the frame in a color (a name from this set, or any linear RGB) and cut the zone pieces out.</summary>
    public TintedFrame Tint(string colorName, IEnumerable<(string Key, int X, int Y, int W, int H)> pieces)
    {
        var c = _colors[Resolve(colorName)];
        return Tint((c.R, c.G, c.B), pieces);
    }

    public TintedFrame Tint((float R, float G, float B) linear, IEnumerable<(string Key, int X, int Y, int W, int H)> pieces)
    {
        FrameTint.Apply(_backWhite, _backBlack, _backBand, linear.R, linear.G, linear.B, _scratch);
        var back = ToBitmap(_scratch, Width, Height);
        FrameTint.Apply(_cutsWhite, _cutsBlack, _cutsBand, linear.R, linear.G, linear.B, _scratch);
        var result = new TintedFrame { Back = back };
        foreach (var (key, x, y, w, h) in pieces)
        {
            var crop = new byte[w * h * 4];
            for (int row = 0; row < h; row++)
                Buffer.BlockCopy(_scratch, ((y + row) * Width + x) * 4, crop, row * w * 4, w * 4);
            result.Pieces[key] = ToBitmap(crop, w, h);
        }
        return result;
    }

    // ---- pixels <-> bitmaps ---------------------------------------------------
    static (byte[] Pixels, int W, int H) ReadPixels(string path)
    {
        using var bmp = new Bitmap(path);
        var size = bmp.PixelSize;
        if (bmp.Format != PixelFormat.Bgra8888 || bmp.AlphaFormat != AlphaFormat.Premul)
            throw new InvalidOperationException($"{Path.GetFileName(path)} decoded as {bmp.Format}/{bmp.AlphaFormat}; expected Bgra8888 premultiplied.");
        var buf = new byte[size.Width * size.Height * 4];
        var handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
        try { bmp.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buf.Length, size.Width * 4); }
        finally { handle.Free(); }
        return (buf, size.Width, size.Height);
    }

    static Bitmap ToBitmap(byte[] pixels, int w, int h)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, handle.AddrOfPinnedObject(), new PixelSize(w, h), new Vector(96, 96), w * 4); }
        finally { handle.Free(); }
    }
}
