using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ThreeXUiDesktop
{
    public class SettingsDialog : Form
    {
        private CheckBox chkMinimizeToTray;
        private CheckBox chkCloseToTray;
        private CheckBox chkStartWithWindows;
        private Button btnClearCache;
        private Button btnSave;
        private Button btnCancel;

        public AppConfig Config { get; private set; }
        public bool ClearCacheRequested { get; private set; }

        public SettingsDialog(AppConfig config)
        {
            Config = config;
            InitializeComponent();
            ApplyStyling();
        }

        private void InitializeComponent()
        {
            this.Text = "Настройки приложения";
            this.Size = new Size(480, 310);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            Label lblTitle = new Label();
            lblTitle.Text = "Параметры 3X-UI Desktop";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(24, 18);
            lblTitle.AutoSize = true;

            chkMinimizeToTray = new CheckBox();
            chkMinimizeToTray.Text = "Сворачивать в системный трей (возле часов)";
            chkMinimizeToTray.Checked = Config.MinimizeToTray;
            chkMinimizeToTray.Font = new Font("Segoe UI", 9.5f);
            chkMinimizeToTray.ForeColor = Color.FromArgb(220, 220, 220);
            chkMinimizeToTray.Location = new Point(24, 60);
            chkMinimizeToTray.Size = new Size(420, 24);

            chkCloseToTray = new CheckBox();
            chkCloseToTray.Text = "При нажатии [✕] закрывать в трей, а не выходить";
            chkCloseToTray.Checked = Config.CloseToTray;
            chkCloseToTray.Font = new Font("Segoe UI", 9.5f);
            chkCloseToTray.ForeColor = Color.FromArgb(220, 220, 220);
            chkCloseToTray.Location = new Point(24, 94);
            chkCloseToTray.Size = new Size(420, 24);

            chkStartWithWindows = new CheckBox();
            chkStartWithWindows.Text = "Запускать приложение при старте Windows";
            chkStartWithWindows.Checked = Win32Helper.IsStartupEnabled();
            chkStartWithWindows.Font = new Font("Segoe UI", 9.5f);
            chkStartWithWindows.ForeColor = Color.FromArgb(220, 220, 220);
            chkStartWithWindows.Location = new Point(24, 128);
            chkStartWithWindows.Size = new Size(420, 24);

            btnClearCache = new Button();
            btnClearCache.Text = "🧹 Очистить кэш и сохраненные сессии";
            btnClearCache.Font = new Font("Segoe UI", 9f);
            btnClearCache.Location = new Point(24, 168);
            btnClearCache.Size = new Size(270, 32);
            btnClearCache.Click += (s, e) =>
            {
                DialogResult dr = MessageBox.Show(this, 
                    "Это действие удалит кэш браузера и сохраненную авторизацию. Потребуется войти в панель заново. Продолжить?", 
                    "Очистка кэша", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    ClearCacheRequested = true;
                    MessageBox.Show(this, "Кэш будет очищен при следующем запуске или перезагрузке.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnSave = new Button();
            btnSave.Text = "Сохранить";
            btnSave.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnSave.Location = new Point(225, 224);
            btnSave.Size = new Size(110, 34);
            btnSave.Click += (s, e) => { SaveSettings(); };

            btnCancel = new Button();
            btnCancel.Text = "Отмена";
            btnCancel.Font = new Font("Segoe UI", 9.5f);
            btnCancel.Location = new Point(345, 224);
            btnCancel.Size = new Size(100, 34);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblTitle);
            this.Controls.Add(chkMinimizeToTray);
            this.Controls.Add(chkCloseToTray);
            this.Controls.Add(chkStartWithWindows);
            this.Controls.Add(btnClearCache);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void ApplyStyling()
        {
            this.BackColor = Color.FromArgb(24, 25, 30);
            Win32Helper.SetDarkMode(this.Handle, true);

            StyleButton(btnClearCache, Color.FromArgb(45, 48, 58), Color.FromArgb(220, 220, 220));
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

        private void SaveSettings()
        {
            Config.MinimizeToTray = chkMinimizeToTray.Checked;
            Config.CloseToTray = chkCloseToTray.Checked;
            Config.StartWithWindows = chkStartWithWindows.Checked;

            Win32Helper.SetStartup(Config.StartWithWindows);
            Config.Save();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
