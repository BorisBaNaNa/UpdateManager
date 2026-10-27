using System;
using System.IO;
using System.Text.Json;

namespace UpdateManager.Core.SelfUpdate
{
    /// <summary>
    /// Настройки самообновления UpdateManager из appsettings.json (секция "SelfUpdate").
    /// Сам файл лежит рядом с exe и входит в патч, поэтому смена сервера
    /// разъезжается к пользователям вместе с обновлением.
    /// </summary>
    public class SelfUpdateConfig
    {
        /// <summary>Имя файла конфигурации рядом с exe.</summary>
        public const string FileName = "appsettings.json";

        private const string SectionName = "SelfUpdate";
        private const string VersionInfoUrlKey = "VersionInfoUrl";
        private const string PublicKeyKey = "PublicKey";
        private const string VersionInfoFileName = "VersionInfo.info";

        public SelfUpdateConfig(string versionInfoUrl, string publicKey)
        {
            VersionInfoUrl = (versionInfoUrl ?? "").Trim();
            PublicKey = (publicKey ?? "").Trim();
        }

        /// <summary>URL манифеста VersionInfo.info самого UpdateManager; "" = самообновление выключено.</summary>
        public string VersionInfoUrl { get; }

        /// <summary>RSA-XML публичного ключа; "" = без проверки подписи.</summary>
        public string PublicKey { get; }

        public bool IsEnabled
        {
            get { return VersionInfoUrl.Length > 0; }
        }

        public bool HasPublicKey
        {
            get { return PublicKey.Length > 0; }
        }

        /// <summary>
        /// BaseDownloadURL для проекта самого UpdateManager: URL манифеста без "VersionInfo.info"
        /// (движок кладёт манифест и файлы патча в одно место). "" если URL не задан.
        /// </summary>
        public string BaseDownloadUrl
        {
            get
            {
                var url = VersionInfoUrl;
                if (url.EndsWith("/" + VersionInfoFileName, StringComparison.OrdinalIgnoreCase))
                    url = url.Substring(0, url.Length - VersionInfoFileName.Length);
                return url;
            }
        }

        /// <summary>Прочитать appsettings.json из папки приложения.</summary>
        public static SelfUpdateConfig Load()
        {
            return Load(Path.Combine(AppContext.BaseDirectory, FileName));
        }

        /// <summary>
        /// Прочитать конфиг из файла. Нет файла/секции — самообновление выключено;
        /// битый JSON — тоже выключено (не роняем запуск приложения из-за конфига).
        /// </summary>
        public static SelfUpdateConfig Load(string path)
        {
            if (!File.Exists(path))
                return new SelfUpdateConfig("", "");

            try
            {
                using (var doc = JsonDocument.Parse(File.ReadAllText(path),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
                {
                    JsonElement section;
                    if (!doc.RootElement.TryGetProperty(SectionName, out section) ||
                        section.ValueKind != JsonValueKind.Object)
                        return new SelfUpdateConfig("", "");

                    return new SelfUpdateConfig(ReadString(section, VersionInfoUrlKey), ReadString(section, PublicKeyKey));
                }
            }
            catch (JsonException)
            {
                return new SelfUpdateConfig("", "");
            }
        }

        private static string ReadString(JsonElement section, string key)
        {
            JsonElement value;
            return section.TryGetProperty(key, out value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : "";
        }
    }
}
