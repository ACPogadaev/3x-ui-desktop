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
        public string SourceName { get; set; }
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
                if (data == null)
                {
                    data = new MetricsData();
                    data.IsServer = true;
                    data.SourceName = currentServer != null ? currentServer.Name : "Сервер";
                    data.IsAvailable = false;
                    data.ErrorMessage = "Ожидание загрузки страницы...";
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

        public void ProcessWebMessage(string messageJson)
        {
            try
            {
                if (string.IsNullOrEmpty(messageJson)) return;
                JavaScriptSerializer jss = new JavaScriptSerializer();
                ServerStatusResult parsed = null;

                try
                {
                    parsed = jss.Deserialize<ServerStatusResult>(messageJson);
                }
                catch { }

                if (parsed == null || !parsed.ok)
                {
                    try
                    {
                        string unescaped = jss.Deserialize<string>(messageJson);
                        if (!string.IsNullOrEmpty(unescaped))
                        {
                            parsed = jss.Deserialize<ServerStatusResult>(unescaped);
                        }
                    }
                    catch { }
                }

                if (parsed != null && parsed.ok)
                {
                    MetricsData data = new MetricsData();
                    data.IsServer = true;
                    data.SourceName = currentServer != null ? currentServer.Name : "Сервер";
                    data.CpuPercent = Math.Round(parsed.cpu, 1);
                    data.RamPercent = Math.Round(parsed.mem, 1);
                    data.IsAvailable = true;

                    if (OnMetricsUpdated != null)
                    {
                        OnMetricsUpdated(data);
                    }
                    CheckThresholds(data);
                }
            }
            catch { }
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
                // Synchronous JavaScript expression:
                // Evaluated immediately by ExecuteScriptAsync without returning unresolved Promises
                string script = @"(function() {
    try {
        var cpu = null;
        var mem = null;

        function parseNum(str) {
            if (!str) return null;
            var m = str.match(/([0-9]+(?:[\.,][0-9]+)?)/);
            if (m) {
                var v = parseFloat(m[1].replace(',', '.'));
                if (!isNaN(v) && v >= 0 && v <= 100) return v;
            }
            return null;
        }

        // 1. Scan cards and statistic containers
        var cards = document.querySelectorAll('.ant-card, [class*=""Card""], [class*=""card""], [class*=""Tile""], [class*=""tile""], [class*=""statistic""], [class*=""stat""], [class*=""strip""], [class*=""cell""]');
        for (var i = 0; i < cards.length; i++) {
            var c = cards[i];
            var txt = (c.innerText || c.textContent || '').trim();
            if (!txt || txt.length > 500) continue;

            if (cpu === null && /(?:^|[\r\n\s])(?:ЦП|CPU)\b/i.test(txt)) {
                var lines = txt.split(/[\r\n]+/).map(function(s) { return s.trim(); }).filter(Boolean);
                for (var j = 0; j < lines.length; j++) {
                    var l = lines[j];
                    if (/(?:ЦП|CPU)\b/i.test(l)) {
                        var sameVal = parseNum(l.replace(/(?:ЦП|CPU)/ig, ''));
                        if (sameVal !== null) { cpu = sameVal; break; }
                        for (var k = j + 1; k < Math.min(lines.length, j + 4); k++) {
                            var nextVal = parseNum(lines[k]);
                            if (nextVal !== null) { cpu = nextVal; break; }
                        }
                        if (cpu !== null) break;
                    }
                }
            }

            if (mem === null && /(?:^|[\r\n\s])(?:ПАМЯТЬ|ОЗУ|RAM|MEMORY)\b/i.test(txt)) {
                var lines2 = txt.split(/[\r\n]+/).map(function(s) { return s.trim(); }).filter(Boolean);
                for (var j2 = 0; j2 < lines2.length; j2++) {
                    var l2 = lines2[j2];
                    if (/(?:ПАМЯТЬ|ОЗУ|RAM|MEMORY)\b/i.test(l2)) {
                        var sameVal2 = parseNum(l2.replace(/(?:ПАМЯТЬ|ОЗУ|RAM|MEMORY)/ig, ''));
                        if (sameVal2 !== null) { mem = sameVal2; break; }
                        for (var k2 = j2 + 1; k2 < Math.min(lines2.length, j2 + 4); k2++) {
                            var pctMatch = lines2[k2].match(/\(([0-9]+(?:[\.,][0-9]+)?)\s*%\)/);
                            if (pctMatch) {
                                var vPct = parseFloat(pctMatch[1].replace(',', '.'));
                                if (!isNaN(vPct) && vPct >= 0 && vPct <= 100) { mem = vPct; break; }
                            }
                            var nextMem = parseNum(lines2[k2]);
                            if (nextMem !== null) { mem = nextMem; break; }
                        }
                        if (mem !== null) break;
                    }
                }
            }

            if (cpu !== null && mem !== null) break;
        }

        // 2. Leaf element inspection
        if (cpu === null || mem === null) {
            var allElements = document.querySelectorAll('span, div, p, b, strong, label, h3, h4');
            for (var e = 0; e < allElements.length; e++) {
                var elem = allElements[e];
                if (elem.children.length > 2) continue;
                var elText = (elem.innerText || elem.textContent || '').trim();
                if (!elText || elText.length > 30) continue;

                if (cpu === null && /^(?:ЦП|CPU)$/i.test(elText)) {
                    var sib = elem.nextElementSibling;
                    if (sib) {
                        var v = parseNum(sib.innerText || sib.textContent);
                        if (v !== null) cpu = v;
                    }
                    if (cpu === null && elem.parentElement) {
                        var pText = (elem.parentElement.innerText || elem.parentElement.textContent || '');
                        var v2 = parseNum(pText.replace(/(?:ЦП|CPU)/ig, ''));
                        if (v2 !== null) cpu = v2;
                    }
                }

                if (mem === null && /^(?:ПАМЯТЬ|ОЗУ|RAM|MEMORY)$/i.test(elText)) {
                    var sib2 = elem.nextElementSibling;
                    if (sib2) {
                        var v3 = parseNum(sib2.innerText || sib2.textContent);
                        if (v3 !== null) mem = v3;
                    }
                    if (mem === null && elem.parentElement) {
                        var pText2 = (elem.parentElement.innerText || elem.parentElement.textContent || '');
                        var v4 = parseNum(pText2.replace(/(?:ПАМЯТЬ|ОЗУ|RAM|MEMORY)/ig, ''));
                        if (v4 !== null) mem = v4;
                    }
                }

                if (cpu !== null && mem !== null) break;
            }
        }

        // 3. Fallback: Full body text regex
        if (cpu === null || mem === null) {
            var bodyText = (document.body ? (document.body.innerText || '') : '');
            if (cpu === null) {
                var mCpu = bodyText.match(/(?:^|[\r\n\s])(?:ЦП|CPU)\s*[:\-\s]*[\r\n\s]*([0-9]+(?:[\.,][0-9]+)?)/i);
                if (mCpu) {
                    var vBodyCpu = parseFloat(mCpu[1].replace(',', '.'));
                    if (!isNaN(vBodyCpu) && vBodyCpu >= 0 && vBodyCpu <= 100) cpu = vBodyCpu;
                }
            }
            if (mem === null) {
                var mMem = bodyText.match(/(?:^|[\r\n\s])(?:ПАМЯТЬ|ОЗУ|RAM|MEMORY)\s*[:\-\s]*[\r\n\s]*([0-9]+(?:[\.,][0-9]+)?)/i);
                if (mMem) {
                    var vBodyMem = parseFloat(mMem[1].replace(',', '.'));
                    if (!isNaN(vBodyMem) && vBodyMem >= 0 && vBodyMem <= 100) mem = vBodyMem;
                }
            }
        }

        // Found in DOM: cache and return synchronously
        if (cpu !== null && mem !== null) {
            window.__xui_cached_metrics = { ok: true, cpu: cpu, mem: mem, time: Date.now() };
            return { ok: true, cpu: cpu, mem: mem, src: 'dom' };
        }

        // Check recent cache (within 20s)
        if (window.__xui_cached_metrics && (Date.now() - window.__xui_cached_metrics.time < 20000)) {
            return { ok: true, cpu: window.__xui_cached_metrics.cpu, mem: window.__xui_cached_metrics.mem, src: 'cache' };
        }

        // Background non-blocking API probe
        if (!window.__xui_fetching && (!window.__xui_last_fetch || Date.now() - window.__xui_last_fetch > 3000)) {
            window.__xui_fetching = true;
            window.__xui_last_fetch = Date.now();

            var path = window.location.pathname || '';
            var basePath = path.replace(/\/(inbounds|setting|clients|nodes|sub).*$/, '');
            if (!basePath.endsWith('/')) basePath += '/';

            var endpoints = [
                basePath + 'panel/api/server/status',
                basePath + 'api/server/status',
                basePath + 'server/status',
                '/panel/api/server/status',
                '/server/status'
            ];

            var fetchIdx = 0;
            var tryNext = function() {
                if (fetchIdx >= endpoints.length) {
                    window.__xui_fetching = false;
                    return;
                }
                var ep = endpoints[fetchIdx++];
                fetch(ep, {
                    method: 'GET',
                    headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                    credentials: 'include'
                }).then(function(res) {
                    if (!res.ok) throw new Error('status ' + res.status);
                    return res.json();
                }).then(function(json) {
                    var s = json.obj || json;
                    if (s && s.cpu !== undefined) {
                        var c = Number(s.cpu) || 0;
                        var m = 0;
                        if (s.mem && Number(s.mem.total) > 0) {
                            m = (Number(s.mem.current) / Number(s.mem.total)) * 100;
                        } else if (s.mem && Number(s.mem) > 0) {
                            m = Number(s.mem);
                        }
                        window.__xui_cached_metrics = { ok: true, cpu: c, mem: m, time: Date.now() };
                        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                            window.chrome.webview.postMessage(JSON.stringify({
                                ok: true,
                                cpu: c,
                                mem: m,
                                src: 'api'
                            }));
                        }
                    }
                    window.__xui_fetching = false;
                }).catch(function() {
                    tryNext();
                });
            };
            tryNext();
        }

        return { ok: false, err: 'extracting' };
    } catch (e) {
        return { ok: false, err: String(e) };
    }
})();";

                string resultJson = await webView.CoreWebView2.ExecuteScriptAsync(script);
                if (!string.IsNullOrEmpty(resultJson) && resultJson != "null")
                {
                    JavaScriptSerializer jss = new JavaScriptSerializer();
                    ServerStatusResult parsed = null;

                    try
                    {
                        parsed = jss.Deserialize<ServerStatusResult>(resultJson);
                    }
                    catch { }

                    if (parsed == null || !parsed.ok)
                    {
                        try
                        {
                            string unescaped = jss.Deserialize<string>(resultJson);
                            if (!string.IsNullOrEmpty(unescaped))
                            {
                                parsed = jss.Deserialize<ServerStatusResult>(unescaped);
                            }
                        }
                        catch { }
                    }

                    if (parsed != null && parsed.ok)
                    {
                        data.CpuPercent = Math.Round(parsed.cpu, 1);
                        data.RamPercent = Math.Round(parsed.mem, 1);
                        data.IsAvailable = true;
                        return data;
                    }
                }

                data.IsAvailable = false;
                data.ErrorMessage = "Ожидание данных...";
            }
            catch (Exception ex)
            {
                data.IsAvailable = false;
                data.ErrorMessage = ex.Message;
            }

            return data;
        }

        public class ServerStatusResult
        {
            public bool ok { get; set; }
            public double cpu { get; set; }
            public double mem { get; set; }
            public string src { get; set; }
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
                ramAlertActive = false;
            }
        }
    }
}
