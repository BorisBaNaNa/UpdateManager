using System;
using System.Collections.Generic;
using UpdateManager.Core;

namespace UpdateManager.Views
{
    /// <summary>
    /// Контракт главного окна. Презентер работает ТОЛЬКО через него
    /// и ничего не знает про WinForms — поэтому вью можно заменить (например, на веб).
    /// </summary>
    public interface IMainView
    {
        // События UI — презентер на них подписывается.
        event EventHandler CreateProjectRequested;
        event EventHandler OpenProjectRequested;
        event EventHandler<string> OpenRecentRequested; // выбран недавний проект (путь)
        event EventHandler BrowseBuildSourceRequested;  // нажата кнопка выбора папки билда
        event EventHandler CreatePatchRequested;        // нажата кнопка "Создать патч"
        event EventHandler OpenInExplorerRequested;     // "Открыть в проводнике"
        event EventHandler EditSettingsRequested;       // "Настройки"
        event EventHandler DeliverPatchRequested;       // "Доставить патч"

        // Команды презентера к вью — обновить отображение.
        void RenderProject(UpdateProject project);
        void RenderNoProject();
        void RenderRecentProjects(IReadOnlyList<string> projectPaths);
        void RenderBuildSource(string sourcePath, string mainExecutable, string version);

        // Диалоги: за UI отвечает вью, презентер только просит результат.
        string BrowseForFolder(string description);   // null = пользователь отменил
        string PromptProjectName(string defaultName); // null = пользователь отменил
        string ConfirmVersion(string detectedVersion); // подтверждение/ввод версии; null = отмена
        bool Confirm(string message);                  // да/нет
        void ShowError(string message);
        void ShowInfo(string message);
        void ShowPatchProgress(PatchBuilder builder);     // модальное окно сборки
        ProjectSettings EditSettings(ProjectSettings current); // окно настроек; null = отмена
        DeliveryConfig ConfigureDelivery(DeliveryConfig current); // окно доставки; null = отмена
        void OpenInExplorer(string path);                 // открыть папку в проводнике
    }
}
