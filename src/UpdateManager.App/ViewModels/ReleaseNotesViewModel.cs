using CommunityToolkit.Mvvm.Input;
using UpdateManager.App.Services;

namespace UpdateManager.App.ViewModels;

/// <summary>Окно «Что нового»: заметки выпусков от новых к старым.</summary>
public sealed partial class ReleaseNotesViewModel : DialogViewModel
{
    public ReleaseNotesViewModel(IReadOnlyList<ReleaseNote> notes, string subtitle)
    {
        Notes = notes;
        Subtitle = subtitle;
    }

    public IReadOnlyList<ReleaseNote> Notes { get; }

    /// <summary>Строка под заголовком: «Обновлено до 2.0.1» или «Версия 2.0.1».</summary>
    public string Subtitle { get; }

    public bool IsEmpty => Notes.Count == 0;

    [RelayCommand]
    private void Dismiss() => Close(true);
}
