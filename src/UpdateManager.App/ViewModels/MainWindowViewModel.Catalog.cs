using SimplePatchToolCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Project;
using UpdateManager.Core.Versioning;

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
                "Их папки в " + ProjectService.VersionsFolder + "/ уйдут в Корзину, записи этапов — из проекта; " +
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
    /// Откатить сервер на выбранный выпуск — по каждой платформе, где он архивный и хранится в проекте.
    /// Windows: версии новее — в Корзину, Output пересобирается так, что этот выпуск последний (RollbackTo);
    /// движок понижает версию у клиентов (EnginePipelineTests.RollbackTo_…). Android: сохранённые выкладки новее —
    /// в Корзину, выкладка выпуска снова текущая (RollbackAndroidTo). Дальше — доставка и проверка, как обычно.
    /// </summary>
    [RelayCommand]
    private async Task RollbackRelease()
    {
        var release = SelectedRelease;
        if (_project == null || release == null)
            return;

        var windows = release.Platforms.FirstOrDefault(p => p.Platform == ReleaseCatalog.Windows && !p.IsCurrent);
        var android = release.Platforms.FirstOrDefault(p => p.IsAndroid && !p.IsCurrent && p.CanRestore);
        if (windows == null && android == null)
        {
            await _dialogs.ShowErrorAsync(release.IsNew ? "Запись «новый» ещё не выпускалась — откатываться не на что."
                : release.Platforms.Any(p => p.IsAndroid && !p.IsCurrent)
                    ? "Выкладка Android " + release.Version + " не сохранена в проекте (выпущена до 2.2) — откатиться на неё нельзя. " +
                      "Выберите её папку билда и запишите версию заново (под новым номером)."
                    : "Выпуск " + release.Version + " и так текущий.");
            return;
        }

        var settings = _service.LoadSettings(_project.RootPath);
        if (windows != null && ((settings.BaseDownloadURL ?? "").Trim().Length == 0 ||
            (!settings.CreateRepairPatch && !settings.CreateInstallerPatch && !settings.CreateIncrementalPatch)))
        {
            await _dialogs.ShowErrorAsync("Патч не соберётся: в настройках проекта не задан BaseDownloadURL или не выбран ни один тип патча.");
            return;
        }

        var target = new VersionCode(release.Version);
        bool Newer(string v) => VersionRules.IsValid(v) && new VersionCode(v).CompareTo(target) > 0;
        var newerWindows = windows == null ? new List<string>()
            : _project.Versions.Select(v => v.Version).Where(Newer).ToList();
        var newerAndroid = android == null ? new List<string>()
            : ReleaseCatalog.AndroidVersions(_project.Meta).Where(Newer).OrderBy(v => new VersionCode(v)).ToList();
        var all = newerWindows.Concat(newerAndroid).Distinct().OrderBy(v => new VersionCode(v)).ToList();
        var list = string.Join(", ", all);

        var text = "Откатить сервер на версию " + release.Version + "?\n\n" +
            (all.Count == 1 ? "Версия " : "Версии ") + list + " уйдут в Корзину (из " + ProjectService.VersionsFolder +
            "/ и каталога; номера выпусков не изменятся).";
        if (windows != null)
            text += "\nWindows: патч " + release.Version + " пересоберётся последним — затем доставьте и проверьте его. " +
                "Клиенты на более новых версиях вернутся на " + release.Version + ": движок ставит версию с сервера, даже если она ниже.";
        if (android != null)
            text += "\nAndroid: выкладка " + release.Version + " снова текущая — доставьте её в канал и проверьте.";
        else if (release.Platforms.Any(p => p.IsAndroid) || _project.Meta.AndroidEnabled)
            text += "\nAndroid не откатывается: " + (release.Platforms.Any(p => p.IsAndroid && p.IsCurrent)
                ? "этот выпуск для него и так текущий." : "выкладка этой версии не сохранена в проекте.");
        text += "\n\nНомера " + list + " больше не используются — следующий выпуск должен быть новее.";
        if (!await _dialogs.ConfirmAsync(text))
            return;

        try
        {
            if (windows != null)
                _service.RollbackTo(_project.RootPath, release.Version);
            if (android != null)
                _service.RollbackAndroidTo(_project.RootPath, release.Version);
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
        if (windows != null)
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
