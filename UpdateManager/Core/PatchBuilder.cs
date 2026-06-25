using SimplePatchToolCore;

namespace UpdateManager.Core
{
    /// <summary>
    /// Обёртка над ProjectManager для сборки патча. Движок крутит работу в своём потоке,
    /// снаружи мы просто поллим: запустить -> тянуть лог + проверять IsRunning -> прочитать результат.
    /// </summary>
    public class PatchBuilder
    {
        private readonly ProjectManager _project;

        public PatchBuilder(string projectRoot)
        {
            _project = new ProjectManager(projectRoot);
        }

        /// <summary>Запустить сборку. true = запущено, false = не удалось (например, уже идёт).</summary>
        public bool Start()
        {
            return _project.GeneratePatch();
        }

        /// <summary>
        /// Очередная строка лога, либо null если логов в очереди нет.
        /// (Прогресс в процентах движок на стороне СОЗДАНИЯ патча не отдаёт — только лог.)
        /// </summary>
        public string FetchLog()
        {
            return _project.FetchLog();
        }

        /// <summary>Идёт ли сборка прямо сейчас.</summary>
        public bool IsRunning
        {
            get { return _project.IsRunning; }
        }

        /// <summary>Завершилась ли сборка неудачей (читать после IsRunning == false).</summary>
        public bool Failed
        {
            get { return _project.Result == PatchResult.Failed; }
        }
    }
}
