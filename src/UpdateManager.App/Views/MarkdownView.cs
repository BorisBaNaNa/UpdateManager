using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace UpdateManager.App.Views;

/// <summary>
/// Markdown заметок выпуска в стиле «каталога» — ровно то подмножество, которым они пишутся:
/// «## версия», «### раздел», «- пункт», абзацы, **жирный**, `код` и HTTPS-ссылки внутри строки.
/// Остальное выводится как обычный текст.
/// </summary>
public sealed partial class MarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private static readonly FontFamily CodeFont = new("Cascadia Mono,Consolas,monospace");

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();
        var paragraph = new List<string>();

        foreach (var raw in (Markdown ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                FlushParagraph(paragraph);
                continue;
            }

            if (line.StartsWith("### "))
            {
                FlushParagraph(paragraph);
                Children.Add(new TextBlock { Classes = { "heading" }, Text = line[4..], Margin = new Thickness(0, 18, 0, 8) });
            }
            else if (line.StartsWith("## "))
            {
                FlushParagraph(paragraph);
                Children.Add(new TextBlock { Classes = { "noteversion", "number" }, Text = line[3..], Margin = new Thickness(0, 0, 0, 12) });
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                FlushParagraph(paragraph);
                Children.Add(Bullet(line[2..]));
            }
            else if (line.StartsWith("  ") && Children.Count > 0 && Children[^1] is Grid)
            {
                // Продолжение пункта списка (перенос с отступом) — дописываем в последний пункт.
                var text = (SelectableTextBlock)((Grid)Children[^1]).Children[1];
                text.Inlines!.Add(new Run(" "));
                AddInlines(text.Inlines, line.Trim());
            }
            else
            {
                paragraph.Add(line.Trim());
            }
        }
        FlushParagraph(paragraph);
    }

    private void FlushParagraph(List<string> lines)
    {
        if (lines.Count == 0)
            return;
        var text = Body();
        text.Margin = new Thickness(0, 0, 0, 10);
        AddInlines(text.Inlines!, string.Join(" ", lines));
        Children.Add(text);
        lines.Clear();
    }

    private static Grid Bullet(string content)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*"), Margin = new Thickness(0, 0, 0, 6) };
        grid.Children.Add(new TextBlock { Classes = { "muted" }, Text = "–", LineHeight = 21 });
        var text = Body();
        Grid.SetColumn(text, 1);
        AddInlines(text.Inlines!, content);
        grid.Children.Add(text);
        return grid;
    }

    private static SelectableTextBlock Body() =>
        new() { TextWrapping = TextWrapping.Wrap, LineHeight = 21, Inlines = new InlineCollection() };

    // **жирный**, `код` и именованные HTTPS-ссылки — разметка внутри строки.
    private static void AddInlines(InlineCollection inlines, string text)
    {
        foreach (var part in InlineToken().Split(text))
        {
            if (part.Length == 0)
                continue;
            var link = LinkToken().Match(part);
            if (link.Success && Uri.TryCreate(link.Groups[2].Value, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps)
            {
                var button = new HyperlinkButton
                {
                    Content = new TextBlock { Text = link.Groups[1].Value, TextWrapping = TextWrapping.Wrap },
                    NavigateUri = uri,
                    Padding = new Thickness(0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                ToolTip.SetTip(button, uri.AbsoluteUri);
                inlines.Add(new InlineUIContainer { Child = button });
            }
            else if (part.Length > 4 && part.StartsWith("**") && part.EndsWith("**"))
                inlines.Add(new Run(part[2..^2]) { FontWeight = FontWeight.SemiBold });
            else if (part.Length > 2 && part.StartsWith('`') && part.EndsWith('`'))
                inlines.Add(new Run(part[1..^1]) { FontFamily = CodeFont, FontSize = 13 });
            else
                inlines.Add(new Run(part));
        }
    }

    [GeneratedRegex(@"(\[[^\]]+\]\(https://[^\s)]+\)|\*\*[^*]+\*\*|`[^`]+`)")]
    private static partial Regex InlineToken();

    [GeneratedRegex(@"^\[([^\]]+)\]\((https://[^\s)]+)\)$")]
    private static partial Regex LinkToken();
}
