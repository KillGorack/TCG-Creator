using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CardCreator.Models;
using CardCreator.Util;
using CardCreator.ViewModels;

namespace CardCreator.Views;

/// <summary>What the person chose: which cards, how many of each, and the sheet settings.</summary>
public record PrintRequest(List<(Card Card, int Copies)> Items, SheetSettings Settings);

public partial class PrintSheetsWindow : Window
{
    const double Scale = 3;                       // preview pixels per millimetre
    readonly CardFormat _format;
    readonly SheetSettings _s;
    readonly ObservableCollection<PrintItem> _items;
    bool _loading = true;
    int _sheet;

    public PrintSheetsWindow() : this([], CardFormat.Standard, new SheetSettings()) { }    // for the XAML previewer


    PrintSheetsWindow(List<Card> cards, CardFormat format, SheetSettings settings)
    {
        InitializeComponent();
        _format = format;
        _s = settings;
        _items = new ObservableCollection<PrintItem>(cards.Select(c => new PrintItem(c)));
        foreach (var item in _items) item.PropertyChanged += (_, _) => Refresh();
        CardList.ItemsSource = _items;

        PaperBox.ItemsSource = SheetLayout.Papers.Select(p => p.Name).ToList();
        PaperBox.SelectedItem = _s.Paper;
        OrientBox.ItemsSource = new[] { "Portrait", "Landscape" };
        OrientBox.SelectedIndex = _s.Landscape ? 1 : 0;
        UnitsBox.ItemsSource = new[] { "Inches", "Millimetres" };
        UnitsBox.SelectedIndex = _s.Inches ? 0 : 1;
        MarksCheck.IsChecked = _s.CropMarks;
        ShowUnits();

        PaperBox.SelectionChanged += (_, _) => Refresh();
        OrientBox.SelectionChanged += (_, _) => Refresh();
        UnitsBox.SelectionChanged += (_, _) => { if (!_loading) { _s.Inches = UnitsBox.SelectedIndex == 0; ShowUnits(); Refresh(); } };
        foreach (var box in new[] { MarginBox, GutterBox })
        {
            box.TextChanged += (_, _) => Refresh();                  // live: every keystroke, no need to leave the box
            box.LostFocus += (_, _) => ShowUnits();                  // tidy the text (0.250 -> 0.25) once you are done
            box.KeyDown += (_, e) => Step(box, e);
        }
        MarksCheck.IsCheckedChanged += (_, _) => Refresh();
        AllBtn.Click += (_, _) => { foreach (var i in _items) i.Selected = true; };
        NoneBtn.Click += (_, _) => { foreach (var i in _items) i.Selected = false; };
        PrevBtn.Click += (_, _) => { _sheet--; Refresh(); };
        NextBtn.Click += (_, _) => { _sheet++; Refresh(); };
        CancelBtn.Click += (_, _) => Close(null);
        MakeBtn.Click += (_, _) => Make();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(null); };

