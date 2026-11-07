// Dave-Standard for Avalonia (v0.1): theme switch, icon masks, and the shared widgets
// (Section, DataTable, DateEntry, Dialogs). Mirrors python_customtkinter/dave_theme.py. Don't edit per app.
using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

namespace CardCreator.Theme;

/// <summary>Icon name ("trash-2") to the Lucide PNG used as an opacity mask (Icons/*.png).</summary>
public class IconConverter : IValueConverter
{
    static readonly Dictionary<string, Bitmap> Cache = new();
    public static readonly string Assembly = typeof(IconConverter).Assembly.GetName().Name!;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || name.Length == 0) return AvaloniaProperty.UnsetValue;
        if (!Cache.TryGetValue(name, out var bmp))
            Cache[name] = bmp = new Bitmap(AssetLoader.Open(new Uri($"avares://{Assembly}/Icons/{name}.png")));
        return bmp;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public static class ThemeManager
{
    public static bool IsDark => Application.Current!.ActualThemeVariant == ThemeVariant.Dark;
    public static void Apply(bool dark) => Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

    /// <summary>Body text size (Ctrl +/-/0 in apps): the small and heading sizes follow.</summary>
    public static void SetFontSize(double body)
    {
        var r = Application.Current!.Resources;
        r["BodyFontSize"] = Math.Clamp(body, 9, 24);
        r["SmallFontSize"] = Math.Clamp(body, 9, 24) - 2;
        r["HeadingFontSize"] = Math.Clamp(body, 9, 24) + 4;
    }
}

/// <summary>Factory helpers so code-built UI uses the same classes as XAML.</summary>
public static class Ui
{
    public static Button IconButton(string icon, string tip, Action click, bool row = false, bool muted = false)
    {
        var b = new Button { Tag = icon };
        b.Classes.Add("icon");
        if (row) b.Classes.Add("row");
        if (muted) b.Classes.Add("muted");
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    public static TextBlock Text(string text, string? cls = null)
    {
        var t = new TextBlock { Text = text };
        if (cls != null) t.Classes.Add(cls);
        return t;
    }
}

/// <summary>Titled panel. ShowAdd puts a + icon button left of the title (replaces an "Add" button below the
/// content): AddClicked. Header holds anything extra beside the title (a hint).</summary>
public class Section : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Section, string?>(nameof(Title));
    public static readonly StyledProperty<bool> ShowAddProperty = AvaloniaProperty.Register<Section, bool>(nameof(ShowAdd));
    public static readonly StyledProperty<string?> AddTipProperty = AvaloniaProperty.Register<Section, string?>(nameof(AddTip));
    public static readonly StyledProperty<object?> HeaderProperty = AvaloniaProperty.Register<Section, object?>(nameof(Header));
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool ShowAdd { get => GetValue(ShowAddProperty); set => SetValue(ShowAddProperty, value); }
    public string? AddTip { get => GetValue(AddTipProperty); set => SetValue(AddTipProperty, value); }
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public event EventHandler? AddClicked;
    public Button? AddButton { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        AddButton = e.NameScope.Find<Button>("PART_Add");
        if (AddButton != null) AddButton.Click += (_, _) => AddClicked?.Invoke(this, EventArgs.Empty);
    }
}

public enum Skip { None, First, Last }

/// <summary>One table column. Text column: Key = property name on the row object. Icon column: Icon + Click
/// (pencil, trash, move arrows); Skip hides the icon on the first / last row (no up arrow on row 1).</summary>
public class TableColumn
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public double Weight { get; set; } = 1;
    public bool Right { get; set; }
    public string? Icon { get; set; }
    public Action<object>? Click { get; set; }
    public Skip Skip { get; set; }
    public string? Tip { get; set; }
}

/// <summary>Striped, selectable table with per-row icons. Rows are any objects (public properties by column Key).
/// Hover = Rule, selected = Button, double-click = RowOpened. Set Columns, then SetRows().</summary>
public class DataTable : UserControl
{
    public List<TableColumn> Columns { get; } = new();
    public string EmptyText { get; set; } = "Nothing to show.";
    /// <summary>False hides the column titles and the rule under them (a plain list with one obvious column).</summary>
    public bool ShowHeader { get => _headBox.IsVisible; set => _headBox.IsVisible = _rule.IsVisible = value; }
    public object? SelectedItem { get; set; }
    public event Action<object>? RowOpened;
    public event Action<object>? RowSelected;

