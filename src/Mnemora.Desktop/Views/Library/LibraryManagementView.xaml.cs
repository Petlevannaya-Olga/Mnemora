using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource is disposed when the WPF view is unloaded.")]
public partial class LibraryManagementView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private Point _dragStartPoint;
    private LibraryManagementOrderItemViewModel? _draggedItem;
    private ItemsControl? _dragSource;
    private FrameworkElement? _draggedContainer;
    private AdornerLayer? _dragAdornerLayer;
    private DragPreviewAdorner? _dragPreviewAdorner;

    public LibraryManagementView()
    {
        InitializeComponent();
    }

    private async void LibraryManagementView_OnLoaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
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
            // View was unloaded while the library was loading.
        }
    }

    private void LibraryManagementView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        ResetDragState();
        CancelLoading();
    }

    private void SectionsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 || e.ViewportHeight <= 0)
        {
            return;
        }

        double remainingDistance = e.ExtentHeight - e.VerticalOffset - e.ViewportHeight;
        double loadingThreshold = Math.Max(2d, e.ViewportHeight * 0.5d);

        if (remainingDistance > loadingThreshold)
        {
            return;
        }

        if (DataContext is LibraryManagementViewModel viewModel &&
            viewModel.LoadNextSimpleSectionPageCommand.CanExecute(null))
        {
            viewModel.LoadNextSimpleSectionPageCommand.Execute(null);
        }
    }

    private void SimpleSectionTableRow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row ||
            row.DataContext is not LibraryManagementSectionViewModel section ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source &&
            FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (viewModel.OpenSimpleSectionCommand.CanExecute(section))
        {
            viewModel.OpenSimpleSectionCommand.Execute(section);
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        DependencyObject? current = source;

        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void OrderList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl itemsControl ||
            e.OriginalSource is not DependencyObject source ||
            !ReferenceEquals(FindNearestItemsControl(source), itemsControl))
        {
            return;
        }

        FrameworkElement? dragHandle = FindDragHandle(source, itemsControl);

        if (dragHandle?.DataContext is not LibraryManagementOrderItemViewModel item)
        {
            ResetDragState();
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _draggedItem = item;
        _dragSource = itemsControl;
        _draggedContainer = FindItemContainer(itemsControl, dragHandle);
    }

    private void OrderList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ItemsControl itemsControl ||
            !ReferenceEquals(itemsControl, _dragSource) ||
            _draggedItem is null ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point currentPosition = e.GetPosition(this);

        if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(typeof(LibraryManagementOrderItemViewModel), _draggedItem);

        ShowDragPreview();

        try
        {
            DragDrop.DoDragDrop(itemsControl, data, DragDropEffects.Move);
        }
        finally
        {
            ResetDragState();
        }
    }

    private void OrderList_OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl itemsControl ||
            !ReferenceEquals(itemsControl, _dragSource) ||
            !e.Data.GetDataPresent(typeof(LibraryManagementOrderItemViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        UpdateDragPreviewPosition(e.GetPosition(this));
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OrderList_OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl itemsControl ||
            !ReferenceEquals(itemsControl, _dragSource) ||
            e.OriginalSource is not DependencyObject source ||
            e.Data.GetData(typeof(LibraryManagementOrderItemViewModel)) is not LibraryManagementOrderItemViewModel draggedItem ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        FrameworkElement? targetContainer = FindItemContainer(itemsControl, source);

        if (targetContainer?.DataContext is not LibraryManagementOrderItemViewModel targetItem)
        {
            return;
        }

        int targetIndex = itemsControl.Items.IndexOf(targetItem);

        if (targetIndex < 0)
        {
            return;
        }

        switch (itemsControl.Tag as string)
        {
            case "Sections":
                viewModel.MoveSection(draggedItem, targetIndex);
                viewModel.SelectedSection = draggedItem;
                break;

            case "Topics":
                viewModel.MoveTopic(draggedItem, targetIndex);
                viewModel.SelectedTopic = draggedItem;
                break;

            case "Materials":
                viewModel.MoveMaterial(draggedItem, targetIndex);
                viewModel.SelectedMaterial = draggedItem;
                break;

            default:
                return;
        }

        ScrollIntoView(itemsControl, draggedItem);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private static ItemsControl? FindNearestItemsControl(DependencyObject? source)
    {
        DependencyObject? current = source;

        while (current is not null)
        {
            if (current is DataGrid dataGrid)
            {
                return dataGrid;
            }

            if (current is ListBox listBox)
            {
                return listBox;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static FrameworkElement? FindDragHandle(
        DependencyObject source,
        ItemsControl owner)
    {
        DependencyObject? current = source;

        while (current is not null && !ReferenceEquals(current, owner))
        {
            if (current is FrameworkElement element &&
                string.Equals(element.Tag as string, "DragHandle", StringComparison.Ordinal))
            {
                return element;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static FrameworkElement? FindItemContainer(
        ItemsControl owner,
        DependencyObject source)
    {
        DependencyObject? current = source;

        while (current is not null && !ReferenceEquals(current, owner))
        {
            if (current is FrameworkElement element &&
                (current is ListBoxItem || current is DataGridRow) &&
                ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(current), owner))
            {
                return element;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static void ScrollIntoView(
        ItemsControl itemsControl,
        LibraryManagementOrderItemViewModel item)
    {
        switch (itemsControl)
        {
            case ListBox listBox:
                listBox.ScrollIntoView(item);
                break;

            case DataGrid dataGrid:
                dataGrid.ScrollIntoView(item);
                break;
        }
    }

    private void CancelLoading()
    {
        CancellationTokenSource? cancellationTokenSource = _loadCancellationTokenSource;
        _loadCancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private void ShowDragPreview()
    {
        if (_draggedContainer is null || _dragPreviewAdorner is not null)
        {
            return;
        }

        _dragAdornerLayer = AdornerLayer.GetAdornerLayer(this);

        if (_dragAdornerLayer is null)
        {
            return;
        }

        _dragPreviewAdorner = new DragPreviewAdorner(this, _draggedContainer);
        _dragPreviewAdorner.UpdatePosition(_dragStartPoint);
        _dragAdornerLayer.Add(_dragPreviewAdorner);
    }

    private void UpdateDragPreviewPosition(Point position)
    {
        _dragPreviewAdorner?.UpdatePosition(position);
    }

    private void HideDragPreview()
    {
        if (_dragPreviewAdorner is not null && _dragAdornerLayer is not null)
        {
            _dragAdornerLayer.Remove(_dragPreviewAdorner);
        }

        _dragPreviewAdorner = null;
        _dragAdornerLayer = null;
    }

    private void ResetDragState()
    {
        HideDragPreview();
        _draggedItem = null;
        _dragSource = null;
        _draggedContainer = null;
    }

    private sealed class DragPreviewAdorner : Adorner
    {
        private const double CursorOffset = 14d;
        private const double MaxPreviewWidth = 520d;

        private readonly VisualBrush _visualBrush;
        private readonly double _previewWidth;
        private readonly double _previewHeight;
        private Point _position;

        public DragPreviewAdorner(UIElement adornedElement, FrameworkElement source)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            _previewWidth = Math.Min(Math.Max(source.ActualWidth, 1d), MaxPreviewWidth);
            _previewHeight = Math.Max(source.ActualHeight, 1d);

            _visualBrush = new VisualBrush(source)
            {
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                Stretch = Stretch.UniformToFill,
            };
        }

        public void UpdatePosition(Point position)
        {
            _position = position;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var previewRect = new Rect(
                _position.X + CursorOffset,
                _position.Y + CursorOffset,
                _previewWidth,
                _previewHeight);

            var backgroundBrush = new SolidColorBrush(Color.FromArgb(242, 255, 255, 255));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(95, 124, 77, 255)), 1d);

            drawingContext.DrawRoundedRectangle(
                backgroundBrush,
                borderPen,
                previewRect,
                8d,
                8d);

            drawingContext.PushOpacity(0.88);
            drawingContext.DrawRectangle(_visualBrush, null, previewRect);
            drawingContext.Pop();
        }
    }
}
