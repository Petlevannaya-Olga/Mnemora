using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Mnemora.Application.Library.Order;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

public partial class LibraryOrderDialogWindow : Window
{
    private Point _dragStartPoint;
    private LibraryOrderDialogItem? _draggedItem;
    private DataGridRow? _draggedRow;
    private int _insertionIndex = -1;
    private readonly Guid[] _originalOrder;

    private AdornerLayer? _adornerLayer;
    private DragPreviewAdorner? _dragPreviewAdorner;
    private InsertionIndicatorAdorner? _insertionIndicatorAdorner;

    public LibraryOrderDialogWindow(
        LibraryOrderTarget target,
        IReadOnlyList<LibraryManagementOrderItemViewModel> sourceItems,
        string? contextName = null)
    {
        ArgumentNullException.ThrowIfNull(sourceItems);

        InitializeComponent();

        DialogTitle = target switch
        {
            LibraryOrderTarget.Sections => "Настройка порядка разделов",
            LibraryOrderTarget.Topics => "Настройка порядка тем",
            LibraryOrderTarget.Materials => "Настройка порядка материалов",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        ContextText = target switch
        {
            LibraryOrderTarget.Topics when !string.IsNullOrWhiteSpace(contextName) =>
                $"• Раздел: {contextName}",

            LibraryOrderTarget.Materials when !string.IsNullOrWhiteSpace(contextName) =>
                $"• Тема: {contextName}",

            _ => string.Empty,
        };

        _originalOrder = sourceItems.Select(item => item.Id).ToArray();

        Items = new ObservableCollection<LibraryOrderDialogItem>(
            sourceItems.Select((item, index) =>
                new LibraryOrderDialogItem(
                    item.Id,
                    item.Name,
                    item.IconKind,
                    index + 1)));

        DataContext = this;
    }

    public string DialogTitle { get; }

    public string ContextText { get; }

    public bool HasContext => !string.IsNullOrWhiteSpace(ContextText);

    public ObservableCollection<LibraryOrderDialogItem> Items { get; }

    public IReadOnlyList<Guid> OrderedIds =>
        Items.Select(item => item.Id).ToArray();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!HasOrderChanges())
        {
            return;
        }

