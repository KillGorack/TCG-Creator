using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCreator.Util;

public record Paper(string Name, double WidthMm, double HeightMm);

/// <summary>Where the cards go on a printed sheet. Pure arithmetic, so the preview and the PDF can never disagree.</summary>
public class SheetLayout
{
    const double Eps = 0.01;

    public static readonly Paper[] Papers =
    {
        new("Letter", 215.9, 279.4), new("Legal", 215.9, 355.6), new("A4", 210, 297), new("A3", 297, 420),
        new("Tabloid (11 x 17 in)", 279.4, 431.8), new("12 x 18 in", 304.8, 457.2),
    };

    public double CardW, CardH;                        // the finished card (from the card format)
    public double PageW, PageH, Margin, Gutter;
    public int Cols, Rows;
    public double OriginX, OriginY;                    // top-left of the card grid on the page (grid is centered)
    public bool CropMarks;
    public double MarkLeft, MarkRight, MarkTop, MarkBottom;   // length of the crop mark in each margin (0 = no room)

    public int PerSheet => Cols * Rows;
    public bool Fits => PerSheet > 0;
    public double GridW => Cols * CardW + Math.Max(0, Cols - 1) * Gutter;
    public double GridH => Rows * CardH + Math.Max(0, Rows - 1) * Gutter;

    public static SheetLayout Compute(SheetSettings s, double cardW, double cardH)
    {
        var paper = Papers.FirstOrDefault(p => p.Name == s.Paper) ?? Papers[0];
        var l = new SheetLayout
        {
            PageW = s.Landscape ? paper.HeightMm : paper.WidthMm,
            PageH = s.Landscape ? paper.WidthMm : paper.HeightMm,
            Margin = Math.Max(0, s.MarginMm),
            Gutter = Math.Max(0, s.GutterMm),
            CropMarks = s.CropMarks,
            CardW = cardW,
            CardH = cardH,
        };
        l.Cols = Math.Max(0, (int)Math.Floor((l.PageW - 2 * l.Margin + l.Gutter + Eps) / (l.CardW + l.Gutter)));
        l.Rows = Math.Max(0, (int)Math.Floor((l.PageH - 2 * l.Margin + l.Gutter + Eps) / (l.CardH + l.Gutter)));
        l.OriginX = (l.PageW - l.GridW) / 2;
        l.OriginY = (l.PageH - l.GridH) / 2;
        if (l.Fits && s.CropMarks)
        {
            l.MarkLeft = Mark(l.OriginX - l.Margin);
            l.MarkRight = Mark(l.PageW - (l.OriginX + l.GridW) - l.Margin);
            l.MarkTop = Mark(l.OriginY - l.Margin);
            l.MarkBottom = Mark(l.PageH - (l.OriginY + l.GridH) - l.Margin);
        }
        return l;
    }

    // a tick starts 1 mm off the card and is up to 3 mm long, but must stay inside the printable area
    static double Mark(double room) => room >= 2 ? Math.Min(3, room - 1) : 0;

    public bool AllMarksFit => MarkLeft > 0 && MarkRight > 0 && MarkTop > 0 && MarkBottom > 0;

    /// <summary>Top-left corner of card slot <paramref name="slot"/> (0-based, left to right then down).</summary>
    public (double X, double Y) Slot(int slot)
    {
        int c = slot % Cols, r = slot / Cols;
        return (OriginX + c * (CardW + Gutter), OriginY + r * (CardH + Gutter));
    }

    /// <summary>Crop mark segments (x1, y1, x2, y2) in mm: ticks in the margins, in line with every card edge.</summary>
    public IEnumerable<(double X1, double Y1, double X2, double Y2)> Marks()
    {
        if (!Fits || !CropMarks) yield break;
        var xs = Enumerable.Range(0, Cols).SelectMany(c => new[] { OriginX + c * (CardW + Gutter), OriginX + c * (CardW + Gutter) + CardW }).Distinct().ToList();
        var ys = Enumerable.Range(0, Rows).SelectMany(r => new[] { OriginY + r * (CardH + Gutter), OriginY + r * (CardH + Gutter) + CardH }).Distinct().ToList();
        const double gap = 1;
        foreach (var x in xs)
        {
            if (MarkTop > 0) yield return (x, OriginY - gap, x, OriginY - gap - MarkTop);
            if (MarkBottom > 0) yield return (x, OriginY + GridH + gap, x, OriginY + GridH + gap + MarkBottom);
        }
        foreach (var y in ys)
        {
            if (MarkLeft > 0) yield return (OriginX - gap, y, OriginX - gap - MarkLeft, y);
            if (MarkRight > 0) yield return (OriginX + GridW + gap, y, OriginX + GridW + gap + MarkRight, y);
        }
    }

    public int SheetsFor(int cards) => PerSheet == 0 ? 0 : (cards + PerSheet - 1) / PerSheet;
}
