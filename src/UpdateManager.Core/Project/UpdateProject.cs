using System.Collections.Generic;

namespace UpdateManager.Core.Project
{
    /// <summary>
    /// Открытый проект обновления в памяти: путь к корню, имя (из Settings.xml движка),
    /// наша мета (из updatemanager.project.xml) и список версий (из папки Versions/).
    /// </summary>
    public class UpdateProject
    {
        /// <summary>Корневая папка проекта (где лежат Settings.xml, Versions/, Output/ …).</summary>
        public string RootPath { get; set; }

        /// <summary>Имя проекта — читается из Settings.xml движка (элемент Name).</summary>
        public string Name { get; set; }

        /// <summary>Наша мета-информация (updatemanager.project.xml).</summary>
        public ProjectMeta Meta { get; set; } = new ProjectMeta();

        /// <summary>Версии проекта (подпапки Versions/).</summary>
        public List<ProjectVersion> Versions { get; set; } = new List<ProjectVersion>();

        /// <summary>Есть ли в Output/ собранный патч, готовый к доставке.</summary>
        public bool OutputReady { get; set; }
    }
}
