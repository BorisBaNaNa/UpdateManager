using System;
using System.IO;
using SimplePatchToolCore;
using UpdateManager.Core.Operations;

namespace UpdateManager.Core.SelfUpdate
{
    /// <summary>
    /// Скачивание обновления самого UpdateManager в self-patching-режиме: запущенный exe занят,
    /// поэтому движок кладёт файлы в кэш, а подменяет их self-patcher после выхода приложения
    /// (см. <see cref="ApplyAndRestart"/>). Окно прогресса — общее, как у сборки/проверки.
    /// </summary>
    public class SelfUpdateOperation : IEngineOperation
    {
        private const string SelfPatcherExe = "SelfPatcher.exe";

        // Встроенный self-patcher, который кладётся в bin для новых проектов (Content → SelfPatcher\).
        private const string BundledSelfPatcherDirectory = "SelfPatcher";

        private readonly SimplePatchTool _patcher;

        public SelfUpdateOperation(SelfUpdateConfig config)
        {
            _patcher = SelfUpdateClient.Create(config);
        }

        public string Title { get { return "Обновление UpdateManager"; } }

        public bool Start()
        {
            return _patcher.Run(true); // true = self-patching: качаем в кэш, не поверх запущенного
        }

        public void Cancel()
        {
            _patcher.Cancel();
        }

        public string FetchLog()
        {
            return _patcher.FetchLog();
        }

        public int? FetchProgressPercentage()
        {
            int? percentage = null;
            var progress = _patcher.FetchOverallProgress();
            while (progress != null)
            {
                percentage = progress.Percentage;
                progress = _patcher.FetchOverallProgress();
            }
            return percentage;
        }

        public bool IsRunning
        {
            get { return _patcher.IsRunning; }
        }

        public bool Succeeded
        {
            get { return _patcher.Result == PatchResult.Success; }
        }

        /// <summary>Нечего обновлять — версия уже последняя (применять нечего).</summary>
        public bool AlreadyUpToDate
        {
            get { return _patcher.Result == PatchResult.AlreadyUpToDate; }
        }

        public string ResultDetails
        {
            get { return _patcher.Result == PatchResult.Failed ? _patcher.FailDetails : ""; }
        }

        /// <summary>
        /// Запустить self-patcher, который после выхода приложения подменит файлы и запустит
        /// UpdateManager заново. Вызывать после успешного скачивания; затем приложение ДОЛЖНО выйти.
        /// Бросает InvalidOperationException, если self-patcher не найден или не запустился.
        /// </summary>
        public void ApplyAndRestart()
        {
            var selfPatcher = FindSelfPatcher();
            if (selfPatcher == null)
                throw new InvalidOperationException(
                    "Не найден " + SelfPatcherExe + " (ни в " + PatchParameters.SELF_PATCHER_DIRECTORY +
                    "\\, ни в " + BundledSelfPatcherDirectory + "\\ рядом с программой).\n" +
                    "Переустановите UpdateManager вручную.");

            if (!_patcher.ApplySelfPatch(selfPatcher, SelfIdentity.ExecutablePath))
                throw new InvalidOperationException("Не удалось запустить self-patcher:\n" + _patcher.FailDetails);
        }

        private static string FindSelfPatcher()
        {
            // Сначала — куда его раскладывает движок из self-patching-патча, затем — наш встроенный.
            foreach (var directory in new[] { PatchParameters.SELF_PATCHER_DIRECTORY, BundledSelfPatcherDirectory })
            {
                var path = Path.Combine(SelfIdentity.AppDirectory, directory, SelfPatcherExe);
                if (File.Exists(path))
                    return path;
            }
            return null;
        }
    }
}
