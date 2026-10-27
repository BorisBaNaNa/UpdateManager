using System;

namespace UpdateManager.Core.Project
{
    /// <summary>Итог проверки опубликованного патча («Проверить»): когда, прошла ли и почему нет.</summary>
    public class VerificationResult
    {
        public VerificationResult(DateTime at, bool succeeded, string details)
        {
            At = at;
            Succeeded = succeeded;
            Details = details ?? "";
        }

        public DateTime At { get; }

        public bool Succeeded { get; }

        /// <summary>Причина провала (пусто при успехе).</summary>
        public string Details { get; }
    }
}
