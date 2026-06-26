using System;

namespace UpdateManager.Core.Common
{
    /// <summary>
    /// Работа с BaseDownloadURL как с парой «адрес сервера + директория загрузки».
    /// Директория — общий хвост: и публичный путь, по которому клиент качает патч, и относительный
    /// путь внутри FTP-корня, куда патч заливается. Это связывает «где лежит» и «откуда качается».
    /// </summary>
    public static class DownloadUrl
    {
        /// <summary>Разбить BaseDownloadURL на адрес сервера (scheme://host:port/) и директорию (хвост).</summary>
        public static void Split(string baseUrl, out string server, out string directory)
        {
            server = "";
            directory = "";

            baseUrl = (baseUrl ?? "").Trim();
            if (baseUrl.Length == 0)
                return;

            Uri uri;
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out uri))
            {
                server = uri.GetLeftPart(UriPartial.Authority) + "/";
                directory = uri.AbsolutePath.Trim('/');
            }
            else
            {
                // Не разобрали как URL — кладём всё в адрес, директорию не выделяем.
                server = baseUrl;
            }
        }

        /// <summary>Склеить адрес сервера и директорию в нормализованный BaseDownloadURL (с финальным «/»).</summary>
        public static string Join(string server, string directory)
        {
            server = (server ?? "").Trim();
            directory = (directory ?? "").Trim().Trim('/');
            if (server.Length == 0)
                return "";

            var result = server.TrimEnd('/') + "/";
            if (directory.Length > 0)
                result += directory + "/";
            return result;
        }

        /// <summary>
        /// Собрать абсолютный путь на FTP: корень всех патчей + директория загрузки проекта.
        /// Пример: ("patches", "UpdaterTest/Output") -> "/patches/UpdaterTest/Output".
        /// </summary>
        public static string CombineRemote(string root, string directory)
        {
            root = (root ?? "").Trim().Trim('/');
            directory = (directory ?? "").Trim().Trim('/');

            var path = "/";
            if (root.Length > 0)
                path += root;
            if (directory.Length > 0)
                path = path.TrimEnd('/') + "/" + directory;
            return path;
        }
    }
}
