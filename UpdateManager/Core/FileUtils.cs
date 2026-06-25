using System.IO;

namespace UpdateManager.Core
{
    /// <summary>Файловые утилиты, общие для нескольких сервисов.</summary>
    public static class FileUtils
    {
        /// <summary>Рекурсивно копирует содержимое одной папки в другую (с перезаписью файлов).</summary>
        public static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

            foreach (var subDir in Directory.GetDirectories(source))
                CopyDirectory(subDir, Path.Combine(destination, Path.GetFileName(subDir)));
        }
    }
}