        DialogResult = true;
    }

    private void OrderGrid_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            ResetDragState();
            return;
        }

        FrameworkElement? dragHandle = FindDragHandle(source);

        if (dragHandle?.DataContext is not LibraryOrderDialogItem item)
        {
            ResetDragState();
            return;
        }

        _dragStartPoint = e.GetPosition(OrderGrid);
        _draggedItem = item;
        _draggedRow = FindAncestor<DataGridRow>(dragHandle);
    }

    private void OrderGrid_OnPreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_draggedItem is null ||
            _draggedRow is null ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point currentPosition = e.GetPosition(OrderGrid);

        if (Math.Abs(currentPosition.X - _dragStartPoint.X) <
            SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) <
            SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(typeof(LibraryOrderDialogItem), _draggedItem);

        ShowDragPreview();

        try
        {
            DragDrop.DoDragDrop(OrderGrid, data, DragDropEffects.Move);
        }
        finally
        {
            ResetDragState();
        }
    }

    private void OrderGrid_OnDragOver(
        object sender,
        DragEventArgs e)
    {
        if (_draggedItem is null ||
            !e.Data.GetDataPresent(typeof(LibraryOrderDialogItem)))
        {
            e.Effects = DragDropEffects.None;
            HideInsertionIndicator();
            e.Handled = true;
            return;
        }

        UpdateDragPreviewPosition(e.GetPosition(OrderGrid));
        UpdateInsertionIndicator(e);

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OrderGrid_OnDragLeave(
        object sender,
        DragEventArgs e)
    {
        Point position = e.GetPosition(OrderGrid);

        if (position.X < 0 ||
            position.Y < 0 ||
            position.X > OrderGrid.ActualWidth ||
            position.Y > OrderGrid.ActualHeight)
        {
            HideInsertionIndicator();
        }
    }

    private void OrderGrid_OnDrop(
        object sender,
        DragEventArgs e)
    {
        if (_draggedItem is null ||
            e.Data.GetData(typeof(LibraryOrderDialogItem)) is not LibraryOrderDialogItem draggedItem)
        {
            return;
        }

        if (_insertionIndex < 0)
        {
            UpdateInsertionIndicator(e);
        }

        if (_insertionIndex < 0)
        {
            return;
        }

        MoveToInsertionIndex(draggedItem, _insertionIndex);
        OrderGrid.ScrollIntoView(draggedItem);

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void UpdateInsertionIndicator(DragEventArgs e)
    {
        Point position = e.GetPosition(OrderGrid);

        DataGridRow? targetRow =
            e.OriginalSource is DependencyObject source
                ? FindAncestor<DataGridRow>(source)
                : null;

        int insertionIndex;
        double lineY;

        if (targetRow is not null)
        {
            int targetIndex = targetRow.GetIndex();
            Point rowPosition = e.GetPosition(targetRow);
            bool insertAfter = rowPosition.Y >= targetRow.ActualHeight / 2d;

            insertionIndex = targetIndex + (insertAfter ? 1 : 0);
            lineY = targetRow.TranslatePoint(
                new Point(0, insertAfter ? targetRow.ActualHeight : 0),
                OrderGrid).Y;
        }
        else if (position.Y <= 0)
        {
            insertionIndex = 0;
            lineY = 0;
        }
        else
        {
            DataGridRow? lastRealizedRow = GetRealizedRows()
                .OrderBy(row => row.GetIndex())
                .LastOrDefault();

            insertionIndex = Items.Count;

            lineY = lastRealizedRow is null
                ? 0
                : lastRealizedRow.TranslatePoint(
                    new Point(0, lastRealizedRow.ActualHeight),
                    OrderGrid).Y;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, Items.Count);
        _insertionIndex = insertionIndex;

        ShowInsertionIndicator(lineY);
    }

    private IEnumerable<DataGridRow> GetRealizedRows()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (OrderGrid.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row)
            {
                yield return row;
            }
        }
    }

    private void MoveToInsertionIndex(
        LibraryOrderDialogItem item,
        int insertionIndex)
    {
        int sourceIndex = Items.IndexOf(item);

        if (sourceIndex < 0)
        {
            return;
        }

        int targetIndex = Math.Clamp(insertionIndex, 0, Items.Count);

        // insertionIndex описывает промежуток между строками.
        // После удаления исходной строки все промежутки ниже неё сдвигаются на один.
        if (targetIndex > sourceIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Clamp(targetIndex, 0, Items.Count - 1);

        if (targetIndex == sourceIndex)
        {
            return;
        }

        Items.Move(sourceIndex, targetIndex);
        RenumberItems();
        UpdateChangeState();
    }

    private void RenumberItems()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            Items[index].Position = index + 1;
        }
    }

    private bool HasOrderChanges()
    {
        return !_originalOrder.SequenceEqual(Items.Select(item => item.Id));
    }

    private void UpdateChangeState()
    {
        bool hasChanges = HasOrderChanges();
        SaveButton.IsEnabled = hasChanges;
        UnsavedChangesText.Visibility = hasChanges
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static FrameworkElement? FindDragHandle(DependencyObject source)
    {
        DependencyObject? current = source;

        while (current is not null)
        {
            if (current is FrameworkElement element &&
                string.Equals(element.Tag as string, "DragHandle", StringComparison.Ordinal))
            {
                return element;
            }

            if (current is DataGrid)
            {
                return null;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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

    private void EnsureAdornerLayer()
    {
        _adornerLayer ??= AdornerLayer.GetAdornerLayer(OrderGrid);
    }

    private void ShowDragPreview()
    {
        if (_draggedRow is null || _dragPreviewAdorner is not null)
        {
            return;
        }

        EnsureAdornerLayer();

        if (_adornerLayer is null)
        {
            return;
        }

        _dragPreviewAdorner = new DragPreviewAdorner(OrderGrid, _draggedRow);
        _dragPreviewAdorner.UpdatePosition(_dragStartPoint);
        _adornerLayer.Add(_dragPreviewAdorner);
    }

    private void UpdateDragPreviewPosition(Point position)
    {
        _dragPreviewAdorner?.UpdatePosition(position);
    }

    private void ShowInsertionIndicator(double y)
    {
        EnsureAdornerLayer();

        if (_adornerLayer is null)
        {
            return;
        }

        if (_insertionIndicatorAdorner is null)
        {
            _insertionIndicatorAdorner = new InsertionIndicatorAdorner(OrderGrid);
            _adornerLayer.Add(_insertionIndicatorAdorner);
        }

        _insertionIndicatorAdorner.UpdatePosition(y);
    }

    private void HideInsertionIndicator()
    {
        if (_insertionIndicatorAdorner is not null && _adornerLayer is not null)
        {
            _adornerLayer.Remove(_insertionIndicatorAdorner);
        }

        _insertionIndicatorAdorner = null;
        _insertionIndex = -1;
    }

    private void HideDragPreview()
    {
        if (_dragPreviewAdorner is not null && _adornerLayer is not null)
        {
            _adornerLayer.Remove(_dragPreviewAdorner);
        }

        _dragPreviewAdorner = null;
    }

    private void ResetDragState()
    {
        HideInsertionIndicator();
        HideDragPreview();

        _adornerLayer = null;
        _draggedItem = null;
        _draggedRow = null;
    }

    private sealed class InsertionIndicatorAdorner : Adorner
    {
        private readonly Pen _linePen;
        private readonly Brush _markerBrush;
        private double _y;

        public InsertionIndicatorAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            var color = Color.FromRgb(124, 77, 255);
            _markerBrush = new SolidColorBrush(color);
            _linePen = new Pen(_markerBrush, 3d);
            _linePen.Freeze();

            if (_markerBrush.CanFreeze)
            {
                _markerBrush.Freeze();
            }
        }

        public void UpdatePosition(double y)
        {
            _y = y;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            double right = Math.Max(14d, AdornedElement.RenderSize.Width - 18d);
            double left = 10d;

            drawingContext.DrawLine(
                _linePen,
                new Point(left, _y),
                new Point(right, _y));

            drawingContext.DrawEllipse(
                _markerBrush,
                null,
                new Point(left, _y),
                4d,
                4d);

            drawingContext.DrawEllipse(
                _markerBrush,
                null,
                new Point(right, _y),
                4d,
                4d);
        }
    }

    private sealed class DragPreviewAdorner : Adorner
    {
        private const double CursorOffset = 14d;
        private const double MaxPreviewWidth = 640d;

        private readonly VisualBrush _visualBrush;
        private readonly double _previewWidth;
        private readonly double _previewHeight;
        private Point _position;

        public DragPreviewAdorner(
            UIElement adornedElement,
            FrameworkElement source)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            _previewWidth = Math.Min(
                Math.Max(source.ActualWidth, 1d),
                MaxPreviewWidth);

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

            var backgroundBrush =
                new SolidColorBrush(Color.FromArgb(242, 255, 255, 255));

            var borderPen =
                new Pen(
                    new SolidColorBrush(Color.FromArgb(110, 124, 77, 255)),
                    1.5d);

            drawingContext.DrawRoundedRectangle(
                backgroundBrush,
                borderPen,
                previewRect,
                8d,
                8d);

            drawingContext.PushOpacity(0.90);
            drawingContext.DrawRectangle(
                _visualBrush,
                null,
                previewRect);
            drawingContext.Pop();
        }
    }
}

public sealed class LibraryOrderDialogItem : INotifyPropertyChanged
{
    private int _position;

    public LibraryOrderDialogItem(
        Guid id,
        string name,
        PackIconKind iconKind,
        int position)
    {
        Id = id;
        Name = name;
        IconKind = iconKind;
        _position = position;
    }

    public Guid Id { get; }

    public string Name { get; }


    public PackIconKind IconKind { get; }

    public int Position
    {
        get => _position;
        set
        {
            if (_position == value)
            {
                return;
            }

            _position = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
