using System.Windows;

namespace Mnemora.Desktop.Views.Library;

public static class SuppressBringIntoViewBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SuppressBringIntoViewBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if (eventArgs.OldValue is true)
        {
            element.RequestBringIntoView -= Element_OnRequestBringIntoView;
        }

        if (eventArgs.NewValue is true)
        {
            element.RequestBringIntoView += Element_OnRequestBringIntoView;
        }
    }

    private static void Element_OnRequestBringIntoView(
        object sender,
        RequestBringIntoViewEventArgs eventArgs)
    {
        eventArgs.Handled = true;
    }
}
