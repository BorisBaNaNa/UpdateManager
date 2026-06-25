using System;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Одна версия проекта = подпапка внутри Versions/ (например "1.0.0").
    /// </summary>
    public class ProjectVersion
    {
        /// <summary>Имя папки версии, оно же строка версии.</summary>
        public string Version { get; set; }

        /// <summary>Время последнего изменения папки версии (приблизительно — когда собран билд).</summary>
        public DateTime BuildDate { get; set; }

        /// <summary>Доставлен ли патч этой версии на сервер. Пока всегда false — добавим позже.</summary>
        public bool Delivered { get; set; }
    }
}
