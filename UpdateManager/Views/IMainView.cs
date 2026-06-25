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

        // Команды презентера к вью — обновить отображение.
        void RenderProject(UpdateProject project);
        void RenderNoProject();
        void RenderRecentProjects(IReadOnlyList<string> projectPaths);

        // Диалоги: за UI отвечает вью, презентер только просит результат.
        string BrowseForFolder(string description);   // null = пользователь отменил
        string PromptProjectName(string defaultName); // null = пользователь отменил
        void ShowError(string message);
    }
}
