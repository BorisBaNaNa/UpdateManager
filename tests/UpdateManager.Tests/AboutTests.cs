using Avalonia.Controls;
using Avalonia.VisualTree;
using UpdateManager.App.Services;
using UpdateManager.App.ViewModels;
using UpdateManager.App.Views;
using UpdateManager.Core.SelfUpdate;

namespace UpdateManager.Tests;

public sealed class AboutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Window_UsesExistingUpdateCommand_ShowsIdentityAndLinks(bool configured) => HeadlessUi.Run(() =>
    {
        var dialogs = new FakeDialogService();
        var config = new SelfUpdateConfig(configured ? "https://example.com/VersionInfo.info" : "", "");
        var selfUpdate = new SelfUpdateViewModel(dialogs, config, new ReleaseNotesService());
        var vm = new AboutViewModel(selfUpdate);
        var window = new AboutWindow { DataContext = vm, Width = 460, Height = 440 };
        window.Show();
        try
        {
            HeadlessUi.Flush();
            Assert.Equal("Версия " + SelfIdentity.Version, vm.VersionText);
            var check = window.FindControl<Button>("CheckUpdatesButton")!;
            Assert.Same(selfUpdate.CheckCommand, check.Command);
            Assert.Equal(configured, check.IsEffectivelyEnabled);
            Assert.Equal(configured, vm.IsUpdateConfigured);
            var urls = window.GetVisualDescendants().OfType<HyperlinkButton>().Select(b => b.NavigateUri!.AbsoluteUri).ToList();
            Assert.Contains("https://github.com/BorisBaNaNa", urls);
            Assert.Contains("https://github.com/BorisBaNaNa/UpdateManager", urls);
            Assert.Contains("https://github.com/yasirkula", urls);
            Assert.Contains("https://github.com/yasirkula/SimplePatchTool", urls);
            selfUpdate.IsChecking = true;
            HeadlessUi.Flush();
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
                t => t.Text == "Проверка обновлений…" && t.IsVisible);
            vm.DismissCommand.Execute(null);
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });
}
