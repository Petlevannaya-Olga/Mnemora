using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Mnemora.Desktop.Views.Library;

/// <summary>
/// Reusable read-only representation of a material.
/// The create wizard uses this control on the review step; the same control
/// can later be embedded into the normal material page.
/// </summary>
public partial class MaterialPreviewView : UserControl
{
    private bool _isInitialized;

    public MaterialPreviewView()
    {
        InitializeComponent();
        _isInitialized = true;
        RefreshMarkdown();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(MaterialPreviewView),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TypeLabelProperty =
        DependencyProperty.Register(
            nameof(TypeLabel),
            typeof(string),
            typeof(MaterialPreviewView),
            new PropertyMetadata("Статья"));

    public string TypeLabel
    {
        get => (string)GetValue(TypeLabelProperty);
        set => SetValue(TypeLabelProperty, value);
    }

    public static readonly DependencyProperty TopicNameProperty =
        DependencyProperty.Register(
            nameof(TopicName),
            typeof(string),
            typeof(MaterialPreviewView),
            new PropertyMetadata(string.Empty));

    public string TopicName
    {
        get => (string)GetValue(TopicNameProperty);
        set => SetValue(TopicNameProperty, value);
    }

    public static readonly DependencyProperty DifficultyProperty =
        DependencyProperty.Register(
            nameof(Difficulty),
            typeof(string),
            typeof(MaterialPreviewView),
            new PropertyMetadata(string.Empty));

    public string Difficulty
    {
        get => (string)GetValue(DifficultyProperty);
        set => SetValue(DifficultyProperty, value);
    }

    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(
            nameof(IconKind),
            typeof(PackIconKind),
            typeof(MaterialPreviewView),
            new PropertyMetadata(PackIconKind.FileDocumentOutline));

    public PackIconKind IconKind
    {
        get => (PackIconKind)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public static readonly DependencyProperty IsQuestionProperty =
        DependencyProperty.Register(
            nameof(IsQuestion),
            typeof(bool),
            typeof(MaterialPreviewView),
            new PropertyMetadata(false));

    public bool IsQuestion
    {
        get => (bool)GetValue(IsQuestionProperty);
        set => SetValue(IsQuestionProperty, value);
    }

    public static readonly DependencyProperty TagsProperty =
        DependencyProperty.Register(
            nameof(Tags),
            typeof(IEnumerable),
            typeof(MaterialPreviewView),
            new PropertyMetadata(null));

    public IEnumerable? Tags
    {
        get => (IEnumerable?)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public static readonly DependencyProperty BodyMarkdownProperty =
        DependencyProperty.Register(
            nameof(BodyMarkdown),
            typeof(string),
            typeof(MaterialPreviewView),
            new PropertyMetadata(string.Empty, MarkdownPropertyChanged));

    public string BodyMarkdown
    {
        get => (string)GetValue(BodyMarkdownProperty);
        set => SetValue(BodyMarkdownProperty, value);
    }

    public static readonly DependencyProperty ReferenceAnswerMarkdownProperty =
        DependencyProperty.Register(
            nameof(ReferenceAnswerMarkdown),
            typeof(string),
            typeof(MaterialPreviewView),
            new PropertyMetadata(string.Empty, MarkdownPropertyChanged));

    public string ReferenceAnswerMarkdown
    {
        get => (string)GetValue(ReferenceAnswerMarkdownProperty);
        set => SetValue(ReferenceAnswerMarkdownProperty, value);
    }

    private static void MarkdownPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is MaterialPreviewView view &&
            view._isInitialized)
        {
            view.RefreshMarkdown();
        }
    }

    private void RefreshMarkdown()
    {
        if (!_isInitialized)
        {
            return;
        }

        RenderMarkdown(
            ArticleMarkdownHost,
            BodyMarkdown);

        RenderMarkdown(
            QuestionMarkdownHost,
            BodyMarkdown);

        RenderMarkdown(
            AnswerMarkdownHost,
            ReferenceAnswerMarkdown);
    }

    private void RenderMarkdown(
        StackPanel host,
        string? markdown)
    {
        host.Children.Clear();

        string normalized =
            (markdown ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        string[] lines = normalized.Split('\n');

        if (lines.All(string.IsNullOrWhiteSpace))
        {
            host.Children.Add(
                CreateParagraph(
                    "Содержимое Markdown-файла пусто.",
                    muted: true));
            return;
        }

        int index = 0;

        while (index < lines.Length)
        {
            string line = lines[index];
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                index++;
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                index = RenderCodeBlock(
                    host,
                    lines,
                    index);
                continue;
            }

            if (TryParseHeading(
                    trimmed,
                    out int headingLevel,
                    out string headingText))
            {
                host.Children.Add(
                    CreateHeading(
                        headingText,
                        headingLevel));
                index++;
                continue;
            }

            if (IsHorizontalRule(trimmed))
            {
                host.Children.Add(
                    new Border
                    {
                        Height = 1,
                        Margin = new Thickness(0, 16, 0, 18),
                        Background = GetBrush(
                            "Mnemora.Brush.Border",
                            Brushes.LightGray),
                    });
                index++;
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                string quoteText =
                    trimmed.TrimStart('>').Trim();

                var quoteContent =
                    CreateParagraph(
                        quoteText,
                        muted: false);

                quoteContent.FontStyle =
                    FontStyles.Italic;

                host.Children.Add(
                    new Border
                    {
                        Margin = new Thickness(0, 7, 0, 9),
                        Padding = new Thickness(14, 11, 14, 11),
                        Background = GetBrush(
                            "Mnemora.Brush.SurfaceMuted",
                            Brushes.WhiteSmoke),
                        BorderBrush = GetBrush(
                            "Mnemora.Brush.Secondary",
                            Brushes.MediumPurple),
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        CornerRadius = new CornerRadius(4),
                        Child = quoteContent,
                    });

                index++;
                continue;
            }

            if (TryParseBulletListItem(
                    line,
                    out string bulletText))
            {
                host.Children.Add(
                    CreateListItem(
                        "•",
                        bulletText));
                index++;
                continue;
            }

            if (TryParseNumberedListItem(
                    line,
                    out string listNumber,
                    out string numberedText))
            {
                host.Children.Add(
                    CreateListItem(
                        listNumber + ".",
                        numberedText));
                index++;
                continue;
            }

            var paragraphLines = new List<string>
            {
                trimmed,
            };

            index++;

            while (index < lines.Length)
            {
                string next = lines[index];
                string nextTrimmed = next.Trim();

                if (nextTrimmed.Length == 0 ||
                    IsBlockStart(nextTrimmed))
                {
                    break;
                }

                paragraphLines.Add(nextTrimmed);
                index++;
            }

            host.Children.Add(
                CreateParagraph(
                    string.Join(" ", paragraphLines),
                    muted: false));
        }
    }

    private int RenderCodeBlock(
        StackPanel host,
        IReadOnlyList<string> lines,
        int startIndex)
    {
        var codeLines = new List<string>();

        int index = startIndex + 1;

        while (index < lines.Count)
        {
            if (lines[index]
                .Trim()
                .StartsWith(
                    "```",
                    StringComparison.Ordinal))
            {
                index++;
                break;
            }

            codeLines.Add(lines[index]);
            index++;
        }

        var codeText =
            new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                LineHeight = 20,
                Foreground = Brushes.White,
                Text = string.Join(
                    Environment.NewLine,
                    codeLines),
                TextWrapping = TextWrapping.Wrap,
            };

        host.Children.Add(
            new Border
            {
                Margin = new Thickness(0, 9, 0, 13),
                Padding = new Thickness(16),
                Background = new SolidColorBrush(
                    Color.FromRgb(
                        13,
                        24,
                        42)),
                CornerRadius = new CornerRadius(9),
                Child = codeText,
            });

        return index;
    }