        _loading = false;
        Refresh();
    }

    public static Task<PrintRequest?> ShowAsync(Window owner, List<Card> cards, CardFormat format) =>
        new PrintSheetsWindow(cards, format, AppSettings.Print).ShowDialog<PrintRequest?>(owner);

    // ---- settings <-> controls ------------------------------------------------
    double Factor => _s.Inches ? 25.4 : 1;

    /// <summary>Show the stored (millimetre) margin and gap in the units chosen. The unit is a quiet suffix inside
    /// the box, so the text you edit is just the number.</summary>
    void ShowUnits()
    {
        _loading = true;
        foreach (var (box, mm) in new[] { (MarginBox, _s.MarginMm), (GutterBox, _s.GutterMm) })
        {
            if (!box.IsFocused || box.Text == null || !TryNumber(box.Text, out _)) box.Text = Format(mm / Factor);
            box.InnerRightContent = new TextBlock { Text = _s.Inches ? "in" : "mm", Classes = { "muted" }, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        }
        _loading = false;
    }

    string Format(double v) => Math.Round(v, 3).ToString(_s.Inches ? "0.###" : "0.#", CultureInfo.InvariantCulture);

    /// <summary>A number typed by a person: accepts a comma or a point as the decimal mark.</summary>
    static bool TryNumber(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0;

    /// <summary>Up / Down arrow keys nudge the number by 0.05 in (or 1 mm).</summary>
    void Step(TextBox box, KeyEventArgs e)
    {
        if (e.Key != Key.Up && e.Key != Key.Down) return;
        if (!TryNumber(box.Text ?? "", out var v)) v = 0;
        v = Math.Max(0, v + (e.Key == Key.Up ? 1 : -1) * (_s.Inches ? 0.05 : 1));
        box.Text = Format(v);
        e.Handled = true;
    }

    // ---- recompute and redraw ---------------------------------------------------
    int Total => _items.Where(i => i.Selected).Sum(i => i.CopyCount);

    void Refresh()
    {
        if (_loading) return;
        // keep reading in the units the boxes currently show
        _s.Paper = PaperBox.SelectedItem as string ?? _s.Paper;
        _s.Landscape = OrientBox.SelectedIndex == 1;
        _s.CropMarks = MarksCheck.IsChecked == true;
        // text that is not a number (an empty box while typing) keeps the last good value
        if (TryNumber(MarginBox.Text ?? "", out var m)) _s.MarginMm = m * Factor;
        if (TryNumber(GutterBox.Text ?? "", out var g)) _s.GutterMm = g * Factor;

        var lay = SheetLayout.Compute(_s, _format.WidthMm, _format.HeightMm);
        int total = Total, sheets = lay.SheetsFor(total);
        _sheet = Math.Clamp(_sheet, 0, Math.Max(0, sheets - 1));

        LayoutText.Text = lay.Fits ? $"{lay.Cols} across × {lay.Rows} down = {lay.PerSheet} cards per sheet" : "";
        WarnText.Text = !lay.Fits ? "The cards do not fit on this paper with these margins. Try a smaller margin or gap, or another paper size."
            : _s.CropMarks && !lay.AllMarksFit ? "There is not enough room for crop marks on the " + MissingSides(lay) + ". Use a smaller margin or gap, or turn crop marks off."
            : "";
        WarnText.Classes.Set("error", !lay.Fits);
        WarnText.Classes.Set("note", lay.Fits);
        SummaryText.Text = !lay.Fits ? "" : total == 0 ? "Choose at least one card." : $"{total} card{(total == 1 ? "" : "s")} on {sheets} sheet{(sheets == 1 ? "" : "s")}";
        SheetText.Text = sheets == 0 ? "" : $"Sheet {_sheet + 1} of {sheets}";
        PrevBtn.IsEnabled = _sheet > 0;
        NextBtn.IsEnabled = _sheet < sheets - 1;
        MakeBtn.IsEnabled = lay.Fits && total > 0;
        Draw(lay);
    }

    static string MissingSides(SheetLayout l)
    {
        var sides = new List<string>();
        if (l.MarkLeft <= 0) sides.Add("left");
        if (l.MarkRight <= 0) sides.Add("right");
        if (l.MarkTop <= 0) sides.Add("top");
        if (l.MarkBottom <= 0) sides.Add("bottom");
        return string.Join(", ", sides);
    }

    static readonly Dictionary<string, string> Tints = new()
    {
        ["red"] = "#d9605a", ["blue"] = "#5a88d9", ["green"] = "#62b373", ["gold"] = "#dcc05a", ["purple"] = "#9c7ad0", ["black"] = "#6a6a74",
        ["white"] = "#e8e8ee", ["silver"] = "#b8bcc6", ["bronze"] = "#c08a52", ["crimson"] = "#bf4465", ["teal"] = "#52b8b8", ["orange"] = "#e39a4c",
        ["pink"] = "#e68db8", ["navy"] = "#5470b0", ["forest"] = "#54905f", ["copper"] = "#c47c58", ["ice"] = "#a9d9ec", ["obsidian"] = "#6d6384",
        ["bone"] = "#d8cfae", ["toxic"] = "#a6dc58",
    };

    void Draw(SheetLayout lay)
    {
        var canvas = PreviewCanvas;
        canvas.Children.Clear();
        canvas.Width = lay.PageW * Scale;
        canvas.Height = lay.PageH * Scale;
        canvas.Children.Add(new Rectangle { Width = canvas.Width, Height = canvas.Height, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.Parse("#8a8a8a")) });
        if (!lay.Fits) return;

        canvas.Children.Add(Place(new Rectangle   // what the printer can reach
        {
            Width = (lay.PageW - 2 * lay.Margin) * Scale, Height = (lay.PageH - 2 * lay.Margin) * Scale,
            Stroke = new SolidColorBrush(Color.Parse("#b8b8c8")), StrokeDashArray = [4, 4],
        }, lay.Margin, lay.Margin));

        var flat = _items.Where(i => i.Selected).SelectMany(i => Enumerable.Repeat(i, i.CopyCount)).ToList();
        for (int slot = 0; slot < lay.PerSheet; slot++)
        {
            var (x, y) = lay.Slot(slot);
            int index = _sheet * lay.PerSheet + slot;
            if (index >= flat.Count)
            {
                canvas.Children.Add(Place(new Rectangle { Width = lay.CardW * Scale, Height = lay.CardH * Scale,
                    Stroke = new SolidColorBrush(Color.Parse("#d0d0d8")), StrokeDashArray = [3, 3] }, x, y));
                continue;
            }
            var card = flat[index].Card;
            var tint = Tints.TryGetValue(card.FrameColor ?? "", out var hex) ? hex : "#cccccc";
            canvas.Children.Add(Place(new Border
            {
                Width = lay.CardW * Scale, Height = lay.CardH * Scale, CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.Parse(tint)), BorderBrush = new SolidColorBrush(Color.Parse("#555555")), BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = flat[index].Title, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 15, FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#1a1a1a")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8),
                },
            }, x, y));
        }
        foreach (var (x1, y1, x2, y2) in lay.Marks())
            canvas.Children.Add(new Line { StartPoint = new Point(x1 * Scale, y1 * Scale), EndPoint = new Point(x2 * Scale, y2 * Scale), Stroke = Brushes.Black, StrokeThickness = 1 });
    }

    static Control Place(Control c, double xMm, double yMm)
    {
        Canvas.SetLeft(c, xMm * Scale);
        Canvas.SetTop(c, yMm * Scale);
        return c;
    }

    void Make()
    {
        Refresh();
        AppSettings.Print = _s;
        var items = _items.Where(i => i.Selected).Select(i => (i.Card, i.CopyCount)).ToList();
        Close(new PrintRequest(items, _s));
    }
}
