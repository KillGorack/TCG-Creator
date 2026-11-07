using System;
using CardCreator.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CardCreator.ViewModels;

/// <summary>One row of the print dialog: a card, whether to print it, and how many copies.</summary>
public partial class PrintItem : ObservableObject
{
    public PrintItem(Card card) => Card = card;
    public Card Card { get; }
    public string Title => string.IsNullOrWhiteSpace(Card.Title) ? "(no name)" : Card.Title;
    [ObservableProperty] private bool _selected = true;
    /// <summary>What is typed in the copies box. Read live, so the preview follows every keystroke.</summary>
    [ObservableProperty] private string _copiesText = "1";
    public int CopyCount => int.TryParse(CopiesText, out var c) && c >= 1 ? Math.Min(c, 99) : 1;
}
