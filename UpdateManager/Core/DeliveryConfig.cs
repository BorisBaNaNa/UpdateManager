namespace UpdateManager.Core
{
    /// <summary>Сохранённая конфигурация доставки (метод + путь). Хранится в нашем файле проекта.</summary>
    public class DeliveryConfig
    {
        public string Method { get; set; } = DeliveryMethods.Folder;
        public string Path { get; set; } = "";
    }

    /// <summary>Доступные методы доставки. Пока только папка; FTP добавим позже.</summary>
    public static class DeliveryMethods
    {
        public const string Folder = "Folder";

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
                new Info(Folder, "Папка (локальный путь)")
            };
        }
    }
}
