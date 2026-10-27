using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.App.Services;
using UpdateManager.Core.Common;
using UpdateManager.Core.Delivery;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Реквизиты FTP-подключения для доставки. По «Сохранить» заполняет <see cref="Result"/>.
/// Реквизиты хранятся в профиле пользователя (пароль — DPAPI), а не в файле проекта.
/// «Папка с патчами» — общий корень всех патчей; реальный путь загрузки = корень + директория
/// загрузки проекта (строка «Итоговый путь»).
/// </summary>
public sealed partial class FtpConnectionViewModel : DialogViewModel
{
    private readonly IDialogService _dialogs;
    private readonly string _uploadDirectory;

    public FtpConnectionViewModel(FtpConnection current, string uploadDirectory, IDialogService dialogs)
    {
        _dialogs = dialogs;
        _uploadDirectory = uploadDirectory ?? "";

        _host = current.Host;
        _port = Math.Clamp(current.Port, 1, 65535);
        _username = current.Username;
        _password = current.Password;
        _remotePath = current.RemotePath;
        _privateKeyRemotePath = current.PrivateKeyRemotePath;
    }

    public FtpConnection? Result { get; private set; }

    [ObservableProperty] private string _host;
    [ObservableProperty] private decimal? _port;
    [ObservableProperty] private string _username;
    [ObservableProperty] private string _password;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ComputedPath))]
    private string _remotePath;

    [ObservableProperty] private string _privateKeyRemotePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestButtonText))]
    [NotifyCanExecuteChangedFor(nameof(TestCommand), nameof(SaveCommand))]
    private bool _isTesting;

    public string ComputedPath => "Итоговый путь загрузки: " + DownloadUrl.CombineRemote(RemotePath, _uploadDirectory);

    public string TestButtonText => IsTesting ? "Проверка…" : "Проверить соединение";

    // Считать реквизиты из полей; null = не заполнен хост (с предупреждением).
    private async Task<FtpConnection?> ReadConnectionAsync()
    {
        var host = Host.Trim();
        if (host.Length == 0)
        {
            await _dialogs.ShowWarningAsync("FTP-подключение", "Укажите хост FTP-сервера.");
            return null;
        }

        return new FtpConnection
        {
            Host = host,
            Port = (int)(Port ?? FtpConnection.DefaultPort),
            Username = Username.Trim(),
            Password = Password,
            RemotePath = RemotePath.Trim(),
            PrivateKeyRemotePath = PrivateKeyRemotePath.Trim()
        };
    }

    private bool IsIdle() => !IsTesting;

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task Save()
    {
        var conn = await ReadConnectionAsync();
        if (conn == null)
            return;

        Result = conn;
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);

    // Выбор папки на сервере через браузер (по текущим реквизитам формы).
    [RelayCommand]
    private async Task BrowseRemote()
    {
        var conn = await ReadConnectionAsync();
        if (conn == null)
            return;

        var path = await _dialogs.BrowseFtpFolderAsync(conn);
        if (path != null)
            RemotePath = path;
    }

    // Выбор ОТДЕЛЬНОЙ папки ключа тем же серверным браузером.
    [RelayCommand]
    private async Task BrowsePrivateKey()
    {
        var conn = await ReadConnectionAsync();
        if (conn == null)
            return;

        var path = await _dialogs.BrowseFtpFolderAsync(conn);
        if (path != null)
            PrivateKeyRemotePath = path;
    }

    // Проверка соединения в фоне, чтобы не вешать UI на недоступном сервере.
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task Test()
    {
        var conn = await ReadConnectionAsync();
        if (conn == null)
            return;

        IsTesting = true;
        FtpTestResult result;
        try
        {
            result = await Task.Run(() => FtpConnectionTester.Test(conn));
        }
        finally
        {
            IsTesting = false;
        }

        if (result.Success)
            await _dialogs.ShowInfoAsync(result.Message);
        else
            await _dialogs.ShowErrorAsync("Проверка соединения — ошибка:\n" + result.Message);
    }
}
