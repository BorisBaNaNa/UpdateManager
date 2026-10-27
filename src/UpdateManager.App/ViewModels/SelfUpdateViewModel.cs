using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.App.Services;
using UpdateManager.Core.SelfUpdate;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Самообновление UpdateManager (порт SelfUpdatePresenter из 1.x): тихая проверка при старте +
/// ручная из меню, скачивание в окне прогресса и перезапуск через self-patcher. После обновления —
/// один раз окно «Что нового» (и по «Справка → Что нового…»).
/// </summary>
public sealed partial class SelfUpdateViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly SelfUpdateConfig _config;
    private readonly ReleaseNotesService _notes;

    public SelfUpdateViewModel(IDialogService dialogs, SelfUpdateConfig config, ReleaseNotesService notes)
    {
        _dialogs = dialogs;
        _config = config;
        _notes = notes;
    }

    /// <summary>Скачано и запущен self-patcher — приложение должно выйти, чтобы он подменил файлы.</summary>
    public event EventHandler? ShutdownRequested;

    // Идёт проверка — вторую параллельно не запускаем. Пункт меню при этом НЕ гасим: ручной клик
    // во время фоновой проверки просит показать её результат (раньше пункт серел до конца проверки).
    [ObservableProperty]
    private bool _isChecking;

    // Пользователь нажал «Проверить обновления…», пока шла фоновая проверка, — показать её итог.
    private bool _reportRunningCheck;

    /// <summary>Окно впервые показано — «Что нового» (если версия новая), затем фоновая проверка.</summary>
    public void OnStartup() => _ = StartupAsync();

    private async Task StartupAsync()
    {
        // Под отладчиком не показываем и не проверяем сами: отметка «показано» общая с установленной
        // программой (в %AppData%), а обновление перезатёрло бы bin\Debug.
        if (Debugger.IsAttached)
            return;

        await ShowWhatsNewIfUpdated(SelfIdentity.Version);
        if (_config.IsEnabled)
            await RunCheck(manual: false);
    }

    /// <summary>Заметки выпусков, которых эта установка ещё не показывала, — один раз.</summary>
    public async Task ShowWhatsNewIfUpdated(string currentVersion)
    {
        var unseen = _notes.UnseenFor(currentVersion);
        _notes.MarkSeen(currentVersion);
        if (unseen.Count > 0)
            await _dialogs.ShowReleaseNotesAsync(unseen, "Обновлено до " + VersionText(currentVersion));
    }

    // «Справка → Что нового…» — все заметки.
    [RelayCommand]
    private Task ShowReleaseNotes() =>
        _dialogs.ShowReleaseNotesAsync(_notes.All(), "Версия " + VersionText(SelfIdentity.Version));

    private static string VersionText(string version)
    {
        var v = ReleaseNotesService.Normalize(version);
        return v == null ? version : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private bool CanCheck() => _config.IsEnabled;

    // «Справка → Проверить обновления…»
    [RelayCommand(CanExecute = nameof(CanCheck), AllowConcurrentExecutions = true)]
    private async Task Check()
    {
        if (IsChecking)
        {
            _reportRunningCheck = true;
            await _dialogs.ShowInfoAsync("Проверка обновлений уже идёт — покажу результат, как только она закончится " +
                "(не дольше " + (int)SelfUpdateChecker.Timeout.TotalSeconds + " с).");
            return;
        }

        if (Debugger.IsAttached && !await _dialogs.ConfirmAsync(
                "Программа запущена под отладчиком: обновление заменит файлы в папке сборки.\n" +
                "Всё равно проверить?"))
            return;

        await RunCheck(manual: true);
    }

    private async Task RunCheck(bool manual)
    {
        if (IsChecking)
            return;

        IsChecking = true;
        SelfUpdateCheckResult result;
        try
        {
            // Проверка блокирующая (сеть + хеши файлов) — в фоне.
            result = await Task.Run(() => new SelfUpdateChecker(_config).Check());
        }
        catch (Exception ex)
        {
            result = new SelfUpdateCheckResult { Status = SelfUpdateStatus.Failed, NewVersion = "", Error = ex.Message };
        }
        finally
        {
            IsChecking = false;
        }

        manual |= _reportRunningCheck;
        _reportRunningCheck = false;

        await OnChecked(result, manual);
    }

    private async Task OnChecked(SelfUpdateCheckResult result, bool manual)
    {
        switch (result.Status)
        {
            case SelfUpdateStatus.UpToDate:
                if (manual)
                    await _dialogs.ShowInfoAsync("У вас последняя версия UpdateManager (" + SelfIdentity.Version + ").");
                break;

            case SelfUpdateStatus.Failed:
                // При старте сервер может быть просто недоступен — не мешаем работать.
                if (manual)
                    await _dialogs.ShowErrorAsync("Не удалось проверить обновления:\n" + result.Error);
                break;

            case SelfUpdateStatus.UpdateAvailable:
                // Фоновая проверка закончилась посреди сборки/доставки — не всплываем поверх.
                if (!manual && _dialogs.IsBusy)
                    return;

                var version = result.NewVersion.Length > 0 ? "Доступна версия " + result.NewVersion : "Доступно обновление";
                if (await _dialogs.ConfirmAsync(version + " (у вас " + SelfIdentity.Version + ").\nОбновить сейчас?"))
                    await Update();
                break;
        }
    }

    private async Task Update()
    {
        var operation = new SelfUpdateOperation(_config);
        await _dialogs.ShowOperationAsync(operation);

        if (operation.AlreadyUpToDate)
        {
            await _dialogs.ShowInfoAsync("Обновлять нечего — у вас уже последняя версия.");
            return;
        }

        if (!operation.Succeeded)
            return; // ошибку (в т.ч. подписи) окно прогресса уже показало

        if (!await _dialogs.ConfirmAsync("Обновление скачано. Перезапустить UpdateManager сейчас?\n" +
                "(Если нет — обновиться можно позже: «Справка → Проверить обновления…».)"))
            return;

        try
        {
            operation.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(ex.Message);
            return;
        }

        ShutdownRequested?.Invoke(this, EventArgs.Empty); // self-patcher ждёт выхода, чтобы подменить файлы
    }
}
