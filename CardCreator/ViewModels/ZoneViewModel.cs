using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CardCreator.ViewModels;

/// <summary>One text zone of the card: where its frame overlay and its text sit on the 1500x2100 canvas.
/// The layout lives here once; the preview and the editor are both generated from it.</summary>
public partial class ZoneViewModel : ObservableObject
{
    public required string Key { get; init; }          // matches a Card property (Cost, Title, ...)
    public required string Label { get; init; }        // shown above the field
    public required double FrameX { get; init; }
    public required double FrameY { get; init; }
    public required double FrameW { get; init; }
    public required double FrameH { get; init; }
    public required double TextX { get; init; }        // text box, in canvas coordinates
    public required double TextY { get; init; }
    public required double TextW { get; init; }
    public required double TextH { get; init; }
    public required double FontSize { get; init; }

    public double TextLeft => TextX - FrameX;          // text box relative to its frame
    public double TextTop => TextY - FrameY;
    public Thickness TextMargin => new(TextLeft, TextTop, 0, 0);

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasText))] private string _text = "";
    [ObservableProperty] private Bitmap? _frame;
    [ObservableProperty] private string _error = "";

    public bool HasText => !string.IsNullOrEmpty(Text);

    public static ZoneViewModel[] CreateAll() =>
    [
        new() { Key = "Cost", Label = "Top left", FrameX = 78, FrameY = 75, FrameW = 235, FrameH = 225, TextX = 98, TextY = 94, TextW = 186, TextH = 187, FontSize = 64 },
        new() { Key = "Title", Label = "Top center (card name)", FrameX = 394, FrameY = 75, FrameW = 730, FrameH = 147, TextX = 411, TextY = 92, TextW = 677, TextH = 109, FontSize = 56 },
        new() { Key = "TopRight", Label = "Top right", FrameX = 1130, FrameY = 75, FrameW = 312, FrameH = 199, TextX = 1150, TextY = 93, TextW = 259, TextH = 150, FontSize = 56 },
        new() { Key = "NameBar", Label = "Middle bar", FrameX = 138, FrameY = 1151, FrameW = 1248, FrameH = 170, TextX = 157, TextY = 1169, TextW = 1189, TextH = 118, FontSize = 64 },
        new() { Key = "BottomLeft", Label = "Bottom left", FrameX = 96, FrameY = 1803, FrameW = 316, FrameH = 212, TextX = 120, TextY = 1826, TextW = 259, TextH = 149, FontSize = 56 },
        new() { Key = "BottomCenter", Label = "Bottom center", FrameX = 432, FrameY = 1845, FrameW = 652, FrameH = 170, TextX = 456, TextY = 1867, TextW = 589, TextH = 107, FontSize = 56 },
        new() { Key = "BottomRight", Label = "Bottom right", FrameX = 1100, FrameY = 1807, FrameW = 321, FrameH = 208, TextX = 1121, TextY = 1826, TextW = 259, TextH = 150, FontSize = 56 },
    ];
}
