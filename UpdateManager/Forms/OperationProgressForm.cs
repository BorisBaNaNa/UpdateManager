using System;
using System.Windows.Forms;
using UpdateManager.Core.Operations;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Модальное окно фоновой операции движка (сборка/проверка). Поллит IEngineOperation
    /// таймером в UI-потоке. Пока процента нет — бар Marquee; как только пришёл — заполняется.
    /// </summary>
    public partial class OperationProgressForm : Form
    {
        private readonly IEngineOperation _operation;

        // Пользователь запросил прерывание (через кнопку/крестик); ждём остановки операции,
        // затем окно закрывается само.
        private bool _cancelRequested;

        public OperationProgressForm(IEngineOperation operation)
        {
            InitializeComponent();
            Theming.ThemeManager.Register(this);
            _operation = operation;
            Text = operation.Title;
            btnClose.Click += (s, e) => Close();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!_operation.Start())
            {
                AppendLog("Не удалось запустить операцию (возможно, уже идёт).");
                Finish(success: false);
                return;
            }

            AppendLog(_operation.Title + " — запущено…");
            // Пока операция идёт, кнопка прерывает её; по завершении станет «Закрыть».
            btnClose.Text = "Прервать";
            btnClose.Enabled = true;
            pollTimer.Start();
        }

        private void pollTimer_Tick(object sender, EventArgs e)
        {
            // Дренажим весь накопленный лог.
            string log = _operation.FetchLog();
            while (log != null)
            {
                AppendLog(log);
                log = _operation.FetchLog();
            }

            // Прогресс: как только пришёл процент — уходим с Marquee на определённый бар.
            var percentage = _operation.FetchProgressPercentage();
            if (percentage.HasValue)
                SetProgress(percentage.Value);

            if (!_operation.IsRunning)
            {
                pollTimer.Stop();
                Finish(_operation.Succeeded);
            }
        }

        private void SetProgress(int percentage)
        {
            if (progressBar.Style == ProgressBarStyle.Marquee)
                progressBar.Style = ProgressBarStyle.Continuous;

            progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, percentage));
        }

        private void Finish(bool success)
        {
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = success ? progressBar.Maximum : progressBar.Minimum;

            // Прерывание было запрошено — окно закрывается само (таймер уже остановлен).
            if (_cancelRequested)
            {
                AppendLog("Операция прервана.");
                Close();
                return;
            }

            AppendLog(success ? "Готово: успешно." : "Операция завершилась с ошибкой.");

            var details = _operation.ResultDetails;
            if (!success && !string.IsNullOrEmpty(details))
                AppendLog(details);

            btnClose.Text = "Закрыть";
            btnClose.Enabled = true;
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
        }

        // Пока операция идёт (таймер активен) — закрытие не закрывает окно сразу, а прерывает
        // операцию; окно закроется само, когда операция остановится (см. Finish).
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (pollTimer.Enabled)
            {
                e.Cancel = true;
                if (!_cancelRequested)
                {
                    _cancelRequested = true;
                    AppendLog("Прерывание операции…");
                    btnClose.Enabled = false; // защита от повторных нажатий, пока операция тормозит
                    _operation.Cancel();
                }
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
