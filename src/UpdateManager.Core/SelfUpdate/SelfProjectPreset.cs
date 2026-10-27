using UpdateManager.Core.Project;

namespace UpdateManager.Core.SelfUpdate
{
    /// <summary>
    /// Настройки проекта обновлений, нужные, чтобы UpdateManager мог обновлять сам себя.
    /// Применяется только с согласия пользователя (см. MainPresenter), тихо ничего не меняет.
    /// </summary>
    public static class SelfProjectPreset
    {
        // Мусор сборки и логи движка: на клиенте их нет/они свои — в манифест не нужны.
        private static readonly string[] IgnoredPaths = { "*.pdb", "*.vshost.*", "*spt_logs.txt" };

        /// <summary>Пресет уже применён (без self-patching-режима самообновление ломается).</summary>
        public static bool IsApplied(ProjectSettings settings)
        {
            return settings.IsSelfPatchingApp;
        }

        public static void Apply(ProjectSettings settings, SelfUpdateConfig config)
        {
            // Клиент обновляет запущенного себя: файлы качаются в кэш и подменяются self-patcher'ом.
            settings.IsSelfPatchingApp = true;

            if (config.BaseDownloadUrl.Length > 0)
                settings.BaseDownloadURL = config.BaseDownloadUrl;

            foreach (var path in IgnoredPaths)
            {
                if (!settings.IgnoredPaths.Contains(path))
                    settings.IgnoredPaths.Add(path);
            }
        }
    }
}
