using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Operations;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Модальное окно фоновой операции движка (сборка/проверка/заливка). Поллит IEngineOperation
/// таймером в UI-потоке. Пока процента нет — бесконечный бар; как только пришёл — заполняется.
/// Закрытие во время работы не закрывает окно, а прерывает операцию; окно закроется само.
/// </summary>
public sealed partial class OperationViewModel : DialogViewModel
{
    private readonly IEngineOperation _operation;
    private readonly DispatcherTimer _timer;
    private readonly StringBuilder _log = new();

    // Пользователь запросил прерывание; ждём остановки операции, затем окно закрывается само.
    private bool _cancelRequested;

    public OperationViewModel(IEngineOperation operation)
    {
        _operation = operation;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Poll());
    }

    public string Title => _operation.Title;

    [ObservableProperty]
    private string _logText = "";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseOrCancelCommand))]
    private bool _canPressButton = true;

    public string ButtonText => IsRunning ? "Прервать" : "Закрыть";

    /// <summary>Состояние словами: идёт (с процентом), готово, ошибка, прерывание.</summary>
    [ObservableProperty]
    private string _stateText = "Запуск…";

    [ObservableProperty]
    private bool _isFailed;

    /// <summary>Причина провала одной-двумя строками (над логом, не в его конце).</summary>
    [ObservableProperty]
    private string _failureText = "";

    /// <summary>Запустить операцию (окно уже показано).</summary>
    public void Start()
    {
        if (!_operation.Start())
        {
            AppendLog("Не удалось запустить операцию (возможно, уже идёт).");
            Finish(success: false);
            return;
        }

        AppendLog(_operation.Title + " — запущено…");
        StateText = "Выполняется…";
        IsRunning = true;
        _timer.Start();
    }

    private void Poll()
    {
        // Дренажим весь накопленный лог.
        for (var line = _operation.FetchLog(); line != null; line = _operation.FetchLog())
            AppendLog(line);

        var percentage = _operation.FetchProgressPercentage();
        if (percentage.HasValue)
        {
            IsIndeterminate = false;
            Progress = Math.Clamp(percentage.Value, 0, 100);
            if (!_cancelRequested)
                StateText = $"Выполняется · {Progress:0} %";
        }

        if (!_operation.IsRunning)
        {
            _timer.Stop();
            IsRunning = false;
            Finish(_operation.Succeeded);
        }
    }

    private void Finish(bool success)
    {
        IsIndeterminate = false;
        Progress = success ? 100 : 0;

        // Прерывание было запрошено — окно закрывается само.
        if (_cancelRequested)
        {
            AppendLog("Операция прервана.");
            Close(false);
            return;
        }

        AppendLog(success ? "Готово: успешно." : "Операция завершилась с ошибкой.");

        var details = _operation.ResultDetails;
        if (!success && !string.IsNullOrEmpty(details))
            AppendLog(details);

        StateText = success ? "Готово" : "Завершилось с ошибкой";
        IsFailed = !success;
        if (!success)
            FailureText = string.IsNullOrEmpty(details) ? "Подробности — в журнале ниже." : details;

        CanPressButton = true;
    }

    [RelayCommand(CanExecute = nameof(CanPressButton))]
    private void CloseOrCancel()
    {
        if (IsRunning)
            RequestCancel();
        else
            Close(_operation.Succeeded);
    }

    public override bool CanCloseByUser()
    {
        if (!IsRunning)
            return true;

        RequestCancel();
        return false;
    }

    private void RequestCancel()
    {
        if (_cancelRequested)
            return;

        _cancelRequested = true;
        AppendLog("Прерывание операции…");
        StateText = "Прерывание…";
        CanPressButton = false; // защита от повторных нажатий, пока операция тормозит
        _operation.Cancel();
    }

    private void AppendLog(string line)
    {
        _log.AppendLine(line);
        LogText = _log.ToString();
    }
}
