using SimplePatchToolCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Project;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Каталог выпусков: удаление и откат версий. Одна — контекстное меню записи; несколько — выделение областью:
/// удержание записи 1 секунду задаёт точку начала, протяжка выделяет записи от неё до текущей (назад к точке —
/// выделение сжимается), по отпусканию — меню для выделенного. Меню закрыли, ничего не выбрав, — выделение спадает.
/// </summary>
public sealed partial class MainWindowViewModel
{
    [ObservableProperty] private bool _isMarkingReleases;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteMarkedText))]
    private int _markedCount;

    public string DeleteMarkedText => "Удалить выделенные (" + MarkedCount + ")…";

    private ReleaseItem? _markAnchor;
    private bool _marksInUse;

    /// <summary>Начать выделение областью с записи <paramref name="anchor"/>.</summary>
    public void StartMarking(ReleaseItem anchor)
    {
        IsMarkingReleases = true;
        _markAnchor = anchor;
        MarkTo(anchor);
    }

    /// <summary>
    /// Выделить записи от точки начала до <paramref name="current"/> включительно (что можно удалить);
    /// остальные — снять: протяжка назад к точке сжимает выделение.
    /// </summary>
    public void MarkTo(ReleaseItem current)
    {
        if (!IsMarkingReleases || _markAnchor == null)
            return;
        var from = Releases.IndexOf(_markAnchor);
        var to = Releases.IndexOf(current);
        if (from < 0 || to < 0)
            return;
        var (low, high) = from <= to ? (from, to) : (to, from);
        for (int i = 0; i < Releases.Count; i++)
            Releases[i].IsChecked = i >= low && i <= high && Releases[i].CanDelete;
        MarkedCount = Releases.Count(r => r.IsChecked);
    }

    /// <summary>Выделение закончилось ничем (меню закрыли мимо, отпустили на пустом) — снять всё.</summary>
    public void EndMarking()
    {
        if (_marksInUse)
            return; // команда меню уже забрала выделенное — снимет сама
        ClearMarks();
    }

    private void ClearMarks()
    {
        foreach (var item in Releases)
            item.IsChecked = false;
        MarkedCount = 0;
        _markAnchor = null;
        IsMarkingReleases = false;
    }

    [RelayCommand]
    private Task DeleteRelease() =>
        SelectedRelease == null ? Task.CompletedTask : DeleteReleases(new[] { SelectedRelease });

    [RelayCommand]
    private async Task DeleteMarkedReleases()
    {
        _marksInUse = true;
        try
        {
            await DeleteReleases(Releases.Where(r => r.IsChecked).ToList());
        }
        finally
        {
            _marksInUse = false;
            ClearMarks();
        }
    }

    private async Task DeleteReleases(IReadOnlyList<ReleaseItem> items)
    {
        if (_project == null || items.Count == 0)
            return;

        var blocked = items.FirstOrDefault(r => !r.CanDelete);
        if (blocked != null)
        {
            await _dialogs.ShowErrorAsync(blocked.IsNew
                ? "Запись «новый» — это билд, который ещё не собран: удалять нечего."
                : "Выпуск " + blocked.Version + " — текущий для " +
                  string.Join(" и ", blocked.Platforms.Where(p => p.IsCurrent).Select(p => p.Platform)) +
                  ": его нельзя удалить (на сервере он, и из него собирается следующий патч). Удаляются только старые выпуски.");
            return;
        }

        var versions = items.Select(r => r.Version).ToList();
        var list = string.Join(", ", versions);
        if (!await _dialogs.ConfirmAsync((versions.Count == 1 ? "Удалить версию " : "Удалить версии ") + list + "?\n\n" +
                "Папки " + ProjectService.VersionsFolder + "/<версия> уйдут в Корзину, записи этапов — из проекта; " +
                "номера остальных выпусков не изменятся.\nOutput/ и сервер не меняются — клиенты на этих версиях " +
                "обновятся как раньше. Откатить сервер на удалённую версию будет нельзя."))
            return;

        try
        {
            _service.DeleteVersions(_project.RootPath, versions);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Версии не удалены:\n" + ex.Message);
        }
        _project = _service.Open(_project.RootPath);
        ShowProject();
    }

    /// <summary>
    /// Откатить сервер на выбранный выпуск: Windows-версии новее него — в Корзину, Output пересобирается так,
    /// что этот выпуск последний (ProjectService.RollbackTo). Дальше — доставка и проверка, как обычно; движок
    /// понижает версию у клиентов (проверено EnginePipelineTests.RollbackTo_…).
    /// </summary>
    [RelayCommand]
    private async Task RollbackRelease()
    {
        var release = SelectedRelease;
        if (_project == null || release == null)
            return;
        if (!release.CanRollback)
        {
            await _dialogs.ShowErrorAsync(release.IsNew || !release.Platforms.Any(p => p.Platform == ReleaseCatalog.Windows)
                ? "Откатиться можно только на собранный выпуск Windows: Android-сборка лежит вне проекта — " +
                  "чтобы вернуть старую, выберите её папку и запишите версию заново."
                : "Выпуск " + release.Version + " и так текущий для Windows.");
            return;
        }

        var settings = _service.LoadSettings(_project.RootPath);
        if ((settings.BaseDownloadURL ?? "").Trim().Length == 0 ||
            (!settings.CreateRepairPatch && !settings.CreateInstallerPatch && !settings.CreateIncrementalPatch))
        {
            await _dialogs.ShowErrorAsync("Патч не соберётся: в настройках проекта не задан BaseDownloadURL или не выбран ни один тип патча.");
            return;
        }

        var target = new VersionCode(release.Version);
        var newer = _project.Versions.Where(v => new VersionCode(v.Version).CompareTo(target) > 0)
            .Select(v => v.Version).ToList();
        var list = string.Join(", ", newer);
        if (!await _dialogs.ConfirmAsync("Откатить сервер на версию " + release.Version + "?\n\n" +
                (newer.Count == 1 ? "Версия " : "Версии ") + list + " уйдут в Корзину (из " + ProjectService.VersionsFolder +
                "/ и каталога; номера выпусков не изменятся), патч " + release.Version + " пересоберётся последним. " +
                "Затем доставьте и проверьте его, как обычно.\n\n" +
                "Клиенты на " + list + " вернутся на " + release.Version + ": движок ставит версию с сервера, даже если она ниже. " +
                "Номера " + list + " больше не используются — следующий выпуск должен быть новее." +
                (release.Platforms.Any(p => p.IsAndroid) || newer.Any(v => _project.Meta.AndroidSignedVersions.ContainsKey(v))
                    ? "\n\nAndroid не откатывается: его канал остаётся как есть." : "")))
            return;

        try
        {
            _service.RollbackTo(_project.RootPath, release.Version);
        }
        catch (Exception ex)
        {
            _project = _service.Open(_project.RootPath);
            ShowProject();
            await _dialogs.ShowErrorAsync("Откат не выполнен:\n" + ex.Message);
            return;
        }

        _project = _service.Open(_project.RootPath);
        ShowProject();
        await BuildStagedPatch(release.Version, settings);
    }

    // Каталог перерисован — записи новые: прежнее выделение к ним не относится.
    private void ResetReleaseMarks()
    {
        MarkedCount = 0;
        _markAnchor = null;
        IsMarkingReleases = false;
    }
}
