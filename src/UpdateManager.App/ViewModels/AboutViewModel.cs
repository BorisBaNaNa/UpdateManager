using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.SelfUpdate;

namespace UpdateManager.App.ViewModels;

public sealed partial class AboutViewModel : DialogViewModel
{
    public AboutViewModel(SelfUpdateViewModel selfUpdate) => SelfUpdate = selfUpdate;

    public SelfUpdateViewModel SelfUpdate { get; }
    public string VersionText => "Версия " + SelfIdentity.Version;
    public bool IsUpdateConfigured => SelfUpdate.CheckCommand.CanExecute(null);

    [RelayCommand] private void Dismiss() => Close(false);
}
