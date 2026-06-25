using System;
using System.Windows.Forms;
using UpdateManager.Core;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Модальное окно сборки патча: запускает PatchBuilder и поллит его таймером.
    /// Движок работает в своём потоке — мы только тянем лог и проверяем статус,
    /// поэтому Invoke не нужен (таймер тикает в UI-потоке).
    /// </summary>
    public partial class PatchProgressForm : Form
    {
        private readonly PatchBuilder _builder;

        public PatchProgressForm(PatchBuilder builder)
        {
            InitializeComponent();
            _builder = builder;
            btnClose.Click += (s, e) => Close();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!_builder.Start())
            {
                AppendLog("Не удалось запустить сборку (возможно, уже идёт).");
                Finish(success: false);
                return;
            }

            AppendLog("Сборка патча запущена…");
            pollTimer.Start();
        }

        private void pollTimer_Tick(object sender, EventArgs e)
        {
            // Дренажим весь накопленный лог.
            string log = _builder.FetchLog();
            while (log != null)
            {
                AppendLog(log);
                log = _builder.FetchLog();
            }

            if (!_builder.IsRunning)
            {
                pollTimer.Stop();
                Finish(success: !_builder.Failed);
            }
        }

        private void Finish(bool success)
        {
            // Движок не отдаёт процент на сборке, поэтому до завершения бар "крутится" (Marquee),
            // а по итогу показываем определённый результат: полный — успех, пустой — ошибка.
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = success ? progressBar.Maximum : progressBar.Minimum;
            AppendLog(success ? "Готово: патч собран." : "Сборка завершилась с ошибкой.");
            btnClose.Enabled = true;
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
        }

        // Пока сборка идёт (таймер активен) — не даём закрыть окно.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (pollTimer.Enabled)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
