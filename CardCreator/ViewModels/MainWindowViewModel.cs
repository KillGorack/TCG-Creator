using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using CardCreator.Data;
using CardCreator.Models;

namespace CardCreator.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public MainWindowViewModel()
    {
        RefreshCardList();
        NewCard();
    }

    public string[] FrameColors { get; } = [
        "red", "blue", "green", "gold", "purple", "black", "white",
        "silver", "bronze", "crimson", "teal", "orange", "pink",
        "navy", "forest", "copper", "ice", "obsidian", "bone", "toxic"
    ];

    private ObservableCollection<Card> _cards = new();
    public ObservableCollection<Card> Cards
    {
        get => _cards;
        set => Set(ref _cards, value);
    }

    private Card? _currentCard;
    public Card? CurrentCard
    {
        get => _currentCard;
        set => Set(ref _currentCard, value);
    }

    private Card? _selectedCard;
    public Card? SelectedCard
    {
        get => _selectedCard;
        set => Set(ref _selectedCard, value);
    }

    private string _selectedCardId = "";
    public string SelectedCardId
    {
        get => _selectedCardId;
        set => Set(ref _selectedCardId, value);
    }

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        set => Set(ref _errorMessage, value);
    }

    private bool _isDirty = false;
    private bool _isLoading = false;
    public bool IsDirty
    {
        get => _isDirty;
        set => Set(ref _isDirty, value);
    }

    private void MarkDirty()
    {
        if (!_isLoading)
            IsDirty = true;
    }

    public bool ShowCost         => !string.IsNullOrEmpty(_cost);
    public bool ShowTitle        => !string.IsNullOrEmpty(_title);
    public bool ShowTopRight     => !string.IsNullOrEmpty(_topRight);
    public bool ShowNameBar      => !string.IsNullOrEmpty(_nameBar);
    public bool ShowBottomLeft   => !string.IsNullOrEmpty(_bottomLeft);
    public bool ShowBottomCenter => !string.IsNullOrEmpty(_bottomCenter);
    public bool ShowBottomRight  => !string.IsNullOrEmpty(_bottomRight);

    private string _cost          = "";
    private string _title         = "";
    private string _topRight      = "";
    private string _nameBar       = "";
    private string _bottomLeft    = "";
    private string _bottomCenter  = "";
    private string _bottomRight   = "";
    private string _bodyText      = "";
    private string _frameColor    = "silver";

    public string Cost
    {
        get => _cost;
        set { Set(ref _cost, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCost))); MarkDirty(); }
    }

    public string Title
    {
        get => _title;
        set { Set(ref _title, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowTitle))); MarkDirty(); }
    }

    public string TopRight     { get => _topRight;     set { Set(ref _topRight, value);     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowTopRight))); MarkDirty(); } }
    public string NameBar      { get => _nameBar;      set { Set(ref _nameBar, value);      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowNameBar))); MarkDirty(); } }
    public string BottomLeft   { get => _bottomLeft;   set { Set(ref _bottomLeft, value);   PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowBottomLeft))); MarkDirty(); } }
    public string BottomCenter { get => _bottomCenter; set { Set(ref _bottomCenter, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowBottomCenter))); MarkDirty(); } }
    public string BottomRight  { get => _bottomRight;  set { Set(ref _bottomRight, value);  PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowBottomRight))); MarkDirty(); } }
    public string BodyText     { get => _bodyText;     set { Set(ref _bodyText, value); MarkDirty(); } }

    public string FrameColor
    {
        get => _frameColor;
        set { Set(ref _frameColor, value); LoadFrameBitmaps(); MarkDirty(); }
    }

    // Frame bitmaps
    private Bitmap? _baseFrameBitmap;
    private Bitmap? _costBitmap;
    private Bitmap? _titleBitmap;
    private Bitmap? _topRightBitmap;
    private Bitmap? _nameBarBitmap;
    private Bitmap? _bottomLeftBitmap;
    private Bitmap? _bottomCenterBitmap;
    private Bitmap? _bottomRightBitmap;

    public Bitmap? BaseFrameBitmap   { get => _baseFrameBitmap;   set => Set(ref _baseFrameBitmap, value); }
    public Bitmap? CostBitmap        { get => _costBitmap;        set => Set(ref _costBitmap, value); }
    public Bitmap? TitleBitmap       { get => _titleBitmap;       set => Set(ref _titleBitmap, value); }
    public Bitmap? TopRightBitmap    { get => _topRightBitmap;    set => Set(ref _topRightBitmap, value); }
    public Bitmap? NameBarBitmap     { get => _nameBarBitmap;     set => Set(ref _nameBarBitmap, value); }
    public Bitmap? BottomLeftBitmap  { get => _bottomLeftBitmap;  set => Set(ref _bottomLeftBitmap, value); }
    public Bitmap? BottomCenterBitmap{ get => _bottomCenterBitmap;set => Set(ref _bottomCenterBitmap, value); }
    public Bitmap? BottomRightBitmap { get => _bottomRightBitmap; set => Set(ref _bottomRightBitmap, value); }

    private void LoadFrameBitmaps()
    {
        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "frames", _frameColor);
        BaseFrameBitmap    = Load(System.IO.Path.Combine(dir, $"{_frameColor}_base.png"));
        CostBitmap         = Load(System.IO.Path.Combine(dir, "cost.png"));
        TitleBitmap        = Load(System.IO.Path.Combine(dir, "title.png"));
        TopRightBitmap     = Load(System.IO.Path.Combine(dir, "top_right.png"));
        NameBarBitmap      = Load(System.IO.Path.Combine(dir, "name_bar.png"));
        BottomLeftBitmap   = Load(System.IO.Path.Combine(dir, "bottom_left.png"));
        BottomCenterBitmap = Load(System.IO.Path.Combine(dir, "bottom_center.png"));
        BottomRightBitmap  = Load(System.IO.Path.Combine(dir, "bottom_right.png"));

    }

    private static Bitmap? Load(string path)
    {
        try { return new Bitmap(path); }
        catch { return null; }
    }

    private string _bgColor = "#000000";
    public string BgColor { get => _bgColor; set { Set(ref _bgColor, value); OnBgColorChanged(); MarkDirty(); } }

    private void OnBgColorChanged()
    {
        if (Avalonia.Media.Color.TryParse(_bgColor, out var color))
            BgBrush = new Avalonia.Media.SolidColorBrush(color);
    }

    private Avalonia.Media.SolidColorBrush _bgBrush = new(Avalonia.Media.Colors.Black);
    public Avalonia.Media.SolidColorBrush BgBrush { get => _bgBrush; set => Set(ref _bgBrush, value); }

    public void RefreshCardList()
    {
        var cards = CardRepository.ReadAll();
        Cards.Clear();
        foreach (var card in cards)
            Cards.Add(card);
    }

    public void NewCard()
    {
        CurrentCard = new Card();
        SelectedCard = null;
        LoadCardIntoEditor(CurrentCard);
        SelectedCardId = "";
        IsDirty = false;
    }

    public void LoadCard(Card card)
    {
        CurrentCard = card;
        SelectedCard = card;
        LoadCardIntoEditor(card);
        SelectedCardId = card.Id.ToString();
        IsDirty = false;
    }

    public void SaveCard()
    {
        if (CurrentCard == null) return;

        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Card name (Top Center) is required!";
            return;
        }

        ErrorMessage = "";

        CurrentCard.Cost         = Cost;
        CurrentCard.Title        = Title;
        CurrentCard.TopRight     = TopRight;
        CurrentCard.NameBar      = NameBar;
        CurrentCard.BottomLeft   = BottomLeft;
        CurrentCard.BottomCenter = BottomCenter;
        CurrentCard.BottomRight  = BottomRight;
        CurrentCard.BodyText     = BodyText;
        CurrentCard.BgColor      = BgColor;
        CurrentCard.FrameColor   = FrameColor;

        if (CurrentCard.Id == 0)
            CurrentCard.Id = CardRepository.Create(CurrentCard);
        else
            CardRepository.Update(CurrentCard);

        RefreshCardList();
        IsDirty = false;
    }

    public void DeleteCard(int cardId)
    {
        CardRepository.Delete(cardId);
        RefreshCardList();
        NewCard();
    }

    private void LoadCardIntoEditor(Card card)
    {
        if (card == null) return;

        _isLoading = true;
        Cost         = card.Cost         ?? "";
        Title        = card.Title        ?? "";
        TopRight     = card.TopRight     ?? "";
        NameBar      = card.NameBar      ?? "";
        BottomLeft   = card.BottomLeft   ?? "";
        BottomCenter = card.BottomCenter ?? "";
        BottomRight  = card.BottomRight  ?? "";
        BodyText     = card.BodyText     ?? "";
        BgColor      = card.BgColor      ?? "#000000";
        FrameColor = card.FrameColor ?? "silver";
        _isLoading   = false;

        LoadFrameBitmaps();
    }

    public void SetCardImagePath(string? path)
    {
        if (CurrentCard != null)
            CurrentCard.ImagePath = path;
    }
}