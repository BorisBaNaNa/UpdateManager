using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using UpdateManager.App.Services;
using UpdateManager.App.ViewModels;
using UpdateManager.App.Views;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Project;
using UpdateManager.Core.SelfUpdate;
using UpdateManager.Core.Versioning;

namespace UpdateManager.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = ConfigureServices(() => desktop.MainWindow);
            services.GetRequiredService<ThemeService>().Apply();

            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Композиционный корень: сервисы Core + диалоги + ViewModel'и.
    private static ServiceProvider ConfigureServices(Func<Window?> mainWindow)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDialogService>(_ => new DialogService(mainWindow));
        services.AddSingleton<ThemeService>();
        services.AddSingleton(_ => SelfUpdateConfig.Load());

        services.AddSingleton<ProjectService>();
        services.AddSingleton<RecentProjectsStore>();
        services.AddSingleton<VersionDetector>();
        services.AddSingleton<FtpConnectionStore>();

        services.AddSingleton<SelfUpdateViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
