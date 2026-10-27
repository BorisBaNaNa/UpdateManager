using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UpdateManager.Core.Common
{
    /// <summary>
    /// Маски файлов (glob) относительно папки: «latest.json», «*.apk», «manifests/*.json», «**/*.json».
    /// «*» и «?» — в пределах одной папки, «**» — любая глубина. Регистр не важен (Windows),
    /// разделитель — «/» или «\».
    /// </summary>
    public static class FileMask
    {
        /// <summary>Подходит ли относительный путь (от корня папки) под маску.</summary>
        public static bool IsMatch(string relativePath, string mask)
        {
            if (string.IsNullOrWhiteSpace(mask))
                return false;
            return ToRegex(mask.Trim()).IsMatch(Normalize(relativePath));
        }

        /// <summary>Подходит ли путь хотя бы под одну из масок.</summary>
        public static bool IsMatchAny(string relativePath, IEnumerable<string> masks)
        {
            return masks != null && masks.Any(m => IsMatch(relativePath, m));
        }

        /// <summary>Все файлы папки (рекурсивно) — относительными путями с «/», по алфавиту.</summary>
        public static List<string> ListFiles(string folder)
        {
            var root = Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(f => Normalize(Path.GetFullPath(f).Substring(root.Length)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Непустые маски из многострочного текста (по одной в строке).</summary>
        public static List<string> ParseLines(string text)
        {
            return (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        }

        public static string Normalize(string relativePath)
        {
            return (relativePath ?? "").Replace('\\', '/').TrimStart('/');
        }

        private static Regex ToRegex(string mask)
        {
            var glob = Normalize(mask);
            var sb = new StringBuilder("^");
            for (int i = 0; i < glob.Length; i++)
            {
                var c = glob[i];
                if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
                {
                    // «**/» — любое число папок (в т.ч. ни одной); «**» в конце — что угодно.
                    i++;
                    if (i + 1 < glob.Length && glob[i + 1] == '/')
                    {
                        i++;
                        sb.Append("(?:.*/)?");
                    }
                    else
                    {
                        sb.Append(".*");
                    }
                }
                else if (c == '*')
                {
                    sb.Append("[^/]*");
                }
                else if (c == '?')
                {
                    sb.Append("[^/]");
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }
            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