    readonly Grid _head = new();
    readonly Border _headBox = new() { Padding = new Thickness(0, 5) };
    readonly Border _rule = new() { Height = 1 };
    readonly StackPanel _body = new();
    List<object> _rows = new();
    bool _built;

    public DataTable()
    {
        var root = new DockPanel();
        _headBox.Child = _head;
        _headBox.Classes.Add("tablehead");
        DockPanel.SetDock(_headBox, Dock.Top);
        _rule.Bind(Border.BackgroundProperty, this.GetResourceObservable("RuleBrush"));
        DockPanel.SetDock(_rule, Dock.Top);
        root.Children.Add(_headBox);
        root.Children.Add(_rule);
        root.Children.Add(new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    public void SetRows(IEnumerable rows)
    {
        _rows = rows.Cast<object>().ToList();
        if (SelectedItem != null && !_rows.Contains(SelectedItem)) SelectedItem = null;
        Build();
    }

    void Defs(Grid g)
    {
        g.ColumnDefinitions.Clear();
        foreach (var c in Columns)
            g.ColumnDefinitions.Add(new ColumnDefinition(c.Icon != null ? new GridLength(28) : new GridLength(c.Weight, GridUnitType.Star)));
    }

    static string Value(object row, string key) =>
        row.GetType().GetProperty(key)?.GetValue(row)?.ToString() ?? "";

    void Build()
    {
        if (!_built)
        {
            Defs(_head);
            for (int i = 0; i < Columns.Count; i++)
            {
                var t = new TextBlock { Text = Columns[i].Title, Margin = new Thickness(10, 0), HorizontalAlignment = Columns[i].Right ? HorizontalAlignment.Right : HorizontalAlignment.Left };
                Grid.SetColumn(t, i);
                _head.Children.Add(t);
            }
            _built = true;
        }
        _body.Children.Clear();
        if (_rows.Count == 0)
        {
            var empty = Ui.Text(EmptyText, "muted");
            empty.Margin = new Thickness(10, 20);
            _body.Children.Add(empty);
            return;
        }
        for (int r = 0; r < _rows.Count; r++)
        {
            var item = _rows[r];
            var grid = new Grid();
            Defs(grid);
            for (int i = 0; i < Columns.Count; i++)
            {
                var col = Columns[i];
                Control cell;
                if (col.Icon != null)
                {
                    if ((col.Skip == Skip.First && r == 0) || (col.Skip == Skip.Last && r == _rows.Count - 1)) continue;
                    var b = Ui.IconButton(col.Icon, col.Tip ?? "", () => { Choose(item); col.Click?.Invoke(item); }, row: true);
                    b.HorizontalAlignment = HorizontalAlignment.Center;
                    cell = b;
                }
                else
                {
                    cell = new TextBlock
                    {
                        Text = Value(item, col.Key), Margin = new Thickness(10, 0), TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = col.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    };
                }
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }
            var row = new Border { Child = grid, MinHeight = 28, Cursor = new Cursor(StandardCursorType.Arrow) };
            row.Classes.Add("row");
            if (r % 2 == 1) row.Classes.Add("alt");
            if (ReferenceEquals(item, SelectedItem)) row.Classes.Add("selected");
            row.PointerPressed += (_, _) => Choose(item);
            row.DoubleTapped += (_, _) => { Choose(item); RowOpened?.Invoke(item); };
            _body.Children.Add(row);
        }
    }

    /// <summary>Select a row (or none with null) without raising RowSelected.</summary>
    public void Select(object? item)
    {
        SelectedItem = item;
        foreach (var (child, i) in _body.Children.Select((c, i) => (c, i)))
            if (child is Border b && i < _rows.Count) b.Classes.Set("selected", item != null && ReferenceEquals(_rows[i], item));
    }

    void Choose(object item)
    {
        Select(item);
        RowSelected?.Invoke(item);
    }
}

/// <summary>YYYY-MM-DD entry with a calendar icon opening a small popup (month arrows, today in bold, picked day
/// filled, Today / Clear). Type or pick. Built from Dave tokens, not Fluent's large Calendar.</summary>
public class DateEntry : UserControl
{
    readonly TextBox _box = new() { PlaceholderText = "YYYY-MM-DD" };
    readonly Popup _pop = new() { IsLightDismissEnabled = true, Placement = PlacementMode.BottomEdgeAlignedRight };
    readonly TextBlock _title = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Grid _days = new();
    DateTime _shown = DateTime.Today;
    DateTime? _picked;

    public string Text { get => _box.Text ?? ""; set => _box.Text = value; }
    public event Action? TextChanged;
    /// <summary>The calendar popup's content (for snapshots).</summary>
    public Control PopupContent => (Control)_pop.Child!;

    public DateEntry()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var btn = Ui.IconButton("calendar", "Pick a date", Open, muted: true);
        btn.Width = 28; btn.Height = 28; btn.Margin = new Thickness(4, 0, 0, 0); btn.FontSize = 16;
        Grid.SetColumn(btn, 1);
        grid.Children.Add(_box);
        grid.Children.Add(btn);

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(2, 4, 2, 2) };
        var prev = Ui.IconButton("chevron-left", "Previous month", () => Step(-1), row: true);
        var next = Ui.IconButton("chevron-right", "Next month", () => Step(1), row: true);
        _title.Classes.Add("strong");
        Grid.SetColumn(_title, 1); Grid.SetColumn(next, 2);
        head.Children.Add(prev); head.Children.Add(_title); head.Children.Add(next);

        var today = new Button { Content = "Today" }; today.Classes.Add("link");
        var clear = new Button { Content = "Clear", HorizontalAlignment = HorizontalAlignment.Right }; clear.Classes.Add("link");
        today.Click += (_, _) => Pick(DateTime.Today);
        clear.Click += (_, _) => { _box.Text = ""; _pop.IsOpen = false; };
        var foot = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(6, 2, 6, 6) };
        Grid.SetColumn(clear, 1);
        foot.Children.Add(today); foot.Children.Add(clear);

