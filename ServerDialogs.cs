using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ThreeXUiDesktop
{
    public class ServerEditDialog : Form
    {
        private TextBox txtName;
        private TextBox txtUrl;
        private CheckBox chkIgnoreSsl;
        private Button btnTest;
        private Label lblTestResult;
        private Button btnSave;
        private Button btnCancel;

        public ServerProfile Profile { get; private set; }

        public ServerEditDialog(ServerProfile profileToEdit = null)
        {
            Profile = profileToEdit != null ? 
                new ServerProfile(profileToEdit.Name, profileToEdit.Url, profileToEdit.IgnoreSsl) { Id = profileToEdit.Id } :
                new ServerProfile("Мой сервер", "https://127.0.0.1:2053", true);

            InitializeComponent();
            ApplyStyling();
        }

        private void InitializeComponent()
        {
            this.Text = Profile != null && !string.IsNullOrEmpty(Profile.Name) ? "Настройка сервера" : "Добавить сервер";
            this.Size = new Size(540, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            Label lblTitle = new Label();
            lblTitle.Text = "Параметры подключения к 3X-UI";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(24, 18);
            lblTitle.AutoSize = true;

            Label lblName = new Label();
            lblName.Text = "Название сервера:";
            lblName.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblName.ForeColor = Color.FromArgb(200, 200, 200);
            lblName.Location = new Point(24, 60);
            lblName.AutoSize = true;

            txtName = new TextBox();
            txtName.Text = Profile.Name;
            txtName.Font = new Font("Segoe UI", 10f);
            txtName.Location = new Point(24, 85);
            txtName.Size = new Size(475, 28);

            Label lblUrl = new Label();
            lblUrl.Text = "URL-адрес веб-панели (с портом и базовым путем, если есть):";
            lblUrl.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblUrl.ForeColor = Color.FromArgb(200, 200, 200);
            lblUrl.Location = new Point(24, 125);
            lblUrl.AutoSize = true;

            txtUrl = new TextBox();
            txtUrl.Text = Profile.Url;
            txtUrl.Font = new Font("Segoe UI", 10f);
            txtUrl.Location = new Point(24, 150);
            txtUrl.Size = new Size(475, 28);

            Label lblUrlHint = new Label();
            lblUrlHint.Text = "Примеры:  https://194.87.12.34:2053/   или   https://vps.mydomain.com:2053/xui/";
            lblUrlHint.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblUrlHint.ForeColor = Color.FromArgb(140, 140, 150);
            lblUrlHint.Location = new Point(24, 182);
            lblUrlHint.Size = new Size(475, 18);

            chkIgnoreSsl = new CheckBox();
            chkIgnoreSsl.Text = "Доверять самоподписанным сертификатам (игнорировать ошибки SSL)";
            chkIgnoreSsl.Checked = Profile.IgnoreSsl;
            chkIgnoreSsl.Font = new Font("Segoe UI", 9.5f);
            chkIgnoreSsl.ForeColor = Color.FromArgb(220, 220, 220);
            chkIgnoreSsl.Location = new Point(24, 210);
            chkIgnoreSsl.Size = new Size(475, 24);

            btnTest = new Button();
            btnTest.Text = "🔌 Проверить связь";
            btnTest.Font = new Font("Segoe UI", 9f);
            btnTest.Location = new Point(24, 248);
            btnTest.Size = new Size(160, 32);
            btnTest.Click += async (s, e) => { await TestConnectionAsync(); };

            lblTestResult = new Label();
            lblTestResult.Font = new Font("Segoe UI", 9f);
            lblTestResult.Location = new Point(195, 254);
            lblTestResult.Size = new Size(305, 26);
            lblTestResult.Text = "";

            btnSave = new Button();
            btnSave.Text = "Сохранить";
            btnSave.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnSave.Location = new Point(275, 295);
            btnSave.Size = new Size(110, 34);
            btnSave.Click += (s, e) => { SaveProfile(); };

            btnCancel = new Button();
            btnCancel.Text = "Отмена";
            btnCancel.Font = new Font("Segoe UI", 9.5f);
            btnCancel.Location = new Point(395, 295);
            btnCancel.Size = new Size(104, 34);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblUrl);
            this.Controls.Add(txtUrl);
            this.Controls.Add(lblUrlHint);
            this.Controls.Add(chkIgnoreSsl);
            this.Controls.Add(btnTest);
            this.Controls.Add(lblTestResult);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void ApplyStyling()
        {
            this.BackColor = Color.FromArgb(24, 25, 30);
            Win32Helper.SetDarkMode(this.Handle, true);

            txtName.BackColor = Color.FromArgb(36, 38, 46);
            txtName.ForeColor = Color.White;
            txtName.BorderStyle = BorderStyle.FixedSingle;

            txtUrl.BackColor = Color.FromArgb(36, 38, 46);
            txtUrl.ForeColor = Color.White;
            txtUrl.BorderStyle = BorderStyle.FixedSingle;

            StyleButton(btnTest, Color.FromArgb(45, 48, 58), Color.White);
            StyleButton(btnSave, Color.FromArgb(14, 116, 144), Color.White);
            StyleButton(btnCancel, Color.FromArgb(45, 48, 58), Color.FromArgb(200, 200, 200));
        }

        private void StyleButton(Button btn, Color bg, Color fg)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bg;
            btn.ForeColor = fg;
            btn.Cursor = Cursors.Hand;
        }

        private async Task TestConnectionAsync()
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                lblTestResult.ForeColor = Color.OrangeRed;
                lblTestResult.Text = "Введите URL сервера";
                return;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            btnTest.Enabled = false;
            lblTestResult.ForeColor = Color.LightSkyBlue;
            lblTestResult.Text = "Подключение...";

            bool ignoreSsl = chkIgnoreSsl.Checked;

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                var result = await Task.Factory.StartNew<string>(() =>
                {
                    try
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                        request.Timeout = 6000;
                        request.Method = "GET";
                        request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) 3xui-desktop";

                        if (ignoreSsl)
                        {
                            request.ServerCertificateValidationCallback = delegate { return true; };
                        }

                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        {
                            sw.Stop();
                            return string.Format("Доступен! (HTTP {0}, {1}мс)", (int)response.StatusCode, sw.ElapsedMilliseconds);
                        }
                    }
                    catch (WebException wex)
                    {
                        sw.Stop();
                        HttpWebResponse webResp = wex.Response as HttpWebResponse;
                        if (webResp != null)
                        {
                            // A response was received (even 401, 403, 404 means the panel is alive and responding)
                            return string.Format("Сервер ответил: HTTP {0} ({1}мс)", (int)webResp.StatusCode, sw.ElapsedMilliseconds);
                        }
                        if (wex.Status == WebExceptionStatus.TrustFailure)
                        {
                            return "Ошибка сертификата SSL (включите обход SSL)";
                        }
                        return "Ошибка: " + wex.Message;
                    }
                    catch (Exception ex)
                    {
                        return "Ошибка: " + ex.Message;
                    }
                });

                if (result.StartsWith("Доступен") || result.StartsWith("Сервер ответил"))
                {
                    lblTestResult.ForeColor = Color.FromArgb(74, 222, 128); // Green
                }
                else
                {
                    lblTestResult.ForeColor = Color.OrangeRed;
                }
                lblTestResult.Text = result;
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }

        private void SaveProfile()
        {
            string name = txtName.Text.Trim();
            string url = txtUrl.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, "Пожалуйста, введите название сервера.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(this, "Пожалуйста, введите URL-адрес сервера 3x-ui.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUrl.Focus();
                return;
            }

            Profile.Name = name;
            Profile.Url = url;
            Profile.IgnoreSsl = chkIgnoreSsl.Checked;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    public class ServerManagerDialog : Form
    {
        private ListView lvServers;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnSelect;
        private Button btnClose;

        public AppConfig Config { get; private set; }
        public ServerProfile SelectedProfile { get; private set; }

        public ServerManagerDialog(AppConfig config)
        {
            Config = config;
            InitializeComponent();
            ApplyStyling();
            LoadServers();
        }

        private void InitializeComponent()
        {
            this.Text = "Управление серверами 3X-UI";
            this.Size = new Size(680, 430);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            Label lblHeader = new Label();
            lblHeader.Text = "Сохраненные серверы 3X-UI";
            lblHeader.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblHeader.ForeColor = Color.White;
            lblHeader.Location = new Point(20, 16);
            lblHeader.AutoSize = true;

            lvServers = new ListView();
            lvServers.Location = new Point(20, 50);
            lvServers.Size = new Size(500, 310);
            lvServers.View = View.Details;
            lvServers.FullRowSelect = true;
            lvServers.MultiSelect = false;
            lvServers.HideSelection = false;
            lvServers.Columns.Add("Название", 180);
            lvServers.Columns.Add("URL адрес", 220);
            lvServers.Columns.Add("SSL", 70);

            lvServers.DoubleClick += (s, e) => { SelectAndClose(); };

            btnAdd = new Button();
            btnAdd.Text = "➕ Добавить";
            btnAdd.Location = new Point(535, 50);
            btnAdd.Size = new Size(115, 34);
            btnAdd.Click += (s, e) => { AddServer(); };

            btnEdit = new Button();
            btnEdit.Text = "✏️ Изменить";
            btnEdit.Location = new Point(535, 92);
            btnEdit.Size = new Size(115, 34);
            btnEdit.Click += (s, e) => { EditServer(); };

            btnDelete = new Button();
            btnDelete.Text = "🗑️ Удалить";
            btnDelete.Location = new Point(535, 134);
            btnDelete.Size = new Size(115, 34);
            btnDelete.Click += (s, e) => { DeleteServer(); };

            btnSelect = new Button();
            btnSelect.Text = "✓ Выбрать";
            btnSelect.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnSelect.Location = new Point(535, 280);
            btnSelect.Size = new Size(115, 36);
            btnSelect.Click += (s, e) => { SelectAndClose(); };

            btnClose = new Button();
            btnClose.Text = "Закрыть";
            btnClose.Location = new Point(535, 324);
            btnClose.Size = new Size(115, 34);
            btnClose.Click += (s, e) => { this.Close(); };

            this.Controls.Add(lblHeader);
            this.Controls.Add(lvServers);
            this.Controls.Add(btnAdd);
            this.Controls.Add(btnEdit);
            this.Controls.Add(btnDelete);
            this.Controls.Add(btnSelect);
            this.Controls.Add(btnClose);
        }

        private void ApplyStyling()
        {
            this.BackColor = Color.FromArgb(24, 25, 30);
            Win32Helper.SetDarkMode(this.Handle, true);

            lvServers.BackColor = Color.FromArgb(32, 34, 42);
            lvServers.ForeColor = Color.White;
            lvServers.BorderStyle = BorderStyle.FixedSingle;
            lvServers.Font = new Font("Segoe UI", 9.5f);

            StyleButton(btnAdd, Color.FromArgb(45, 48, 58), Color.White);
            StyleButton(btnEdit, Color.FromArgb(45, 48, 58), Color.White);
            StyleButton(btnDelete, Color.FromArgb(60, 30, 35), Color.FromArgb(248, 113, 113));
            StyleButton(btnSelect, Color.FromArgb(14, 116, 144), Color.White);
            StyleButton(btnClose, Color.FromArgb(45, 48, 58), Color.FromArgb(200, 200, 200));
        }

        private void StyleButton(Button btn, Color bg, Color fg)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bg;
            btn.ForeColor = fg;
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9f);
        }

        private void LoadServers()
        {
            lvServers.Items.Clear();
            foreach (ServerProfile sp in Config.Servers)
            {
                ListViewItem lvi = new ListViewItem(sp.Name);
                lvi.SubItems.Add(sp.Url);
                lvi.SubItems.Add(sp.IgnoreSsl ? "Игнор" : "Строгий");
                lvi.Tag = sp;

                if (string.Equals(sp.Id, Config.SelectedServerId, StringComparison.OrdinalIgnoreCase))
                {
                    lvi.Font = new Font(lvServers.Font, FontStyle.Bold);
                    lvi.ForeColor = Color.FromArgb(34, 211, 238); // Cyan
                }

                lvServers.Items.Add(lvi);
            }

            if (lvServers.Items.Count > 0 && lvServers.SelectedItems.Count == 0)
            {
                lvServers.Items[0].Selected = true;
            }
        }

        private void AddServer()
        {
            using (ServerEditDialog dlg = new ServerEditDialog(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Config.Servers.Add(dlg.Profile);
                    Config.SelectedServerId = dlg.Profile.Id;
                    Config.Save();
                    LoadServers();
                }
            }
        }

        private void EditServer()
        {
            if (lvServers.SelectedItems.Count == 0) return;
            ServerProfile current = lvServers.SelectedItems[0].Tag as ServerProfile;
            if (current == null) return;

            using (ServerEditDialog dlg = new ServerEditDialog(current))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    current.Name = dlg.Profile.Name;
                    current.Url = dlg.Profile.Url;
                    current.IgnoreSsl = dlg.Profile.IgnoreSsl;
                    Config.Save();
                    LoadServers();
                }
            }
        }

        private void DeleteServer()
        {
            if (lvServers.SelectedItems.Count == 0) return;
            if (Config.Servers.Count <= 1)
            {
                MessageBox.Show(this, "Нельзя удалить единственный оставшийся сервер.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ServerProfile current = lvServers.SelectedItems[0].Tag as ServerProfile;
            if (current == null) return;

            DialogResult dr = MessageBox.Show(this, string.Format("Удалить сервер '{0}'?", current.Name), "Подтверждение удаления", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                Config.Servers.Remove(current);
                if (string.Equals(Config.SelectedServerId, current.Id, StringComparison.OrdinalIgnoreCase))
                {
                    Config.SelectedServerId = Config.Servers[0].Id;
                }
                Config.Save();
                LoadServers();
            }
        }

        private void SelectAndClose()
        {
            if (lvServers.SelectedItems.Count == 0) return;
            ServerProfile current = lvServers.SelectedItems[0].Tag as ServerProfile;
            if (current == null) return;

            Config.SelectedServerId = current.Id;
            Config.Save();
            SelectedProfile = current;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
