using System.Collections.Generic;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Основные настройки проекта для редактирования в UI — наш DTO поверх движкового ProjectInfo.
    /// Показываем то, что реально настраивают руками; продвинутые поля (компрессия, BinaryDiffQuality
    /// и т.п.) при сохранении сохраняются как есть и здесь не отражены.
    /// </summary>
    public class ProjectSettings
    {
        public string Name { get; set; } = "";

        /// <summary>Базовый URL, по которому клиент качает файлы патча. Главный источник ошибок обновления.</summary>
        public string BaseDownloadURL { get; set; } = "";

        /// <summary>URL проверки техобслуживания (опционально).</summary>
        public string MaintenanceCheckURL { get; set; } = "";

        /// <summary>Приложение само себя патчит (нужен self-patcher).</summary>
        public bool IsSelfPatchingApp { get; set; }

        public bool CreateRepairPatch { get; set; }
        public bool CreateInstallerPatch { get; set; }
        public bool CreateIncrementalPatch { get; set; }

        /// <summary>Делать инкрементальные патчи от каждой предыдущей версии к новой, а не только от ближайшей.</summary>
        public bool CreateAllIncrementalPatches { get; set; }

        /// <summary>Игнорируемые пути/маски (по одному на элемент).</summary>
        public List<string> IgnoredPaths { get; set; } = new List<string>();
    }
}