        for (int i = 0; i < 7; i++) _days.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var panel = new StackPanel { Children = { head, _days, foot }, Margin = new Thickness(6, 0) };
        var box = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = panel };
        box.Bind(Border.BackgroundProperty, this.GetResourceObservable("SurfaceBrush"));
        box.Bind(Border.BorderBrushProperty, this.GetResourceObservable("RuleBrush"));
        _pop.Child = box;
        _pop.PlacementTarget = this;
        grid.Children.Add(_pop);
        _box.TextChanged += (_, _) => TextChanged?.Invoke();
        Content = grid;
    }

    void Pick(DateTime d)
    {
        _box.Text = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _pop.IsOpen = false;
    }

    void Step(int months) { _shown = new DateTime(_shown.Year, _shown.Month, 1).AddMonths(months); Draw(); }

    void Draw()
    {
        _title.Text = _shown.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        _days.Children.Clear();
        _days.RowDefinitions.Clear();
        for (int r = 0; r < 7; r++) _days.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        string[] names = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
        for (int c = 0; c < 7; c++)
        {
            var n = Ui.Text(names[c], "note");
            n.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(n, c);
            _days.Children.Add(n);
        }
        var first = new DateTime(_shown.Year, _shown.Month, 1);
        var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));          // the Monday on or before the 1st
        for (int i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            var t = new TextBlock { Text = d.Day.ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var cell = new Border { Width = 30, Height = 26, Margin = new Thickness(1), Child = t };
            cell.Classes.Add("day");
            if (d.Month != _shown.Month) cell.Classes.Add("other");
            if (d.Date == DateTime.Today) cell.Classes.Add("today");
            if (_picked?.Date == d.Date) cell.Classes.Add("picked");
            cell.PointerPressed += (_, _) => Pick(d);
            Grid.SetColumn(cell, i % 7); Grid.SetRow(cell, 1 + i / 7);
            _days.Children.Add(cell);
        }
    }

    public void Open()
    {
        if (_pop.IsOpen) { _pop.IsOpen = false; return; }
        _picked = DateTime.TryParseExact(Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        _shown = _picked ?? DateTime.Today;
        Draw();
        _pop.IsOpen = true;
    }
}

