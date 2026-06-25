using System;
using System.Windows.Forms;
using UpdateManager.Core;
using UpdateManager.Forms;
using UpdateManager.Presenters;

namespace UpdateManager
{
    internal static class Program
    {
        /// <summary>
        /// Главная точка входа и композиционный корень: здесь связываем вью, презентер и сервисы.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var view = new MainForm();

            // Презентер в конструкторе подписывается на события формы и дальше живёт,
            // пока жива форма (она держит его через эти подписки) — отдельная ссылка не нужна.
            new MainPresenter(view, new ProjectService(), new RecentProjectsStore(), new VersionDetector());

            Application.Run(view);
        }
    }
}
