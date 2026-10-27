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

        /// <summary>
        /// ОТДЕЛЬНАЯ защищённая папка на сервере под приватный ключ (для коллег, чтобы и они могли
        /// подписывать). ⚠ Обязана отличаться от RemotePath/публичной папки патча: приватный ключ
        /// не должен попасть туда, откуда качают клиенты. Пусто = ключ на FTP не заливается.
        /// </summary>
        public string PrivateKeyRemotePath { get; set; } = "";

        /// <summary>
        /// Пароль был сохранён, но его не удалось расшифровать (другая машина/учётка Windows — DPAPI).
        /// Только в памяти, на диск не пишется. Сигнал вызывающему коду: попросить пароль заново.
        /// </summary>
        public bool PasswordDecryptFailed { get; set; }

        /// <summary>Минимально заполнены ли поля для попытки подключения.</summary>
        public bool IsComplete()
        {
            return !string.IsNullOrWhiteSpace(Host) && Port > 0;
        }
    }
}
