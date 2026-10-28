using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UpdateManager.Core.Common;
using UpdateManager.Core.Security;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Что из папки билда Android уходит на сервер: все файлы, кроме масок «не выкладывать», и .sig —
    /// только у подписываемых файлов (чужой или устаревший .sig рядом с неподписанным файлом клиент
    /// счёл бы подделкой).
    /// </summary>
    public static class AndroidPackage
    {
        /// <summary>Относительные пути (с «/») файлов выкладки, по алфавиту.</summary>
        public static List<string> Files(string buildFolder, IList<string> signMasks, IList<string> excludeMasks)
        {
            var all = FileMask.ListFiles(buildFolder);
            var result = new List<string>();
            foreach (var file in all)
            {
                if (FileMask.IsMatchAny(file, excludeMasks))
                    continue;

                if (FileSigner.IsSignature(file))
                {
                    var signedFile = file.Substring(0, file.Length - FileSigner.SignatureExtension.Length);
                    if (!FileMask.IsMatchAny(signedFile, signMasks) || FileMask.IsMatchAny(signedFile, excludeMasks))
                        continue;
                }
                result.Add(file);
            }
            return result;
        }

        /// <summary>Подписываемые файлы выкладки (у каждого обязан быть .sig).</summary>
        public static List<string> SignedFiles(IList<string> files, IList<string> signMasks)
        {
            return files.Where(f => !FileSigner.IsSignature(f) && FileMask.IsMatchAny(f, signMasks)).ToList();
        }

        /// <summary>SHA-256 (hex) файлов выкладки без .sig: подписи выводятся из файлов, сверять их отдельно незачем.</summary>
        public static Dictionary<string, string> Hashes(string buildFolder, IList<string> files)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                foreach (var file in files.Where(f => !FileSigner.IsSignature(f)))
                {
                    using (var stream = File.OpenRead(Path.Combine(buildFolder, file.Replace('/', Path.DirectorySeparatorChar))))
                        result[file] = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
            return result;
        }

        /// <summary>
        /// Чем выкладка отличается от записанной: изменённые, новые и пропавшие файлы (пусто — та же).
        /// </summary>
        public static List<string> Changes(IDictionary<string, string> recorded, IDictionary<string, string> current)
        {
            var result = new List<string>();
            foreach (var pair in current.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                string hash;
                if (!recorded.TryGetValue(pair.Key, out hash))
                    result.Add("новый: " + pair.Key);
                else if (!string.Equals(hash, pair.Value, StringComparison.OrdinalIgnoreCase))
                    result.Add("изменён: " + pair.Key);
            }
            foreach (var file in recorded.Keys.Where(k => !current.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                result.Add("пропал: " + file);
            return result;
        }

        /// <summary>Скопировать файлы выкладки во временную папку (её выкладывают целиком). Удаляет вызывающий.</summary>
        public static string Stage(string buildFolder, IList<string> files)
        {
            var staging = Path.Combine(Path.GetTempPath(), "UpdateManagerAndroid_" + Guid.NewGuid().ToString("N"));
            foreach (var file in files)
            {
                var target = Path.Combine(staging, file.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(buildFolder, file), target);
            }
            Directory.CreateDirectory(staging);
            return staging;
        }
    }
}
