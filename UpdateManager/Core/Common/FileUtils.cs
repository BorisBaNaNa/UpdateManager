using System;
using System.IO;

namespace UpdateManager.Core.Common
{
    /// <summary>Файловые утилиты, общие для нескольких сервисов.</summary>
    public static class FileUtils
    {
        /// <summary>
        /// Папки пересекаются по дереву: совпадают или одна вложена в другую.
        /// Копировать такое в подпапку нельзя — рекурсия и раздувание (StageBuild).
        /// </summary>
        public static bool PathsOverlap(string a, string b)
        {
            var na = NormalizeForCompare(a);
            var nb = NormalizeForCompare(b);
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase)
                || IsInside(na, nb)
                || IsInside(nb, na);
        }

        // child лежит внутри ancestor (строго глубже), сравнение по полным путям.
        private static bool IsInside(string child, string ancestor)
        {
            return child.StartsWith(ancestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeForCompare(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

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
