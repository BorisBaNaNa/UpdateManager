using System;
using System.Collections.Generic;
using System.IO;

namespace UpdateManager.Core.Common
{
    /// <summary>Файловые утилиты, общие для нескольких сервисов.</summary>
    public static class FileUtils
    {
        /// <summary>
        /// Ищет (рекурсивно) файлы и папки с не-ASCII символами в имени, возвращает их
        /// относительные пути — не больше <paramref name="max"/> штук (обход прерывается, как
        /// только набрали достаточно). Такие имена ломают ПРИМЕНЕНИЕ installer-патча: движок
        /// пакует всё в TAR, а вшитый SharpZipLib читает имена записей не в UTF-8 → клиент
        /// падает с "Illegal characters in path". Repair/incremental это не затрагивает.
        /// </summary>
        public static List<string> FindNonAsciiNames(string root, int max)
        {
            var result = new List<string>();
            var full = NormalizeForCompare(root);
            ScanNonAscii(full, full, result, max);
            return result;
        }

        private static void ScanNonAscii(string dir, string root, List<string> result, int max)
        {
            foreach (var entry in Directory.GetFileSystemEntries(dir))
            {
                if (result.Count >= max)
                    return;

                if (HasNonAscii(Path.GetFileName(entry)))
                    result.Add(entry.Substring(root.Length).TrimStart(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                if (Directory.Exists(entry))
                    ScanNonAscii(entry, root, result, max);
            }
        }

        private static bool HasNonAscii(string name)
        {
            foreach (var c in name)
                if (c > 0x7F)
                    return true;
            return false;
        }

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
