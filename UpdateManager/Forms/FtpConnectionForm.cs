using System;
using System.Threading;
using System.Windows.Forms;
using UpdateManager.Core.Delivery;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Окно реквизитов FTP-подключения для доставки. По «Сохранить» возвращает FtpConnection
    /// в Result и DialogResult.OK. Реквизиты хранятся в профиле пользователя (пароль — DPAPI),
    /// а не в файле проекта.
    /// </summary>
    public partial class FtpConnectionForm : Form
    {
        public FtpConnection Result { get; private set; }

        public FtpConnectionForm(FtpConnection current)
        {
            InitializeComponent();
            Theming.ThemeManager.Register(this);

            txtHost.Text = current.Host;
            numPort.Value = Math.Max(numPort.Minimum, Math.Min(numPort.Maximum, current.Port));
            txtUser.Text = current.Username;
            txtPass.Text = current.Password;
            txtRemote.Text = current.RemotePath;

            btnBrowseRemote.Click += OnBrowseRemote;
            btnTest.Click += OnTest;
            btnOk.Click += OnOk;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        // Считать реквизиты из полей; null = не заполнен хост (с предупреждением).
        private FtpConnection ReadConnection()
        {
            var host = txtHost.Text.Trim();
            if (host.Length == 0)
            {
                MessageBox.Show(this, "Укажите хост FTP-сервера.", "FTP-подключение",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            return new FtpConnection
            {
                Host = host,
                Port = (int)numPort.Value,
                Username = txtUser.Text.Trim(),
                Password = txtPass.Text,
                RemotePath = txtRemote.Text.Trim()
            };
        }

        private void OnOk(object sender, EventArgs e)
        {
            var conn = ReadConnection();
            if (conn == null)
                return;

            Result = conn;
            DialogResult = DialogResult.OK;
            Close();
        }

        // Выбор папки на сервере через TreeView-браузер (по текущим реквизитам формы).
        private void OnBrowseRemote(object sender, EventArgs e)
        {
            var conn = ReadConnection();
            if (conn == null)
                return;

            using (var dialog = new FtpRemoteBrowserForm(conn))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtRemote.Text = dialog.SelectedPath;
            }
        }

        // Проверка соединения на фоновом потоке, чтобы не вешать UI на недоступном сервере.
        private void OnTest(object sender, EventArgs e)
        {
            var conn = ReadConnection();
            if (conn == null)
                return;

            SetBusy(true);
            var thread = new Thread(() =>
            {
                var result = FtpConnectionTester.Test(conn);
                BeginInvoke((Action)(() =>
                {
                    SetBusy(false);
                    MessageBox.Show(this, result.Message,
                        result.Success ? "Проверка соединения" : "Проверка соединения — ошибка",
                        MessageBoxButtons.OK,
                        result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }));
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void SetBusy(bool busy)
        {
            btnTest.Text = busy ? "Проверка…" : "Проверить соединение";
            btnTest.Enabled = !busy;
            btnOk.Enabled = !busy;
            UseWaitCursor = busy;
        }
    }
}
