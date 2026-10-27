using System;
using System.Collections.Generic;

namespace UpdateManager.Core.Common
{
    /// <summary>Текст исключения для пользователя — со всей цепочкой InnerException.</summary>
    public static class ExceptionText
    {
        /// <summary>
        /// Сообщения исключения и всех вложенных через " → ". Библиотеки (FluentFTP и т.п.) часто
        /// кладут настоящую причину во вложенное ("See InnerException for more info").
        /// </summary>
        public static string Full(Exception ex)
        {
            var parts = new List<string>();
            for (var e = ex; e != null; e = e.InnerException)
            {
                var message = (e.Message ?? "").Trim();
                if (message.Length > 0 && !parts.Contains(message))
                    parts.Add(message);
            }
            return string.Join(" → ", parts);
        }
    }
}
