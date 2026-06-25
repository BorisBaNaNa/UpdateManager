namespace UpdateManager.Core
{
    /// <summary>
    /// Фоновая операция движка (сборка/проверка): запускается, крутится в своём потоке,
    /// наружу отдаёт лог и опционально процент, по завершении — успех + детали.
    /// Один и тот же UI прогресса работает с любой реализацией.
    /// </summary>
    public interface IEngineOperation
    {
        /// <summary>Заголовок окна прогресса.</summary>
        string Title { get; }

        /// <summary>Запустить. true = запущено, false = не удалось (например, уже идёт).</summary>
        bool Start();

        /// <summary>Очередная строка лога, либо null если логов в очереди нет.</summary>
        string FetchLog();

        /// <summary>Свежий процент (0..100), либо null если операция процент не отдаёт.</summary>
        int? FetchProgressPercentage();

        /// <summary>Идёт ли операция прямо сейчас.</summary>
        bool IsRunning { get; }

        /// <summary>Успех (читать после IsRunning == false).</summary>
        bool Succeeded { get; }

        /// <summary>Доп. детали результата (например, причина ошибки), либо "".</summary>
        string ResultDetails { get; }
    }
}
