using System.Diagnostics;
using System.IO;

namespace UpdateManager.Core.Versioning
{
    /// <summary>Результат определения версии билда.</summary>
    public class DetectedBuild
    {
        /// <summary>Имя найденного главного exe, либо null если не нашли.</summary>
        public string ExecutableName { get; set; }

        /// <summary>FileVersion главного exe, либо null если нет.</summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// Определяет версию билда по главному exe (его FileVersion).
    /// Главный exe = заданный в проекте MainExecutable, иначе "&lt;ИмяПроекта&gt;.exe".
    /// Наше ПО версию не назначает — только пытается прочитать, решение за разработчиком.
    /// </summary>
    public class VersionDetector
    {
        public DetectedBuild Detect(string buildFolder, string projectName, string mainExecutableOverride)
        {
            var result = new DetectedBuild();

            if (string.IsNullOrEmpty(buildFolder) || !Directory.Exists(buildFolder))
                return result;

            var exeName = !string.IsNullOrWhiteSpace(mainExecutableOverride)
                ? mainExecutableOverride
                : projectName + ".exe";

            var exePath = Path.Combine(buildFolder, exeName);
            if (!File.Exists(exePath))
                return result; // exe не найден — ExecutableName остаётся null

            result.ExecutableName = exeName;

            var info = FileVersionInfo.GetVersionInfo(exePath);
            result.Version = string.IsNullOrEmpty(info.FileVersion) ? null : info.FileVersion;
            return result;
        }
    }
}
