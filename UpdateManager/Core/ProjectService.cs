using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimplePatchToolCore;

namespace UpdateManager.Core
{
    /// <summary>
    /// Создание и открытие проектов обновления.
    /// Единственный слой, который напрямую зовёт движок SimplePatchTool.
    /// </summary>
    public class ProjectService
    {
        /// <summary>Файл настроек движка в корне проекта (источник истины — сам движок).</summary>
        public const string EngineSettingsFile = PatchParameters.PROJECT_SETTINGS_FILENAME;

        /// <summary>Папка движка с версиями-билдами.</summary>
        public const string VersionsFolder = PatchParameters.PROJECT_VERSIONS_DIRECTORY;

        /// <summary>Папка движка с self-patcher'ом (для self-patching приложений).</summary>
        public const string SelfPatcherFolder = "SelfPatcher";

        // Встроенный self-patcher лежит рядом с нашим exe (папка приложения\SelfPatcher),
        // куда его кладёт сборка (Content + CopyToOutputDirectory). Кладём его в каждый новый проект.
        private static readonly string SelfPatcherSourceDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SelfPatcherFolder);

        /// <summary>
        /// Создать новый проект в папке folder: движок генерирует структуру,
        /// затем мы прописываем имя в Settings.xml и пишем наш файл проекта.
        /// </summary>
        public UpdateProject Create(string folder, string name)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("Не указана папка проекта.");

            Directory.CreateDirectory(folder);

            // 1. Движок создаёт Settings.xml + папки Versions/ Output/ SelfPatcher/ Other/.
            var manager = new ProjectManager(folder);
            manager.CreateProject();

            // 2. Кладём self-patcher в SelfPatcher/ — без него self-patching приложение не применит патч.
            InstallSelfPatcher(folder);

            // 3. Прописываем имя проекта через движковый ProjectInfo (по умолчанию там "NewProject").
            var info = manager.LoadProjectInfo();
            info.Name = name;
            manager.SaveProjectInfo(info);

            // 4. Пишем наш файл проекта (updatemanager.project.xml).
            new ProjectMeta().Save(folder);

            // 5. Возвращаем уже как открытый проект.
            return Open(folder);
        }

        /// <summary>
        /// Открыть существующий проект: проверяем наличие Settings.xml,
        /// читаем имя, нашу мету и список версий.
        /// </summary>
        public UpdateProject Open(string folder)
        {
            var settingsPath = Path.Combine(folder, EngineSettingsFile);
            if (!File.Exists(settingsPath))
                throw new InvalidOperationException(
                    "В выбранной папке нет " + EngineSettingsFile + " — это не проект обновления.");

            return new UpdateProject
            {
                RootPath = folder,
                Name = GetEngineProjectName(folder),
                Meta = ProjectMeta.Load(folder),
                Versions = ReadVersions(folder)
            };
        }

        /// <summary>Есть ли уже папка такой версии в Versions/.</summary>
        public bool VersionExists(string projectRoot, string version)
        {
            return Directory.Exists(Path.Combine(projectRoot, VersionsFolder, version));
        }

        /// <summary>
        /// Поставить билд как версию: копирует папку-источник в Versions/&lt;версия&gt;.
        /// Если такая версия уже есть — сначала удаляет её (чистая перезапись).
        /// </summary>
        public void StageBuild(string projectRoot, string buildSource, string version)
        {
            var dest = Path.Combine(projectRoot, VersionsFolder, version);
            if (Directory.Exists(dest))
                Directory.Delete(dest, recursive: true);

            CopyDirectory(buildSource, dest);
        }

        // --- имя проекта (движковый ProjectInfo) ---

        private static string GetEngineProjectName(string folder)
        {
            return new ProjectManager(folder).LoadProjectInfo().Name ?? "";
        }

        // --- self-patcher ---

        /// <summary>
        /// Копирует готовый self-patcher в папку SelfPatcher/ проекта.
        /// Пока источник захардкожен; позже заменим на сборку/конфиг.
        /// </summary>
        private static void InstallSelfPatcher(string projectRoot)
        {
            if (!Directory.Exists(SelfPatcherSourceDir))
                throw new InvalidOperationException(
                    "Не найден self-patcher по пути " + SelfPatcherSourceDir +
                    ".\nБез него self-patching приложение не сможет применить патч.");

            CopyDirectory(SelfPatcherSourceDir, Path.Combine(projectRoot, SelfPatcherFolder));
        }

        /// <summary>Рекурсивно копирует содержимое одной папки в другую (с перезаписью).</summary>
        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

            foreach (var subDir in Directory.GetDirectories(source))
                CopyDirectory(subDir, Path.Combine(destination, Path.GetFileName(subDir)));
        }

        // --- список версий из папки Versions/ ---

        private static List<ProjectVersion> ReadVersions(string folder)
        {
            var versionsDir = Path.Combine(folder, VersionsFolder);
            if (!Directory.Exists(versionsDir))
                return new List<ProjectVersion>();

            return Directory.GetDirectories(versionsDir)
                .Select(dir => new ProjectVersion
                {
                    Version = Path.GetFileName(dir),
                    BuildDate = Directory.GetLastWriteTime(dir),
                    Delivered = false
                })
                .OrderBy(v => v.Version)
                .ToList();
        }
    }
}
