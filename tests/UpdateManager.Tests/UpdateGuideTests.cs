using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using UpdateManager.App.Services;
using UpdateManager.App.ViewModels;
using UpdateManager.App.Views;

namespace UpdateManager.Tests;

public sealed class UpdateGuideTests
{
    [Fact]
    public void EmbeddedGuide_NavigatesAllTopics_AndClosesAfterFinalStep()
    {
        var vm = new UpdateGuideViewModel(new FakeDialogService());
        Assert.Equal(3, vm.Steps.Count);
        Assert.Equal(7, vm.Steps.Sum(s => s.Topics.Count));
        var windows = vm.Steps[0].Topics.Single(t => t.Title == "Подготовка Windows");
        Assert.Contains("https://github.com/yasirkula/SimplePatchTool/wiki", windows.Markdown);
        Assert.Contains("https://github.com/yasirkula/SimplePatchTool)", windows.Markdown);
        Assert.Contains("versionCode", vm.Steps[0].Topics.Single(t => t.Title == "Подготовка Android").Markdown);
        Assert.False(vm.CanGoBack);
        vm.NextCommand.Execute(null);
        Assert.Equal(vm.Steps[1], vm.SelectedStep);
        Assert.True(vm.CanGoBack);
        vm.PreviousCommand.Execute(null);
        Assert.Equal(vm.Steps[0], vm.SelectedStep);
        vm.SelectStepCommand.Execute(vm.Steps[^1]);
        Assert.True(vm.IsLast);
        Assert.Single(vm.Steps, s => s.IsSelected);
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        vm.NextCommand.Execute(null);
        Assert.True(closed);
    }

    [Fact]
    public Task Window_ClickStepsExpandTopics_ResetsScroll_Closes() => HeadlessUi.Run(async () =>
    {
        var vm = new UpdateGuideViewModel(new FakeDialogService());
        var window = new UpdateGuideWindow { DataContext = vm, Width = 720, Height = 520 };
        window.Show();
        try
        {
            HeadlessUi.Flush();
            var topic = vm.Steps[0].Topics.Single(t => t.Title == "Подготовка Windows");
            vm.Steps[0].Topics[0].IsExpanded = false;
            await HeadlessUi.Click(window, topic);
            Assert.True(topic.IsExpanded);
            HeadlessUi.Flush();
            Assert.Contains(window.GetVisualDescendants().OfType<HyperlinkButton>(),
                b => b.NavigateUri?.AbsoluteUri == "https://github.com/yasirkula/SimplePatchTool/wiki");
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            HeadlessUi.Flush();
            Assert.False(topic.IsExpanded);
            await HeadlessUi.Click(window, vm.Steps[1]);
            Assert.Equal(vm.Steps[1], vm.SelectedStep);
            var scroll = window.FindControl<ScrollViewer>("GuideScroll")!;
            scroll.Offset = new Vector(0, 200);
            await HeadlessUi.Click(window, vm.Steps[2]);
            Assert.Equal(Vector.Zero, scroll.Offset);
            Assert.Contains(window.GetVisualDescendants().OfType<ToggleButton>(), e => e.IsChecked == true);
            var next = window.GetVisualDescendants().OfType<Button>().Single(b => b.Command == vm.NextCommand);
            Assert.True(next.IsEffectivelyVisible);
            Assert.True(next.TranslatePoint(new Point(next.Bounds.Width, next.Bounds.Height), window)!.Value.X <= window.Width);
            vm.NextCommand.Execute(null);
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task DialogService_GuideIsNonmodal_ReusesWindow_AndReopens() => HeadlessUi.Run(() =>
    {
        var owner = new Window();
        owner.Show();
        var service = new DialogService(() => owner);
        try
        {
            Assert.True(service.ShowUpdateGuideAsync().IsCompletedSuccessfully);
            HeadlessUi.Flush();
            var guide = Assert.Single(owner.OwnedWindows.OfType<UpdateGuideWindow>());
            Assert.False(service.IsBusy);
            Assert.True(service.ShowUpdateGuideAsync().IsCompletedSuccessfully);
            Assert.Single(owner.OwnedWindows.OfType<UpdateGuideWindow>());
            guide.Close();
            service.ShowUpdateGuideAsync();
            Assert.NotSame(guide, Assert.Single(owner.OwnedWindows.OfType<UpdateGuideWindow>()));
        }
        finally { owner.Close(); }
        return Task.CompletedTask;
    });
}
