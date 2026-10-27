using Avalonia.Controls;
using UpdateManager.App.ViewModels;

namespace UpdateManager.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ExitRequested += (_, _) => Close();
            vm.SelfUpdate.ShutdownRequested += (_, _) => Close();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Окно впервые показано — время фоновой проверки обновлений.
        (DataContext as MainWindowViewModel)?.SelfUpdate.OnStartup();
    }
}
