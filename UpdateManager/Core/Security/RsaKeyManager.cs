using System;
using System.IO;
using SimplePatchToolSecurity;

namespace UpdateManager.Core.Security
{
    /// <summary>
    /// Управление парой RSA-ключей проекта. Ключи лежат В КОРНЕ ПАПКИ ПРОЕКТА обновлений
    /// (public.key / private.key), рядом с Settings.xml — но НЕ внутри Output/, поэтому при
    /// доставке они не попадают на публичный сервер.
    ///
    /// public.key (RSA-XML-строка вида &lt;RSAKeyValue&gt;…&lt;/RSAKeyValue&gt;) свободно
    /// отдаётся разработчику клиента — он передаёт её содержимое в UseVersionInfoVerifier(...).
    /// private.key — СЕКРЕТ: им подписывается патч, его нельзя коммитить и класть в публичную папку.
    /// </summary>
    public class RsaKeyManager
    {
        /// <summary>Имя файла публичного ключа в корне проекта.</summary>
        public const string PublicKeyFileName = "public.key";

        /// <summary>Имя файла приватного ключа в корне проекта.</summary>
        public const string PrivateKeyFileName = "private.key";

        /// <summary>Полный путь к public.key в корне проекта.</summary>
        public string PublicKeyPath(string projectRoot)
        {
            return Path.Combine(projectRoot, PublicKeyFileName);
        }

        /// <summary>Полный путь к private.key в корне проекта.</summary>
        public string PrivateKeyPath(string projectRoot)
        {
            return Path.Combine(projectRoot, PrivateKeyFileName);
        }

        /// <summary>Есть ли уже сгенерированный публичный ключ.</summary>
        public bool HasPublicKey(string projectRoot)
        {
            return File.Exists(PublicKeyPath(projectRoot));
        }

        /// <summary>Есть ли приватный ключ для подписи.</summary>
        public bool HasPrivateKey(string projectRoot)
        {
            return File.Exists(PrivateKeyPath(projectRoot));
        }

        /// <summary>Оба ключа на месте.</summary>
        public bool HasKeys(string projectRoot)
        {
            return HasPublicKey(projectRoot) && HasPrivateKey(projectRoot);
        }

        /// <summary>
        /// Сгенерировать новую пару ключей и сохранить в корень проекта.
        /// ВНИМАНИЕ: перезапись ключей инвалидирует всех уже разошедшихся клиентов (у них зашит
        /// старый публичный ключ) — вызывающий код должен спросить подтверждение.
        /// </summary>
        public void Generate(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Не указана папка проекта.");

            string publicKey, privateKey;
            SecurityUtils.CreateRSAKeyPair(out publicKey, out privateKey);

            // Пишем без BOM, чистые RSA-XML-строки — как ожидает движковый Verifier на клиенте.
            File.WriteAllText(PublicKeyPath(projectRoot), publicKey);
            File.WriteAllText(PrivateKeyPath(projectRoot), privateKey);
        }

        /// <summary>Прочитать текст приватного ключа (для подписи). Бросает, если файла нет.</summary>
        public string ReadPrivateKey(string projectRoot)
        {
            var path = PrivateKeyPath(projectRoot);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Не найден приватный ключ (" + PrivateKeyFileName + ") в папке проекта.", path);

            return File.ReadAllText(path);
        }
    }
}
