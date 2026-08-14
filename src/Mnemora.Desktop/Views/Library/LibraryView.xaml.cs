using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

public partial class LibraryView : UserControl
{
    private CancellationTokenSource?
        _loadCancellationTokenSource;

    private Popup?
        _openSectionMenuPopup;

    public LibraryView()
    {
        InitializeComponent();
    }

    private async void LibraryView_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();

        _loadCancellationTokenSource =
            new CancellationTokenSource();

        if (DataContext is LibraryViewModel viewModel)
        {
            await viewModel.LoadAsync(
                _loadCancellationTokenSource.Token);
        }
    }

    private void LibraryView_OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        CloseSectionMenu();

        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();

        _loadCancellationTokenSource =
            null;
    }

    private void SectionMenuPopup_OnOpened(
        object? sender,
        EventArgs e)
    {
        if (sender is not Popup popup)
        {
            return;
        }

        if (_openSectionMenuPopup is not null &&
            !ReferenceEquals(
                _openSectionMenuPopup,
                popup))
        {
            _openSectionMenuPopup.IsOpen =
                false;
        }

        _openSectionMenuPopup =
            popup;

        SectionMenuOverlay.Visibility =
            Visibility.Visible;
    }

    private void SectionMenuPopup_OnClosed(
        object? sender,
        EventArgs e)
    {
        if (sender is not Popup popup ||
            !ReferenceEquals(
                _openSectionMenuPopup,
                popup))
        {
            return;
        }

        _openSectionMenuPopup =
            null;

        SectionMenuOverlay.Visibility =
            Visibility.Collapsed;
    }

    private void SectionMenuOverlay_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        CloseSectionMenu();

        e.Handled =
            true;
    }

    private void CloseSectionMenu()
    {
        if (_openSectionMenuPopup is not null)
        {
            _openSectionMenuPopup.IsOpen =
                false;

            _openSectionMenuPopup =
                null;
        }

        SectionMenuOverlay.Visibility =
            Visibility.Collapsed;
    }
}