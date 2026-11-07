namespace CardCreator.Models;

public class Card
{
    public int Id { get; set; }
    
    // Text fields
    public string Cost { get; set; } = "";
    public string Title { get; set; } = "";
    public string TopRight { get; set; } = "";
    public string NameBar { get; set; } = "";
    public string BottomLeft { get; set; } = "";
    public string BottomCenter { get; set; } = "";
    public string BottomRight { get; set; } = "";
    public string BodyText { get; set; } = "";
    
    // Display properties
    public string BgColor { get; set; } = "#000000";
    public string? ImagePath { get; set; }
    public string FrameColor { get; set; } = "white";
}
