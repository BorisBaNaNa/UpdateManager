using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        public const int CurrentSchemaVersion = 2; // 2 — версии по платформам: Versions/Windows, Versions/Android (2.2)

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
        private const string SignPatchElement = "SignPatch";
        private const string DeliveredVersionsElement = "DeliveredVersions";
        private const string DeliveredVersionElement = "Version";
        private const string DeliveredVersionIdAttribute = "id";
        private const string DeliveredVersionAtAttribute = "at";
        private const string PatchedVersionsElement = "PatchedVersions";
        private const string VerifiedVersionsElement = "VerifiedVersions";
        private const string VerifiedOkAttribute = "ok";
        private const string VerifiedDetailsAttribute = "details";
        private const string PlatformsElement = "Platforms";
        private const string WindowsAttribute = "windows";
        private const string AndroidAttribute = "android";
        private const string AndroidElement = "Android";
        private const string AndroidBuildSourceAttribute = "buildSource";
        private const string AndroidChannelAttribute = "channel";
        private const string AndroidSignAttribute = "sign";
        private const string AndroidSignElement = "Sign";
        private const string AndroidExcludeElement = "Exclude";
        private const string AndroidSignedElement = "AndroidSigned";
        private const string AndroidDeliveredElement = "AndroidDelivered";
        private const string AndroidVerifiedElement = "AndroidVerified";
        private const string AndroidRecordsElement = "AndroidRecords";
        private const string RemovedVersionsElement = "RemovedVersions";
        private const string RemovedAndroidVersionsElement = "RemovedAndroidVersions";
        private const string AndroidRecordSignedAttribute = "signed";
        private const string AndroidRecordMaskElement = "Mask";
        private const string AndroidRecordFileElement = "File";
        private const string AndroidRecordPathAttribute = "path";
        private const string AndroidRecordHashAttribute = "sha256";

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
        /// Когда доставляли каждую версию (версия → время последней доставки). Output всегда содержит
        /// патч до самой новой собранной версии — доставка отмечается за ней.
        /// </summary>
        public Dictionary<string, DateTime> DeliveredVersions { get; set; } =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Когда для каждой версии последний раз успешно собрали патч (версия → время).</summary>
        public Dictionary<string, DateTime> PatchedVersions { get; set; } =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Итог последней проверки («Проверить») по версиям: проверка качает то, что лежит на сервере,
        /// а там патч до самой новой собранной версии — итог отмечается за ней.
        /// </summary>
        public Dictionary<string, VerificationResult> VerifiedVersions { get; set; } =
            new Dictionary<string, VerificationResult>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Отпечаток настроек на момент последней успешной сборки патча (пусто = ещё не собирали).
        /// При доставке сравниваем с текущим, чтобы поймать устаревший Output после смены настроек.
        /// </summary>
        public string LastBuiltSettings { get; set; } = "";

        /// <summary>
        /// Подписывать патч RSA-ключом при сборке. Живёт здесь, а не в движковом Settings.xml
        /// (у движка нет такого поля); в UI прокидывается через ProjectSettings.
        /// </summary>
        public bool SignPatch { get; set; }

        /// <summary>Платформа Windows (патч движка) включена. У старых проектов — да.</summary>
        public bool WindowsEnabled { get; set; } = true;

        /// <summary>Платформа Android (подпись файлов по маске, без патчей) включена.</summary>
        public bool AndroidEnabled { get; set; }

        /// <summary>Настройки платформы Android.</summary>
        public AndroidConfig Android { get; set; } = new AndroidConfig();

        /// <summary>Android: когда файлы версии подписаны (версия → время).</summary>
        public Dictionary<string, DateTime> AndroidSignedVersions { get; set; } =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Android: как версия записана на этапе подписи (подписана ли, маски, отпечатки файлов).
        /// У записей 2.1.0 его нет — тогда признак подписи берётся из текущих масок, а отпечатки не сверяются.
        /// </summary>
        public Dictionary<string, AndroidRecord> AndroidRecords { get; set; } =
            new Dictionary<string, AndroidRecord>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Удалённые версии («Удалить версию…»): их нет ни в Versions/, ни в записях этапов, но каталог
        /// учитывает их в нумерации — «№ N» остальных выпусков не сдвигается.
        /// </summary>
        public HashSet<string> RemovedVersions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Удалённые или откатанные Android-версии: их номер повторно не записывается (клиент не обновился бы).</summary>
        public HashSet<string> RemovedAndroidVersions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Забыть Windows-версию (откат): записи патча, доставки и проверки; номер в каталоге остаётся занятым.</summary>
        public void ForgetWindowsVersion(string version)
        {
            PatchedVersions.Remove(version);
            DeliveredVersions.Remove(version);
            VerifiedVersions.Remove(version);
            RemovedVersions.Add(version);
        }

        /// <summary>Забыть Android-версию (откат): все её записи этапов; номер повторно не записывается.</summary>
        public void ForgetAndroidVersion(string version)
        {
            AndroidSignedVersions.Remove(version);
            AndroidRecords.Remove(version);
            AndroidDeliveredVersions.Remove(version);
            AndroidVerifiedVersions.Remove(version);
            RemovedAndroidVersions.Add(version);
        }

        /// <summary>Забыть версию: все записи этапов обеих платформ; номер в каталоге остаётся занятым.</summary>
        public void ForgetVersion(string version)
        {
            ForgetWindowsVersion(version);
            ForgetAndroidVersion(version);
        }

        /// <summary>Android: когда версия выложена в канал (версия → время).</summary>
        public Dictionary<string, DateTime> AndroidDeliveredVersions { get; set; } =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Android: итог проверки канала по версиям.</summary>
        public Dictionary<string, VerificationResult> AndroidVerifiedVersions { get; set; } =
            new Dictionary<string, VerificationResult>(StringComparer.OrdinalIgnoreCase);

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
                LastBuiltSettings = (string)root.Element(LastBuiltSettingsElement) ?? "",
                SignPatch = (bool?)root.Element(SignPatchElement) ?? false
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

            LoadVersionDates(root.Element(DeliveredVersionsElement), meta.DeliveredVersions);
            LoadVersionDates(root.Element(PatchedVersionsElement), meta.PatchedVersions);

            LoadVerification(root.Element(VerifiedVersionsElement), meta.VerifiedVersions);

            var platforms = root.Element(PlatformsElement);
            if (platforms != null)
            {
                meta.WindowsEnabled = (bool?)platforms.Attribute(WindowsAttribute) ?? true;
                meta.AndroidEnabled = (bool?)platforms.Attribute(AndroidAttribute) ?? false;
            }

            var android = root.Element(AndroidElement);
            if (android != null)
            {
                meta.Android.BuildSource = (string)android.Attribute(AndroidBuildSourceAttribute) ?? "";
                meta.Android.Channel = (string)android.Attribute(AndroidChannelAttribute) ?? AndroidConfig.DefaultChannel;
                meta.Android.SignMasks = android.Elements(AndroidSignElement).Select(e => e.Value).ToList();
                meta.Android.ExcludeMasks = android.Elements(AndroidExcludeElement).Select(e => e.Value).ToList();
                // До флажка (2.1.0) подписью управляли маски: пусто — выпуск без подписи.
                meta.Android.Sign = (bool?)android.Attribute(AndroidSignAttribute) ?? meta.Android.SignMasks.Count > 0;
            }

            LoadVersionDates(root.Element(AndroidSignedElement), meta.AndroidSignedVersions);
            LoadVersionDates(root.Element(AndroidDeliveredElement), meta.AndroidDeliveredVersions);
            LoadVerification(root.Element(AndroidVerifiedElement), meta.AndroidVerifiedVersions);
            LoadAndroidRecords(root.Element(AndroidRecordsElement), meta.AndroidRecords);

            LoadVersionSet(root.Element(RemovedVersionsElement), meta.RemovedVersions);
            LoadVersionSet(root.Element(RemovedAndroidVersionsElement), meta.RemovedAndroidVersions);

            return meta;
        }

        // <X><Version id="1.0.0"/></X> → множество версий.
        private static void LoadVersionSet(XElement container, HashSet<string> target)
        {
            if (container == null)
                return;
            foreach (var item in container.Elements(DeliveredVersionElement))
            {
                var id = (string)item.Attribute(DeliveredVersionIdAttribute);
                if (!string.IsNullOrEmpty(id))
                    target.Add(id);
            }
        }

        private static XElement SaveVersionSet(string name, HashSet<string> source)
        {
            if (source.Count == 0)
                return null;
            return new XElement(name, source.OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Select(v => new XElement(DeliveredVersionElement, new XAttribute(DeliveredVersionIdAttribute, v))));
        }

        // <AndroidRecords><Version id signed><Mask/>…<File path sha256/>…</Version></AndroidRecords>
        private static void LoadAndroidRecords(XElement container, Dictionary<string, AndroidRecord> target)
        {
            if (container == null)
                return;

            foreach (var item in container.Elements(DeliveredVersionElement))
            {
                var id = (string)item.Attribute(DeliveredVersionIdAttribute);
                if (string.IsNullOrEmpty(id))
                    continue;

                var record = new AndroidRecord
                {
                    Signed = (bool?)item.Attribute(AndroidRecordSignedAttribute) ?? false,
                    Masks = item.Elements(AndroidRecordMaskElement).Select(e => e.Value).ToList()
                };
                foreach (var file in item.Elements(AndroidRecordFileElement))
                {
                    var path = (string)file.Attribute(AndroidRecordPathAttribute);
                    if (!string.IsNullOrEmpty(path))
                        record.Files[path] = (string)file.Attribute(AndroidRecordHashAttribute) ?? "";
                }
                target[id] = record;
            }
        }

        private static XElement SaveAndroidRecords(Dictionary<string, AndroidRecord> source)
        {
            if (source.Count == 0)
                return null;

            return new XElement(AndroidRecordsElement,
                source.Select(pair => new XElement(DeliveredVersionElement,
                    new XAttribute(DeliveredVersionIdAttribute, pair.Key),
                    new XAttribute(AndroidRecordSignedAttribute, pair.Value.Signed),
                    pair.Value.Masks.Select(m => new XElement(AndroidRecordMaskElement, m)),
                    pair.Value.Files.Select(f => new XElement(AndroidRecordFileElement,
                        new XAttribute(AndroidRecordPathAttribute, f.Key),
                        new XAttribute(AndroidRecordHashAttribute, f.Value))))));
        }

        // <X><Version id at ok details/></X> → словарь версия → итог проверки.
        private static void LoadVerification(XElement container, Dictionary<string, VerificationResult> target)
        {
            if (container == null)
                return;

            foreach (var item in container.Elements(DeliveredVersionElement))
            {
                var id = (string)item.Attribute(DeliveredVersionIdAttribute);
                DateTime parsed;
                if (!string.IsNullOrEmpty(id) && TryParseAt(item, out parsed))
                    target[id] = new VerificationResult(parsed,
                        (bool?)item.Attribute(VerifiedOkAttribute) ?? false,
                        (string)item.Attribute(VerifiedDetailsAttribute) ?? "");
            }
        }

        private static XElement SaveVerification(string name, Dictionary<string, VerificationResult> source)
        {
            if (source.Count == 0)
                return null;

            return new XElement(name,
                source.Select(pair => new XElement(DeliveredVersionElement,
                    new XAttribute(DeliveredVersionIdAttribute, pair.Key),
                    new XAttribute(DeliveredVersionAtAttribute, pair.Value.At.ToString("o", CultureInfo.InvariantCulture)),
                    new XAttribute(VerifiedOkAttribute, pair.Value.Succeeded),
                    new XAttribute(VerifiedDetailsAttribute, pair.Value.Details ?? ""))));
        }

        // <X><Version id="1.0.0" at="…"/></X> → словарь версия → время.
        private static void LoadVersionDates(XElement container, Dictionary<string, DateTime> target)
        {
            if (container == null)
                return;

            foreach (var item in container.Elements(DeliveredVersionElement))
            {
                var id = (string)item.Attribute(DeliveredVersionIdAttribute);
                DateTime parsed;
                if (!string.IsNullOrEmpty(id) && TryParseAt(item, out parsed))
                    target[id] = parsed;
            }
        }

        private static bool TryParseAt(XElement item, out DateTime value)
        {
            return DateTime.TryParse((string)item.Attribute(DeliveredVersionAtAttribute), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out value);
        }

        private static XElement SaveVersionDates(string name, Dictionary<string, DateTime> source)
        {
            if (source.Count == 0)
                return null;

            return new XElement(name,
                source.Select(pair => new XElement(DeliveredVersionElement,
                    new XAttribute(DeliveredVersionIdAttribute, pair.Key),
                    new XAttribute(DeliveredVersionAtAttribute, pair.Value.ToString("o", CultureInfo.InvariantCulture)))));
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
                    SaveVersionDates(DeliveredVersionsElement, DeliveredVersions),
                    SaveVersionDates(PatchedVersionsElement, PatchedVersions),
                    SaveVerification(VerifiedVersionsElement, VerifiedVersions),
                    new XElement(LastBuiltSettingsElement, LastBuiltSettings ?? ""),
                    new XElement(SignPatchElement, SignPatch),
                    new XElement(PlatformsElement,
                        new XAttribute(WindowsAttribute, WindowsEnabled),
                        new XAttribute(AndroidAttribute, AndroidEnabled)),
                    new XElement(AndroidElement,
                        new XAttribute(AndroidBuildSourceAttribute, Android.BuildSource ?? ""),
                        new XAttribute(AndroidChannelAttribute, Android.Channel ?? AndroidConfig.DefaultChannel),
                        new XAttribute(AndroidSignAttribute, Android.Sign),
                        Android.SignMasks.Select(m => new XElement(AndroidSignElement, m)),
                        Android.ExcludeMasks.Select(m => new XElement(AndroidExcludeElement, m))),
                    SaveVersionDates(AndroidSignedElement, AndroidSignedVersions),
                    SaveVersionDates(AndroidDeliveredElement, AndroidDeliveredVersions),
                    SaveVerification(AndroidVerifiedElement, AndroidVerifiedVersions),
                    SaveAndroidRecords(AndroidRecords),
                    SaveVersionSet(RemovedVersionsElement, RemovedVersions),
                    SaveVersionSet(RemovedAndroidVersionsElement, RemovedAndroidVersions)
                ));
            doc.Save(Path.Combine(projectRoot, FileName));
        }
    }
}
