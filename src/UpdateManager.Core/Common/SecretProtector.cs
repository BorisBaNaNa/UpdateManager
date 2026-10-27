using System;
using System.Security.Cryptography;
using System.Text;

namespace UpdateManager.Core.Common
{
    /// <summary>
    /// Шифрование секретов (паролей) через Windows DPAPI с привязкой к текущему пользователю.
    /// Расшифровать строку можно только под той же учёткой Windows на той же машине — поэтому
    /// зашифрованное значение безопасно хранить в файле в профиле пользователя.
    /// </summary>
    public static class SecretProtector
    {
        /// <summary>Зашифровать строку в base64. Пустой вход -> пустая строка.</summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                return "";

            var bytes = Encoding.UTF8.GetBytes(plain);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Расшифровать строку, зашифрованную через <see cref="Protect"/>.
        /// Возвращает null, если расшифровать не удалось (другая учётка/машина или повреждённые данные)
        /// — вызывающий код в этом случае запрашивает пароль заново.
        /// </summary>
        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return "";

            try
            {
                var encrypted = Convert.FromBase64String(stored);
                var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
