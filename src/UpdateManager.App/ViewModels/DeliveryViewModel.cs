using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.App.Services;
using UpdateManager.Core.Delivery;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Окно доставки: выбор метода (запоминается последний) и пути.
/// По «Доставить» (или «Сохранить», если окно открыто только для настройки) заполняет
/// <see cref="Result"/> и закрывает окно с OK.
/// </summary>
public sealed partial class DeliveryViewModel : DialogViewModel
{
    private readonly IDialogService _dialogs;

    public DeliveryViewModel(DeliveryConfig current, IDialogService dialogs, bool deliverNow)
    {
        _dialogs = dialogs;
        ActionText = deliverNow ? "Доставить" : "Сохранить";
        Methods = DeliveryMethods.Available();
        // Преселект сохранённого метода (или первый доступный).
        _selectedMethod = Methods.FirstOrDefault(m => m.Id == current.Method) ?? Methods[0];
        _path = current.Path;
    }

    public DeliveryMethods.Info[] Methods { get; }

    public DeliveryConfig? Result { get; private set; }

    /// <summary>Подпись главной кнопки: «Доставить» или «Сохранить».</summary>
    public string ActionText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFtp))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    private DeliveryMethods.Info _selectedMethod;

    // Путь папки хранится и при выбранном FTP — при возврате к папке не теряется.
    [ObservableProperty]
    private string _path;

    public bool IsFtp => SelectedMethod.Id == DeliveryMethods.Ftp;

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task Browse()
    {
        var folder = await _dialogs.PickFolderAsync("Папка назначения для патча");
        if (folder != null)
            Path = folder;
    }

    private bool CanBrowse() => !IsFtp;

    [RelayCommand]
    private async Task Deliver()
    {
        // Для FTP путь хранится не здесь (реквизиты — в профиле пользователя).
        if (IsFtp)
        {
            Result = new DeliveryConfig { Method = SelectedMethod.Id, Path = "" };
            Close(true);
            return;
        }

        var path = Path.Trim();
        if (path.Length == 0)
        {
            await _dialogs.ShowWarningAsync("Доставка", "Укажите путь доставки.");
            return;
        }

        Result = new DeliveryConfig { Method = SelectedMethod.Id, Path = path };
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}
