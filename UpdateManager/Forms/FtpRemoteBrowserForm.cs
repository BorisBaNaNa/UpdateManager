using FluentFTP;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using UpdateManager.Core.Delivery;

namespace UpdateManager.Forms
{
    /// <summary>
    /// Браузер папок на FTP-сервере (TreeView с ленивой подгрузкой). Держит одно подключение на
    /// время жизни окна; листинг каждого узла — на фоновом потоке. По «Выбрать» возвращает полный
    /// путь выбранной папки в SelectedPath и DialogResult.OK.
    /// </summary>
    public partial class FtpRemoteBrowserForm : Form
    {
        // Узлы-папки помечаем "dir" (их можно выбрать), служебные (загрузка/ошибка) — "info".
        private const string DirNode = "dir";
        private const string InfoNode = "info";
        private const string PlaceholderNode = "placeholder";

        private readonly FtpConnection _conn;
        private readonly object _clientLock = new object();
        private FtpClient _client;
        private bool _loading;

        /// <summary>Выбранный путь (валиден при DialogResult.OK).</summary>
        public string SelectedPath { get; private set; }

        public FtpRemoteBrowserForm(FtpConnection conn)
        {
            InitializeComponent();
            Theming.ThemeManager.Register(this);
            _conn = conn;

            treeView.BeforeExpand += OnBeforeExpand;
            btnSelect.Click += OnSelect;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Connect();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            try
            {
                lock (_clientLock)
                {
                    if (_client != null)
                        _client.Dispose(); // Dispose закрывает соединение
                }
            }
            catch { /* при закрытии окна ошибки разрыва не важны */ }
        }

        // Подключение + загрузка корня на фоне.
        private void Connect()
        {
            SetBusy(true, "Подключение к " + _conn.Host + " …");
            RunInBackground(() =>
            {
                string root;
                FtpListItem[] items;
                try
                {
                    lock (_clientLock)
                    {
                        _client = new FtpClient(_conn.Host, _conn.Username, _conn.Password, _conn.Port);
                        _client.Config.ConnectTimeout = 8000;
                        _client.Config.DataConnectionConnectTimeout = 8000;
                        _client.Connect();

                        root = _client.GetWorkingDirectory();
                        if (string.IsNullOrEmpty(root))
                            root = "/";
                        items = _client.GetListing(root);
                    }
                }
                catch (Exception ex)
                {
                    SafeInvoke(() =>
                    {
                        SetBusy(false, "");
                        MessageBox.Show(this, "Не удалось подключиться:\n" + ex.Message,
                            "FTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.Cancel;
                        Close();
                    });
                    return;
                }

                SafeInvoke(() =>
                {
                    var rootNode = new TreeNode(root) { Name = DirNode, Tag = root };
                    AddDirChildren(rootNode, items);
                    treeView.Nodes.Add(rootNode);
                    rootNode.Expand();
                    treeView.SelectedNode = rootNode;
                    SetBusy(false, "");
                });
            });
        }

        // Лениво грузим детей при раскрытии узла, у которого ещё стоит заглушка.
        private void OnBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (node.Nodes.Count != 1 || node.Nodes[0].Name != PlaceholderNode)
                return; // уже загружен

            if (_loading)
            {
                e.Cancel = true;
                return;
            }

            LoadChildren(node);
        }

        private void LoadChildren(TreeNode node)
        {
            var path = (string)node.Tag;
            SetBusy(true, "Загрузка " + path + " …");
            node.Nodes.Clear();
            node.Nodes.Add(new TreeNode("Загрузка…") { Name = InfoNode });

            RunInBackground(() =>
            {
                FtpListItem[] items = null;
                string error = null;
                try
                {
                    lock (_clientLock)
                        items = _client.GetListing(path);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                SafeInvoke(() =>
                {
                    node.Nodes.Clear();
                    if (error != null)
                        node.Nodes.Add(new TreeNode("Ошибка: " + error) { Name = InfoNode });
                    else
                        AddDirChildren(node, items);
                    SetBusy(false, "");
                });
            });
        }

        // Добавляет в узел только подпапки; каждой вешает заглушку, чтобы её можно было раскрыть.
        private static void AddDirChildren(TreeNode parent, FtpListItem[] items)
        {
            var dirs = items
                .Where(i => i.Type == FtpObjectType.Directory)
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                var child = new TreeNode(dir.Name) { Name = DirNode, Tag = dir.FullName };
                child.Nodes.Add(new TreeNode("") { Name = PlaceholderNode });
                parent.Nodes.Add(child);
            }
        }

        private void OnSelect(object sender, EventArgs e)
        {
            var node = treeView.SelectedNode;
            if (node == null || node.Name != DirNode)
            {
                MessageBox.Show(this, "Выберите папку на сервере.", "FTP",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedPath = (string)node.Tag;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetBusy(bool busy, string status)
        {
            _loading = busy;
            lblStatus.Text = status;
            btnSelect.Enabled = !busy;
            treeView.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private static void RunInBackground(ThreadStart work)
        {
            new Thread(work) { IsBackground = true }.Start();
        }

        // Маршалинг в UI-поток с защитой от закрытого окна.
        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(action);
            }
            catch (ObjectDisposedException) { /* окно закрыли во время загрузки — игнорируем */ }
            catch (InvalidOperationException) { /* дескриптор уже уничтожен */ }
        }
    }
}
