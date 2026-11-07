using System;

namespace CardCreator.Util;

/// <summary>Recolors a rendered frame without rendering it again.
/// A frame set holds two renders of the same scene: one with a white base color and one with black. For a
/// material like this one the picture is a straight line between them in linear light:
///     result = black + color * (white - black)
/// so any color is one multiply per pixel, and only the pixels where the two renders differ (the painted band)
/// need computing. Pixels are premultiplied BGRA bytes, as Avalonia hands them over.</summary>
public static class FrameTint
{
    static readonly float[] ToLinear = BuildToLinear();
    static readonly byte[] ToSrgb = BuildToSrgb();     // index = linear * 4096

    static float[] BuildToLinear()
    {
        var t = new float[256];
        for (int i = 0; i < 256; i++)
        {
            double c = i / 255.0;
            t[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
        }
        return t;
    }

    static byte[] BuildToSrgb()
    {
        var t = new byte[4097];
        for (int i = 0; i <= 4096; i++)
        {
            double l = i / 4096.0;
            double c = l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055;
            t[i] = (byte)Math.Clamp((int)Math.Round(c * 255), 0, 255);
        }
        return t;
    }

    /// <summary>Indexes (in pixels) where the white and black renders differ: the only pixels a color can change.</summary>
    public static int[] FindBand(byte[] white, byte[] black)
    {
        var list = new System.Collections.Generic.List<int>(white.Length / 16);
        for (int p = 0, o = 0; o < white.Length; p++, o += 4)
            if (white[o] != black[o] || white[o + 1] != black[o + 1] || white[o + 2] != black[o + 2]) list.Add(p);
        return list.ToArray();
    }

    /// <summary>Fill <paramref name="dest"/> (same size as the inputs) with the frame painted in the given linear RGB color.</summary>
    public static void Apply(byte[] white, byte[] black, int[] band, float r, float g, float b, byte[] dest)
    {
        Buffer.BlockCopy(white, 0, dest, 0, white.Length);          // everything the color does not touch is the same in both renders
        Span<float> color = stackalloc float[3] { b, g, r };         // BGRA order
        foreach (int p in band)
        {
            int o = p * 4, a = white[o + 3];
            if (a == 0) continue;
            for (int ch = 0; ch < 3; ch++)
            {
                // undo premultiplication, blend in linear light, premultiply again
                int w = Math.Min(255, (white[o + ch] * 255 + a / 2) / a);
                int k = Math.Min(255, (black[o + ch] * 255 + a / 2) / a);
                float lk = ToLinear[k];
                float lin = lk + color[ch] * (ToLinear[w] - lk);
                int s = ToSrgb[(int)(Math.Clamp(lin, 0f, 1f) * 4096 + 0.5f)];
                dest[o + ch] = (byte)((s * a + 127) / 255);
            }
        }
    }
}
