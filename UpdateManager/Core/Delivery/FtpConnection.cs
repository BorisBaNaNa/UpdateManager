namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Параметры FTP-подключения для доставки патча. Пароль хранится в открытом виде
    /// только в памяти; на диск (в профиль пользователя) он уходит зашифрованным через DPAPI.
    /// </summary>
    public class FtpConnection
    {
        /// <summary>Порт FTP по умолчанию.</summary>
        public const int DefaultPort = 21;

        public string Host { get; set; } = "";
        public int Port { get; set; } = DefaultPort;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        /// <summary>Папка на сервере, в которую заливается содержимое Output/ (пусто = корень).</summary>
        public string RemotePath { get; set; } = "";

        /// <summary>Минимально заполнены ли поля для попытки подключения.</summary>
        public bool IsComplete()
        {
            return !string.IsNullOrWhiteSpace(Host) && Port > 0;
        }
    }
}
