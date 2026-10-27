using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
