using System;
using System.Drawing;
using System.Windows.Forms;

namespace ThreeXUiDesktop
{
    public class SettingsDialog : Form
    {
        private CheckBox chkMinimizeToTray;
        private CheckBox chkCloseToTray;
        private CheckBox chkStartWithWindows;
        private Button btnClearCache;

        // Resource Monitoring Controls
        private CheckBox chkMonitoringEnabled;
        private ComboBox cbMonitoringSource;
        private CheckBox chkNotifyCpu;
        private NumericUpDown numCpuThreshold;
        private CheckBox chkNotifyRam;
        private NumericUpDown numRamThreshold;
        private ComboBox cbPollingInterval;
        private ComboBox cbCooldown;

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
            this.Size = new Size(540, 530);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 14;

            // Header
            Label lblTitle = new Label();
            lblTitle.Text = "Параметры 3X-UI Desktop";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(24, y);
            lblTitle.AutoSize = true;
            this.Controls.Add(lblTitle);

            y += 32;

            // Group: Behavior
            chkMinimizeToTray = new CheckBox();
            chkMinimizeToTray.Text = "Сворачивать в системный трей (возле часов)";
            chkMinimizeToTray.Checked = Config.MinimizeToTray;
            chkMinimizeToTray.Font = new Font("Segoe UI", 9.5f);
            chkMinimizeToTray.ForeColor = Color.FromArgb(220, 220, 220);
            chkMinimizeToTray.Location = new Point(24, y);
            chkMinimizeToTray.Size = new Size(470, 24);
            this.Controls.Add(chkMinimizeToTray);
            y += 28;

            chkCloseToTray = new CheckBox();
            chkCloseToTray.Text = "При нажатии [✕] закрывать в трей, а не выходить";
            chkCloseToTray.Checked = Config.CloseToTray;
            chkCloseToTray.Font = new Font("Segoe UI", 9.5f);
            chkCloseToTray.ForeColor = Color.FromArgb(220, 220, 220);
            chkCloseToTray.Location = new Point(24, y);
            chkCloseToTray.Size = new Size(470, 24);
            this.Controls.Add(chkCloseToTray);
            y += 28;

            chkStartWithWindows = new CheckBox();
            chkStartWithWindows.Text = "Запускать приложение при старте Windows";
            chkStartWithWindows.Checked = Win32Helper.IsStartupEnabled();
            chkStartWithWindows.Font = new Font("Segoe UI", 9.5f);
            chkStartWithWindows.ForeColor = Color.FromArgb(220, 220, 220);
            chkStartWithWindows.Location = new Point(24, y);
            chkStartWithWindows.Size = new Size(470, 24);
            this.Controls.Add(chkStartWithWindows);
            y += 38;

            // Group: Monitoring Section Header
            Label lblMonHeader = new Label();
            lblMonHeader.Text = "📊 Мониторинг ресурсов и уведомления";
            lblMonHeader.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            lblMonHeader.ForeColor = Color.FromArgb(56, 189, 248); // Sky blue
            lblMonHeader.Location = new Point(24, y);
            lblMonHeader.AutoSize = true;
            this.Controls.Add(lblMonHeader);
            y += 26;

            chkMonitoringEnabled = new CheckBox();
            chkMonitoringEnabled.Text = "Включить мониторинг загруженности (CPU и RAM)";
            chkMonitoringEnabled.Checked = Config.MonitoringEnabled;
            chkMonitoringEnabled.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            chkMonitoringEnabled.ForeColor = Color.White;
            chkMonitoringEnabled.Location = new Point(24, y);
            chkMonitoringEnabled.Size = new Size(470, 24);
            chkMonitoringEnabled.CheckedChanged += (s, e) => { UpdateMonitoringControlsState(); };
            this.Controls.Add(chkMonitoringEnabled);
            y += 30;

            // Source Selector
            Label lblSource = new Label();
            lblSource.Text = "Источник данных:";
            lblSource.Font = new Font("Segoe UI", 9f);
            lblSource.ForeColor = Color.FromArgb(190, 195, 210);
            lblSource.Location = new Point(44, y + 3);
            lblSource.AutoSize = true;
            this.Controls.Add(lblSource);

