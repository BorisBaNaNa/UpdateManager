using System.Collections.Generic;
using SimplePatchToolCore;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Основные настройки проекта для редактирования в UI — наш DTO поверх движкового ProjectInfo.
    /// Показываем основные поля + раздел «Дополнительно» (компрессия, BinaryDiffQuality);
    /// остальные продвинутые поля движка при сохранении сохраняются как есть.
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

        // --- Дополнительно (продвинутые поля движка) ---

        /// <summary>Формат сжатия repair-патча (LZMA/GZIP/NONE).</summary>
        public CompressionFormat CompressionFormatRepairPatch { get; set; } = CompressionFormat.LZMA;

        /// <summary>Формат сжатия installer-патча (LZMA/GZIP/NONE).</summary>
        public CompressionFormat CompressionFormatInstallerPatch { get; set; } = CompressionFormat.LZMA;

        /// <summary>Формат сжатия инкрементального патча (LZMA/GZIP/NONE).</summary>
        public CompressionFormat CompressionFormatIncrementalPatch { get; set; } = CompressionFormat.LZMA;

        /// <summary>Качество бинарного диффа инкрементальных патчей (по умолчанию 3).</summary>
        public int BinaryDiffQuality { get; set; } = 3;

        /// <summary>Не создавать патч-файлы для неизменённых файлов.</summary>
        public bool DontCreatePatchFilesForUnchangedFiles { get; set; }
    }
}
