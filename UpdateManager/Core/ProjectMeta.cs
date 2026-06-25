using System.IO;
using System.Xml.Linq;

namespace UpdateManager.Core
{
    /// <summary>
    /// Наш файл проекта updatemanager.project.xml — мета-информация UpdateManager,
    /// которой нет у движка SimplePatchTool. Лежит в корне проекта рядом с Settings.xml.
    /// </summary>
    public class ProjectMeta
    {
        /// <summary>Имя нашего файла проекта в корне проекта.</summary>
        public const string FileName = "updatemanager.project.xml";

        /// <summary>Текущая версия схемы файла — увеличиваем при изменении формата.</summary>
        public const int CurrentSchemaVersion = 1;

        // Имена узлов нашего XML — в одном месте, чтобы Load и Save не расходились.
        private const string RootElement = "UpdateManagerProject";
        private const string SchemaVersionAttribute = "schemaVersion";
        private const string MainExecutableElement = "MainExecutable";
        private const string LastBuildSourceElement = "LastBuildSource";
        private const string DeliveryTargetsElement = "DeliveryTargets";

        /// <summary>Версия схемы этого файла — чтобы в будущем безболезненно менять формат.</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>Имя главного exe для определения версии (пусто = угадываем по имени проекта).</summary>
        public string MainExecutable { get; set; } = "";

        /// <summary>Последняя выбранная папка-источник билда (запоминаем между выкатками).</summary>
        public string LastBuildSource { get; set; } = "";

        // DeliveryTargets (цели доставки: папка / FTP) — зарезервировано, добавим позже.

        /// <summary>Прочитать наш файл из корня проекта. Если его нет — вернуть значения по умолчанию.</summary>
        public static ProjectMeta Load(string projectRoot)
        {
            var path = Path.Combine(projectRoot, FileName);
            if (!File.Exists(path))
                return new ProjectMeta();

            var root = XDocument.Load(path).Root;
            return new ProjectMeta
            {
                SchemaVersion = (int?)root.Attribute(SchemaVersionAttribute) ?? CurrentSchemaVersion,
                MainExecutable = (string)root.Element(MainExecutableElement) ?? "",
                LastBuildSource = (string)root.Element(LastBuildSourceElement) ?? ""
            };
        }

        /// <summary>Записать наш файл в корень проекта.</summary>
        public void Save(string projectRoot)
        {
            var doc = new XDocument(
                new XElement(RootElement,
                    new XAttribute(SchemaVersionAttribute, SchemaVersion),
                    new XElement(MainExecutableElement, MainExecutable ?? ""),
                    new XElement(LastBuildSourceElement, LastBuildSource ?? ""),
                    new XElement(DeliveryTargetsElement) // зарезервировано под папку / FTP
                ));
            doc.Save(Path.Combine(projectRoot, FileName));
        }
    }
}
