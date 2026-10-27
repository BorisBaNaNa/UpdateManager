using System;
using FluentFTP;

namespace UpdateManager.Core.Delivery
{
    /// <summary>
    /// Заливка ОДНОГО файла приватного ключа в отдельную защищённую папку на FTP (для коллег).
    /// Намеренно НЕ переиспользует <see cref="FtpUploadOperation"/> с атомарной подменой каталога:
    /// та стратегия заменяет всю целевую папку содержимым Output и стёрла бы чужие файлы в общей
    /// папке ключей. Здесь — простая заливка одного файла поверх (Overwrite), папка не трогается.
    /// Файл маленький, поэтому операция синхронная (без окна прогресса).
    /// </summary>
    public class FtpKeyUploader
    {
        private readonly FtpConnection _conn;

        public FtpKeyUploader(FtpConnection conn)
        {
            _conn = conn;
        }

        /// <summary>
        /// Залить localFile в удалённую папку remoteDir под именем remoteFileName.
        /// Бросает при ошибке подключения/заливки.
        /// </summary>
        public void Upload(string localFile, string remoteDir, string remoteFileName)
        {
            var dir = (remoteDir ?? "").Trim().TrimEnd('/');
            if (dir.Length == 0)
                dir = "/";
            var remotePath = dir.TrimEnd('/') + "/" + remoteFileName;

            using (var client = new FtpClient(_conn.Host, _conn.Username, _conn.Password, _conn.Port))
            {
                client.Config.UploadDataType = FtpDataType.Binary;
                client.Connect();

                // createRemoteDir=true — создаст папку ключей, если её ещё нет; чужие файлы не трогаем.
                var status = client.UploadFile(localFile, remotePath, FtpRemoteExists.Overwrite,
                    createRemoteDir: true);

                if (status == FtpStatus.Failed)
                    throw new Exception("Не удалось залить приватный ключ в " + remotePath + ".");
            }
        }
    }
}
