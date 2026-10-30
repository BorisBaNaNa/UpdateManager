using System.ComponentModel;
using Avalonia;
using UpdateManager.App.ViewModels;

namespace UpdateManager.App.Views;

public partial class UpdateGuideWindow : DialogWindow
{
    private UpdateGuideViewModel? _guide;
    public UpdateGuideWindow() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_guide != null)
            _guide.PropertyChanged -= OnGuideChanged;
        base.OnDataContextChanged(e);
        _guide = DataContext as UpdateGuideViewModel;
        if (_guide != null)
            _guide.PropertyChanged += OnGuideChanged;
    }

    private void OnGuideChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdateGuideViewModel.SelectedStep))
            GuideScroll.Offset = Vector.Zero;
    }
}
