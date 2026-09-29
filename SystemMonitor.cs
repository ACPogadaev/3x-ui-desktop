using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace ThreeXUiDesktop
{
    public class MetricsData
    {
        public double CpuPercent { get; set; }
        public double RamPercent { get; set; }
        public string SourceName { get; set; } // e.g. "Сервер: Нидерланды" or "Локальный ПК"
        public bool IsServer { get; set; }
        public bool IsAvailable { get; set; }
        public string ErrorMessage { get; set; }

        public MetricsData()
        {
            SourceName = "Неизвестно";
            IsAvailable = false;
        }
    }

    public class SystemMonitor
    {
        private AppConfig config;
        private ServerProfile currentServer;
        private WebView2 webView;
        private Timer timer;

        private long lastIdleTime;
        private long lastKernelTime;
        private long lastUserTime;
        private bool hasInitialTimes = false;

        private DateTime lastCpuAlertTime = DateTime.MinValue;
        private DateTime lastRamAlertTime = DateTime.MinValue;
        private bool cpuAlertActive = false;
        private bool ramAlertActive = false;

        public event Action<MetricsData> OnMetricsUpdated;
        public event Action<string, string, bool> OnAlertTriggered;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        public SystemMonitor(AppConfig appConfig, ServerProfile server, WebView2 webViewControl)
        {
            this.config = appConfig;
            this.currentServer = server;
            this.webView = webViewControl;

            timer = new Timer();
            int intervalSec = config.PollingIntervalSeconds > 0 ? config.PollingIntervalSeconds : 5;
            timer.Interval = intervalSec * 1000;
            timer.Tick += async (s, e) => { await PollMetricsAsync(); };

            InitLocalTimes();

            if (config.MonitoringEnabled)
            {
                timer.Start();
            }
        }

        private void InitLocalTimes()
        {
            try
            {
                GetSystemTimes(out lastIdleTime, out lastKernelTime, out lastUserTime);
                hasInitialTimes = true;
            }
            catch { }
        }

        public void SetServer(ServerProfile server)
        {
            this.currentServer = server;
            // Reset alert states when switching server
            cpuAlertActive = false;
            ramAlertActive = false;
        }

        public void UpdateConfig(AppConfig newConfig)
        {
            this.config = newConfig;
            int intervalSec = config.PollingIntervalSeconds > 0 ? config.PollingIntervalSeconds : 5;
            timer.Interval = intervalSec * 1000;

            if (config.MonitoringEnabled)
            {
                if (!timer.Enabled) timer.Start();
            }
            else
            {
                if (timer.Enabled) timer.Stop();
            }
        }

        public async Task ForceRefreshAsync()
        {
            await PollMetricsAsync();
        }

        private async Task PollMetricsAsync()
        {
            if (!config.MonitoringEnabled) return;

            MetricsData data = null;

            bool preferServer = string.Equals(config.MonitoringSource, "server", StringComparison.OrdinalIgnoreCase);

            if (preferServer)
            {
                data = await FetchServerMetricsAsync();
                // If server is not responding (e.g. before login or connection lost), we mark it clearly
                if (data == null || !data.IsAvailable)
                {
                    // Fallback to local indicator with note or report server unavailable
                    if (data == null)
                    {
                        data = new MetricsData();
                        data.IsServer = true;
                        data.SourceName = currentServer != null ? currentServer.Name : "Сервер";
                        data.IsAvailable = false;
                        data.ErrorMessage = "Нет связи с панелью 3x-ui";
                    }
                }
            }
            else
            {
                data = FetchLocalMetrics();
            }

            if (data != null)
            {
                if (OnMetricsUpdated != null)
                {
                    OnMetricsUpdated(data);
                }

                if (data.IsAvailable)
                {
                    CheckThresholds(data);
                }
            }
        }

        private MetricsData FetchLocalMetrics()
        {
            MetricsData data = new MetricsData();
            data.IsServer = false;
            data.SourceName = "Локальный ПК";

            try
            {
                // Local RAM
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(mem))
                {
                    data.RamPercent = (double)mem.dwMemoryLoad;
                }

                // Local CPU
                if (!hasInitialTimes)
                {
                    InitLocalTimes();
                }

                long idle2, kernel2, user2;
                if (GetSystemTimes(out idle2, out kernel2, out user2))
                {
                    long usr = user2 - lastUserTime;
                    long ker = kernel2 - lastKernelTime;
                    long idl = idle2 - lastIdleTime;
                    long total = ker + usr;

                    if (total > 0)
                    {
                        double cpu = (double)(total - idl) / (double)total * 100.0;
                        if (cpu < 0) cpu = 0;
                        if (cpu > 100) cpu = 100;
                        data.CpuPercent = Math.Round(cpu, 1);
                    }

                    lastIdleTime = idle2;
                    lastKernelTime = kernel2;
                    lastUserTime = user2;
                }

                data.IsAvailable = true;
            }
            catch (Exception ex)
            {
                data.IsAvailable = false;
                data.ErrorMessage = ex.Message;
            }

            return data;
        }

        private async Task<MetricsData> FetchServerMetricsAsync()
        {
            if (webView == null || webView.CoreWebView2 == null)
                return null;

            MetricsData data = new MetricsData();
            data.IsServer = true;
            data.SourceName = currentServer != null ? currentServer.Name : "Сервер";

            try
            {
                // Execute javascript inside WebView2 which already has session cookies & credentials
                string script = @"
(async function() {
    try {
        let p = window.location.pathname;
        if (!p.endsWith('/')) p += '/';
        let endpoints = [
            p + 'server/status',
            p + 'panel/api/server/status',
            '/server/status',
            '/panel/api/server/status'
        ];
        for (let i = 0; i < endpoints.length; i++) {
            try {
                let r = await fetch(endpoints[i], { credentials: 'include' });
                if (r.ok) {
                    let d = await r.json();
                    let s = d.obj || d;
                    if (s && s.cpu !== undefined) {
                        let c = Number(s.cpu) || 0;
                        let m = 0;
                        if (s.mem && Number(s.mem.total) > 0) {
                            m = (Number(s.mem.current) / Number(s.mem.total)) * 100;
                        }
                        return JSON.stringify({ ok: true, cpu: c, mem: m });
                    }
                }
            } catch(e) {}
        }
        return JSON.stringify({ ok: false, err: 'not_authenticated_or_unreachable' });
    } catch(err) {
        return JSON.stringify({ ok: false, err: String(err) });
    }
})();";

                string resultJson = await webView.CoreWebView2.ExecuteScriptAsync(script);
                if (!string.IsNullOrEmpty(resultJson) && resultJson != "null")
                {
                    JavaScriptSerializer jss = new JavaScriptSerializer();
                    // WebView2 returns JSON string of string result
                    string unescaped = jss.Deserialize<string>(resultJson);
                    if (!string.IsNullOrEmpty(unescaped))
                    {
                        var parsed = jss.Deserialize<ServerStatusResult>(unescaped);
                        if (parsed != null && parsed.ok)
                        {
                            data.CpuPercent = Math.Round(parsed.cpu, 1);
                            data.RamPercent = Math.Round(parsed.mem, 1);
                            data.IsAvailable = true;
                            return data;
                        }
                    }
                }

                data.IsAvailable = false;
                data.ErrorMessage = "Панель не отвечает или требуется вход";
            }
            catch (Exception ex)
            {
                data.IsAvailable = false;
                data.ErrorMessage = ex.Message;
            }

            return data;
        }

        private class ServerStatusResult
        {
            public bool ok { get; set; }
            public double cpu { get; set; }
            public double mem { get; set; }
            public string err { get; set; }
        }

        private void CheckThresholds(MetricsData data)
        {
            TimeSpan cooldown = TimeSpan.FromMinutes(config.CooldownMinutes > 0 ? config.CooldownMinutes : 3);

            // CPU Check
            if (config.NotifyCpu && data.CpuPercent >= config.CpuThresholdPercent)
            {
                if (!cpuAlertActive || (DateTime.Now - lastCpuAlertTime) >= cooldown)
                {
                    cpuAlertActive = true;
                    lastCpuAlertTime = DateTime.Now;

                    string title = string.Format("⚠️ Высокая нагрузка CPU: {0}%", Math.Round(data.CpuPercent, 0));
                    string msg = string.Format("На источнике '{0}' загрузка процессора достигла {1}% (порог: {2}%)", 
                        data.SourceName, Math.Round(data.CpuPercent, 1), config.CpuThresholdPercent);

                    if (OnAlertTriggered != null)
                    {
                        OnAlertTriggered(title, msg, true);
                    }
                }
            }
            else if (data.CpuPercent < (config.CpuThresholdPercent - 5))
            {
                // Hysteresis reset
                cpuAlertActive = false;
            }

            // RAM Check
            if (config.NotifyRam && data.RamPercent >= config.RamThresholdPercent)
            {
                if (!ramAlertActive || (DateTime.Now - lastRamAlertTime) >= cooldown)
                {
                    ramAlertActive = true;
                    lastRamAlertTime = DateTime.Now;

                    string title = string.Format("⚠️ Высокая нагрузка RAM: {0}%", Math.Round(data.RamPercent, 0));
                    string msg = string.Format("На источнике '{0}' использование памяти достигло {1}% (порог: {2}%)", 
                        data.SourceName, Math.Round(data.RamPercent, 1), config.RamThresholdPercent);

                    if (OnAlertTriggered != null)
                    {
                        OnAlertTriggered(title, msg, false);
                    }
                }
            }
            else if (data.RamPercent < (config.RamThresholdPercent - 5))
            {
                // Hysteresis reset
                ramAlertActive = false;
            }
        }
    }
}
