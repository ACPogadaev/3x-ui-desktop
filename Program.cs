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
            bool createdNew;
            using (Mutex mutex = new Mutex(true, AppGuid, out createdNew))
            {
                if (!createdNew)
                {
                    // Bring already running process window to front
                    Process current = Process.GetCurrentProcess();
                    foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                        {
                            Win32Helper.ShowWindow(process.MainWindowHandle, Win32Helper.SW_RESTORE);
                            Win32Helper.SetForegroundWindow(process.MainWindowHandle);
                            break;
                        }
                    }
                    return;
                }

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
        }
    }
}
