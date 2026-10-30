using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;

namespace UpdateManager.App.Services;

/// <summary>
/// Диалоги и системные действия для ViewModel'ей. VM просит результат и не знает про окна —
/// в тестах подставляется фейк. Отмена во всех диалогах = null/false.
/// </summary>
public interface IDialogService
{
    /// <summary>Открыт модальный диалог/операция — всплывающие предложения сейчас неуместны.</summary>
    bool IsBusy { get; }

    Task<string?> PickFolderAsync(string title);
    Task<string?> PickExecutableAsync(string title, string? initialDirectory);

    /// <summary>Ввод строки. Пустая строка/Отмена → null.</summary>
    Task<string?> PromptAsync(string title, string message, string defaultValue);

    Task<bool> ConfirmAsync(string message);
    Task ShowErrorAsync(string message);
    Task ShowInfoAsync(string message);
    Task ShowWarningAsync(string title, string message);

    /// <summary>Модальное окно операции движка (сборка/проверка/заливка) — до её завершения.</summary>
    Task ShowOperationAsync(IEngineOperation operation);

    Task<ProjectSettings?> EditSettingsAsync(ProjectSettings current);
    /// <summary>Метод и путь доставки. deliverNow — окно открыто перед доставкой (кнопка «Доставить»), иначе «Сохранить».</summary>
    Task<DeliveryConfig?> ConfigureDeliveryAsync(DeliveryConfig current, bool deliverNow);
    Task<FtpConnection?> ConfigureFtpAsync(FtpConnection current, string uploadDirectory);

    /// <summary>Выбор папки на FTP-сервере по реквизитам. null = отмена.</summary>
    Task<string?> BrowseFtpFolderAsync(FtpConnection connection);

    /// <summary>Окно «Что нового»: заметки выпусков (от новых к старым) и строка под заголовком.</summary>
    Task ShowReleaseNotesAsync(IReadOnlyList<ReleaseNote> notes, string subtitle);

    /// <summary>Немодальное руководство: можно читать рядом с настройками проекта.</summary>
    Task ShowUpdateGuideAsync();

    Task ShowAboutAsync(ViewModels.SelfUpdateViewModel selfUpdate);

    void OpenInExplorer(string path);
}
