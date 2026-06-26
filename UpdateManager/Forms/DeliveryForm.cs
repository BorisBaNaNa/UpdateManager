using System;
using System.Linq;
using System.Windows.Forms;
using UpdateManager.Core.Delivery;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Окно доставки: выбор метода (запоминается последний) и пути.
    /// По «Доставить» возвращает DeliveryConfig в Result и DialogResult.OK.
    /// </summary>
    public partial class DeliveryForm : Form
    {
        public DeliveryConfig Result { get; private set; }

        public DeliveryForm(DeliveryConfig current)
        {
            InitializeComponent();

            var methods = DeliveryMethods.Available();
            cmbMethod.Items.AddRange(methods);

            // Преселект сохранённого метода (или первый доступный).
            cmbMethod.SelectedItem = methods.FirstOrDefault(m => m.Id == current.Method) ?? methods[0];
            txtPath.Text = current.Path;

            cmbMethod.SelectedIndexChanged += (s, e) => UpdatePathState();
            UpdatePathState();

            btnBrowse.Click += OnBrowse;
            btnOk.Click += OnOk;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        private bool IsFtpSelected()
        {
            var method = (DeliveryMethods.Info)cmbMethod.SelectedItem;
            return method != null && method.Id == DeliveryMethods.Ftp;
        }

        // У FTP путь/реквизиты задаются отдельной кнопкой «FTP-сервер…», поле пути ни к чему.
        private void UpdatePathState()
        {
            bool ftp = IsFtpSelected();
            lblPath.Text = ftp ? "Сервер:" : "Путь доставки:";
            txtPath.Enabled = !ftp;
            btnBrowse.Enabled = !ftp;
            txtPath.Text = ftp
                ? "Параметры подключения — кнопка «FTP-сервер…» в главном окне"
                : (txtPath.Text.StartsWith("Параметры подключения") ? "" : txtPath.Text);
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Папка назначения для патча";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtPath.Text = dialog.SelectedPath;
            }
        }

        private void OnOk(object sender, EventArgs e)
        {
            var method = (DeliveryMethods.Info)cmbMethod.SelectedItem;

            // Для FTP путь хранится не здесь (реквизиты — в профиле пользователя), путь не требуется.
            if (method.Id == DeliveryMethods.Ftp)
            {
                Result = new DeliveryConfig { Method = method.Id, Path = "" };
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            var path = txtPath.Text.Trim();
            if (path.Length == 0)
            {
                MessageBox.Show(this, "Укажите путь доставки.", "Доставка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = new DeliveryConfig { Method = method.Id, Path = path };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
