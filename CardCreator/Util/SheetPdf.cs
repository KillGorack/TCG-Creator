using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace CardCreator.Util;

/// <summary>Writes the print sheets as a multi-page PDF at true size: every card exactly 2.5 x 3.5 inches.</summary>
public static class SheetPdf
{
    static double Pt(double mm) => mm / 25.4 * 72;

    /// <param name="cards">One entry per card to print, already repeated for copies. Entries with the same key
    /// share one embedded image, so printing 3 copies does not make the file 3 times bigger.</param>
    public static void Write(string path, SheetLayout layout, IReadOnlyList<(int Key, byte[] Png)> cards)
    {
        if (!layout.Fits) throw new InvalidOperationException("The cards do not fit on this paper.");
        using var doc = new PdfDocument();
        doc.Info.Title = "Card sheets";
        doc.Info.Creator = "CardCreator";

        var images = new Dictionary<int, XImage>();
        var pen = new XPen(XColors.Black, 0.3);
        for (int start = 0; start < cards.Count; start += layout.PerSheet)
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(layout.PageW);
            page.Height = XUnit.FromMillimeter(layout.PageH);
            using var gfx = XGraphics.FromPdfPage(page);
            for (int i = 0; i < layout.PerSheet && start + i < cards.Count; i++)
            {
                var (key, png) = cards[start + i];
                if (!images.TryGetValue(key, out var image))
                    images[key] = image = XImage.FromStream(new MemoryStream(png));
                var (x, y) = layout.Slot(i);
                gfx.DrawImage(image, Pt(x), Pt(y), Pt(layout.CardW), Pt(layout.CardH));
            }
            foreach (var (x1, y1, x2, y2) in layout.Marks())
                gfx.DrawLine(pen, Pt(x1), Pt(y1), Pt(x2), Pt(y2));
        }
        doc.Save(path);
        foreach (var image in images.Values) image.Dispose();
    }
}
