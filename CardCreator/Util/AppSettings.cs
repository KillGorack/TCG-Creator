using System;
using System.IO;
using System.Text.Json;

namespace CardCreator.Util;

/// <summary>How the print sheet is laid out. Sizes are in millimetres whatever units the dialog shows.</summary>
public class SheetSettings
{
    public string Paper { get; set; } = System.Globalization.RegionInfo.CurrentRegion.IsMetric ? "A4" : "Letter";
    public bool Landscape { get; set; } = true;     // landscape fits 4 x 2 on Letter or A4, with room for crop marks
    public double MarginMm { get; set; } = 6.35;     // 0.25 in: the part of the paper a home printer cannot print on
    public double GutterMm { get; set; } = 2.54;     // 0.1 in: the gap between cards (0 = they share a cut line)
    public bool CropMarks { get; set; } = true;
    public bool Inches { get; set; } = !System.Globalization.RegionInfo.CurrentRegion.IsMetric;
}

/// <summary>Remembers the light/dark choice and the print settings next to the card database.</summary>
public static class AppSettings
{
    class Data
    {
        public bool Dark { get; set; } = true;
        public SheetSettings Print { get; set; } = new();
    }

    static string PathFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CardCreator", "settings.json");
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    static Data? _data;

    static Data Load()
    {
        if (_data != null) return _data;
        try { _data = JsonSerializer.Deserialize<Data>(File.ReadAllText(PathFile), Json); }
        catch { /* no file yet, or an old / damaged one: start from the defaults */ }
        return _data ??= new Data();
    }

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!);
            File.WriteAllText(PathFile, JsonSerializer.Serialize(Load(), Json));
        }
        catch { /* a setting that cannot be saved is not worth an error box */ }
    }

    public static bool Dark
    {
        get => Load().Dark;
        set { Load().Dark = value; Save(); }
    }

    public static SheetSettings Print
    {
        get => Load().Print;
        set { Load().Print = value; Save(); }
    }
}
