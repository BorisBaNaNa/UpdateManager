using Avalonia.Controls;
using UpdateManager.App.ViewModels;

namespace UpdateManager.App.Views;

/// <summary>
/// Основа модальных окон: связывает <see cref="DialogViewModel.CloseRequested"/> с Close(result)
/// и спрашивает VM, можно ли закрыть окно крестиком.
/// </summary>
public class DialogWindow : Window
{
    private DialogViewModel? _vm;
    private bool _closingByVm;

    protected override Type StyleKeyOverride => typeof(Window);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
            _vm.CloseRequested -= OnCloseRequested;
        _vm = DataContext as DialogViewModel;
        if (_vm != null)
            _vm.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        _closingByVm = true;
        Close(result);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closingByVm && _vm != null && !_vm.CanCloseByUser())
            e.Cancel = true;
        base.OnClosing(e);
    }
}
