using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ThreeXUiDesktop
{
    public class MainForm : Form
    {
        private AppConfig config;
        private ServerProfile currentServer;
        private bool isExiting = false;

        // Monitoring
        private SystemMonitor monitor;
        private MetricsData latestMetrics;

        // UI Controls
        private Panel topBar;
        private ComboBox cbServers;
        private Button btnAddServer;
        private Button btnManageServers;
        private Button btnBack;
        private Button btnForward;
        private Button btnReload;
        private Button btnDashboard;
        private Button btnInbounds;
        private Button btnSettingsNav;
        private Button btnMetrics;
        private Button btnToggleBar;
        private Button btnAppSettings;
        private Label lblStatus;

        private WebView2 webView;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        public MainForm()
        {
            config = AppConfig.Load();
            currentServer = config.GetSelectedServer();

            InitializeComponent();
            ApplyStyling();
            SetupTrayIcon();
            PopulateServersDropdown();

            // Initialize System Monitor
            monitor = new SystemMonitor(config, currentServer, webView);
            monitor.OnMetricsUpdated += UpdateMetricsUi;
            monitor.OnAlertTriggered += HandleAlertTriggered;

            this.Load += async (s, e) => { await InitializeWebViewAsync(); };
            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;
            this.KeyDown += MainForm_KeyDown;
            this.KeyPreview = true;
        }

        private void InitializeComponent()
        {
            this.Text = "3X-UI Desktop";
            this.Size = new Size(config.WindowWidth > 400 ? config.WindowWidth : 1280, 
                                 config.WindowHeight > 300 ? config.WindowHeight : 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            if (config.WindowMaximized)
            {
                this.WindowState = FormWindowState.Maximized;
            }

            // Load app icon
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(icoPath))
            {
                try { this.Icon = new Icon(icoPath); } catch { }
            }

            // Top Bar
            topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 44;
            topBar.Visible = !config.HideToolbar;

            // Back / Forward / Reload
            btnBack = CreateIconButton("◀", "Назад (Alt+Left)", 34, 30);
            btnBack.Location = new Point(8, 7);
            btnBack.Click += (s, e) => { if (webView != null && webView.CanGoBack) webView.GoBack(); };

            btnForward = CreateIconButton("▶", "Вперед (Alt+Right)", 34, 30);
            btnForward.Location = new Point(44, 7);
            btnForward.Click += (s, e) => { if (webView != null && webView.CanGoForward) webView.GoForward(); };

            btnReload = CreateIconButton("⟳", "Обновить страницу (F5)", 34, 30);
            btnReload.Location = new Point(80, 7);
            btnReload.Click += (s, e) => { if (webView != null && webView.CoreWebView2 != null) webView.Reload(); };

            // Server Selector
            Label lblServerIcon = new Label();
            lblServerIcon.Text = "🌐 Сервер:";
            lblServerIcon.ForeColor = Color.FromArgb(180, 185, 200);
            lblServerIcon.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblServerIcon.AutoSize = true;
            lblServerIcon.Location = new Point(126, 13);

            cbServers = new ComboBox();
            cbServers.DropDownStyle = ComboBoxStyle.DropDownList;
            cbServers.Font = new Font("Segoe UI", 9.5f);
            cbServers.Location = new Point(195, 8);
            cbServers.Width = 200;
            cbServers.SelectedIndexChanged += CbServers_SelectedIndexChanged;

            btnAddServer = CreateIconButton("➕", "Добавить новый сервер", 34, 30);
            btnAddServer.Location = new Point(400, 7);
            btnAddServer.Click += (s, e) => { AddNewServer(); };

            btnManageServers = CreateIconButton("📋", "Список и управление серверами", 34, 30);
            btnManageServers.Location = new Point(438, 7);
            btnManageServers.Click += (s, e) => { OpenServerManager(); };

            // Separator label
            Label lblSep = new Label();
            lblSep.Text = "|";
            lblSep.ForeColor = Color.FromArgb(60, 65, 80);
            lblSep.Font = new Font("Segoe UI", 12f);
            lblSep.AutoSize = true;
            lblSep.Location = new Point(478, 10);

            // Fast Links
            btnDashboard = CreateTextButton("📊 Дашборд", "Главная страница панели");
            btnDashboard.Location = new Point(494, 7);
            btnDashboard.Size = new Size(95, 30);
            btnDashboard.Click += (s, e) => { NavigateToRelative(""); };

            btnInbounds = CreateTextButton("⚡ Inbounds", "Список подключений и клиентов");
            btnInbounds.Location = new Point(593, 7);
            btnInbounds.Size = new Size(98, 30);
            btnInbounds.Click += (s, e) => { NavigateToRelative("inbounds"); };

            btnSettingsNav = CreateTextButton("⚙️ Панель", "Настройки панели 3x-ui");
            btnSettingsNav.Location = new Point(695, 7);
            btnSettingsNav.Size = new Size(85, 30);
            btnSettingsNav.Click += (s, e) => { NavigateToRelative("settings"); };

            // Metrics Badge Button
            btnMetrics = new Button();
            btnMetrics.FlatStyle = FlatStyle.Flat;
            btnMetrics.FlatAppearance.BorderSize = 1;
            btnMetrics.FlatAppearance.BorderColor = Color.FromArgb(45, 52, 68);
            btnMetrics.BackColor = Color.FromArgb(28, 32, 42);
            btnMetrics.ForeColor = Color.FromArgb(148, 163, 184);
            btnMetrics.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            btnMetrics.Location = new Point(788, 7);
            btnMetrics.Size = new Size(220, 30);
            btnMetrics.Text = config.MonitoringEnabled ? "📊 Мониторинг..." : "📊 Мониторинг выкл.";
            btnMetrics.Cursor = Cursors.Hand;
            btnMetrics.Click += (s, e) => { ShowMetricsContextMenu(btnMetrics); };

            ToolTip ttMetrics = new ToolTip();
            ttMetrics.SetToolTip(btnMetrics, "Мониторинг нагрузки CPU и RAM. Нажмите для меню управления и порогов.");

            // Right side buttons: Status, Hide bar, App Settings
            btnAppSettings = CreateIconButton("🛠️", "Настройки приложения", 36, 30);
            btnAppSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAppSettings.Location = new Point(this.ClientSize.Width - 46, 7);
            btnAppSettings.Click += (s, e) => { OpenAppSettings(); };

            btnToggleBar = CreateIconButton("▲", "Скрыть верхнюю панель (F11)", 36, 30);
            btnToggleBar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnToggleBar.Location = new Point(this.ClientSize.Width - 86, 7);
            btnToggleBar.Click += (s, e) => { ToggleToolbar(); };

            lblStatus = new Label();
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblStatus.ForeColor = Color.FromArgb(120, 130, 150);
            lblStatus.Font = new Font("Segoe UI", 8.5f);
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(this.ClientSize.Width - 230, 14);
            lblStatus.Text = "";

            topBar.Controls.Add(btnBack);
            topBar.Controls.Add(btnForward);
            topBar.Controls.Add(btnReload);
            topBar.Controls.Add(lblServerIcon);
            topBar.Controls.Add(cbServers);
            topBar.Controls.Add(btnAddServer);
            topBar.Controls.Add(btnManageServers);
            topBar.Controls.Add(lblSep);
            topBar.Controls.Add(btnDashboard);
            topBar.Controls.Add(btnInbounds);
            topBar.Controls.Add(btnSettingsNav);
            topBar.Controls.Add(btnMetrics);
            topBar.Controls.Add(lblStatus);
            topBar.Controls.Add(btnToggleBar);
            topBar.Controls.Add(btnAppSettings);

            // WebView2 control
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;

            this.Controls.Add(webView);
            this.Controls.Add(topBar);
        }

        private void ApplyStyling()
        {
            this.BackColor = Color.FromArgb(18, 19, 23);
            Win32Helper.SetDarkMode(this.Handle, true);

            topBar.BackColor = Color.FromArgb(24, 26, 32);

            cbServers.BackColor = Color.FromArgb(36, 38, 48);
            cbServers.ForeColor = Color.White;
            cbServers.FlatStyle = FlatStyle.Flat;
        }

        private Button CreateIconButton(string text, string tooltip, int width, int height)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Size = new Size(width, height);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(36, 38, 48);
            btn.ForeColor = Color.FromArgb(220, 220, 230);
            btn.Font = new Font("Segoe UI", 9.5f);
            btn.Cursor = Cursors.Hand;

            ToolTip tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);
            return btn;
        }

        private Button CreateTextButton(string text, string tooltip)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(36, 38, 48);
            btn.ForeColor = Color.FromArgb(210, 215, 230);
            btn.Font = new Font("Segoe UI", 9f);
            btn.Cursor = Cursors.Hand;

            ToolTip tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);
            return btn;
        }

        private void SetupTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Color.FromArgb(30, 32, 40);
            trayMenu.ForeColor = Color.White;

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            Icon iconToUse = null;
            if (File.Exists(icoPath))
            {
                try { iconToUse = new Icon(icoPath); } catch { }
            }
            if (iconToUse == null) iconToUse = SystemIcons.Application;

            trayIcon = new NotifyIcon();
            trayIcon.Icon = iconToUse;
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = trayMenu;

            trayIcon.DoubleClick += (s, e) => { RestoreWindow(); };
            trayIcon.BalloonTipClicked += (s, e) => { RestoreWindow(); };
            trayIcon.Click += (s, e) =>
            {
                MouseEventArgs me = e as MouseEventArgs;
                if (me != null && me.Button == MouseButtons.Left)
                {
                    if (this.Visible && this.WindowState != FormWindowState.Minimized)
                    {
                        this.WindowState = FormWindowState.Minimized;
                        if (config.MinimizeToTray) this.Hide();
                    }
                    else
                    {
                        RestoreWindow();
                    }
                }
            };

            UpdateTrayMenu();
        }

        private void UpdateTrayMenu()
        {
            trayMenu.Items.Clear();

            string title = currentServer != null ? string.Format("3X-UI: {0}", currentServer.Name) : "3X-UI Desktop";
            ToolStripMenuItem itemHeader = new ToolStripMenuItem(title);
            itemHeader.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            itemHeader.Enabled = false;
            trayMenu.Items.Add(itemHeader);

            // Metrics item in tray
            if (config.MonitoringEnabled && latestMetrics != null && latestMetrics.IsAvailable)
            {
                string metricsText = string.Format("📈 CPU: {0}%  |  RAM: {1}% ({2})", 
                    Math.Round(latestMetrics.CpuPercent, 0), 
                    Math.Round(latestMetrics.RamPercent, 0),
                    latestMetrics.IsServer ? "Сервер" : "ПК");
                ToolStripMenuItem itemMetrics = new ToolStripMenuItem(metricsText);
                itemMetrics.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                itemMetrics.Click += (s, e) => { RestoreWindow(); };
                trayMenu.Items.Add(itemMetrics);
            }

            trayMenu.Items.Add(new ToolStripSeparator());

            // Servers list in tray
            ToolStripMenuItem itemServers = new ToolStripMenuItem("🌐 Переключить сервер");
            foreach (ServerProfile sp in config.Servers)
            {
                ServerProfile target = sp;
                ToolStripMenuItem sItem = new ToolStripMenuItem(target.Name);
                if (currentServer != null && string.Equals(currentServer.Id, target.Id, StringComparison.OrdinalIgnoreCase))
                {
                    sItem.Checked = true;
                }
                sItem.Click += (s, e) =>
                {
                    SwitchServer(target);
                    RestoreWindow();
                };
                itemServers.DropDownItems.Add(sItem);
            }
            trayMenu.Items.Add(itemServers);

            ToolStripMenuItem itemManage = new ToolStripMenuItem("📋 Управление серверами...", null, (s, e) =>
            {
                RestoreWindow();
                OpenServerManager();
            });
            trayMenu.Items.Add(itemManage);

            ToolStripMenuItem itemReload = new ToolStripMenuItem("⟳ Обновить (F5)", null, (s, e) =>
            {
                if (webView != null && webView.CoreWebView2 != null) webView.Reload();
            });
            trayMenu.Items.Add(itemReload);

            ToolStripMenuItem itemSettings = new ToolStripMenuItem("⚙️ Настройки и мониторинг...", null, (s, e) =>
            {
                RestoreWindow();
                OpenAppSettings();
            });
            trayMenu.Items.Add(itemSettings);

            ToolStripMenuItem itemShow = new ToolStripMenuItem("🪟 Показать окно", null, (s, e) => { RestoreWindow(); });
            trayMenu.Items.Add(itemShow);

            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemExit = new ToolStripMenuItem("❌ Выход", null, (s, e) =>
            {
                isExiting = true;
                Application.Exit();
            });
            trayMenu.Items.Add(itemExit);

            string tooltip = title;
            if (config.MonitoringEnabled && latestMetrics != null && latestMetrics.IsAvailable)
            {
                tooltip = string.Format("{0} | CPU: {1}% | RAM: {2}%", title, Math.Round(latestMetrics.CpuPercent, 0), Math.Round(latestMetrics.RamPercent, 0));
            }
            trayIcon.Text = (tooltip.Length > 63 ? tooltip.Substring(0, 60) + "..." : tooltip);
        }

        private void RestoreWindow()
        {
            this.Show();
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            Win32Helper.ShowWindow(this.Handle, Win32Helper.SW_RESTORE);
            Win32Helper.SetForegroundWindow(this.Handle);
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            try
            {
                lblStatus.Text = "Инициализация...";
                string userDataDir = AppConfig.GetUserDataDir();
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataDir, null);
                await webView.EnsureCoreWebView2Async(env);

                // Handle SSL certificates (Self-signed 3x-ui setups)
                webView.CoreWebView2.ServerCertificateErrorDetected += CoreWebView2_ServerCertificateErrorDetected;

                // Handle new window requests (keep within app or open in place)
                webView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    if (!string.IsNullOrEmpty(e.Uri))
                    {
                        if (e.Uri.Contains("github.com") || e.Uri.Contains("docs.sanaei.dev") || e.Uri.Contains("t.me"))
                        {
                            try { Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true }); } catch { }
                        }
                        else
                        {
                            webView.CoreWebView2.Navigate(e.Uri);
                        }
                    }
                };

                // Track navigation state
                webView.NavigationStarting += (s, e) =>
                {
                    lblStatus.Text = "Загрузка...";
                };

                webView.NavigationCompleted += (s, e) =>
                {
                    lblStatus.Text = e.IsSuccess ? "Готово" : "Ошибка загрузки";
                    btnBack.Enabled = webView.CanGoBack;
                    btnForward.Enabled = webView.CanGoForward;

                    if (currentServer != null)
                    {
                        this.Text = string.Format("3X-UI: {0} ({1})", currentServer.Name, currentServer.Url);
                    }
                };

                NavigateToCurrentServer();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка WebView2";
                MessageBox.Show(this, "Не удалось инициализировать WebView2: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CoreWebView2_ServerCertificateErrorDetected(object sender, CoreWebView2ServerCertificateErrorDetectedEventArgs e)
        {
            if (currentServer != null && currentServer.IgnoreSsl)
            {
                e.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
            }
        }

        private void PopulateServersDropdown()
        {
            cbServers.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < config.Servers.Count; i++)
            {
                ServerProfile sp = config.Servers[i];
                cbServers.Items.Add(sp);
                if (currentServer != null && string.Equals(sp.Id, currentServer.Id, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (cbServers.Items.Count > 0)
            {
                cbServers.SelectedIndex = selectedIndex;
            }
        }

        private void CbServers_SelectedIndexChanged(object sender, EventArgs e)
        {
            ServerProfile selected = cbServers.SelectedItem as ServerProfile;
            if (selected != null && (currentServer == null || !string.Equals(selected.Id, currentServer.Id, StringComparison.OrdinalIgnoreCase)))
            {
                SwitchServer(selected);
            }
        }

        private void SwitchServer(ServerProfile newServer)
        {
            if (newServer == null) return;
            currentServer = newServer;
            config.SelectedServerId = newServer.Id;
            config.Save();

            if (monitor != null)
            {
                monitor.SetServer(currentServer);
            }

            // Sync combobox without firing extra loop
            for (int i = 0; i < cbServers.Items.Count; i++)
            {
                ServerProfile sp = cbServers.Items[i] as ServerProfile;
                if (sp != null && string.Equals(sp.Id, newServer.Id, StringComparison.OrdinalIgnoreCase))
                {
                    if (cbServers.SelectedIndex != i) cbServers.SelectedIndex = i;
                    break;
                }
            }

            this.Text = string.Format("3X-UI: {0} ({1})", currentServer.Name, currentServer.Url);
            UpdateTrayMenu();
            NavigateToCurrentServer();
        }

        private void NavigateToCurrentServer()
        {
            if (webView != null && webView.CoreWebView2 != null && currentServer != null)
            {
                string target = currentServer.GetNormalizedUrl();
                webView.CoreWebView2.Navigate(target);
            }
        }

        private void NavigateToRelative(string relativePath)
        {
            if (webView == null || webView.CoreWebView2 == null || currentServer == null) return;

            string baseTarget = currentServer.GetNormalizedUrl();
            if (!string.IsNullOrEmpty(relativePath))
            {
                if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
                baseTarget = baseTarget + relativePath;
            }
            webView.CoreWebView2.Navigate(baseTarget);
        }

        private void AddNewServer()
        {
            using (ServerEditDialog dlg = new ServerEditDialog(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    config.Servers.Add(dlg.Profile);
                    config.SelectedServerId = dlg.Profile.Id;
                    config.Save();

                    PopulateServersDropdown();
                    SwitchServer(dlg.Profile);
                }
            }
        }

        private void OpenServerManager()
        {
            using (ServerManagerDialog dlg = new ServerManagerDialog(config))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedProfile != null)
                {
                    PopulateServersDropdown();
                    SwitchServer(dlg.SelectedProfile);
                }
                else
                {
                    PopulateServersDropdown();
                    currentServer = config.GetSelectedServer();
                    UpdateTrayMenu();
                }
            }
        }

        private void OpenAppSettings()
        {
            using (SettingsDialog dlg = new SettingsDialog(config))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (monitor != null)
                    {
                        monitor.UpdateConfig(config);
                    }

                    if (!config.MonitoringEnabled)
                    {
                        btnMetrics.Text = "📊 Мониторинг выкл.";
                        btnMetrics.BackColor = Color.FromArgb(28, 30, 38);
                        btnMetrics.ForeColor = Color.FromArgb(120, 130, 150);
                    }

                    UpdateTrayMenu();

                    if (dlg.ClearCacheRequested && webView != null && webView.CoreWebView2 != null)
                    {
                        try
                        {
                            webView.CoreWebView2.Profile.ClearBrowsingDataAsync();
                            NavigateToCurrentServer();
                        }
                        catch { }
                    }
                }
            }
        }

        private void UpdateMetricsUi(MetricsData data)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            this.BeginInvoke(new Action(delegate
            {
                latestMetrics = data;

                if (!config.MonitoringEnabled)
                {
                    btnMetrics.Text = "📊 Мониторинг выкл.";
                    btnMetrics.BackColor = Color.FromArgb(28, 30, 38);
                    btnMetrics.ForeColor = Color.FromArgb(120, 130, 150);
                    return;
                }

                if (!data.IsAvailable)
                {
                    btnMetrics.Text = string.Format("📊 {0}: ожидание...", data.IsServer ? "Сервер" : "ПК");
                    btnMetrics.BackColor = Color.FromArgb(32, 34, 44);
                    btnMetrics.ForeColor = Color.FromArgb(150, 155, 170);
                    return;
                }

                string sourceTag = data.IsServer ? "SVR" : "PC";
                btnMetrics.Text = string.Format("💻 {0}% | 🧠 {1}% [{2}]", 
                    Math.Round(data.CpuPercent, 0), 
                    Math.Round(data.RamPercent, 0), 
                    sourceTag);

                bool isCpuAlert = config.NotifyCpu && data.CpuPercent >= config.CpuThresholdPercent;
                bool isRamAlert = config.NotifyRam && data.RamPercent >= config.RamThresholdPercent;

                if (isCpuAlert || isRamAlert)
                {
                    // Alert red
                    btnMetrics.BackColor = Color.FromArgb(64, 22, 28);
                    btnMetrics.ForeColor = Color.FromArgb(248, 113, 113);
                    btnMetrics.FlatAppearance.BorderColor = Color.FromArgb(239, 68, 68);
                }
                else if (data.CpuPercent >= 70 || data.RamPercent >= 75)
                {
                    // Warning yellow
                    btnMetrics.BackColor = Color.FromArgb(50, 42, 22);
                    btnMetrics.ForeColor = Color.FromArgb(251, 191, 36);
                    btnMetrics.FlatAppearance.BorderColor = Color.FromArgb(245, 158, 11);
                }
                else
                {
                    // Normal green/cyan
                    btnMetrics.BackColor = Color.FromArgb(22, 32, 36);
                    btnMetrics.ForeColor = Color.FromArgb(74, 222, 128);
                    btnMetrics.FlatAppearance.BorderColor = Color.FromArgb(34, 211, 238);
                }

                UpdateTrayMenu();
            }));
        }

        private void HandleAlertTriggered(string title, string message, bool isCpu)
        {
            if (this.IsDisposed) return;

            this.BeginInvoke(new Action(delegate
            {
                if (trayIcon != null && trayIcon.Visible)
                {
                    trayIcon.ShowBalloonTip(7000, title, message, ToolTipIcon.Warning);
                }
            }));
        }

        private void ShowMetricsContextMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(30, 32, 40);
            menu.ForeColor = Color.White;

            ToolStripMenuItem itemToggle = new ToolStripMenuItem(config.MonitoringEnabled ? "✓ Мониторинг включен" : "Включить мониторинг");
            itemToggle.Click += (s, e) =>
            {
                config.MonitoringEnabled = !config.MonitoringEnabled;
                config.Save();
                if (monitor != null) monitor.UpdateConfig(config);
                if (!config.MonitoringEnabled)
                {
                    btnMetrics.Text = "📊 Мониторинг выкл.";
                    btnMetrics.BackColor = Color.FromArgb(28, 30, 38);
                    btnMetrics.ForeColor = Color.FromArgb(120, 130, 150);
                }
            };
            menu.Items.Add(itemToggle);

            ToolStripMenuItem itemSource = new ToolStripMenuItem(string.Format("Источник: {0}", 
                string.Equals(config.MonitoringSource, "local", StringComparison.OrdinalIgnoreCase) ? "Локальный ПК" : "Сервер 3X-UI"));
            itemSource.DropDownItems.Add(new ToolStripMenuItem("Сервер 3X-UI (VPS)", null, (s, e) =>
            {
                config.MonitoringSource = "server";
                config.Save();
                if (monitor != null) monitor.UpdateConfig(config);
            }));
            itemSource.DropDownItems.Add(new ToolStripMenuItem("Локальный компьютер (Windows)", null, (s, e) =>
            {
                config.MonitoringSource = "local";
                config.Save();
                if (monitor != null) monitor.UpdateConfig(config);
            }));
            menu.Items.Add(itemSource);

            menu.Items.Add(new ToolStripSeparator());

            string threshInfo = string.Format("Пороги: CPU {0}% | RAM {1}%", config.CpuThresholdPercent, config.RamThresholdPercent);
            ToolStripMenuItem itemThresh = new ToolStripMenuItem(threshInfo);
            itemThresh.Enabled = false;
            menu.Items.Add(itemThresh);

            ToolStripMenuItem itemSettings = new ToolStripMenuItem("⚙️ Настроить пороги и уведомления...", null, (s, e) =>
            {
                OpenAppSettings();
            });
            menu.Items.Add(itemSettings);

            ToolStripMenuItem itemRefresh = new ToolStripMenuItem("⟳ Обновить метрики сейчас", null, async (s, e) =>
            {
                if (monitor != null) await monitor.ForceRefreshAsync();
            });
            menu.Items.Add(itemRefresh);

            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void ToggleToolbar()
        {
            topBar.Visible = !topBar.Visible;
            config.HideToolbar = !topBar.Visible;
            config.Save();
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.Reload();
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.F11)
            {
                ToggleToolbar();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F12)
            {
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.OpenDevToolsWindow();
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.D0)
            {
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.ZoomFactor = 1.0;
                    e.Handled = true;
                }
            }
            else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
            {
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.ZoomFactor = Math.Min(3.0, webView.ZoomFactor + 0.1);
                    e.Handled = true;
                }
            }
            else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
            {
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.ZoomFactor = Math.Max(0.5, webView.ZoomFactor - 0.1);
                    e.Handled = true;
                }
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                if (config.MinimizeToTray)
                {
                    this.Hide();
                }
            }
            else
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    config.WindowWidth = this.Width;
                    config.WindowHeight = this.Height;
                    config.WindowMaximized = false;
                }
                else if (this.WindowState == FormWindowState.Maximized)
                {
                    config.WindowMaximized = true;
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isExiting && config.CloseToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "3X-UI Desktop", "Приложение свернуто в системный трей. Мониторинг продолжает работать в фоне.", ToolTipIcon.Info);
                return;
            }

            config.Save();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }
    }
}
