using UpdateManager.App.Services;
using UpdateManager.App.ViewModels;
using UpdateManager.Core.SelfUpdate;

namespace UpdateManager.Tests;

/// <summary>«Что нового»: вшитые заметки, показ один раз после обновления.</summary>
public sealed class ReleaseNotesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("UpdateManagerNotes_").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* временная папка */ }
    }

    private string SeenFile => Path.Combine(_root, "whatsnew.txt");

    private ReleaseNotesService Service(params string[] versions) =>
        new(SeenFile, () => versions
            .Select(v => new ReleaseNote(ReleaseNotesService.Normalize(v)!, "## " + v + ".0"))
            .OrderByDescending(n => n.Version)
            .ToList());

    [Fact]
    public void EmbeddedNotes_ContainEveryFileFromDocsReleases()
    {
        var all = new ReleaseNotesService(SeenFile, () => new ReleaseNotesService().All()).All();

        Assert.Contains(all, n => n.VersionText == "2.0.0" && n.Markdown.Contains("## 2.0.0.0"));
        Assert.Equal(all.OrderByDescending(n => n.Version), all);
    }

    [Fact]
    public void FirstRun_ShowsEverythingUpToCurrent()
    {
        var unseen = Service("2.0.0", "2.0.1", "2.1.0").UnseenFor("2.0.1.0");

        Assert.Equal(new[] { "2.0.1", "2.0.0" }, unseen.Select(n => n.VersionText));
    }

    [Fact]
    public void AfterUpdate_ShowsOnlyNewerThanSeen_ThenNothing()
    {
        var service = Service("2.0.0", "2.0.1", "2.0.2");
        service.MarkSeen("2.0.0.0");

        Assert.Equal(new[] { "2.0.2", "2.0.1" }, service.UnseenFor("2.0.2.0").Select(n => n.VersionText));

        service.MarkSeen("2.0.2.0");
        Assert.Empty(service.UnseenFor("2.0.2.0"));
        Assert.Equal("2.0.2", File.ReadAllText(SeenFile));
    }

    [Fact]
    public async Task SelfUpdate_ShowsWhatsNewOnce()
    {
        var dialogs = new FakeDialogService();
        var vm = new SelfUpdateViewModel(dialogs, new SelfUpdateConfig("", ""), Service("2.0.0", "2.0.1"));

        await vm.ShowWhatsNewIfUpdated("2.0.1.0");
        await vm.ShowWhatsNewIfUpdated("2.0.1.0");

        var shown = Assert.Single(dialogs.ReleaseNotes);
        Assert.Equal("Обновлено до 2.0.1", shown.Subtitle);
        Assert.Equal(new[] { "2.0.1", "2.0.0" }, shown.Notes.Select(n => n.VersionText));

        await vm.ShowReleaseNotesCommand.ExecuteAsync(null); // «Справка → Что нового…» — всегда
        Assert.Equal(2, dialogs.ReleaseNotes.Count);
    }
}
