using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UpdateManager.Core.Common;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Доставка в локальную папку: папка назначения становится копией источника. Старое удаляется,
    /// кроме записей из keepNames (папка канала Android — она выкладывается отдельно).
    /// </summary>
    public class FolderDeliveryTarget : IDeliveryTarget
    {
        private readonly string _destination;

        public FolderDeliveryTarget(string destination)
        {
            _destination = destination;
        }

        public void Deliver(string sourceDir, ICollection<string> keepNames)
        {
            if (string.IsNullOrWhiteSpace(_destination))
                throw new InvalidOperationException("Не указан путь доставки.");

            // Зеркальная доставка удаляет всё лишнее — корень диска под это не подставляем.
            var full = Path.GetFullPath(_destination);
            if (string.Equals(full.TrimEnd('\\', '/'), Path.GetPathRoot(full)?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Путь доставки — корень диска (" + full + "). " +
                    "Доставка удаляет из папки назначения всё лишнее — укажите отдельную папку.");
            if (FileUtils.PathsOverlap(full, sourceDir))
                throw new InvalidOperationException("Путь доставки совпадает с папкой источника или вложен в неё: " + full);

            Directory.CreateDirectory(full);
            var keep = new HashSet<string>(keepNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Directory.GetFileSystemEntries(full).Where(e => !keep.Contains(Path.GetFileName(e))))
            {
                if (Directory.Exists(entry))
                    Directory.Delete(entry, recursive: true);
                else
                    File.Delete(entry);
            }

            FileUtils.CopyDirectory(sourceDir, full);
        }
    }
}
