using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UpdateManager.Core.Common;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Хранилище FTP-подключений — настройка УРОВНЯ ПОЛЬЗОВАТЕЛЯ, а не проекта:
    /// %AppData%\UpdateManager\ftp.xml. Намеренно НЕ в файле проекта, чтобы реквизиты
    /// (и тем более пароль) не уезжали вместе с проектом. Запись привязана к пути проекта;
    /// пароль шифруется через DPAPI (см. <see cref="SecretProtector"/>).
    /// </summary>
    public class FtpConnectionStore
    {
        private const string AppFolderName = "UpdateManager";
        private const string FileName = "ftp.xml";
        private const string RootElement = "FtpConnections";
        private const string ItemElement = "Connection";
        private const string ProjectAttribute = "project";

        private readonly string _filePath;

        public FtpConnectionStore()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);
            _filePath = Path.Combine(dir, FileName);
        }

        /// <summary>
        /// Прочитать подключение для проекта. Возвращает null, если его нет.
        /// Если пароль не удалось расшифровать (другая учётка/машина) — поле Password пустое,
        /// остальные реквизиты возвращаются (пароль попросят заново).
        /// </summary>
        public FtpConnection Load(string projectRoot)
        {
            if (!File.Exists(_filePath))
                return null;

            var item = XDocument.Load(_filePath).Root
                .Elements(ItemElement)
                .FirstOrDefault(e => string.Equals(
                    (string)e.Attribute(ProjectAttribute), projectRoot, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return null;

            int port;
            if (!int.TryParse((string)item.Element("Port"), out port))
                port = FtpConnection.DefaultPort;

            return new FtpConnection
            {
                Host = (string)item.Element("Host") ?? "",
                Port = port,
                Username = (string)item.Element("Username") ?? "",
                Password = SecretProtector.Unprotect((string)item.Element("Password")) ?? "",
                RemotePath = (string)item.Element("RemotePath") ?? ""
            };
        }

        /// <summary>Сохранить (или обновить) подключение для проекта. Пароль шифруется DPAPI.</summary>
        public void Save(string projectRoot, FtpConnection conn)
        {
            var root = File.Exists(_filePath)
                ? XDocument.Load(_filePath).Root
                : new XElement(RootElement);

            root.Elements(ItemElement)
                .Where(e => string.Equals(
                    (string)e.Attribute(ProjectAttribute), projectRoot, StringComparison.OrdinalIgnoreCase))
                .Remove();

            root.Add(new XElement(ItemElement,
                new XAttribute(ProjectAttribute, projectRoot),
                new XElement("Host", conn.Host ?? ""),
                new XElement("Port", conn.Port),
                new XElement("Username", conn.Username ?? ""),
                new XElement("Password", SecretProtector.Protect(conn.Password)),
                new XElement("RemotePath", conn.RemotePath ?? "")));

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            new XDocument(root).Save(_filePath);
        }
    }
}
