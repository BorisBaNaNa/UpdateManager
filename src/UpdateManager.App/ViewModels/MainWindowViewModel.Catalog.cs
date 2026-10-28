using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Project;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Каталог выпусков: удаление старых версий. Одна — контекстное меню записи; несколько — режим отметки
/// (удержание записи 2 секунды или «Отметить несколько»): у записей появляются флажки, протяжка отмечает
/// всё, что задела, а по отпусканию открывается меню для отмеченного.
/// </summary>
public sealed partial class MainWindowViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotMarkingReleases))]
    private bool _isMarkingReleases;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteMarkedText))]
    private int _markedCount;

    public bool IsNotMarkingReleases => !IsMarkingReleases;

    public string DeleteMarkedText => "Удалить отмеченные (" + MarkedCount + ")…";

    /// <summary>Войти в режим отметки; <paramref name="first"/> — запись, с которой начали (если её можно удалить).</summary>
    public void StartMarking(ReleaseItem? first)
    {
        IsMarkingReleases = true;
        if (first != null)
            Mark(first);
    }

    /// <summary>Отметить запись протяжкой (только добавляет: что задели — то отмечено).</summary>
    public void Mark(ReleaseItem item)
    {
        if (IsMarkingReleases && item.CanDelete)
            item.IsChecked = true;
    }

    [RelayCommand]
    private void MarkReleases() => StartMarking(SelectedRelease);

    [RelayCommand]
    private void ClearMarks()
    {
        foreach (var item in Releases)
            item.IsChecked = false;
        IsMarkingReleases = false;
    }

    [RelayCommand]
    private Task DeleteRelease() =>
        SelectedRelease == null ? Task.CompletedTask : DeleteReleases(new[] { SelectedRelease });

    [RelayCommand]
    private Task DeleteMarkedReleases() => DeleteReleases(Releases.Where(r => r.IsChecked).ToList());

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

    // Каталог перерисован — записи новые: режим отметки заканчивается, счётчик следит за флажками новых записей.
    private void WatchReleaseMarks()
    {
        IsMarkingReleases = false;
        MarkedCount = 0;
        foreach (var item in Releases)
            item.PropertyChanged += OnReleaseMarkChanged;
    }

    private void OnReleaseMarkChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReleaseItem.IsChecked))
            return;
        var before = MarkedCount;
        MarkedCount = Releases.Count(r => r.IsChecked);
        if (before > 0 && MarkedCount == 0)
            IsMarkingReleases = false; // сняли последний флажок — режим закончен
    }
}
