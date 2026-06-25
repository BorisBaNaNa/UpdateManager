using SimplePatchToolCore;

namespace UpdateManager.Core.Operations
{
    /// <summary>
    /// Сборка патча через ProjectManager. Движок крутит работу в своём потоке,
    /// снаружи поллим через IEngineOperation. Процент на создании движок не отдаёт — только лог.
    /// </summary>
    public class PatchBuilder : IEngineOperation
    {
        private readonly ProjectManager _project;

        public PatchBuilder(string projectRoot)
        {
            _project = new ProjectManager(projectRoot);
        }

        public string Title { get { return "Сборка патча"; } }

        public bool Start()
        {
            return _project.GeneratePatch();
        }

        public string FetchLog()
        {
            return _project.FetchLog();
        }

        public int? FetchProgressPercentage()
        {
            return null; // движок на создании патча процент не отдаёт
        }

        public bool IsRunning
        {
            get { return _project.IsRunning; }
        }

        public bool Succeeded
        {
            get { return _project.Result != PatchResult.Failed; }
        }

        public string ResultDetails
        {
            get { return ""; }
        }
    }
}
