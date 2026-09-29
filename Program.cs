using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ThreeXUiDesktop
{
    static class Program
    {
        private const string AppGuid = "3xui-desktop-mutex-fa27419e-4b68-45be-bb7f-712b8fbb09a2";

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    System.IO.File.WriteAllText("crash.log", e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown crash");
                }
                catch { }
            };

            bool createdNew = false;
            Mutex mutex = null;
            try
            {
                mutex = new Mutex(true, AppGuid, out createdNew);
            }
            catch { }

            if (!createdNew)
            {
                Process current = Process.GetCurrentProcess();
                Process[] procs = Process.GetProcessesByName(current.ProcessName);
                bool hasOther = false;
                foreach (Process p in procs)
                {
                    if (p.Id != current.Id)
                    {
                        hasOther = true;
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            Win32Helper.ShowWindow(p.MainWindowHandle, Win32Helper.SW_RESTORE);
                            Win32Helper.SetForegroundWindow(p.MainWindowHandle);
                        }
                        break;
                    }
                }

                if (hasOther)
                {
                    return;
                }
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool startInTray = false;
                if (args != null && args.Length > 0)
                {
                    foreach (string arg in args)
                    {
                        if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase))
                        {
                            startInTray = true;
                            break;
                        }
                    }
                }

                MainForm mainForm = new MainForm();
                if (startInTray)
                {
                    mainForm.WindowState = FormWindowState.Minimized;
                    mainForm.ShowInTaskbar = false;
                    // Let form load but stay hidden in tray
                    mainForm.Load += (s, e) => { mainForm.Hide(); mainForm.ShowInTaskbar = true; };
                }

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                try { System.IO.File.WriteAllText("crash.log", ex.ToString()); } catch { }
            }
        }
    }
}
