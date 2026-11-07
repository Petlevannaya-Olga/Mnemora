using System.Windows.Controls;
using System.Windows.Threading;

namespace Mnemora.Desktop.Views.Library;

internal static class LibraryScrollAnchor
{
    private const double OFFSET_TOLERANCE = 0.01;

    public static async Task RestoreAsync(
        Dispatcher dispatcher,
        ScrollViewer scrollViewer,
        double verticalOffset)
    {
        // Даём WPF закончить привязки и пересчитать диапазон прокрутки
        // после добавления новой страницы.
        await dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.Background);

        double maximumOffset = Math.Max(0, scrollViewer.ScrollableHeight);
        double targetOffset = Math.Clamp(verticalOffset, 0, maximumOffset);

        if (Math.Abs(scrollViewer.VerticalOffset - targetOffset) > OFFSET_TOLERANCE)
        {
            scrollViewer.ScrollToVerticalOffset(targetOffset);
        }

        // ScrollToVerticalOffset сам может инициировать новый layout/ScrollChanged.
        // Ждём его завершения, пока внешний guard блокирует повторную догрузку.
        await dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.Background);
    }
}
