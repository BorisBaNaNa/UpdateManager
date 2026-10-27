using System.Collections.Generic;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Платформа Android проекта: без патчей — файлы папки билда (apk, манифест) подписываются по маске
    /// и выкладываются в папку канала на сервере. Ключ и доставка — общие с Windows.
    /// </summary>
    public class AndroidConfig
    {
        /// <summary>Папка канала по умолчанию — внутри папки Windows-патча.</summary>
        public const string DefaultChannel = "AndroidPatch";

        /// <summary>Папка билда Android (собирается вне UpdateManager).</summary>
        public string BuildSource { get; set; } = "";

        /// <summary>Папка канала на сервере относительно папки Windows-патча (BaseDownloadURL).</summary>
        public string Channel { get; set; } = DefaultChannel;

        /// <summary>Маски подписываемых файлов (glob относительно папки билда). Пусто — ничего не подписывается.</summary>
        public List<string> SignMasks { get; set; } = new List<string>();

        /// <summary>Маски файлов, которые не выкладываются.</summary>
        public List<string> ExcludeMasks { get; set; } = new List<string>();

        public AndroidConfig Clone()
        {
            return new AndroidConfig
            {
                BuildSource = BuildSource,
                Channel = Channel,
                SignMasks = new List<string>(SignMasks),
                ExcludeMasks = new List<string>(ExcludeMasks)
            };
        }
    }
}
