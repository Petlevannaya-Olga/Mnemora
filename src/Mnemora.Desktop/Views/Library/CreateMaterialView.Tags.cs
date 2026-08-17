using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Mnemora.Domain.Materials;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView
{
    private const string TagsFieldHostMarker = "TagsFieldHost";
    private const string TagsErrorMarker = "TagsError";

    private void TagInput_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox input)
        {
            return;
        }

        e.Handled = true;

        WrapPanel? tagsPanel = FindVisualParent<WrapPanel>(input);
        if (tagsPanel is null)
        {
            return;
        }

        string tag = input.Text.Trim();

        if (tag.Length == 0)
        {
            HideTagError(tagsPanel);
            return;
        }

        if (tag.Length > MaterialTag.MaxLength)
        {
            ShowTagError(
                tagsPanel,
                $"Тег не может быть длиннее {MaterialTag.MaxLength} символов.");
            return;
        }

        string[] existingTags = GetTags(tagsPanel);

        if (existingTags.Length >= Material.MaxTags)
        {
            ShowTagError(
                tagsPanel,
                $"Можно добавить не более {Material.MaxTags} тегов.");
            return;
        }

        if (existingTags.Any(existing =>
                string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
        {
            ShowTagError(tagsPanel, "Такой тег уже добавлен.");
            return;
        }

        Border chip = CreateTagChip(tag);

        // Поле ввода находится внутри Grid и всегда остаётся последним элементом панели.
        int inputContainerIndex = tagsPanel.Children
            .Cast<UIElement>()
            .Select((child, index) => new { child, index })
            .FirstOrDefault(item =>
                item.child is Grid grid &&
                grid.Children.OfType<TextBox>().Contains(input))
            ?.index ?? tagsPanel.Children.Count;

        tagsPanel.Children.Insert(inputContainerIndex, chip);

        input.Clear();
        HideTagError(tagsPanel);
        input.Focus();
    }

    private void TagInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox input &&
            FindVisualParent<WrapPanel>(input) is { } tagsPanel)
        {
            HideTagError(tagsPanel);
        }
    }

    private void RemoveTag_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string tag ||
            FindVisualParent<WrapPanel>(button) is not { } tagsPanel)
        {
            return;
        }

        Border? chip = tagsPanel.Children
            .OfType<Border>()
            .FirstOrDefault(border =>
                border.Tag is string existing &&
                string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase));

        if (chip is not null)
        {
            tagsPanel.Children.Remove(chip);
        }

        HideTagError(tagsPanel);
        e.Handled = true;
    }

    private Border CreateTagChip(string tag)
    {
        var label = new TextBlock
        {
            Text = tag,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource(
                "Mnemora.Brush.TextPrimary"),
        };

        var closeIcon = new PackIcon
        {
            Kind = PackIconKind.Close,
            Width = 15,
            Height = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource(
                "Mnemora.Brush.TextSecondary"),
        };

        var removeButton = new Button
        {
            Width = 18,
            Height = 18,
            MinWidth = 18,
            Margin = new Thickness(5, 0, -3, 0),
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            Tag = tag,
            ToolTip = "Удалить тег",
            Content = closeIcon,
        };

        removeButton.Click += RemoveTag_OnClick;

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };
        content.Children.Add(label);
        content.Children.Add(removeButton);

        return new Border
        {
            Tag = tag,
            Style = (Style)FindResource("CreateMaterial.TagChip"),
            Child = content,
        };
    }

    private static string[] GetTags(WrapPanel tagsPanel)
    {
        return tagsPanel.Children
            .OfType<Border>()
            .Select(border => border.Tag as string)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Cast<string>()
            .ToArray();
    }

    private void ShowTagError(WrapPanel tagsPanel, string message)
    {
        TextBlock? error = FindTagError(tagsPanel);
        if (error is null)
        {
            return;
        }

        error.Text = message;
        error.Visibility = Visibility.Visible;
    }

    private void HideTagError(WrapPanel tagsPanel)
    {
        TextBlock? error = FindTagError(tagsPanel);
        if (error is null)
        {
            return;
        }

        error.Text = string.Empty;
        error.Visibility = Visibility.Collapsed;
    }

    private static TextBlock? FindTagError(WrapPanel tagsPanel)
    {
        DependencyObject? current = tagsPanel;

        while (current is not null)
        {
            if (current is StackPanel host &&
                Equals(host.Tag, TagsFieldHostMarker))
            {
                return host.Children
                    .OfType<TextBlock>()
                    .FirstOrDefault(text => Equals(text.Tag, TagsErrorMarker));
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
