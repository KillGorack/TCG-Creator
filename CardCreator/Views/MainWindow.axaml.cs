using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CardCreator.Data;
using CardCreator.Models;
using CardCreator.Theme;
using CardCreator.Util;
using CardCreator.ViewModels;

namespace CardCreator.Views;

public partial class MainWindow : Window
{
    static readonly CardFormat Format = CardFormat.Standard;   // the only format so far; sizes and DPI come from it
    readonly MainWindowViewModel _vm = new();
    readonly DispatcherTimer _noteTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        ThemeManager.Apply(AppSettings.Dark);
        ThemeBtn.Tag = AppSettings.Dark ? "sun" : "moon";
        ThemeBtn.Click += (_, _) =>
        {
            ThemeManager.Apply(!ThemeManager.IsDark);
            AppSettings.Dark = ThemeManager.IsDark;
            ThemeBtn.Tag = ThemeManager.IsDark ? "sun" : "moon";
        };

        // card list: the name, and a trash can on every row
        CardsTable.Columns.Add(new TableColumn { Key = nameof(Card.Title), Title = "Name", Weight = 6 });
        CardsTable.Columns.Add(new TableColumn { Icon = "trash-2", Tip = "Delete card", Click = async o => await DeleteCard((Card)o) });
        CardsTable.RowSelected += async o => await OpenCard((Card)o);
        CardsSection.AddClicked += async (_, _) => await NewCard();
        _vm.Cards.CollectionChanged += (_, _) => ShowCards();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentCard)) SyncSelection();
            if (e.PropertyName == nameof(MainWindowViewModel.IsDirty)) ShowDirty();
        };
        ShowCards();
        ShowDirty();

        SaveBtn.Click += (_, _) => Save();
        ArtBtn.Click += async (_, _) => await ChooseArt();
        ExportBtn.Click += async (_, _) => await Export();
        ExportAllBtn.Click += async (_, _) => await ExportAll();
        PrintBtn.Click += async (_, _) => await PrintSheets();
        _noteTimer.Tick += (_, _) => { _noteTimer.Stop(); ShowDirty(); };

        KeyDown += async (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            if (e.Key == Key.S) { Save(); e.Handled = true; }
            else if (e.Key == Key.N) { await NewCard(); e.Handled = true; }
            else if (e.Key == Key.P) { await PrintSheets(); e.Handled = true; }
            else if (e.Key == Key.E) { if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) await ExportAll(); else await Export(); e.Handled = true; }
        };

        Closing += async (_, e) =>
        {
            if (_closeConfirmed || !_vm.IsDirty) return;
            e.Cancel = true;
            if (await Discard("Close CardCreator?"))
            {
                _closeConfirmed = true;
                Close();
            }
        };
    }

    // ---- list and selection --------------------------------------------------
    void ShowCards()
    {
        CardsTable.SetRows(_vm.Cards);
        SyncSelection();
    }

    void SyncSelection()
    {
        var id = _vm.CurrentCard?.Id ?? 0;
        CardsTable.Select(id > 0 ? _vm.Cards.FirstOrDefault(c => c.Id == id) : null);
    }

    async Task<bool> Discard(string title) =>
        !_vm.IsDirty || await Dialogs.Confirm(this, title, "This card has changes that are not saved.", "Discard", danger: true);

    async Task OpenCard(Card card)
    {
        if (card.Id == _vm.CurrentCard?.Id) return;
        if (await Discard("Open another card?")) _vm.LoadCard(card);
        else SyncSelection();                      // put the highlight back on the card still being edited
    }

    async Task NewCard()
    {
        if (await Discard("Start a new card?")) _vm.NewCard();
    }

    async Task DeleteCard(Card card)
    {
        if (!await Dialogs.Confirm(this, $"Delete '{card.Title}'?", "The card and its art are removed for good.")) return;
        _vm.DeleteCard(card);
    }

    // ---- save, art, export ---------------------------------------------------
    void Save()
    {
        Note(_vm.Save() ? "Saved" : "Fix the highlighted field first");
    }

    async Task ChooseArt()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the card's art",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] }],
        });
        if (files.Count == 0) return;
        _vm.PickArt(files[0].Path.LocalPath);
    }

    async Task Export()
    {
        var name = _vm.Zones.First(z => z.Key == "Title").Text;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export card image",
            SuggestedFileName = string.IsNullOrWhiteSpace(name) ? "card" : SafeName(name),
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG image") { Patterns = ["*.png"] }],
        });
        if (file is null) return;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(RenderPng());
        Note($"Exported {Format.CanvasW} x {Format.CanvasH} pixels, {Format.Dpi} DPI");
    }

    /// <summary>The card on screen, drawn at its full size (1500 x 2100) and tagged 600 DPI so print shops get the right size.</summary>
    byte[] RenderPng()
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)CardCanvas.Width, (int)CardCanvas.Height));
        bitmap.Render(CardCanvas);
        using var memory = new MemoryStream();
        bitmap.Save(memory);
        return PngDpi.WithDpi(memory.ToArray(), Format.Dpi);
    }

    /// <summary>Export every saved card into a folder you choose: one PNG per card, named after the card.</summary>
    async Task ExportAll()
    {
        var cards = _vm.Cards.ToList();
        if (cards.Count == 0) { Note("There are no cards to export yet"); return; }
        if (_vm.IsDirty) { Note("Save the open card first, so its changes are included"); return; }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Export all cards to which folder?",
            AllowMultiple = false,
        });
        if (folders.Count == 0 || folders[0].Path.LocalPath is not { } dir) return;

        var targets = PlanTargets(cards, dir);
        int existing = targets.Count(File.Exists);
        if (existing > 0 && !await Dialogs.Confirm(this, "Replace existing files?",
                $"{existing} of these files already exist in that folder and will be replaced.", "Replace"))
            return;

        await ExportCardsTo(cards, targets, dir);
    }

    /// <summary>One file name per card; two cards with the same name get -2, -3 ...</summary>
    static List<string> PlanTargets(List<Card> cards, string dir)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return cards.Select(card =>
        {
            var baseName = SafeName(card.Title);
            if (baseName.Length == 0) baseName = $"card-{card.Id}";
            var name = baseName;
            for (int n = 2; !used.Add(name); n++) name = $"{baseName}-{n}";
            return Path.Combine(dir, name + ".png");
        }).ToList();
    }

    async Task ExportCardsTo(List<Card> cards, List<string> targets, string dir)
    {
        int done = 0;
        var ok = await RenderEach(cards, "Exporting cards", async (i, _, png) =>
        {
            await File.WriteAllBytesAsync(targets[i], png);
            done++;
        });
        if (ok) Note($"Exported {done} cards to {dir}");
    }

    /// <summary>Draw each card in turn on the canvas (the only place a card is laid out) and hand its PNG to
    /// <paramref name="sink"/>. Shows a progress window, and puts the editor back on the card that was open.</summary>
    async Task<bool> RenderEach(List<Card> cards, string title, Func<int, Card, byte[], Task> sink)
    {
        var openId = _vm.CurrentCard?.Id ?? 0;
        var progress = Dialogs.Progress(this, title, "Starting...");
        int done = 0;
        try
        {
            for (int i = 0; i < cards.Count; i++)
            {
                progress.Text = $"Drawing card {i + 1} of {cards.Count}: {cards[i].Title}";
                _vm.LoadCard(cards[i]);                                   // show it on the canvas ...
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);   // ... let it lay out ...
                await sink(i, cards[i], RenderPng());                     // ... and draw it
                done++;
            }
            return true;
        }
        catch (Exception ex)
        {
            Note($"Stopped after {done} of {cards.Count}: {ex.Message}");
            return false;
        }
        finally
        {
            progress.Close();
            var back = _vm.Cards.FirstOrDefault(c => c.Id == openId);     // put the editor back on the card you had open
            if (back != null) _vm.LoadCard(back); else _vm.NewCard();
        }
    }

    /// <summary>Lay cards out on paper (paper size, margin, gap, crop marks, copies) and save a PDF at true size.</summary>
    async Task PrintSheets()
    {
        var cards = _vm.Cards.ToList();
        if (cards.Count == 0) { Note("There are no cards to print yet"); return; }
        if (_vm.IsDirty) { Note("Save the open card first, so its changes are included"); return; }

        var request = await PrintSheetsWindow.ShowAsync(this, cards, Format);
        if (request is null || request.Items.Count == 0) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the print sheets",
            SuggestedFileName = "Card sheets",
            DefaultExtension = "pdf",
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });
        if (file?.Path.LocalPath is not { } path) return;
        await MakePdf(request, path);
    }

    async Task MakePdf(PrintRequest request, string path)
    {
        // draw each chosen card once, then repeat the picture for its copies
        var drawn = new Dictionary<int, byte[]>();
        var unique = request.Items.Select(i => i.Card).ToList();
        if (!await RenderEach(unique, "Preparing the sheets", (_, card, png) => { drawn[card.Id] = png; return Task.CompletedTask; })) return;

        var flat = request.Items.SelectMany(i => Enumerable.Repeat((i.Card.Id, drawn[i.Card.Id]), i.Copies)).ToList();
        var layout = SheetLayout.Compute(request.Settings, Format.WidthMm, Format.HeightMm);
        var progress = Dialogs.Progress(this, "Making the PDF", $"Laying out {flat.Count} cards on {layout.SheetsFor(flat.Count)} sheets...");
        try
        {
            await Task.Run(() => SheetPdf.Write(path, layout, flat));
            Note($"Saved {layout.SheetsFor(flat.Count)} sheet{(layout.SheetsFor(flat.Count) == 1 ? "" : "s")} ({flat.Count} cards) to {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Note($"Could not make the PDF: {ex.Message}"); }
        finally { progress.Close(); }
    }

    static string SafeName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_").Trim('_');

    // ---- the status line -----------------------------------------------------
    void Note(string text)
    {
        NoteText.Text = text;
        NoteText.Classes.Set("error", false);
        _noteTimer.Stop();
        _noteTimer.Start();
    }

    void ShowDirty()
    {
        NoteText.Text = _vm.IsDirty ? "Unsaved changes" : "";
        Title = "CardCreator" + (_vm.IsDirty ? " *" : "");
    }
}
