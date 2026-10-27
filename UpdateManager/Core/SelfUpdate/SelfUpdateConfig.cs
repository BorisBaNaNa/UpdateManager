using System.Configuration;

namespace UpdateManager.Core.SelfUpdate
{
    /// <summary>
    /// Настройки самообновления UpdateManager из App.config (appSettings).
    /// Сам конфиг (UpdateManager.exe.config) входит в патч, поэтому смена сервера
    /// разъезжается к пользователям вместе с обновлением.
    /// </summary>
    public class SelfUpdateConfig
    {
        private const string VersionInfoUrlKey = "SelfUpdate.VersionInfoUrl";
        private const string PublicKeyKey = "SelfUpdate.PublicKey";
        private const string VersionInfoFileName = "VersionInfo.info";

        /// <summary>URL манифеста VersionInfo.info самого UpdateManager; "" = самообновление выключено.</summary>
        public string VersionInfoUrl { get; private set; }

        /// <summary>RSA-XML публичного ключа; "" = без проверки подписи.</summary>
        public string PublicKey { get; private set; }

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
                if (url.EndsWith("/" + VersionInfoFileName, System.StringComparison.OrdinalIgnoreCase))
                    url = url.Substring(0, url.Length - VersionInfoFileName.Length);
                return url;
            }
        }

        public static SelfUpdateConfig Load()
        {
            return new SelfUpdateConfig
            {
                VersionInfoUrl = (ConfigurationManager.AppSettings[VersionInfoUrlKey] ?? "").Trim(),
                PublicKey = (ConfigurationManager.AppSettings[PublicKeyKey] ?? "").Trim()
            };
        }
    }
}
