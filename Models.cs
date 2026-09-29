using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ThreeXUiDesktop
{
    public class ServerProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public bool IgnoreSsl { get; set; }

        public ServerProfile()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Новый сервер";
            Url = "http://127.0.0.1:2053";
            IgnoreSsl = true;
        }

        public ServerProfile(string name, string url, bool ignoreSsl)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name;
            Url = url;
            IgnoreSsl = ignoreSsl;
        }

        public string GetNormalizedUrl()
        {
            if (string.IsNullOrWhiteSpace(Url))
                return "about:blank";

            string trimmed = Url.Trim();
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "https://" + trimmed;
            }

            if (!trimmed.EndsWith("/"))
            {
                trimmed = trimmed + "/";
            }

            return trimmed;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class AppConfig
    {
        public List<ServerProfile> Servers { get; set; }
        public string SelectedServerId { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool CloseToTray { get; set; }
        public bool StartWithWindows { get; set; }
        public bool HideToolbar { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }

        public AppConfig()
        {
            Servers = new List<ServerProfile>();
            SelectedServerId = null;
            MinimizeToTray = true;
            CloseToTray = true;
            StartWithWindows = false;
            HideToolbar = false;
            WindowWidth = 1280;
            WindowHeight = 850;
            WindowMaximized = false;
        }

        private static string GetConfigDir()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "3xui-desktop");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static string GetConfigFilePath()
        {
            return Path.Combine(GetConfigDir(), "config.json");
        }

        public static string GetUserDataDir()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(localAppData, "3xui-desktop", "WebViewProfile");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public ServerProfile GetSelectedServer()
        {
            if (Servers == null || Servers.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(SelectedServerId))
            {
                foreach (ServerProfile sp in Servers)
                {
                    if (string.Equals(sp.Id, SelectedServerId, StringComparison.OrdinalIgnoreCase))
                        return sp;
                }
            }

            return Servers[0];
        }

        public static AppConfig Load()
        {
            string path = GetConfigFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    JavaScriptSerializer jss = new JavaScriptSerializer();
                    AppConfig cfg = jss.Deserialize<AppConfig>(json);
                    if (cfg != null && cfg.Servers != null && cfg.Servers.Count > 0)
                    {
                        return cfg;
                    }
                }
                catch
                {
                    // Ignore and create fallback default config
                }
            }

            AppConfig defaultCfg = new AppConfig();
            defaultCfg.Servers.Add(new ServerProfile("Мой 3X-UI Сервер", "https://127.0.0.1:2053", true));
            defaultCfg.SelectedServerId = defaultCfg.Servers[0].Id;
            defaultCfg.Save();
            return defaultCfg;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigFilePath();
                JavaScriptSerializer jss = new JavaScriptSerializer();
                string json = jss.Serialize(this);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Fallback silently if disk is protected
            }
        }
    }
}
