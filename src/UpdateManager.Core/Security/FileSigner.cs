using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UpdateManager.Core.Common;

namespace UpdateManager.Core.Security
{
    /// <summary>
    /// Подпись файлов по маске (Android и любые другие выпуски без патчей). Файлы не читаются как текст
    /// и не меняются: RSA, SHA-256, PKCS#1 v1.5 по сырым байтам; подпись — base64 в «&lt;файл&gt;.sig» рядом
    /// (UTF-8 без BOM, без перевода строки). Ключ — тот же RSA-XML проекта, что подписывает патчи Windows.
    /// PKCS#1 v1.5 детерминирован: тот же файл и ключ дают ту же подпись байт в байт.
    /// </summary>
    public class FileSigner
    {
        public const string SignatureExtension = ".sig";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Подписать файлы папки, подходящие под маски. Возвращает подписанные (относительные пути с «/»).
        /// Каждая маска обязана что-то найти — иначе выложилось бы обновление без подписи. После подписи —
        /// самопроверка публичным ключом проекта.
        /// </summary>
        public List<string> Sign(string folder, IList<string> masks, string privateKeyXml, string publicKeyXml)
        {
            if (masks == null || masks.Count == 0)
                return new List<string>();

            var files = FileMask.ListFiles(folder).Where(f => !IsSignature(f)).ToList();
            var targets = new List<string>();
            foreach (var mask in masks)
            {
                var matched = files.Where(f => FileMask.IsMatch(f, mask)).ToList();
                if (matched.Count == 0)
                    throw new InvalidOperationException("Маска подписи «" + mask + "» не нашла ни одного файла в " +
                        folder + ".\nБез подписи клиент не примет обновление — проверьте маску и папку билда.");
                targets.AddRange(matched.Where(f => !targets.Contains(f)));
            }

            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(privateKeyXml);
                foreach (var relative in targets)
                {
                    var path = Path.Combine(folder, relative);
                    var signature = rsa.SignData(File.ReadAllBytes(path), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    File.WriteAllText(path + SignatureExtension, Convert.ToBase64String(signature), Utf8NoBom);
                }
            }

            foreach (var relative in targets)
            {
                var path = Path.Combine(folder, relative);
                if (!Verify(File.ReadAllBytes(path), File.ReadAllText(path + SignatureExtension), publicKeyXml))
                    throw new InvalidOperationException("Самопроверка подписи не прошла: " + relative +
                        ".\nПриватный и публичный ключи проекта не из одной пары?");
            }

            return targets;
        }

        /// <summary>Сходится ли подпись (base64) с данными по публичному ключу.</summary>
        public static bool Verify(byte[] data, string signatureBase64, string publicKeyXml)
        {
            byte[] signature;
            try
            {
                signature = Convert.FromBase64String((signatureBase64 ?? "").Trim());
            }
            catch (FormatException)
            {
                return false;
            }

            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(publicKeyXml);
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        public static bool IsSignature(string relativePath)
        {
            return relativePath.EndsWith(SignatureExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
