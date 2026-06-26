using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using UpdateManager.Core.Delivery;

namespace UpdateManager.Core.Project
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
        private const string DeliveryElement = "Delivery";
        private const string DeliveryMethodAttribute = "method";
        private const string DeliveryPathElement = "Path";
        private const string LastDeliveredAtElement = "LastDeliveredAt";
        private const string LastBuiltSettingsElement = "LastBuiltSettings";

        /// <summary>Версия схемы этого файла — чтобы в будущем безболезненно менять формат.</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>Имя главного exe для определения версии (пусто = угадываем по имени проекта).</summary>
        public string MainExecutable { get; set; } = "";

        /// <summary>Последняя выбранная папка-источник билда (запоминаем между выкатками).</summary>
        public string LastBuildSource { get; set; } = "";

        /// <summary>Сохранённая конфигурация доставки (метод + путь).</summary>
        public DeliveryConfig Delivery { get; set; } = new DeliveryConfig();

        /// <summary>Когда патч последний раз доставляли (null = ещё не доставляли).</summary>
        public DateTime? LastDeliveredAt { get; set; }

        /// <summary>
        /// Отпечаток настроек на момент последней успешной сборки патча (пусто = ещё не собирали).
        /// При доставке сравниваем с текущим, чтобы поймать устаревший Output после смены настроек.
        /// </summary>
        public string LastBuiltSettings { get; set; } = "";

        /// <summary>Прочитать наш файл из корня проекта. Если его нет — вернуть значения по умолчанию.</summary>
        public static ProjectMeta Load(string projectRoot)
        {
            var path = Path.Combine(projectRoot, FileName);
            if (!File.Exists(path))
                return new ProjectMeta();

            var root = XDocument.Load(path).Root;
            var meta = new ProjectMeta
            {
                SchemaVersion = (int?)root.Attribute(SchemaVersionAttribute) ?? CurrentSchemaVersion,
                MainExecutable = (string)root.Element(MainExecutableElement) ?? "",
                LastBuildSource = (string)root.Element(LastBuildSourceElement) ?? "",
                LastBuiltSettings = (string)root.Element(LastBuiltSettingsElement) ?? ""
            };

            var delivery = root.Element(DeliveryElement);
            if (delivery != null)
            {
                meta.Delivery.Method = (string)delivery.Attribute(DeliveryMethodAttribute) ?? DeliveryMethods.Folder;
                meta.Delivery.Path = (string)delivery.Element(DeliveryPathElement) ?? "";
            }

            var deliveredAt = (string)root.Element(LastDeliveredAtElement);
            DateTime parsed;
            if (!string.IsNullOrEmpty(deliveredAt) &&
                DateTime.TryParse(deliveredAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                meta.LastDeliveredAt = parsed;

            return meta;
        }

        /// <summary>Записать наш файл в корень проекта.</summary>
        public void Save(string projectRoot)
        {
            var doc = new XDocument(
                new XElement(RootElement,
                    new XAttribute(SchemaVersionAttribute, SchemaVersion),
                    new XElement(MainExecutableElement, MainExecutable ?? ""),
                    new XElement(LastBuildSourceElement, LastBuildSource ?? ""),
                    new XElement(DeliveryElement,
                        new XAttribute(DeliveryMethodAttribute, Delivery.Method ?? DeliveryMethods.Folder),
                        new XElement(DeliveryPathElement, Delivery.Path ?? "")),
                    LastDeliveredAt.HasValue
                        ? new XElement(LastDeliveredAtElement, LastDeliveredAt.Value.ToString("o", CultureInfo.InvariantCulture))
                        : null,
                    new XElement(LastBuiltSettingsElement, LastBuiltSettings ?? "")
                ));
            doc.Save(Path.Combine(projectRoot, FileName));
        }
    }
}
