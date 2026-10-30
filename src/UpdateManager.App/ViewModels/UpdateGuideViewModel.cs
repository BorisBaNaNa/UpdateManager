using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.App.Services;

namespace UpdateManager.App.ViewModels;

public sealed partial class GuideTopic : ObservableObject
{
    public GuideTopic(string title, string markdown)
    {
        Title = title;
        Markdown = markdown;
    }

    public string Title { get; }
    public string Markdown { get; }
    [ObservableProperty] private bool _isExpanded;
}

public sealed partial class GuideStep : ObservableObject
{
    public GuideStep(string title, string heading, string description, IReadOnlyList<GuideTopic> topics)
    {
        Title = title;
        Heading = heading;
        Description = description;
        Topics = topics;
        Topics[0].IsExpanded = true;
    }

    public string Title { get; }
    public string Heading { get; }
    public string Description { get; }
    public IReadOnlyList<GuideTopic> Topics { get; }
    [ObservableProperty] private bool _isSelected;
}

/// <summary>Маршрут первого обновления: текст из документации, состояние только для чтения.</summary>
public sealed partial class UpdateGuideViewModel : DialogViewModel
{
    private readonly IDialogService _dialogs;

    public UpdateGuideViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UpdateSystem.UserGuide.md")
            ?? throw new InvalidOperationException("Не найдена встроенная инструкция подключения обновлений.");
        using var reader = new StreamReader(stream);
        var parts = reader.ReadToEnd().Replace("\r", "").Split("\n### ").Skip(1);
        var topics = parts.Select(part =>
        {
            var end = part.IndexOf('\n');
            return new GuideTopic(part[..end].Trim(), part[(end + 1)..].Trim());
        }).ToArray();
        Steps = new[]
        {
            new GuideStep("1. Подготовить приложение", "Подготовьте клиент обновлений",
                "UpdateManager публикует файлы. Ваше приложение проверяет версию, скачивает обновление и предлагает установку.", topics[..4]),
            new GuideStep("2. Настроить UpdateManager", "Настройте издатель",
                "Создайте проект обновлений, выберите сборки, задайте канал и подпись.", topics[4..5]),
            new GuideStep("3. Проверить A → B", "Проверьте первое обновление",
                "Пройдите весь путь в тестовом канале: от установленной A до новой B с сохранёнными данными.", topics[5..])
        };
        SelectedStep = Steps[0];
    }

    public IReadOnlyList<GuideStep> Steps { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(NextText), nameof(Position), nameof(IsLast))]
    private GuideStep _selectedStep = null!;

    public bool CanGoBack => SelectedStep != Steps[0];
    public bool IsLast => SelectedStep == Steps[^1];
    public string NextText => SelectedStep == Steps[^1] ? "Закрыть" : SelectedStep == Steps[0] ? "К настройке издателя" : "К проверке A → B";
    public string Position => "Этап " + (Steps.ToList().IndexOf(SelectedStep) + 1) + " из " + Steps.Count;

    partial void OnSelectedStepChanged(GuideStep? oldValue, GuideStep newValue)
    {
        if (oldValue != null)
            oldValue.IsSelected = false;
        newValue.IsSelected = true;
    }

    [RelayCommand]
    private void SelectStep(GuideStep? step)
    {
        if (step != null && Steps.Contains(step))
            SelectedStep = step;
    }

    [RelayCommand]
    private void Previous()
    {
        if (CanGoBack)
            SelectedStep = Steps[Steps.ToList().IndexOf(SelectedStep) - 1];
    }

    [RelayCommand]
    private void Next()
    {
        if (SelectedStep == Steps[^1])
            Close(false);
        else
            SelectedStep = Steps[Steps.ToList().IndexOf(SelectedStep) + 1];
    }

    [RelayCommand] private void Dismiss() => Close(false);
    [RelayCommand] private void OpenDocuments() => _dialogs.OpenInExplorer(Path.Combine(AppContext.BaseDirectory, "docs"));
}
