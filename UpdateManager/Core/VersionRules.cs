using SimplePatchToolCore;

namespace UpdateManager.Core
{
    /// <summary>
    /// Правила версий. Используем движковый VersionCode, чтобы наша проверка
    /// 1-в-1 совпадала с тем, что реально примет движок.
    /// </summary>
    public static class VersionRules
    {
        /// <summary>
        /// Допустима ли версия. Движок принимает только числа через точку
        /// (1, 1.0, 1.0.0.0); буквы и суффиксы (1.2-b1, v1.0, 1.0.0a) недопустимы.
        /// </summary>
        public static bool IsValid(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            return new VersionCode(version).IsValid;
        }
    }
}
