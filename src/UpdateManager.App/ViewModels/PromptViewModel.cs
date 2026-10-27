using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace UpdateManager.App.ViewModels;

/// <summary>Ввод одной строки (имя проекта, версия патча) — замена InputBox из 1.x.</summary>
public sealed partial class PromptViewModel : DialogViewModel
{
    public PromptViewModel(string title, string message, string value)
    {
        Title = title;
        Message = message;
        _value = value;
    }

    public string Title { get; }
    public string Message { get; }

    [ObservableProperty]
    private string _value;

    [RelayCommand]
    private void Ok() => Close(true);

    [RelayCommand]
    private void Cancel() => Close(false);
}
