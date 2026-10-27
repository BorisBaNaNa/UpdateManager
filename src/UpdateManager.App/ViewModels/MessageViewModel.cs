using CommunityToolkit.Mvvm.Input;

namespace UpdateManager.App.ViewModels;

public enum MessageKind { Info, Warning, Error, Question }

/// <summary>Окно сообщения / вопроса да-нет (замена MessageBox).</summary>
public sealed partial class MessageViewModel : DialogViewModel
{
    public MessageViewModel(string title, string message, MessageKind kind)
    {
        Title = title;
        Message = message;
        Kind = kind;
    }

    public string Title { get; }
    public string Message { get; }
    public MessageKind Kind { get; }

    public bool IsQuestion => Kind == MessageKind.Question;
    public bool IsError => Kind == MessageKind.Error;
    public string OkText => IsQuestion ? "Да" : "OK";

    [RelayCommand]
    private void Ok() => Close(true);

    [RelayCommand]
    private void Cancel() => Close(false);
}
