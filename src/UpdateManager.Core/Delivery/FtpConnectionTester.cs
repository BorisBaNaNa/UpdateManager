using FluentFTP;
using System;

namespace UpdateManager.Core.Delivery
{
    /// <summary>Результат проверки FTP-соединения.</summary>
    public class FtpTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Проверка FTP-соединения по реквизитам: коннект + (если задана) проверка папки назначения.
    /// Не заливает ничего. Таймаут короткий, чтобы кнопка не висела долго на недоступном сервере.
    /// </summary>
    public static class FtpConnectionTester
    {
        private const int TimeoutMs = 8000;

        public static FtpTestResult Test(FtpConnection conn)
        {
            try
            {
                using (var client = new FtpClient(conn.Host, conn.Username, conn.Password, conn.Port))
                {
                    client.Config.ConnectTimeout = TimeoutMs;
                    client.Config.DataConnectionConnectTimeout = TimeoutMs;
                    client.Connect();

                    var message = "Подключение успешно. Рабочая папка: " + client.GetWorkingDirectory();

                    if (!string.IsNullOrWhiteSpace(conn.RemotePath))
                    {
                        bool exists = client.DirectoryExists(conn.RemotePath);
                        message += exists
                            ? "\nПапка \"" + conn.RemotePath + "\" существует."
                            : "\nВнимание: папка \"" + conn.RemotePath + "\" не найдена (будет создана при заливке).";
                    }

                    client.Disconnect();
                    return new FtpTestResult { Success = true, Message = message };
                }
            }
            catch (Exception ex)
            {
                return new FtpTestResult { Success = false, Message = ex.Message };
            }
        }
    }
}
