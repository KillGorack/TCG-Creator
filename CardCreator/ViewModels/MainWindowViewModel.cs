using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CardCreator.Data;
using CardCreator.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CardCreator.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>The frame set (the rendered frame and its colors). Colors are rebuilt from it on demand.</summary>
    static readonly FrameSet Frames = FrameSet.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "frames", "monolith"));
    public IReadOnlyList<string> FrameColors => Frames.ColorNames;

    public ZoneViewModel[] Zones { get; } = ZoneViewModel.CreateAll();
    public ObservableCollection<Card> Cards { get; } = new();

    [ObservableProperty] private Card? _currentCard;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _bodyText = "";
    [ObservableProperty] private string _frameColor = "white";
    [ObservableProperty] private string _bgColor = "#000000";
    [ObservableProperty] private IBrush _bgBrush = Brushes.Black;
    [ObservableProperty] private Bitmap? _baseFrame;
    TintedFrame? _tinted;                       // the bitmaps currently on the card; freed when the color changes
    [ObservableProperty] private Bitmap? _art;

    bool _loading;
    string? _pendingArtPath;       // art picked for a card that is not saved yet (or replaced): copied on Save
    Bitmap? _placeholder;

    public MainWindowViewModel()
    {
        foreach (var z in Zones)
            z.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ZoneViewModel.Text)) MarkDirty(); };
        RefreshCardList();
        if (Cards.Count > 0) LoadCard(Cards[0]); else NewCard();
    }

    void MarkDirty() { if (!_loading) IsDirty = true; }

    partial void OnBodyTextChanged(string value) => MarkDirty();
    partial void OnFrameColorChanged(string value) { LoadFrames(); MarkDirty(); }
    partial void OnBgColorChanged(string value)
    {
        if (Color.TryParse(value, out var c)) BgBrush = new SolidColorBrush(c);
        MarkDirty();
    }

    // ---- cards -----------------------------------------------------------
    public void RefreshCardList()
    {
        var cards = CardRepository.ReadAll().OrderBy(c => c.Title ?? "", StringComparer.OrdinalIgnoreCase).ToArray();
        Cards.Clear();
        foreach (var c in cards) Cards.Add(c);
    }

    public void NewCard()
    {
        CurrentCard = new Card();
        Fill(CurrentCard);
    }

    public void LoadCard(Card card)
    {
        CurrentCard = card;
        Fill(card);
    }

    void Fill(Card card)
    {
        _loading = true;
        foreach (var z in Zones)
        {
            z.Text = (string?)typeof(Card).GetProperty(z.Key)!.GetValue(card) ?? "";
            z.Error = "";
        }
        BodyText = card.BodyText ?? "";
        BgColor = card.BgColor ?? "#000000";
        FrameColor = Frames.Resolve(card.FrameColor);          // a color from an older frame set (silver, gold...) becomes its nearest match
        _pendingArtPath = null;
        SetArt(card.Id > 0 && ImageManager.CardArtExists(card.Id) ? Load(ImageManager.GetCardArtPath(card.Id)) : null);
        _loading = false;
        if (_tinted == null) LoadFrames();                // first card only: later cards re-tint when their color differs
        IsDirty = false;
    }

    /// <summary>Validate and save. Returns false when something needs fixing (the zone shows the error).</summary>
    public bool Save()
    {
        if (CurrentCard == null) return false;
        var title = Zones.First(z => z.Key == "Title");
        if (string.IsNullOrWhiteSpace(title.Text)) { title.Error = "Give the card a name."; return false; }
        title.Error = "";

        foreach (var z in Zones) typeof(Card).GetProperty(z.Key)!.SetValue(CurrentCard, z.Text);
        CurrentCard.BodyText = BodyText;
        CurrentCard.BgColor = BgColor;
        CurrentCard.FrameColor = FrameColor;

        if (CurrentCard.Id == 0) CurrentCard.Id = CardRepository.Create(CurrentCard);
        else CardRepository.Update(CurrentCard);

        if (_pendingArtPath != null)                     // a new card has its id only now
        {
            ImageManager.SaveCardArt(CurrentCard.Id, _pendingArtPath);
            _pendingArtPath = null;
        }
        RefreshCardList();
        IsDirty = false;
        return true;
    }

    public void DeleteCard(Card card)
    {
        ImageManager.DeleteCardArt(card.Id);
        CardRepository.Delete(card.Id);
        RefreshCardList();
        if (CurrentCard?.Id == card.Id) NewCard();
    }

    // ---- art -------------------------------------------------------------
    public void PickArt(string path)
    {
        var bmp = Load(path);
        if (bmp == null) return;
        _pendingArtPath = path;
        SetArt(bmp);
        MarkDirty();
    }

    void SetArt(Bitmap? bmp)
    {
        var old = Art;
        Art = bmp ?? Placeholder();
        if (old != null && !ReferenceEquals(old, _placeholder)) old.Dispose();
    }

    Bitmap Placeholder() => _placeholder ??= new Bitmap(AssetLoader.Open(new Uri("avares://CardCreator/Assets/art_placeholder.png")));

    // ---- frames ----------------------------------------------------------
    void LoadFrames()
    {
        var next = Frames.Tint(FrameColor, Zones.Select(z => (z.Key, (int)z.FrameX, (int)z.FrameY, (int)z.FrameW, (int)z.FrameH)));
        var old = _tinted;
        _tinted = next;
        BaseFrame = next.Back;
        foreach (var z in Zones) z.Frame = next.Pieces[z.Key];
        old?.Dispose();                                  // the previous color's bitmaps are no longer shown
    }

    static Bitmap? Load(string path)
    {
        try { return new Bitmap(path); }
        catch { return null; }
    }
}
