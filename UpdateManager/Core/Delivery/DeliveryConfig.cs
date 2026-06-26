namespace UpdateManager.Core.Delivery
{
    /// <summary>Сохранённая конфигурация доставки (метод + путь). Хранится в нашем файле проекта.</summary>
    public class DeliveryConfig
    {
        public string Method { get; set; } = DeliveryMethods.Folder;
        public string Path { get; set; } = "";
    }

    /// <summary>Доступные методы доставки: локальная папка и FTP.</summary>
    public static class DeliveryMethods
    {
        public const string Folder = "Folder";
        public const string Ftp = "Ftp";

        /// <summary>Описание метода для выпадающего списка (ToString = отображаемое имя).</summary>
        public sealed class Info
        {
            public string Id { get; }
            public string Name { get; }

            public Info(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public static Info[] Available()
        {
            return new[]
            {
                new Info(Folder, "Папка (локальный путь)"),
                new Info(Ftp, "FTP (сервер)")
            };
        }
    }
}
