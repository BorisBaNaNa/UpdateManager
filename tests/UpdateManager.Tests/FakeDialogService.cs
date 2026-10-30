using System.Diagnostics;
using UpdateManager.App.Services;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;

namespace UpdateManager.Tests;

/// <summary>
/// Сценарный фейк диалогов: ответы задаются заранее, показанные сообщения записываются.
/// Операции движка прогоняются до конца синхронно (как модальное окно).
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    public Queue<string?> Folders { get; } = new();
    public Queue<string?> Prompts { get; } = new();
    public Func<string, bool> ConfirmAnswer { get; set; } = _ => true;
    public Func<ProjectSettings, ProjectSettings?> SettingsAnswer { get; set; } = s => s;
    public Func<DeliveryConfig, DeliveryConfig?> DeliveryAnswer { get; set; } = c => c;

    public List<string> Confirms { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Infos { get; } = new();
    public List<IEngineOperation> Operations { get; } = new();
    public List<(IReadOnlyList<ReleaseNote> Notes, string Subtitle)> ReleaseNotes { get; } = new();

    public Task ShowReleaseNotesAsync(IReadOnlyList<ReleaseNote> notes, string subtitle)
    {
        ReleaseNotes.Add((notes, subtitle));
        return Task.CompletedTask;
    }

    public bool IsBusy => false;
    public int UpdateGuideShown { get; private set; }
    public Task ShowUpdateGuideAsync()
    {
        UpdateGuideShown++;
        return Task.CompletedTask;
    }

    public Task<string?> PickFolderAsync(string title) => Task.FromResult(Folders.Dequeue());

    public Task<string?> PickExecutableAsync(string title, string? initialDirectory) => Task.FromResult<string?>(null);

    public Task<string?> PromptAsync(string title, string message, string defaultValue) =>
        Task.FromResult(Prompts.Count > 0 ? Prompts.Dequeue() : defaultValue);

    public Task<bool> ConfirmAsync(string message)
    {
        Confirms.Add(message);
        return Task.FromResult(ConfirmAnswer(message));
    }

    public Task ShowErrorAsync(string message)
    {
        Errors.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string message)
    {
        Infos.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowWarningAsync(string title, string message)
    {
        Errors.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowOperationAsync(IEngineOperation operation)
    {
        Operations.Add(operation);
        if (!operation.Start())
            return Task.CompletedTask;

        var timer = Stopwatch.StartNew();
        while (operation.IsRunning && timer.Elapsed < TimeSpan.FromMinutes(2))
        {
            while (operation.FetchLog() != null) { }
            Thread.Sleep(50);
        }
        return Task.CompletedTask;
    }

    public Task<ProjectSettings?> EditSettingsAsync(ProjectSettings current) => Task.FromResult(SettingsAnswer(current));

    public Task<DeliveryConfig?> ConfigureDeliveryAsync(DeliveryConfig current, bool deliverNow) => Task.FromResult(DeliveryAnswer(current));

    public Task<FtpConnection?> ConfigureFtpAsync(FtpConnection current, string uploadDirectory) =>
        Task.FromResult<FtpConnection?>(null);

    public Task<string?> BrowseFtpFolderAsync(FtpConnection connection) => Task.FromResult<string?>(null);

    public void OpenInExplorer(string path) { }
}