    private TextBlock CreateHeading(
        string text,
        int level)
    {
        double fontSize = level switch
        {
            1 => 27,
            2 => 22,
            3 => 18,
            4 => 16,
            _ => 15,
        };

        var block =
            CreateInlineTextBlock(
                text,
                fontSize);

        block.FontWeight =
            FontWeights.SemiBold;

        block.Margin =
            new Thickness(
                0,
                level == 1 ? 2 : 18,
                0,
                level == 1 ? 15 : 9);

        return block;
    }

    private TextBlock CreateParagraph(
        string text,
        bool muted)
    {
        var block =
            CreateInlineTextBlock(
                text,
                14);

        block.Margin =
            new Thickness(
                0,
                0,
                0,
                11);

        block.LineHeight = 22;

        if (muted)
        {
            block.Foreground =
                GetBrush(
                    "Mnemora.Brush.TextSecondary",
                    Brushes.Gray);
        }

        return block;
    }

    private Grid CreateListItem(
        string marker,
        string text)
    {
        var grid =
            new Grid
            {
                Margin = new Thickness(
                    0,
                    2,
                    0,
                    7),
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(26),
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star),
            });

        var markerText =
            new TextBlock
            {
                Text = marker,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush(
                    "Mnemora.Brush.Secondary",
                    Brushes.MediumPurple),
                VerticalAlignment =
                    VerticalAlignment.Top,
            };

        var content =
            CreateInlineTextBlock(
                text,
                14);

        content.LineHeight = 22;
        Grid.SetColumn(
            content,
            1);

        grid.Children.Add(markerText);
        grid.Children.Add(content);

        return grid;
    }

    private TextBlock CreateInlineTextBlock(
        string text,
        double fontSize)
    {
        var block =
            new TextBlock
            {
                FontSize = fontSize,
                Foreground = GetBrush(
                    "Mnemora.Brush.TextPrimary",
                    Brushes.Black),
                TextWrapping =
                    TextWrapping.Wrap,
            };

        AddInlineRuns(
            block,
            text);

        return block;
    }

    private static void AddInlineRuns(
        TextBlock block,
        string text)
    {
        int position = 0;

        while (position < text.Length)
        {
            int bold = text.IndexOf(
                "**",
                position,
                StringComparison.Ordinal);

            int code = text.IndexOf(
                '`',
                position);

            int italic = FindItalicStart(
                text,
                position);

            int next = MinPositive(
                bold,
                code,
                italic);

            if (next < 0)
            {
                block.Inlines.Add(
                    new Run(
                        text[position..]));
                return;
            }

            if (next > position)
            {
                block.Inlines.Add(
                    new Run(
                        text[position..next]));
            }

            if (next == bold)
            {
                int end = text.IndexOf(
                    "**",
                    bold + 2,
                    StringComparison.Ordinal);

                if (end < 0)
                {
                    block.Inlines.Add(
                        new Run(
                            text[bold..]));
                    return;
                }

                block.Inlines.Add(
                    new Run(
                        text[(bold + 2)..end])
                    {
                        FontWeight =
                            FontWeights.SemiBold,
                    });

                position = end + 2;
                continue;
            }

            if (next == code)
            {
                int end = text.IndexOf(
                    '`',
                    code + 1);

                if (end < 0)
                {
                    block.Inlines.Add(
                        new Run(
                            text[code..]));
                    return;
                }

                block.Inlines.Add(
                    new Run(
                        text[(code + 1)..end])
                    {
                        FontFamily =
                            new FontFamily(
                                "Consolas"),
                        Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(
                                    88,
                                    62,
                                    154)),
                    });

                position = end + 1;
                continue;
            }

            int italicEnd = text.IndexOf(
                '*',
                italic + 1);

            if (italicEnd < 0)
            {
                block.Inlines.Add(
                    new Run(
                        text[italic..]));
                return;
            }

            block.Inlines.Add(
                new Run(
                    text[(italic + 1)..italicEnd])
                {
                    FontStyle =
                        FontStyles.Italic,
                });

            position = italicEnd + 1;
        }
    }

    private static int FindItalicStart(
        string text,
        int start)
    {
        for (int index = start;
             index < text.Length;
             index++)
        {
            if (text[index] != '*')
            {
                continue;
            }

            bool partOfBold =
                index + 1 < text.Length &&
                text[index + 1] == '*';

            bool secondOfBold =
                index > 0 &&
                text[index - 1] == '*';

            if (!partOfBold &&
                !secondOfBold)
            {
                return index;
            }
        }

        return -1;
    }

    private static int MinPositive(
        params int[] values)
    {
        return values
            .Where(value => value >= 0)
            .DefaultIfEmpty(-1)
            .Min();
    }

    private static bool IsHorizontalRule(
        string line)
    {
        return line is "---" or "***" or "___";
    }

    private static bool IsBlockStart(
        string line)
    {
        return line.StartsWith(
                   "```",
                   StringComparison.Ordinal)
               || TryParseHeading(
                   line,
                   out _,
                   out _)
               || TryParseNumberedListItem(
                   line,
                   out _,
                   out _)
               || TryParseBulletListItem(
                   line,
                   out _)
               || line.StartsWith('>')
               || IsHorizontalRule(line);
    }

    private static bool TryParseHeading(
        string line,
        out int level,
        out string text)
    {
        level = 0;
        text = string.Empty;

        while (level < line.Length &&
               level < 6 &&
               line[level] == '#')
        {
            level++;
        }

        if (level == 0 ||
            level >= line.Length ||
            !char.IsWhiteSpace(line[level]))
        {
            level = 0;
            return false;
        }

        text = line[level..].Trim();

        if (text.Length == 0)
        {
            level = 0;
            return false;
        }

        return true;
    }

    private static bool TryParseBulletListItem(
        string line,
        out string text)
    {
        text = string.Empty;

        ReadOnlySpan<char> span =
            line.AsSpan().TrimStart();

        if (span.Length < 3 ||
            span[0] is not ('-' or '+' or '*') ||
            !char.IsWhiteSpace(span[1]))
        {
            return false;
        }

        text = span[2..]
            .Trim()
            .ToString();

        return text.Length > 0;
    }

    private static bool TryParseNumberedListItem(
        string line,
        out string number,
        out string text)
    {
        number = string.Empty;
        text = string.Empty;

        ReadOnlySpan<char> span =
            line.AsSpan().TrimStart();

        int digitCount = 0;

        while (digitCount < span.Length &&
               char.IsAsciiDigit(span[digitCount]))
        {
            digitCount++;
        }

        if (digitCount == 0 ||
            digitCount + 2 > span.Length ||
            span[digitCount] != '.' ||
            !char.IsWhiteSpace(span[digitCount + 1]))
        {
            return false;
        }

        number =
            span[..digitCount]
                .ToString();

        text =
            span[(digitCount + 2)..]
                .Trim()
                .ToString();

        return text.Length > 0;
    }

    private Brush GetBrush(
        string resourceKey,
        Brush fallback)
    {
        return TryFindResource(
                   resourceKey)
               as Brush
               ?? fallback;
    }
}
