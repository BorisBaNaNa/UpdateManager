using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using UpdateManager.App.ViewModels;
using UpdateManager.App.Views;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;

namespace UpdateManager.App.Services;

/// <summary>
/// Avalonia-реализация диалогов. Владелец каждого модального окна — верхнее из уже открытых
/// (вложенные диалоги: FTP-реквизиты → браузер → сообщение), иначе главное окно.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Func<Window?> _mainWindow;
    private readonly List<Window> _open = new();
    private int _pickers; // открыт системный диалог выбора файла/папки

    public DialogService(Func<Window?> mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public bool IsBusy => _open.Count > 0 || _pickers > 0;

    private Window Owner => _open.Count > 0
        ? _open[^1]
        : _mainWindow() ?? throw new InvalidOperationException("Главное окно ещё не создано.");

    private async Task<bool> ShowAsync(Window window)
    {
        var owner = Owner;
        _open.Add(window);
        try
        {
            return await window.ShowDialog<bool>(owner);
        }
        finally
        {
            _open.Remove(window);
        }
    }

    // --- файлы и папки ---

    public async Task<string?> PickFolderAsync(string title)
    {
        _pickers++;
        try
        {
            var result = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
            return result.Count > 0 ? result[0].TryGetLocalPath() : null;
        }
        finally
        {
            _pickers--;
        }
    }

    public async Task<string?> PickExecutableAsync(string title, string? initialDirectory)
    {
        _pickers++;
        try
        {
            var storage = Owner.StorageProvider;
            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Исполняемые файлы (*.exe)") { Patterns = new[] { "*.exe" } },
                    FilePickerFileTypes.All
                }
            };
            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(initialDirectory);

            var result = await storage.OpenFilePickerAsync(options);
            return result.Count > 0 ? result[0].TryGetLocalPath() : null;
        }
        finally
        {
            _pickers--;
        }
    }

    // --- сообщения и ввод ---

    public async Task<string?> PromptAsync(string title, string message, string defaultValue)
    {
        var vm = new PromptViewModel(title, message, defaultValue);
        if (!await ShowAsync(new PromptWindow { DataContext = vm }))
            return null;

        var value = (vm.Value ?? "").Trim();
        return value.Length > 0 ? value : null; // пусто/Отмена -> null
    }

    public Task<bool> ConfirmAsync(string message) =>
        ShowMessage("Подтверждение", message, MessageKind.Question);

    public Task ShowErrorAsync(string message) =>
        ShowMessage("Ошибка", message, MessageKind.Error);

    public Task ShowInfoAsync(string message) =>
        ShowMessage("Готово", message, MessageKind.Info);

    public Task ShowWarningAsync(string title, string message) =>
        ShowMessage(title, message, MessageKind.Warning);

    private Task<bool> ShowMessage(string title, string message, MessageKind kind) =>
        ShowAsync(new MessageWindow { DataContext = new MessageViewModel(title, message, kind) });

    // --- окна приложения ---

    public async Task ShowOperationAsync(IEngineOperation operation)
    {
        var vm = new OperationViewModel(operation);
        var window = new OperationWindow { DataContext = vm };
        window.Opened += (_, _) => vm.Start();
        await ShowAsync(window);
    }

    public async Task<ProjectSettings?> EditSettingsAsync(ProjectSettings current)
    {
        var vm = new ProjectSettingsViewModel(current);
        return await ShowAsync(new ProjectSettingsWindow { DataContext = vm }) ? vm.Result : null;
    }

    public async Task<DeliveryConfig?> ConfigureDeliveryAsync(DeliveryConfig current, bool deliverNow)
    {
        var vm = new DeliveryViewModel(current, this, deliverNow);
        return await ShowAsync(new DeliveryWindow { DataContext = vm }) ? vm.Result : null;
    }

    public async Task<FtpConnection?> ConfigureFtpAsync(FtpConnection current, string uploadDirectory)
    {
        var vm = new FtpConnectionViewModel(current, uploadDirectory, this);
        return await ShowAsync(new FtpConnectionWindow { DataContext = vm }) ? vm.Result : null;
    }

    public async Task<string?> BrowseFtpFolderAsync(FtpConnection connection)
    {
        using var vm = new FtpBrowserViewModel(connection, this);
        var window = new FtpBrowserWindow { DataContext = vm };
        window.Opened += async (_, _) => await vm.ConnectAsync();
        return await ShowAsync(window) ? vm.SelectedPath : null;
    }

    public void OpenInExplorer(string path)
    {
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path } });
    }
}
