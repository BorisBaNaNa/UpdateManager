using System.Collections.Generic;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Способ доставки готового патча. Реализации: папка (<see cref="FolderDeliveryTarget"/>);
    /// FTP — фоновая операция <see cref="FtpUploadOperation"/>.
    /// Презентер выбирает реализацию через DeliveryFactory по сохранённому DeliveryConfig.
    /// </summary>
    public interface IDeliveryTarget
    {
        /// <summary>
        /// Доставить содержимое sourceDir к месту назначения зеркально: всё, чего в sourceDir нет, удаляется —
        /// кроме записей верхнего уровня из keepNames (например, папки канала Android внутри папки патча).
        /// </summary>
        void Deliver(string sourceDir, ICollection<string> keepNames);
    }
}
