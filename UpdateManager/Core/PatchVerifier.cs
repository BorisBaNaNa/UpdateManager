using SimplePatchToolCore;

namespace UpdateManager.Core
{
    /// <summary>
    /// Проверка доставки: клиентский движок реально качает патч во временную папку.
    /// Успех = патч скачался/применился; ошибка ловит неверный BaseDownloadURL и недоступные файлы.
    /// У клиента (в отличие от создания) есть процент — окно покажет заполняющийся бар.
    /// </summary>
    public class PatchVerifier : IEngineOperation
    {
        private readonly SimplePatchTool _patcher;

        public PatchVerifier(string rootPath, string versionInfoUrl)
        {
            _patcher = new SimplePatchTool(rootPath, versionInfoUrl)
                .UseRepairPatch(true)
                .UseIncrementalPatch(true)
                .UseInstallerPatch(true)
                .SilentMode(false)
                .LogProgress(true)
                .LogToFile(false);
        }

        public string Title { get { return "Проверка патча (скачивание)"; } }

        public bool Start()
        {
            return _patcher.Run(false); // false = не self-patching, обычное скачивание/применение
        }

        public string FetchLog()
        {
            return _patcher.FetchLog();
        }

        public int? FetchProgressPercentage()
        {
            int? percentage = null;
            var progress = _patcher.FetchOverallProgress();
            while (progress != null)
            {
                percentage = progress.Percentage;
                progress = _patcher.FetchOverallProgress();
            }
            return percentage;
        }

        public bool IsRunning
        {
            get { return _patcher.IsRunning; }
        }

        public bool Succeeded
        {
            get { return _patcher.Result == PatchResult.Success || _patcher.Result == PatchResult.AlreadyUpToDate; }
        }

        public string ResultDetails
        {
            get { return Succeeded ? "" : _patcher.FailDetails; }
        }
    }
}