            cbMonitoringSource = new ComboBox();
            cbMonitoringSource.DropDownStyle = ComboBoxStyle.DropDownList;
            cbMonitoringSource.Font = new Font("Segoe UI", 9f);
            cbMonitoringSource.Items.Add("Сервер 3X-UI (удаленный VPS)");
            cbMonitoringSource.Items.Add("Локальный компьютер (Windows)");
            cbMonitoringSource.SelectedIndex = string.Equals(Config.MonitoringSource, "local", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            cbMonitoringSource.Location = new Point(190, y);
            cbMonitoringSource.Size = new Size(300, 26);
            this.Controls.Add(cbMonitoringSource);
            y += 32;

            // CPU Threshold
            chkNotifyCpu = new CheckBox();
            chkNotifyCpu.Text = "Уведомлять при нагрузке Процессора (CPU) выше:";
            chkNotifyCpu.Checked = Config.NotifyCpu;
            chkNotifyCpu.Font = new Font("Segoe UI", 9f);
            chkNotifyCpu.ForeColor = Color.FromArgb(220, 220, 220);
            chkNotifyCpu.Location = new Point(44, y);
            chkNotifyCpu.Size = new Size(340, 24);
            this.Controls.Add(chkNotifyCpu);

            numCpuThreshold = new NumericUpDown();
            numCpuThreshold.Minimum = 10;
            numCpuThreshold.Maximum = 100;
            numCpuThreshold.Value = Math.Max(10, Math.Min(100, Config.CpuThresholdPercent));
            numCpuThreshold.Font = new Font("Segoe UI", 9f);
            numCpuThreshold.Location = new Point(390, y);
            numCpuThreshold.Size = new Size(60, 24);
            this.Controls.Add(numCpuThreshold);

            Label lblCpuPercent = new Label();
            lblCpuPercent.Text = "%";
            lblCpuPercent.ForeColor = Color.FromArgb(200, 200, 200);
            lblCpuPercent.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblCpuPercent.Location = new Point(455, y + 2);
            lblCpuPercent.AutoSize = true;
            this.Controls.Add(lblCpuPercent);
            y += 28;

            // RAM Threshold
            chkNotifyRam = new CheckBox();
            chkNotifyRam.Text = "Уведомлять при использовании Памяти (RAM) выше:";
            chkNotifyRam.Checked = Config.NotifyRam;
            chkNotifyRam.Font = new Font("Segoe UI", 9f);
            chkNotifyRam.ForeColor = Color.FromArgb(220, 220, 220);
            chkNotifyRam.Location = new Point(44, y);
            chkNotifyRam.Size = new Size(340, 24);
            this.Controls.Add(chkNotifyRam);

            numRamThreshold = new NumericUpDown();
            numRamThreshold.Minimum = 10;
            numRamThreshold.Maximum = 100;
            numRamThreshold.Value = Math.Max(10, Math.Min(100, Config.RamThresholdPercent));
            numRamThreshold.Font = new Font("Segoe UI", 9f);
            numRamThreshold.Location = new Point(390, y);
            numRamThreshold.Size = new Size(60, 24);
            this.Controls.Add(numRamThreshold);

            Label lblRamPercent = new Label();
            lblRamPercent.Text = "%";
            lblRamPercent.ForeColor = Color.FromArgb(200, 200, 200);
            lblRamPercent.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblRamPercent.Location = new Point(455, y + 2);
            lblRamPercent.AutoSize = true;
            this.Controls.Add(lblRamPercent);
            y += 32;

            // Polling Interval
            Label lblInterval = new Label();
            lblInterval.Text = "Интервал проверки:";
            lblInterval.Font = new Font("Segoe UI", 9f);
            lblInterval.ForeColor = Color.FromArgb(190, 195, 210);
            lblInterval.Location = new Point(44, y + 3);
            lblInterval.AutoSize = true;
            this.Controls.Add(lblInterval);

            cbPollingInterval = new ComboBox();
            cbPollingInterval.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPollingInterval.Font = new Font("Segoe UI", 9f);
            cbPollingInterval.Items.Add("Каждые 3 секунды");
            cbPollingInterval.Items.Add("Каждые 5 секунд");
            cbPollingInterval.Items.Add("Каждые 10 секунд");
            cbPollingInterval.Items.Add("Каждые 30 секунд");
            cbPollingInterval.Items.Add("Каждую минуту");

            int intervalIndex = 1;
            if (Config.PollingIntervalSeconds <= 3) intervalIndex = 0;
            else if (Config.PollingIntervalSeconds <= 5) intervalIndex = 1;
            else if (Config.PollingIntervalSeconds <= 10) intervalIndex = 2;
            else if (Config.PollingIntervalSeconds <= 30) intervalIndex = 3;
            else intervalIndex = 4;
            cbPollingInterval.SelectedIndex = intervalIndex;
            cbPollingInterval.Location = new Point(190, y);
            cbPollingInterval.Size = new Size(180, 26);
            this.Controls.Add(cbPollingInterval);
            y += 32;

            // Cooldown Interval
            Label lblCooldown = new Label();
            lblCooldown.Text = "Повтор уведомлений:";
            lblCooldown.Font = new Font("Segoe UI", 9f);
            lblCooldown.ForeColor = Color.FromArgb(190, 195, 210);
            lblCooldown.Location = new Point(44, y + 3);
            lblCooldown.AutoSize = true;
            this.Controls.Add(lblCooldown);

            cbCooldown = new ComboBox();
            cbCooldown.DropDownStyle = ComboBoxStyle.DropDownList;
            cbCooldown.Font = new Font("Segoe UI", 9f);
            cbCooldown.Items.Add("Не чаще 1 минуты");
            cbCooldown.Items.Add("Не чаще 3 минут");
            cbCooldown.Items.Add("Не чаще 5 минут");
            cbCooldown.Items.Add("Не чаще 10 минут");

            int cooldownIndex = 1;
            if (Config.CooldownMinutes <= 1) cooldownIndex = 0;
            else if (Config.CooldownMinutes <= 3) cooldownIndex = 1;
            else if (Config.CooldownMinutes <= 5) cooldownIndex = 2;
            else cooldownIndex = 3;
            cbCooldown.SelectedIndex = cooldownIndex;
            cbCooldown.Location = new Point(190, y);
            cbCooldown.Size = new Size(180, 26);
            this.Controls.Add(cbCooldown);
            y += 42;

            // Clear Cache button
            btnClearCache = new Button();
            btnClearCache.Text = "🧹 Очистить кэш и сессии браузера";
            btnClearCache.Font = new Font("Segoe UI", 9f);
            btnClearCache.Location = new Point(24, y);
            btnClearCache.Size = new Size(240, 32);
            btnClearCache.Click += (s, e) =>
            {
                DialogResult dr = MessageBox.Show(this,
                    "Это действие удалит кэш браузера и сохраненную авторизацию. Потребуется войти в панель заново. Продолжить?",
                    "Очистка кэша", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    ClearCacheRequested = true;
                    MessageBox.Show(this, "Кэш будет очищен при сохранении настроек.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            this.Controls.Add(btnClearCache);

            // Save / Cancel buttons
            btnSave = new Button();
            btnSave.Text = "Сохранить";
            btnSave.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnSave.Location = new Point(275, y);
            btnSave.Size = new Size(115, 34);
            btnSave.Click += (s, e) => { SaveSettings(); };

            btnCancel = new Button();
            btnCancel.Text = "Отмена";
            btnCancel.Font = new Font("Segoe UI", 9.5f);
            btnCancel.Location = new Point(400, y);
            btnCancel.Size = new Size(100, 34);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;

            UpdateMonitoringControlsState();
        }

        private void UpdateMonitoringControlsState()
        {
            bool enabled = chkMonitoringEnabled.Checked;
            cbMonitoringSource.Enabled = enabled;
            chkNotifyCpu.Enabled = enabled;
            numCpuThreshold.Enabled = enabled && chkNotifyCpu.Checked;
            chkNotifyRam.Enabled = enabled;
            numRamThreshold.Enabled = enabled && chkNotifyRam.Checked;
            cbPollingInterval.Enabled = enabled;
            cbCooldown.Enabled = enabled;
        }

        private void ApplyStyling()
        {
            this.BackColor = Color.FromArgb(24, 25, 30);
            Win32Helper.SetDarkMode(this.Handle, true);

            cbMonitoringSource.BackColor = Color.FromArgb(36, 38, 48);
            cbMonitoringSource.ForeColor = Color.White;

            cbPollingInterval.BackColor = Color.FromArgb(36, 38, 48);
            cbPollingInterval.ForeColor = Color.White;

            cbCooldown.BackColor = Color.FromArgb(36, 38, 48);
            cbCooldown.ForeColor = Color.White;

            numCpuThreshold.BackColor = Color.FromArgb(36, 38, 48);
            numCpuThreshold.ForeColor = Color.White;

            numRamThreshold.BackColor = Color.FromArgb(36, 38, 48);
            numRamThreshold.ForeColor = Color.White;

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

            Config.MonitoringEnabled = chkMonitoringEnabled.Checked;
            Config.MonitoringSource = cbMonitoringSource.SelectedIndex == 1 ? "local" : "server";
            Config.NotifyCpu = chkNotifyCpu.Checked;
            Config.CpuThresholdPercent = (int)numCpuThreshold.Value;
            Config.NotifyRam = chkNotifyRam.Checked;
            Config.RamThresholdPercent = (int)numRamThreshold.Value;

            int[] intervalValues = new int[] { 3, 5, 10, 30, 60 };
            Config.PollingIntervalSeconds = intervalValues[Math.Max(0, Math.Min(intervalValues.Length - 1, cbPollingInterval.SelectedIndex))];

            int[] cooldownValues = new int[] { 1, 3, 5, 10 };
            Config.CooldownMinutes = cooldownValues[Math.Max(0, Math.Min(cooldownValues.Length - 1, cbCooldown.SelectedIndex))];

            Win32Helper.SetStartup(Config.StartWithWindows);
            Config.Save();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
