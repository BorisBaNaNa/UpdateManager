using System;
using UpdateManager.Core.Operations;

namespace UpdateManager.Views
{
    /// <summary>
    /// Контракт вью для самообновления. Реализуется главным окном, но отделён от IMainView,
    /// чтобы SelfUpdatePresenter не зависел от логики проектов.
    /// </summary>
    public interface ISelfUpdateView
    {
        event EventHandler ViewShown;                 // окно впервые показано — время фоновой проверки
        event EventHandler CheckSelfUpdateRequested;  // «Справка → Проверить обновления…»

        /// <summary>Открыт модальный диалог/операция — всплывающие предложения сейчас неуместны.</summary>
        bool IsBusy { get; }

        void SetSelfUpdateAvailable(bool available);  // доступность пункта меню

        bool Confirm(string message);
        void ShowError(string message);
        void ShowInfo(string message);
        void ShowOperation(IEngineOperation operation);
        void CloseApplication();
    }
}
