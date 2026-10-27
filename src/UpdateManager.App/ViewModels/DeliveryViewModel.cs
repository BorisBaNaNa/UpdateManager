using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.App.Services;
using UpdateManager.Core.Delivery;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Окно доставки: выбор метода (запоминается последний) и пути.
/// По «Доставить» заполняет <see cref="Result"/> и закрывает окно с OK.
/// </summary>
public sealed partial class DeliveryViewModel : DialogViewModel
{
    private const string FtpHint = "Параметры подключения — кнопка «FTP-сервер…» в главном окне";

    private readonly IDialogService _dialogs;

    public DeliveryViewModel(DeliveryConfig current, IDialogService dialogs)
    {
        _dialogs = dialogs;
        Methods = DeliveryMethods.Available();
        // Преселект сохранённого метода (или первый доступный).
        _selectedMethod = Methods.FirstOrDefault(m => m.Id == current.Method) ?? Methods[0];
        _path = current.Path;
    }

    public DeliveryMethods.Info[] Methods { get; }

    public DeliveryConfig? Result { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFtp), nameof(PathLabel), nameof(PathText))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    private DeliveryMethods.Info _selectedMethod;

    // Путь папки хранится отдельно — у FTP в поле показывается подсказка, путь не теряется.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathText))]
    private string _path;

    public bool IsFtp => SelectedMethod.Id == DeliveryMethods.Ftp;

    public string PathLabel => IsFtp ? "Сервер:" : "Путь доставки:";

    public string PathText
    {
        get => IsFtp ? FtpHint : Path;
        set
        {
            if (!IsFtp)
                Path = value;
        }
    }

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
