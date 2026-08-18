using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Mnemora.Desktop.Views.Shell;

namespace Mnemora.Desktop.Views.Library;

/// <summary>
/// Reusable read-only representation of a material.
/// The create wizard uses this control on the review step; the same control
/// can later be embedded into the normal material page.
/// </summary>
public partial class MaterialPreviewView : UserControl
{
    private const double ExpandedTocWidth = 270;
    private const double CollapsedTocWidth = 48;

    private bool _isInitialized;
    private bool _isTocExpanded = true;
    private bool _isFocusMode;
    private AppShellView? _focusShell;
    private Window? _focusWindow;
    private MaterialPreviewView? _expandedPreview;
    private MaterialPreviewView? _expandedOwner;

    public MaterialPreviewView()
    {
        InitializeComponent();
        _isInitialized = true;
        Unloaded += MaterialPreviewView_OnUnloaded;
        RefreshMarkdown();
        UpdateReadingControls();
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

        RebuildTableOfContents();
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
        string language = GetCodeFenceLanguage(
            lines[startIndex]);

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
                Foreground = CodePlainBrush,
                TextWrapping = TextWrapping.NoWrap,
            };

        AddHighlightedCode(
            codeText,
            codeLines,
            language);

        var codeScrollViewer =
            new ScrollViewer
            {
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                Content = codeText,
            };

        var codeLayout =
            new Grid();

