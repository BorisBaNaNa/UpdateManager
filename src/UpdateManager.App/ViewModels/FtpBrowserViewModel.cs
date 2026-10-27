using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentFTP;
using UpdateManager.App.Services;
using UpdateManager.Core.Delivery;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Браузер папок на FTP-сервере (дерево с ленивой подгрузкой). Держит одно подключение на время
/// жизни окна; листинг каждого узла — в фоне. По «Выбрать» возвращает путь в <see cref="SelectedPath"/>.
/// </summary>
public sealed partial class FtpBrowserViewModel : DialogViewModel, IDisposable
{
    private const int TimeoutMs = 8000;

    private readonly FtpConnection _conn;
    private readonly IDialogService _dialogs;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private FtpClient? _client;

    public FtpBrowserViewModel(FtpConnection conn, IDialogService dialogs)
    {
        _conn = conn;
        _dialogs = dialogs;
    }

    public ObservableCollection<FtpFolderNode> Roots { get; } = new();

    /// <summary>Выбранный путь (валиден после закрытия с OK).</summary>
    public string? SelectedPath { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    private FtpFolderNode? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "";

    /// <summary>Подключение + загрузка корня (вызывается при открытии окна).</summary>
    public async Task ConnectAsync()
    {
        SetBusy(true, "Подключение к " + _conn.Host + " …");
        string root;
        FtpListItem[] items;
        try
        {
            (root, items) = await RunLocked(() =>
            {
                _client = new FtpClient(_conn.Host, _conn.Username, _conn.Password, _conn.Port);
                _client.Config.ConnectTimeout = TimeoutMs;
                _client.Config.DataConnectionConnectTimeout = TimeoutMs;
                _client.Connect();

                var dir = _client.GetWorkingDirectory();
                if (string.IsNullOrEmpty(dir))
                    dir = "/";
                return (dir, _client.GetListing(dir));
            });
        }
        catch (Exception ex)
        {
            SetBusy(false, "");
            await _dialogs.ShowErrorAsync("Не удалось подключиться:\n" + FtpErrorText.Describe(ex));
            Close(false);
            return;
        }

        var rootNode = new FtpFolderNode(this, root, root);
        rootNode.SetChildren(items);
        rootNode.IsExpanded = true;
        Roots.Add(rootNode);
        Selected = rootNode;
        SetBusy(false, "");
    }

    // Лениво грузим детей узла (вызывается узлом при первом раскрытии).
    internal async Task LoadChildrenAsync(FtpFolderNode node)
    {
        SetBusy(true, "Загрузка " + node.FullPath + " …");
        node.ShowInfo("Загрузка…");
        try
        {
            var items = await RunLocked(() => _client!.GetListing(node.FullPath));
            node.SetChildren(items);
        }
        catch (Exception ex)
        {
            node.ShowInfo("Ошибка: " + FtpErrorText.Describe(ex));
        }
        finally
        {
            SetBusy(false, "");
        }
    }

    private bool CanSelect() => !IsBusy && Selected is { IsFolder: true };

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select()
    {
        SelectedPath = Selected!.FullPath;
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);

    private void SetBusy(bool busy, string status)
    {
        IsBusy = busy;
        Status = status;
    }

    private async Task<T> RunLocked<T>(Func<T> work)
    {
        await _clientLock.WaitAsync();
        try
        {
            return await Task.Run(work);
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            _client?.Dispose(); // Dispose закрывает соединение
        }
        catch
        {
            // при закрытии окна ошибки разрыва не важны
        }
    }
}

/// <summary>Узел дерева: папка на сервере либо служебная строка («Загрузка…», «Ошибка: …»).</summary>
public sealed partial class FtpFolderNode : ObservableObject
{
    private readonly FtpBrowserViewModel? _owner;
    private bool _loaded;

    public FtpFolderNode(FtpBrowserViewModel owner, string name, string fullPath)
    {
        _owner = owner;
        Name = name;
        FullPath = fullPath;
        IsFolder = true;
        // Заглушка, чтобы папку можно было раскрыть до загрузки.
        Children.Add(new FtpFolderNode(""));
    }

    private FtpFolderNode(string info)
    {
        Name = info;
        FullPath = "";
        _loaded = true;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsFolder { get; }
    public ObservableCollection<FtpFolderNode> Children { get; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_loaded && _owner != null)
        {
            _loaded = true;
            _ = _owner.LoadChildrenAsync(this);
        }
    }

    // Только подпапки, по имени.
    internal void SetChildren(IEnumerable<FtpListItem> items)
    {
        _loaded = true;
        Children.Clear();
        foreach (var dir in items
                     .Where(i => i.Type == FtpObjectType.Directory)
                     .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            Children.Add(new FtpFolderNode(_owner!, dir.Name, dir.FullName));
    }

    internal void ShowInfo(string text)
    {
        Children.Clear();
        Children.Add(new FtpFolderNode(text));
    }
}
