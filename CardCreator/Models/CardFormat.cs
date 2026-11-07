namespace CardCreator.Models;

/// <summary>A card format: the physical size of the finished card and the pixel canvas it is drawn on.
/// Today there is one (the Monolith frames, a standard 2.5 x 3.5 inch card); printing already reads the size from
/// here, so another format with a different size needs no change to the sheet code.</summary>
public record CardFormat(string Name, double WidthMm, double HeightMm, int CanvasW, int CanvasH)
{
    public static readonly CardFormat Standard = new("Monolith (2.5 x 3.5 in)", 63.5, 88.9, 1500, 2100);

    /// <summary>Dots per inch the canvas works out to at the physical size (600 for the standard card).</summary>
    public int Dpi => (int)System.Math.Round(CanvasW / (WidthMm / 25.4));
}
