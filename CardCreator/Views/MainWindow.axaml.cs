using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CardCreator.ViewModels;
using CardCreator.Data;

namespace CardCreator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        DataContext = viewModel;

        if (viewModel.CurrentCard != null && viewModel.CurrentCard.Id != 0)
        {
            LoadCardArtOrPlaceholder(viewModel.CurrentCard.Id);
        }
    }

    private async void ShowUnsavedChangesDialog(Action onDiscard, Action? onCancel = null)
    {
        var confirmed = await ConfirmDialog.ShowAsync(this, "Unsaved Changes", "Discard unsaved changes and continue?");
        if (confirmed)
        {
            onDiscard();
        }
        else
        {
            onCancel?.Invoke();
        }
    }

    private async void LoadArt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Art Image",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] }]
        });

        if (files.Count == 0) return;

        await using var stream = await files[0].OpenReadAsync();
        var bitmap = new Bitmap(stream);
        var artImage = this.FindControl<Image>("ArtImage")!;
        artImage.Source = bitmap;

        // Save the image to the art folder
        if (DataContext is MainWindowViewModel vm && vm.CurrentCard != null)
        {
            ImageManager.SaveCardArt(vm.CurrentCard.Id, files[0].Path.LocalPath);
        }
    }

    private void NewCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.IsDirty)
            {
                ShowUnsavedChangesDialog(() =>
                {
                    var cardListBox = this.FindControl<ListBox>("CardListBox")!;
                    cardListBox.SelectedIndex = -1;
                    vm.NewCard();
                    LoadPlaceholder();
                });
            }
            else
            {
                var cardListBox = this.FindControl<ListBox>("CardListBox")!;
                cardListBox.SelectedIndex = -1;
                vm.NewCard();
                LoadPlaceholder();
            }
        }
    }

    private void SaveCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SaveCard();
        }
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Concat(name.Split(invalid)).Replace(" ", "_").Trim('_');
    }

    private async Task ExportCardImage()
    {
        var suggestedFileName = "card";
        
        if (DataContext is MainWindowViewModel vm && !string.IsNullOrWhiteSpace(vm.Title))
        {
            suggestedFileName = SanitizeFilename(vm.Title);
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Card Image",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }]
        });

        if (file is null) return;

        var canvas = this.FindControl<Canvas>("CardCanvas")!;
        var bitmap = new RenderTargetBitmap(
            new Avalonia.PixelSize((int)canvas.Width, (int)canvas.Height));

        bitmap.Render(canvas);

        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }

    private async void DeleteCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.CurrentCard != null)
        {
            var confirmed = await ConfirmDialog.ShowAsync(this, "Delete Card", "Are you sure you want to delete this card?");
            if (confirmed)
            {
                // Delete the card art as well
                ImageManager.DeleteCardArt(vm.CurrentCard.Id);
                vm.DeleteCard(vm.CurrentCard.Id);
            }
        }
    }

    private void CardListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.CurrentCard != null)
        {
            if (vm.IsDirty)
            {
                // Show confirmation dialog
                var card = vm.CurrentCard;
                ShowUnsavedChangesDialog(() =>
                {
                    vm.LoadCard(card);
                    LoadCardArtOrPlaceholder(card.Id);
                });
            }
            else
            {
                vm.LoadCard(vm.CurrentCard);
                LoadCardArtOrPlaceholder(vm.CurrentCard.Id);
            }
        }
    }

    private void CardListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedCard != null)
        {
            if (vm.SelectedCard == vm.CurrentCard)
                return;

            if (vm.IsDirty)
            {
                var selectedCard = vm.SelectedCard;
                ShowUnsavedChangesDialog(() =>
                {
                    vm.LoadCard(selectedCard);
                    LoadCardArtOrPlaceholder(selectedCard.Id);
                }, () =>
                {
                    var cardListBox = this.FindControl<ListBox>("CardListBox")!;
                    cardListBox.SelectedItem = vm.CurrentCard;
                });
            }
            else
            {
                vm.LoadCard(vm.SelectedCard);
                LoadCardArtOrPlaceholder(vm.SelectedCard.Id);
            }
        }
    }

    private void LoadCardArtOrPlaceholder(int cardId)
    {
        if (!LoadCardArt(cardId))
        {
            LoadPlaceholder();
        }
    }

    private void LoadPlaceholder()
    {
        try
        {
            var bitmap = new Bitmap("Assets/art_placeholder.png");
            var artImage = this.FindControl<Image>("ArtImage")!;
            artImage.Source = bitmap;
        }
        catch
        {
            // If placeholder fails to load, just skip it
        }
    }

    private bool LoadCardArt(int cardId)
    {
        if (!ImageManager.CardArtExists(cardId))
            return false;

        try
        {
            var imagePath = ImageManager.GetCardArtPath(cardId);
            var bitmap = new Bitmap(imagePath);
            var artImage = this.FindControl<Image>("ArtImage")!;
            artImage.Source = bitmap;
            return true;
        }
        catch
        {
            // If image fails to load, just skip it
            return false;
        }
    }

    private async void ExportCardImage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ExportCardImage();
    }
}
