using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Security;
using UpdateManager.Core.Versioning;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Донастройка проекта, созданного прежней версией UpdateManager (проверка при открытии). Идемпотентна:
    /// на актуальном проекте ничего не делает и возвращает пустой список.
    /// Схема 2 (2.2): версии по платформам — Versions/&lt;версия&gt; → Versions/Windows/&lt;версия&gt;; выкладка
    /// последней Android-версии сохраняется в Versions/Android/&lt;версия&gt;, если билд с записи не менялся.
    /// </summary>
    public static class ProjectUpgrade
    {
        /// <summary>Довести проект до текущей схемы; что сделано (и что не удалось) — по строке.</summary>
        public static List<string> Run(string projectRoot)
        {
            var notes = new List<string>();
            var meta = ProjectMeta.Load(projectRoot);

            MoveWindowsVersions(projectRoot, notes);
            Directory.CreateDirectory(ProjectService.WindowsVersionsPath(projectRoot));
            Directory.CreateDirectory(ProjectService.AndroidVersionsPath(projectRoot));
            if (meta.SchemaVersion < 2)
                SaveLatestAndroid(projectRoot, meta, notes);

            if (meta.SchemaVersion < ProjectMeta.CurrentSchemaVersion)
            {
                meta = ProjectMeta.Load(projectRoot); // SaveLatestAndroid мог дописать запись
                meta.SchemaVersion = ProjectMeta.CurrentSchemaVersion;
                meta.Save(projectRoot);
            }
            return notes;
        }

        // Versions/<версия> → Versions/Windows/<версия>: движок теперь смотрит в Versions/Windows.
        private static void MoveWindowsVersions(string projectRoot, List<string> notes)
        {
            var versions = Path.Combine(projectRoot, ProjectService.VersionsFolder);
            if (!Directory.Exists(versions))
                return;

            var moved = new List<string>();
            foreach (var dir in Directory.GetDirectories(versions))
            {
                var name = Path.GetFileName(dir);
                if (string.Equals(name, ProjectService.WindowsPlatformFolder, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ProjectService.AndroidPlatformFolder, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!VersionRules.IsValid(name))
                {
                    notes.Add("В " + ProjectService.VersionsFolder + "/ лишняя папка «" + name + "» — не версия, оставлена как есть. " +
                        "Движок считает версиями все подпапки; уберите её оттуда.");
                    continue;
                }
                var target = ProjectService.WindowsVersionPath(projectRoot, name);
                if (Directory.Exists(target))
                {
                    notes.Add("Версия " + name + " есть и в " + ProjectService.VersionsFolder + "/, и в " +
                        ProjectService.VersionsFolder + "/" + ProjectService.WindowsPlatformFolder + "/ — старая копия оставлена, разберите вручную.");
                    continue;
                }
                Directory.CreateDirectory(ProjectService.WindowsVersionsPath(projectRoot));
                Directory.Move(dir, target);
                moved.Add(name);
            }
            if (moved.Count > 0)
                notes.Add("Версии Windows перенесены в " + ProjectService.VersionsFolder + "/" + ProjectService.WindowsPlatformFolder +
                    "/: " + string.Join(", ", moved) + ".");
        }

        // До 2.2 выкладка Android жила только в папке билда. Последнюю версию сохраняем в проект, если билд
        // с записи не менялся (по отпечаткам записи или, у записей 2.1.0, по времени файлов); старшие — уже никак.
        private static void SaveLatestAndroid(string projectRoot, ProjectMeta meta, List<string> notes)
        {
            var versions = meta.AndroidSignedVersions.Keys.Where(VersionRules.IsValid)
                .OrderBy(v => new SimplePatchToolCore.VersionCode(v)).ToList();
            if (versions.Count == 0)
                return;

            var latest = versions[versions.Count - 1];
            var older = versions.Take(versions.Count - 1)
                .Where(v => !Directory.Exists(ProjectService.AndroidVersionPath(projectRoot, v))).ToList();
            if (older.Count > 0)
                notes.Add("Android " + string.Join(", ", older) + ": выкладки этих версий не сохранялись (до 2.2) — откатиться на них нельзя.");

            var target = ProjectService.AndroidVersionPath(projectRoot, latest);
            if (Directory.Exists(target))
                return;

            var folder = meta.Android.BuildSource ?? "";
            if (folder.Length == 0 || !Directory.Exists(folder))
            {
                notes.Add("Android " + latest + ": папки билда нет — выкладка не сохранена. Запишите версию заново, чтобы её можно было доставить и откатить.");
                return;
            }

            AndroidRecord record;
            var hasRecord = meta.AndroidRecords.TryGetValue(latest, out record);
            var masks = hasRecord ? record.Masks : meta.Android.ActiveSignMasks;
            var files = AndroidPackage.Files(folder, masks, meta.Android.ExcludeMasks);
            var hashes = AndroidPackage.Hashes(folder, files);

            bool unchanged;
            if (hasRecord && record.Files.Count > 0)
                unchanged = AndroidPackage.Changes(record.Files, hashes).Count == 0;
            else
            {
                var signedAt = meta.AndroidSignedVersions[latest];
                unchanged = files.Where(f => !FileSigner.IsSignature(f))
                    .All(f => File.GetLastWriteTime(Path.Combine(folder, f.Replace('/', Path.DirectorySeparatorChar))) <= signedAt);
            }
            if (!unchanged)
            {
                notes.Add("Android " + latest + ": билд изменился после записи — выкладка не сохранена. Запишите (подпишите) версию заново.");
                return;
            }

            AndroidPackage.CopyTo(folder, files, target);
            if (!hasRecord)
                meta.AndroidRecords[latest] = new AndroidRecord { Signed = masks.Count > 0, Masks = new List<string>(masks), Files = hashes };
            else if (record.Files.Count == 0)
                record.Files = hashes;
            meta.Save(projectRoot);
            notes.Add("Выкладка Android " + latest + " сохранена в " + ProjectService.VersionsFolder + "/" +
                ProjectService.AndroidPlatformFolder + "/" + latest + "/ — дальше доставка берёт её оттуда.");
        }
    }
}
