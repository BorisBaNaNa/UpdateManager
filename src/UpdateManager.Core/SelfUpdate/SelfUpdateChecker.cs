using System.Threading;
using SimplePatchToolCore;
using SimplePatchToolSecurity;

namespace UpdateManager.Core.SelfUpdate
{
    public enum SelfUpdateStatus
    {
        UpdateAvailable,
        UpToDate,
        Failed
    }

    public class SelfUpdateCheckResult
    {
        public SelfUpdateStatus Status { get; set; }

        /// <summary>Версия на сервере (если движок её сообщил), иначе "".</summary>
        public string NewVersion { get; set; }

        /// <summary>Причина ошибки при Failed, иначе "".</summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Клиентский движок, настроенный на обновление самого UpdateManager. Корень — папка
    /// запущенного приложения; при заданном публичном ключе манифесты проверяются по RSA.
    /// </summary>
    public static class SelfUpdateClient
    {
        public static SimplePatchTool Create(SelfUpdateConfig config)
        {
            var patcher = new SimplePatchTool(SelfIdentity.AppDirectory, config.VersionInfoUrl)
                .UseRepairPatch(true)
                .UseIncrementalPatch(true)
                .UseInstallerPatch(true)
                .CheckForMultipleRunningInstances(false) // запущены мы сами — это ожидаемо
                .SilentMode(false)
                .LogProgress(true)
                .LogToFile(false);

            if (config.HasPublicKey)
            {
                var publicKey = config.PublicKey;
                patcher.UseVersionInfoVerifier((ref string xml) => XMLSigner.VerifyXMLContents(xml, publicKey))
                    .UsePatchInfoVerifier((ref string xml) => XMLSigner.VerifyXMLContents(xml, publicKey));
            }

            return patcher;
        }
    }

    /// <summary>
    /// Проверка наличия обновления. Блокирующая — вызывать из фонового потока.
    /// CheckForUpdates(false) сверяет файлы, а не только version holder: работает и на установке,
    /// где *_vers.sptv ещё нет.
    /// </summary>
    public class SelfUpdateChecker
    {
        private readonly SelfUpdateConfig _config;

        public SelfUpdateChecker(SelfUpdateConfig config)
        {
            _config = config;
        }

        public SelfUpdateCheckResult Check()
        {
            var patcher = SelfUpdateClient.Create(_config);
            if (!patcher.CheckForUpdates(false))
                return Fail("Не удалось запустить проверку обновлений.");

            while (patcher.IsRunning)
            {
                while (patcher.FetchLog() != null) { } // очередь логов не копим
                Thread.Sleep(100);
            }

            switch (patcher.Result)
            {
                case PatchResult.AlreadyUpToDate:
                    return new SelfUpdateCheckResult { Status = SelfUpdateStatus.UpToDate, NewVersion = "", Error = "" };
                case PatchResult.Success:
                    return new SelfUpdateCheckResult
                    {
                        Status = SelfUpdateStatus.UpdateAvailable,
                        NewVersion = patcher.NewVersion ?? "",
                        Error = ""
                    };
                default:
                    return Fail(patcher.FailDetails);
            }
        }

        private static SelfUpdateCheckResult Fail(string error)
        {
            return new SelfUpdateCheckResult { Status = SelfUpdateStatus.Failed, NewVersion = "", Error = error ?? "" };
        }
    }
}
