using System;

namespace UpdateManager.Core.Delivery
{
    /// <summary>Создаёт обработчик доставки по сохранённой конфигурации (метод -> реализация).</summary>
    public static class DeliveryFactory
    {
        public static IDeliveryTarget Create(DeliveryConfig config)
        {
            switch (config.Method)
            {
                case DeliveryMethods.Folder:
                    return new FolderDeliveryTarget(config.Path);
                default:
                    throw new NotSupportedException("Неизвестный метод доставки: " + config.Method);
            }
        }
    }
}
