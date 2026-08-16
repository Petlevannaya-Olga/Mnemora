using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "CancellationTokenSource is disposed when the WPF view is unloaded.")]
public partial class LibraryManagementView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private Popup? _openSectionMenuPopup;

    public LibraryManagementView()
    {
        InitializeComponent();
    }

    private async void LibraryManagementView_OnLoaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            if (DataContext is LibraryManagementViewModel viewModel)
            {
                await viewModel.LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignore
        }
    }

    private void LibraryManagementView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        CloseSectionMenu();
        CancelLoading();
    }

    private void SectionMenuPopup_OnOpened(object? sender, EventArgs e)
    {
        if (sender is not Popup popup)
        {
            return;
        }

        if (_openSectionMenuPopup is not null && !ReferenceEquals(_openSectionMenuPopup, popup))
        {
            _openSectionMenuPopup.IsOpen = false;
        }

        _openSectionMenuPopup = popup;
        SectionMenuOverlay.Visibility = Visibility.Visible;
    }

    private void SectionMenuPopup_OnClosed(object? sender, EventArgs e)
    {
        if (sender is not Popup popup || !ReferenceEquals(_openSectionMenuPopup, popup))
        {
            return;
        }

        _openSectionMenuPopup = null;
        SectionMenuOverlay.Visibility = Visibility.Collapsed;
    }

    private void SectionMenuOverlay_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CloseSectionMenu();
        e.Handled = true;
    }

    private void CancelLoading()
    {
        var cancellationTokenSource = _loadCancellationTokenSource;
        _loadCancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private void CloseSectionMenu()
    {
        if (_openSectionMenuPopup is not null)
        {
            _openSectionMenuPopup.IsOpen = false;
            _openSectionMenuPopup = null;
        }

        SectionMenuOverlay.Visibility = Visibility.Collapsed;
    }
}