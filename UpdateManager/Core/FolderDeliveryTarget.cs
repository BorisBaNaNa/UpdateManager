using System;
using System.IO;

namespace UpdateManager.Core
{
    /// <summary>Доставка в локальную папку: копирует Output/ в папку назначения.</summary>
    public class FolderDeliveryTarget : IDeliveryTarget
    {
        private readonly string _destination;

        public FolderDeliveryTarget(string destination)
        {
            _destination = destination;
        }

        public void Deliver(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(_destination))
                throw new InvalidOperationException("Не указан путь доставки.");

            Directory.CreateDirectory(_destination);
            FileUtils.CopyDirectory(outputDir, _destination);
        }
    }
}
