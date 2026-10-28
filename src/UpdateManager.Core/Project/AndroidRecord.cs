using System;
using System.Collections.Generic;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Что записано на этапе подписи Android-версии: подписана ли она и какими масками (настройки потом
    /// могут поменяться — каталог показывает версию такой, какой её записали) и отпечатки файлов выкладки
    /// (SHA-256) — файл, изменённый после записи, выкладывать нельзя, даже без подписи.
    /// </summary>
    public class AndroidRecord
    {
        public bool Signed { get; set; }

        public List<string> Masks { get; set; } = new List<string>();

        /// <summary>Файл выкладки (относительный путь с «/», без .sig) → SHA-256 в hex.</summary>
        public Dictionary<string, string> Files { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
