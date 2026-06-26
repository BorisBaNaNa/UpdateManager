using System;
using System.Windows.Forms;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Project;
using UpdateManager.Core.Versioning;
using UpdateManager.Forms;
using UpdateManager.Forms.Theming;
using UpdateManager.Presenters;

namespace UpdateManager
{
    internal static class Program
    {
        /// <summary>
        /// Главная точка входа и композиционный корень: здесь связываем вью, презентер и сервисы.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ThemeManager.Init(); // загрузить сохранённый режим темы и поставить рендерер меню

            var view = new MainForm();

            // Презентер в конструкторе подписывается на события формы и дальше живёт,
            // пока жива форма (она держит его через эти подписки) — отдельная ссылка не нужна.
            new MainPresenter(view, new ProjectService(), new RecentProjectsStore(),
                new VersionDetector(), new FtpConnectionStore());

            Application.Run(view);
        }
    }
}