        codeLayout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto,
            });

        codeLayout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto,
            });

        if (!string.IsNullOrWhiteSpace(language))
        {
            var languageLabel =
                new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 9),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CodeCommentBrush,
                    Text = GetDisplayLanguageName(language),
                };

            codeLayout.Children.Add(languageLabel);
        }

        Grid.SetRow(
            codeScrollViewer,
            1);
        codeLayout.Children.Add(codeScrollViewer);

        host.Children.Add(
            new Border
            {
                Margin = new Thickness(0, 9, 0, 13),
                Padding = new Thickness(16, 12, 16, 14),
                Background = CodeBackgroundBrush,
                BorderBrush = CodeBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Child = codeLayout,
            });

        return index;
    }

    private static string GetCodeFenceLanguage(
        string openingFence)
    {
        string trimmed = openingFence.Trim();

        if (!trimmed.StartsWith(
                "```",
                StringComparison.Ordinal) ||
            trimmed.Length <= 3)
        {
            return string.Empty;
        }

        string value = trimmed[3..].Trim();

        if (value.Length == 0)
        {
            return string.Empty;
        }

        int separator = value.IndexOfAny(
            [' ', '\t', '{']);

        if (separator >= 0)
        {
            value = value[..separator];
        }

        return NormalizeCodeLanguage(value);
    }

    private static string NormalizeCodeLanguage(
        string language)
    {
        string normalized = language
            .Trim()
            .TrimStart('.')
            .ToLowerInvariant();

        return normalized switch
        {
            "cs" or "c#" or "dotnet" => "csharp",
            "js" => "javascript",
            "ts" => "typescript",
            "ps" or "ps1" => "powershell",
            "sh" or "shell" or "zsh" => "bash",
            "yml" => "yaml",
            "htm" => "html",
            _ => normalized,
        };
    }

    private static string GetDisplayLanguageName(
        string language) =>
        language switch
        {
            "csharp" => "C#",
            "javascript" => "JavaScript",
            "typescript" => "TypeScript",
            "powershell" => "PowerShell",
            "bash" => "Bash",
            "json" => "JSON",
            "sql" => "SQL",
            "xml" => "XML",
            "xaml" => "XAML",
            "html" => "HTML",
            "css" => "CSS",
            "yaml" => "YAML",
            _ => language.ToUpperInvariant(),
        };

    private static void AddHighlightedCode(
        TextBlock block,
        IReadOnlyList<string> lines,
        string language)
    {
        var state = new SyntaxState();

        for (int lineIndex = 0;
             lineIndex < lines.Count;
             lineIndex++)
        {
            AddHighlightedCodeLine(
                block,
                lines[lineIndex],
                language,
                state);

            if (lineIndex < lines.Count - 1)
            {
                block.Inlines.Add(new LineBreak());
            }
        }
    }

    private static void AddHighlightedCodeLine(
        TextBlock block,
        string line,
        string language,
        SyntaxState state)
    {
        switch (language)
        {
            case "json":
                AddJsonLine(block, line);
                break;

            case "xml":
            case "xaml":
            case "html":
                AddMarkupLine(
                    block,
                    line,
                    state);
                break;

            case "sql":
                AddCodeLikeLine(
                    block,
                    line,
                    SqlKeywords,
                    EmptyTypes,
                    state,
                    lineComment: "--",
                    hashComment: false);
                break;

            case "javascript":
            case "typescript":
                AddCodeLikeLine(
                    block,
                    line,
                    JavaScriptKeywords,
                    JavaScriptTypes,
                    state,
                    lineComment: "//",
                    hashComment: false);
                break;

            case "bash":
            case "powershell":
                AddCodeLikeLine(
                    block,
                    line,
                    ShellKeywords,
                    EmptyTypes,
                    state,
                    lineComment: null,
                    hashComment: true);
                break;

            case "csharp":
                AddCodeLikeLine(
                    block,
                    line,
                    CSharpKeywords,
                    CSharpTypes,
                    state,
                    lineComment: "//",
                    hashComment: false);
                break;

            default:
                AddCodeLikeLine(
                    block,
                    line,
                    EmptyKeywords,
                    EmptyTypes,
                    state,
                    lineComment: "//",
                    hashComment: true);
                break;
        }
    }

    private static void AddCodeLikeLine(
        TextBlock block,
        string line,
        IReadOnlySet<string> keywords,
        IReadOnlySet<string> types,
        SyntaxState state,
        string? lineComment,
        bool hashComment)
    {
        int position = 0;

        while (position < line.Length)
        {
            if (state.InBlockComment)
            {
                int commentEnd = line.IndexOf(
                    "*/",
                    position,
                    StringComparison.Ordinal);

                if (commentEnd < 0)
                {
                    AddCodeRun(
                        block,
                        line[position..],
                        CodeCommentBrush);
                    return;
                }

                AddCodeRun(
                    block,
                    line[position..(commentEnd + 2)],
                    CodeCommentBrush);

                position = commentEnd + 2;
                state.InBlockComment = false;
                continue;
            }

            if (lineComment is not null &&
                StartsWithAt(
                    line,
                    position,
                    lineComment))
            {
                AddCodeRun(
                    block,
                    line[position..],
                    CodeCommentBrush);
                return;
            }

            if (hashComment &&
                line[position] == '#' &&
                IsHashCommentStart(line, position))
            {
                AddCodeRun(
                    block,
                    line[position..],
                    CodeCommentBrush);
                return;
            }

            if (StartsWithAt(
                    line,
                    position,
                    "/*"))
            {
                int commentEnd = line.IndexOf(
                    "*/",
                    position + 2,
                    StringComparison.Ordinal);

                if (commentEnd < 0)
                {
                    AddCodeRun(
                        block,
                        line[position..],
                        CodeCommentBrush);
                    state.InBlockComment = true;
                    return;
                }

                AddCodeRun(
                    block,
                    line[position..(commentEnd + 2)],
                    CodeCommentBrush);

                position = commentEnd + 2;
                continue;
            }

            int stringLength =
                GetStringTokenLength(
                    line,
                    position);

            if (stringLength > 0)
            {
                AddCodeRun(
                    block,
                    line.Substring(
                        position,
                        stringLength),
                    CodeStringBrush);

                position += stringLength;
                continue;
            }

            char current = line[position];

            if (char.IsWhiteSpace(current))
            {
                int whitespaceEnd = position + 1;

                while (whitespaceEnd < line.Length &&
                       char.IsWhiteSpace(
                           line[whitespaceEnd]))
                {
                    whitespaceEnd++;
                }

                AddCodeRun(
                    block,
                    line[position..whitespaceEnd],
                    CodePlainBrush);

                position = whitespaceEnd;
                continue;
            }

            if (char.IsDigit(current))
            {
                int numberEnd = position + 1;

                while (numberEnd < line.Length &&
                       IsNumberCharacter(
                           line[numberEnd]))
                {
                    numberEnd++;
                }

                AddCodeRun(
                    block,
                    line[position..numberEnd],
                    CodeNumberBrush);

                position = numberEnd;
                continue;
            }

            if (IsIdentifierStart(current))
            {
                int identifierEnd = position + 1;

                while (identifierEnd < line.Length &&
                       IsIdentifierPart(
                           line[identifierEnd]))
                {
                    identifierEnd++;
                }

                string identifier =
                    line[position..identifierEnd];

                Brush brush = keywords.Contains(identifier)
                    ? CodeKeywordBrush
                    : types.Contains(identifier)
                        ? CodeTypeBrush
                        : CodePlainBrush;

                AddCodeRun(
                    block,
                    identifier,
                    brush);

                position = identifierEnd;
                continue;
            }

            Brush punctuationBrush =
                IsAccentPunctuation(current)
                    ? CodePunctuationBrush
                    : CodePlainBrush;

            AddCodeRun(
                block,
                current.ToString(),
                punctuationBrush);

            position++;
        }
    }

    private static void AddJsonLine(
        TextBlock block,
        string line)
    {
        int position = 0;

        while (position < line.Length)
        {
            char current = line[position];

            if (char.IsWhiteSpace(current))
            {
                int end = position + 1;

                while (end < line.Length &&
                       char.IsWhiteSpace(line[end]))
                {
                    end++;
                }

                AddCodeRun(
                    block,
                    line[position..end],
                    CodePlainBrush);

                position = end;
                continue;
            }

            if (current == '"')
            {
                int length =
                    GetQuotedStringLength(
                        line,
                        position,
                        '"',
                        verbatim: false);

                int end = position + length;
                int lookAhead = end;

                while (lookAhead < line.Length &&
                       char.IsWhiteSpace(
                           line[lookAhead]))
                {
                    lookAhead++;
                }

                Brush brush =
                    lookAhead < line.Length &&
                    line[lookAhead] == ':'
                        ? CodePropertyBrush
                        : CodeStringBrush;

                AddCodeRun(
                    block,
                    line.Substring(position, length),
                    brush);

                position = end;
                continue;
            }

            if (char.IsDigit(current) ||
                current == '-' &&
                position + 1 < line.Length &&
                char.IsDigit(line[position + 1]))
            {
                int end = position + 1;

                while (end < line.Length &&
                       IsNumberCharacter(line[end]))
                {
                    end++;
                }

                AddCodeRun(
                    block,
                    line[position..end],
                    CodeNumberBrush);

                position = end;
                continue;
            }

            if (IsIdentifierStart(current))
            {
                int end = position + 1;

                while (end < line.Length &&
                       IsIdentifierPart(line[end]))
                {
                    end++;
                }

                string token = line[position..end];

                AddCodeRun(
                    block,
                    token,
                    JsonLiterals.Contains(token)
                        ? CodeKeywordBrush
                        : CodePlainBrush);

                position = end;
                continue;
            }

            AddCodeRun(
                block,
                current.ToString(),
                IsAccentPunctuation(current)
                    ? CodePunctuationBrush
                    : CodePlainBrush);

            position++;
        }
    }

    private static void AddMarkupLine(
        TextBlock block,
        string line,
        SyntaxState state)
    {
        int position = 0;

        while (position < line.Length)
        {
            if (state.InMarkupComment)
            {
                int end = line.IndexOf(
                    "-->",
                    position,
                    StringComparison.Ordinal);

                if (end < 0)
                {
                    AddCodeRun(
                        block,
                        line[position..],
                        CodeCommentBrush);
                    return;
                }

                AddCodeRun(
                    block,
                    line[position..(end + 3)],
                    CodeCommentBrush);

                position = end + 3;
                state.InMarkupComment = false;
                continue;
            }

            if (StartsWithAt(
                    line,
                    position,
                    "<!--"))
            {
                int end = line.IndexOf(
                    "-->",
                    position + 4,
                    StringComparison.Ordinal);

                if (end < 0)
                {
                    AddCodeRun(
                        block,
                        line[position..],
                        CodeCommentBrush);
                    state.InMarkupComment = true;
                    return;
                }

                AddCodeRun(
                    block,
                    line[position..(end + 3)],
                    CodeCommentBrush);

                position = end + 3;
                continue;
            }

            if (line[position] != '<')
            {
                int nextTag = line.IndexOf('<', position);

                if (nextTag < 0)
                {
                    AddCodeRun(
                        block,
                        line[position..],
                        CodePlainBrush);
                    return;
                }

                AddCodeRun(
                    block,
                    line[position..nextTag],
                    CodePlainBrush);

                position = nextTag;
                continue;
            }

            int tagEnd = line.IndexOf('>', position);

            if (tagEnd < 0)
            {
                tagEnd = line.Length - 1;
            }

            AddMarkupTag(
                block,
                line,
                position,
                tagEnd);

            position = tagEnd + 1;
        }
    }

    private static void AddMarkupTag(
        TextBlock block,
        string line,
        int start,
        int end)
    {
        int position = start;

        AddCodeRun(
            block,
            "<",
            CodePunctuationBrush);
        position++;

        if (position <= end &&
            line[position] is '/' or '?' or '!')
        {
            AddCodeRun(
                block,
                line[position].ToString(),
                CodePunctuationBrush);
            position++;
        }

        int tagNameStart = position;

        while (position <= end &&
               IsMarkupNameCharacter(
                   line[position]))
        {
            position++;
        }

        if (position > tagNameStart)
        {
            AddCodeRun(
                block,
                line[tagNameStart..position],
                CodeKeywordBrush);
        }

        while (position <= end)
        {
            char current = line[position];

            if (char.IsWhiteSpace(current))
            {
                int whitespaceEnd = position + 1;

                while (whitespaceEnd <= end &&
                       char.IsWhiteSpace(
                           line[whitespaceEnd]))
                {
                    whitespaceEnd++;
                }

                AddCodeRun(
                    block,
                    line[position..whitespaceEnd],
                    CodePlainBrush);

                position = whitespaceEnd;
                continue;
            }

            if (current is '"' or '\'')
            {
                int length =
                    GetQuotedStringLength(
                        line,
                        position,
                        current,
                        verbatim: false);

                int available = end - position + 1;
                length = Math.Min(length, available);

                AddCodeRun(
                    block,
                    line.Substring(position, length),
                    CodeStringBrush);

                position += length;
                continue;
            }

            if (IsMarkupNameCharacter(current))
            {
                int nameEnd = position + 1;

                while (nameEnd <= end &&
                       IsMarkupNameCharacter(
                           line[nameEnd]))
                {
                    nameEnd++;
                }

                AddCodeRun(
                    block,
                    line[position..nameEnd],
                    CodePropertyBrush);

                position = nameEnd;
                continue;
            }

            AddCodeRun(
                block,
                current.ToString(),
                current is '>' or '/' or '=' or '?' or ':'
                    ? CodePunctuationBrush
                    : CodePlainBrush);

            position++;
        }
    }

    private static int GetStringTokenLength(
        string line,
        int position)
    {
        if (position >= line.Length)
        {
            return 0;
        }

        if (line[position] is '"' or '\'')
        {
            return GetQuotedStringLength(
                line,
                position,
                line[position],
                verbatim: false);
        }

        int quotePosition = -1;
        bool verbatim = false;

        if (line[position] == '@' &&
            position + 1 < line.Length &&
            line[position + 1] == '"')
        {
            quotePosition = position + 1;
            verbatim = true;
        }
        else if (line[position] == '$' &&
                 position + 1 < line.Length &&
                 line[position + 1] == '"')
        {
            quotePosition = position + 1;
        }
        else if (position + 2 < line.Length &&
                 (line.AsSpan(position, 3).SequenceEqual("$@\"") ||
                  line.AsSpan(position, 3).SequenceEqual("@$\"")))
        {
            quotePosition = position + 2;
            verbatim = true;
        }

        if (quotePosition < 0)
        {
            return 0;
        }

        return quotePosition - position +
               GetQuotedStringLength(
                   line,
                   quotePosition,
                   '"',
                   verbatim);
    }

    private static int GetQuotedStringLength(
        string line,
        int quotePosition,
        char quote,
        bool verbatim)
    {
        int position = quotePosition + 1;

        while (position < line.Length)
        {
            if (line[position] != quote)
            {
                if (!verbatim &&
                    line[position] == '\\' &&
                    position + 1 < line.Length)
                {
                    position += 2;
                    continue;
                }

                position++;
                continue;
            }

            if (verbatim &&
                position + 1 < line.Length &&
                line[position + 1] == quote)
            {
                position += 2;
                continue;
            }

            return position - quotePosition + 1;
        }

        return line.Length - quotePosition;
    }

    private static bool StartsWithAt(
        string text,
        int position,
        string value)
    {
        return position + value.Length <= text.Length &&
               text.AsSpan(position, value.Length)
                   .SequenceEqual(value);
    }

    private static bool IsHashCommentStart(
        string line,
        int position)
    {
        return position == 0 ||
               char.IsWhiteSpace(
                   line[position - 1]);
    }

    private static bool IsIdentifierStart(
        char value) =>
        char.IsLetter(value) ||
        value is '_' or '@' or '$';

    private static bool IsIdentifierPart(
        char value) =>
        char.IsLetterOrDigit(value) ||
        value is '_' or '@' or '$';

    private static bool IsNumberCharacter(
        char value) =>
        char.IsLetterOrDigit(value) ||
        value is '.' or '_' or '+' or '-';

    private static bool IsAccentPunctuation(
        char value) =>
        value is '{' or '}' or '[' or ']' or
        '(' or ')' or ':' or ';' or '=' or
        '<' or '>';

    private static bool IsMarkupNameCharacter(
        char value) =>
        char.IsLetterOrDigit(value) ||
        value is '_' or '-' or ':' or '.';

    private static void AddCodeRun(
        TextBlock block,
        string text,
        Brush brush)
    {
        if (text.Length == 0)
        {
            return;
        }

        block.Inlines.Add(
            new Run(text)
            {
                Foreground = brush,
            });
    }

    private sealed class SyntaxState
    {
        public bool InBlockComment { get; set; }

        public bool InMarkupComment { get; set; }
    }

    private static readonly IReadOnlySet<string> EmptyKeywords =
        new HashSet<string>(StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> EmptyTypes =
        new HashSet<string>(StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> CSharpKeywords =
        new HashSet<string>(
            [
                "abstract", "as", "base", "bool", "break", "byte", "case",
                "catch", "char", "checked", "class", "const", "continue",
                "decimal", "default", "delegate", "do", "double", "else",
                "enum", "event", "explicit", "extern", "false", "finally",
                "fixed", "float", "for", "foreach", "goto", "if", "implicit",
                "in", "int", "interface", "internal", "is", "lock", "long",
                "namespace", "new", "null", "object", "operator", "out",
                "override", "params", "private", "protected", "public",
                "readonly", "record", "ref", "required", "return", "sbyte",
                "sealed", "short", "sizeof", "stackalloc", "static", "string",
                "struct", "switch", "this", "throw", "true", "try", "typeof",
                "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
                "virtual", "void", "volatile", "while", "async", "await",
                "var", "when", "where", "with", "yield", "init", "file",
                "global", "not", "and", "or"
            ],
            StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> CSharpTypes =
        new HashSet<string>(
            [
                "Task", "ValueTask", "Guid", "DateTime", "DateTimeOffset",
                "TimeSpan", "CancellationToken", "IEnumerable", "IReadOnlyList",
                "IReadOnlyCollection", "ICollection", "IList", "List", "Dictionary",
                "HashSet", "Result", "UnitResult", "Error", "Exception", "String",
                "Int32", "Int64", "Boolean", "Decimal", "Double", "Object"
            ],
            StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> JavaScriptKeywords =
        new HashSet<string>(
            [
                "break", "case", "catch", "class", "const", "continue", "debugger",
                "default", "delete", "do", "else", "export", "extends", "false",
                "finally", "for", "function", "if", "import", "in", "instanceof",
                "let", "new", "null", "return", "static", "super", "switch",
                "this", "throw", "true", "try", "typeof", "undefined", "var",
                "void", "while", "with", "yield", "async", "await", "interface",
                "type", "enum", "implements", "private", "protected", "public",
                "readonly", "keyof", "namespace", "declare"
            ],
            StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> JavaScriptTypes =
        new HashSet<string>(
            [
                "string", "number", "boolean", "object", "unknown", "never", "any",
                "Array", "Promise", "Map", "Set", "Date", "Error", "Record"
            ],
            StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> SqlKeywords =
        new HashSet<string>(
            [
                "select", "from", "where", "join", "inner", "left", "right", "full",
                "outer", "on", "group", "by", "order", "having", "limit", "offset",
                "insert", "into", "values", "update", "set", "delete", "create",
                "alter", "drop", "table", "index", "unique", "primary", "key",
                "foreign", "references", "constraint", "and", "or", "not", "null",
                "is", "in", "exists", "between", "like", "as", "distinct", "union",
                "all", "case", "when", "then", "else", "end", "with", "recursive",
                "returning", "asc", "desc", "count", "sum", "avg", "min", "max"
            ],
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> ShellKeywords =
        new HashSet<string>(
            [
                "if", "then", "else", "elif", "fi", "for", "while", "do", "done",
                "case", "esac", "function", "return", "export", "local", "in",
                "switch", "foreach", "param", "begin", "process", "end", "filter"
            ],
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> JsonLiterals =
        new HashSet<string>(
            ["true", "false", "null"],
            StringComparer.Ordinal);

    private static readonly Brush CodeBackgroundBrush =
        CreateFrozenBrush(13, 24, 42);

    private static readonly Brush CodeBorderBrush =
        CreateFrozenBrush(32, 47, 69);

    private static readonly Brush CodePlainBrush =
        CreateFrozenBrush(220, 226, 236);

    private static readonly Brush CodeKeywordBrush =
        CreateFrozenBrush(198, 146, 255);

    private static readonly Brush CodeTypeBrush =
        CreateFrozenBrush(130, 170, 255);

    private static readonly Brush CodeStringBrush =
        CreateFrozenBrush(170, 218, 135);

    private static readonly Brush CodeNumberBrush =
        CreateFrozenBrush(247, 140, 108);

    private static readonly Brush CodeCommentBrush =
        CreateFrozenBrush(126, 144, 160);

    private static readonly Brush CodePropertyBrush =
        CreateFrozenBrush(137, 221, 255);

    private static readonly Brush CodePunctuationBrush =
        CreateFrozenBrush(137, 221, 255);

    private static Brush CreateFrozenBrush(
        byte red,
        byte green,
        byte blue)
    {
        var brush =
            new SolidColorBrush(
                Color.FromRgb(
                    red,
                    green,
                    blue));

        brush.Freeze();
        return brush;
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

        block.Tag = new MarkdownHeadingTag(level, text);

        return block;
    }

    private void RebuildTableOfContents()
    {
        if (!_isInitialized)
        {
            return;
        }

        TocItemsHost.Children.Clear();

        foreach (FrameworkElement element in ArticleMarkdownHost.Children)
        {
            if (element is not TextBlock heading ||
                heading.Tag is not MarkdownHeadingTag headingTag ||
                headingTag.Level is not (2 or 3))
            {
                continue;
            }

            TextBlock label =
                CreateInlineTextBlock(
                    headingTag.Text,
                    13);

            label.FontWeight = headingTag.Level == 2
                ? FontWeights.SemiBold
                : FontWeights.Normal;

            var button = new Button
            {
                Content = label,
                Margin = new Thickness(
                    headingTag.Level == 3 ? 16 : 0,
                    0,
                    0,
                    2),
                Style = (Style)FindResource(
                    "MaterialPreview.TocButton"),
                Tag = heading,
                ToolTip = headingTag.Text,
            };

            button.Click += TocItem_OnClick;
            TocItemsHost.Children.Add(button);
        }

        TocEmptyState.Visibility =
            TocItemsHost.Children.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private static void TocItem_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                Tag: FrameworkElement target,
            })
        {
            target.BringIntoView();
        }
    }

    private void ToggleToc_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        _isTocExpanded = !_isTocExpanded;
        UpdateReadingControls();
    }

    private void ToggleFocusMode_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        // В раскрытой копии кнопка должна закрывать режим чтения
        // у исходного MaterialPreviewView.
        if (_expandedOwner is not null)
        {
            _expandedOwner.ExitFocusMode();
            return;
        }

        if (_isFocusMode)
        {
            ExitFocusMode();
            return;
        }

        EnterFocusMode();
    }

    private void EnterFocusMode()
    {
        AppShellView? shell =
            FindVisualParent<AppShellView>(this);

        if (shell is null ||
            shell.FindName("ReaderOverlayHost") is not FrameworkElement overlayHost ||
            shell.FindName("ReaderOverlayContent") is not ContentControl overlayContent)
        {
            return;
        }

        var expandedPreview = new MaterialPreviewView
        {
            Title = Title,
            TypeLabel = TypeLabel,
            TopicName = TopicName,
            Difficulty = Difficulty,
            IconKind = IconKind,
            IsQuestion = IsQuestion,
            Tags = Tags,
            BodyMarkdown = BodyMarkdown,
            ReferenceAnswerMarkdown = ReferenceAnswerMarkdown,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        expandedPreview._expandedOwner = this;
        expandedPreview._isTocExpanded = _isTocExpanded;
        expandedPreview.UpdateReadingControls();

        _focusShell = shell;
        _focusWindow = Window.GetWindow(this);
        _expandedPreview = expandedPreview;

        overlayContent.Content = expandedPreview;
        overlayHost.Visibility = Visibility.Visible;

        if (_focusWindow is not null)
        {
            _focusWindow.PreviewKeyDown +=
                FocusWindow_OnPreviewKeyDown;
        }

        _isFocusMode = true;
        UpdateReadingControls();
        expandedPreview.UpdateReadingControls();
    }

    private void ExitFocusMode()
    {
        if (_focusWindow is not null)
        {
            _focusWindow.PreviewKeyDown -=
                FocusWindow_OnPreviewKeyDown;
        }

        if (_focusShell is not null)
        {
            if (_focusShell.FindName("ReaderOverlayContent") is
                ContentControl overlayContent)
            {
                overlayContent.Content = null;
            }

            if (_focusShell.FindName("ReaderOverlayHost") is
                FrameworkElement overlayHost)
            {
                overlayHost.Visibility = Visibility.Collapsed;
            }
        }

        if (_expandedPreview is not null)
        {
            _expandedPreview._expandedOwner = null;
        }

        _expandedPreview = null;
        _focusWindow = null;
        _focusShell = null;
        _isFocusMode = false;
        UpdateReadingControls();
    }

    private void FocusWindow_OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape ||
            !_isFocusMode)
        {
            return;
        }

        ExitFocusMode();
        e.Handled = true;
    }

    private void MaterialPreviewView_OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_isFocusMode)
        {
            ExitFocusMode();
        }
    }

    private void UpdateReadingControls()
    {
        if (!_isInitialized)
        {
            return;
        }

        ReadingPanel.Width = _isTocExpanded
            ? ExpandedTocWidth
            : CollapsedTocWidth;

        TocTitle.Visibility = _isTocExpanded
            ? Visibility.Visible
            : Visibility.Collapsed;

        TocContentHost.Visibility = _isTocExpanded
            ? Visibility.Visible
            : Visibility.Collapsed;

        TocDivider.Visibility = _isTocExpanded
            ? Visibility.Visible
            : Visibility.Collapsed;

        TocHeader.Margin = _isTocExpanded
            ? new Thickness(12, 10, 10, 8)
            : new Thickness(3);

        ToggleTocButton.Width = _isTocExpanded
            ? 36
            : 40;

        ToggleTocButton.Height = _isTocExpanded
            ? 36
            : 40;

        ToggleTocMenuIcon.Visibility = _isTocExpanded
            ? Visibility.Collapsed
            : Visibility.Visible;

        ToggleTocIcon.Width = _isTocExpanded
            ? 18
            : 13;

        ToggleTocIcon.Height = _isTocExpanded
            ? 18
            : 13;

        ToggleTocIcon.Kind = _isTocExpanded
            ? PackIconKind.ChevronRight
            : PackIconKind.ChevronLeft;

        ToggleTocButton.ToolTip = _isTocExpanded
            ? "Свернуть содержание"
            : "Показать содержание";

        bool isExpandedPresentation =
            _isFocusMode || _expandedOwner is not null;

        FocusModeIcon.Kind = isExpandedPresentation
            ? PackIconKind.FullscreenExit
            : PackIconKind.Fullscreen;

        FocusModeButton.ToolTip = isExpandedPresentation
            ? "Свернуть статью (Esc)"
            : "Развернуть статью";
    }

    private static T? FindVisualParent<T>(
        DependencyObject child)
        where T : DependencyObject
    {
        DependencyObject? current = child;

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

    private sealed record MarkdownHeadingTag(
        int Level,
        string Text);

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
