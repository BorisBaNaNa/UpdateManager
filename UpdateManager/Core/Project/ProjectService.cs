using SimplePatchToolCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UpdateManager.Core.Common;

namespace UpdateManager.Core.Project
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

            var outputPath = new ProjectManager(folder).outputPath;

            return new UpdateProject
            {
                RootPath = folder,
                Name = GetEngineProjectName(folder),
                Meta = ProjectMeta.Load(folder),
                Versions = ReadVersions(folder),
                OutputReady = Directory.Exists(outputPath) && Directory.GetFileSystemEntries(outputPath).Length > 0
            };
        }

        /// <summary>В папке уже лежит проект обновления (есть Settings.xml движка).</summary>
        public bool IsProjectFolder(string folder)
        {
            return File.Exists(Path.Combine(folder, EngineSettingsFile));
        }

        /// <summary>В папке есть хоть что-то (файлы/подпапки).</summary>
        public bool IsFolderNonEmpty(string folder)
        {
            return Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length > 0;
        }

        /// <summary>Есть ли уже папка такой версии в Versions/.</summary>
        public bool VersionExists(string projectRoot, string version)
        {
            return Directory.Exists(Path.Combine(projectRoot, VersionsFolder, version));
        }

        /// <summary>Максимальная (числовая) версия в Versions/ или null, если версий ещё нет.</summary>
        public string GetMaxVersion(string projectRoot)
        {
            var versions = ReadVersions(projectRoot); // отсортированы по возрастанию через VersionCode
            return versions.Count > 0 ? versions[versions.Count - 1].Version : null;
        }

        /// <summary>
        /// Версия ниже максимальной собранной (равная — нет). Движок строит инкрементальные патчи
        /// по возрастанию версий, поэтому более низкая версия рвёт цепочку.
        /// </summary>
        public bool IsBelowMaxVersion(string projectRoot, string version, out string maxVersion)
        {
            maxVersion = GetMaxVersion(projectRoot);
            return maxVersion != null && new VersionCode(version).CompareTo(new VersionCode(maxVersion)) < 0;
        }

        /// <summary>
        /// Отпечаток настроек, влияющих на содержимое Output (BaseDownloadURL, типы патчей, сжатие и т.п.).
        /// Пишем его в мету при сборке; при доставке сравниваем с текущим, чтобы поймать «собрал →
        /// поменял настройки → доставил старый Output». Сравнение по значениям, поэтому повторное
        /// сохранение тех же настроек не даёт ложного срабатывания.
        /// </summary>
        public string BuildSettingsFingerprint(string projectRoot)
        {
            var s = LoadSettings(projectRoot);
            var parts = string.Join("", new[]
            {
                s.BaseDownloadURL, s.MaintenanceCheckURL,
                s.IsSelfPatchingApp.ToString(),
                s.CreateRepairPatch.ToString(), s.CreateInstallerPatch.ToString(),
                s.CreateIncrementalPatch.ToString(), s.CreateAllIncrementalPatches.ToString(),
                string.Join("", s.IgnoredPaths),
                s.CompressionFormatRepairPatch.ToString(), s.CompressionFormatInstallerPatch.ToString(),
                s.CompressionFormatIncrementalPatch.ToString(),
                s.BinaryDiffQuality.ToString(),
                s.DontCreatePatchFilesForUnchangedFiles.ToString()
            });

            // Хэшируем: служебные разделители — недопустимые в XML символы, в файл проекта их класть нельзя.
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(parts));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
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

            FileUtils.CopyDirectory(buildSource, dest);
        }

        /// <summary>Абсолютный путь к папке Output/ проекта (берём у движка).</summary>
        public string GetOutputPath(string projectRoot)
        {
            return new ProjectManager(projectRoot).outputPath;
        }

        /// <summary>
        /// URL манифеста VersionInfo.info из BaseDownloadURL (всё лежит в одном месте).
        /// </summary>
        public string BuildVersionInfoUrl(string baseDownloadUrl)
        {
            return baseDownloadUrl.TrimEnd('/') + "/" + PatchParameters.VERSION_INFO_FILENAME;
        }

        /// <summary>
        /// Нормализует BaseDownloadURL: гарантирует завершающий «/».
        /// Движок клеит ссылки как BaseDownloadURL + путь без вставки слеша, поэтому без него
        /// файлы патча не скачиваются (классическая неочевидная ошибка).
        /// </summary>
        private static string NormalizeBaseUrl(string url)
        {
            url = (url ?? "").Trim();
            return url.Length == 0 ? "" : url.TrimEnd('/') + "/";
        }

        // --- настройки проекта (движковый ProjectInfo) ---

        private static string GetEngineProjectName(string folder)
        {
            return new ProjectManager(folder).LoadProjectInfo().Name ?? "";
        }

        /// <summary>Прочитать основные настройки проекта в наш DTO.</summary>
        public ProjectSettings LoadSettings(string projectRoot)
        {
            var info = new ProjectManager(projectRoot).LoadProjectInfo();
            return new ProjectSettings
            {
                Name = info.Name ?? "",
                BaseDownloadURL = info.BaseDownloadURL ?? "",
                MaintenanceCheckURL = info.MaintenanceCheckURL ?? "",
                IsSelfPatchingApp = info.IsSelfPatchingApp,
                CreateRepairPatch = info.CreateRepairPatch,
                CreateInstallerPatch = info.CreateInstallerPatch,
                CreateIncrementalPatch = info.CreateIncrementalPatch,
                CreateAllIncrementalPatches = info.CreateAllIncrementalPatches,
                IgnoredPaths = new List<string>(info.IgnoredPaths),
                CompressionFormatRepairPatch = info.CompressionFormatRepairPatch,
                CompressionFormatInstallerPatch = info.CompressionFormatInstallerPatch,
                CompressionFormatIncrementalPatch = info.CompressionFormatIncrementalPatch,
                BinaryDiffQuality = info.BinaryDiffQuality,
                DontCreatePatchFilesForUnchangedFiles = info.DontCreatePatchFilesForUnchangedFiles
            };
        }

        /// <summary>
        /// Сохранить основные настройки. Сначала грузим текущий ProjectInfo,
        /// чтобы НЕ потерять продвинутые поля (компрессия и т.п.), которые мы не показываем.
        /// </summary>
        public void SaveSettings(string projectRoot, ProjectSettings settings)
        {
            var manager = new ProjectManager(projectRoot);
            var info = manager.LoadProjectInfo();

            info.Name = settings.Name;
            info.BaseDownloadURL = NormalizeBaseUrl(settings.BaseDownloadURL);
            info.MaintenanceCheckURL = settings.MaintenanceCheckURL;
            info.IsSelfPatchingApp = settings.IsSelfPatchingApp;
            info.CreateRepairPatch = settings.CreateRepairPatch;
            info.CreateInstallerPatch = settings.CreateInstallerPatch;
            info.CreateIncrementalPatch = settings.CreateIncrementalPatch;
            info.CreateAllIncrementalPatches = settings.CreateAllIncrementalPatches;
            info.IgnoredPaths.Clear();
            info.IgnoredPaths.AddRange(settings.IgnoredPaths);
            info.CompressionFormatRepairPatch = settings.CompressionFormatRepairPatch;
            info.CompressionFormatInstallerPatch = settings.CompressionFormatInstallerPatch;
            info.CompressionFormatIncrementalPatch = settings.CompressionFormatIncrementalPatch;
            info.BinaryDiffQuality = settings.BinaryDiffQuality;
            info.DontCreatePatchFilesForUnchangedFiles = settings.DontCreatePatchFilesForUnchangedFiles;

            manager.SaveProjectInfo(info);
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

            FileUtils.CopyDirectory(SelfPatcherSourceDir, Path.Combine(projectRoot, SelfPatcherFolder));
        }

        // --- список версий из папки Versions/ ---

        private static List<ProjectVersion> ReadVersions(string folder)
        {
            var versionsDir = Path.Combine(folder, VersionsFolder);
            if (!Directory.Exists(versionsDir))
                return new List<ProjectVersion>();

            var versions = Directory.GetDirectories(versionsDir)
                .Select(dir => new ProjectVersion
                {
                    Version = Path.GetFileName(dir),
                    BuildDate = Directory.GetLastWriteTime(dir)
                })
                .ToList();

            // Числовая сортировка версий через движковый VersionCode (1.0.0.10 после 1.0.0.2),
            // а не строковая, где "10" < "2".
            versions.Sort((a, b) => new VersionCode(a.Version).CompareTo(new VersionCode(b.Version)));
            return versions;
        }
    }
}
