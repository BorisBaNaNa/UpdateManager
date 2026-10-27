namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Способ доставки готового патча. Реализации: папка (сейчас), FTP (позже).
    /// Презентер выбирает реализацию через DeliveryFactory по сохранённому DeliveryConfig.
    /// </summary>
    public interface IDeliveryTarget
    {
        /// <summary>Доставить содержимое outputDir (папка Output проекта) к месту назначения.</summary>
        void Deliver(string outputDir);
    }
}