public static class Dialogs
{
    /// <summary>Raised when a dialog opens (used by the --snapshot check).</summary>
    public static event Action<Window>? Shown;

    static Window Make(string title)
    {
        var w = new Window
        {
            Title = title, SizeToContent = SizeToContent.WidthAndHeight, CanResize = false, MinWidth = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
        };
        w.KeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(null); };
        w.Opened += (_, _) => Shown?.Invoke(w);
        return w;
    }

    static Border Card(Control inner)
    {
        var b = new Border { Padding = new Thickness(14), Margin = new Thickness(10, 10, 10, 0), Child = inner };
        b.Classes.Add("panel");
        return b;
    }

    static StackPanel Buttons(Button cancel, Button ok)
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new Thickness(10) };
        bar.Children.Add(cancel);
        bar.Children.Add(ok);
        return bar;
    }

    /// <summary>One line of text (add / rename). Returns the text, or null if cancelled. The error line's room is
    /// always reserved, so showing it never pushes the buttons out.</summary>
    public static async Task<string?> Prompt(Window owner, string title, string label = "Name", string value = "", string ok = "Save")
    {
        var w = Make(title);
        var box = new TextBox { Text = value, Margin = new Thickness(0, 4, 0, 0) };
        var err = Ui.Text(" ", "error");
        err.Margin = new Thickness(0, 3, 0, 0);
        var okBtn = new Button { Content = ok };
        var cancel = new Button { Content = "Cancel" };
        void Done()
        {
            if (string.IsNullOrWhiteSpace(box.Text)) { err.Text = "Type a name."; return; }
            w.Close(box.Text.Trim());
        }
        okBtn.Click += (_, _) => Done();
        cancel.Click += (_, _) => w.Close(null);
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Done(); };
        var lab = Ui.Text(label, "note");
        w.Content = new StackPanel { Children = { Card(new StackPanel { Children = { lab, box, err } }), Buttons(cancel, okBtn) } };
        w.Opened += (_, _) => box.Focus();
        return await w.ShowDialog<string?>(owner);
    }

    /// <summary>Title + one sentence, Cancel on the left, the action on the right (red if destructive).</summary>
    public static async Task<bool> Confirm(Window owner, string title, string message, string action = "Delete", bool danger = true)
    {
        var w = Make(title);
        var ok = new Button { Content = action };
        if (danger) ok.Classes.Add("danger");
        var cancel = new Button { Content = "Cancel" };
        ok.Click += (_, _) => w.Close(true);
        cancel.Click += (_, _) => w.Close(false);
        var msg = Ui.Text(message, "muted");
        msg.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        msg.MaxWidth = 340;
        w.Content = new StackPanel { Children = { Card(new StackPanel { Spacing = 4, Children = { Ui.Text(title, "section"), msg } }), Buttons(cancel, ok) } };
        return await w.ShowDialog<bool>(owner);
    }

    /// <summary>A small modal status window for a job that runs while it is shown ("Exporting 3 of 21").
    /// It blocks the owner but does not stop your code: update Text, then Close() when done.</summary>
    public static ProgressDialog Progress(Window owner, string title, string text)
    {
        var w = Make(title);
        var label = Ui.Text(text);
        label.MinWidth = 320;
        w.Content = new StackPanel { Children = { Card(label) }, Margin = new Thickness(0, 0, 0, 10) };
        var p = new ProgressDialog(w, label);
        _ = w.ShowDialog(owner);
        return p;
    }
}

public sealed class ProgressDialog
{
    readonly Window _window;
    readonly TextBlock _label;
    bool _done;
    internal ProgressDialog(Window window, TextBlock label)
    {
        _window = window;
        _label = label;
        window.Closing += (_, e) => { if (!_done) e.Cancel = true; };   // no closing it half-way (Esc is ignored too)
    }
    public string Text { get => _label.Text ?? ""; set => _label.Text = value; }
    public void Close() { _done = true; _window.Close(); }
}
