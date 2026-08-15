using System;
using System.Diagnostics;
using System.Drawing;
using System.Security.Principal;
using System.Windows.Forms;

namespace NoForcedReboots
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show(
                    "NoForcedReboots needs to run as Administrator to change Windows Update's " +
                    "Active Hours and reboot policy in the registry.\n\n" +
                    "Right-click the exe and choose \"Run as administrator\", or just relaunch it " +
                    "and accept the UAC prompt.",
                    "Administrator rights required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }

        static bool IsRunningAsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    class TrayApp : Form
    {
        const int WM_POWERBROADCAST = 0x0218;
        const int PBT_APMRESUMESUSPEND = 0x7;
        const int PBT_APMRESUMEAUTOMATIC = 0x12;

        const string TaskName = "NoForcedReboots";

        NotifyIcon trayIcon;
        ContextMenuStrip menu;
        ToolStripMenuItem activeHoursHeader;
        ToolStripMenuItem rebootPendingHeader;
        ToolStripMenuItem shiftNowItem;
        ToolStripMenuItem startWithWindowsItem;
        Timer hourlyTimer;

        public TrayApp()
        {
            // no visible window, tray icon only
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Opacity = 0;
            this.Load += (s, e) => this.Hide();

            BuildTrayIcon();

            EnsureStartupTaskOnFirstRun();

            // enforce immediately on launch so state is correct right away,
            // not just after the first hourly tick
            ShiftNow(showBalloon: false);

            hourlyTimer = new Timer();
            hourlyTimer.Interval = 60 * 60 * 1000; // 1 hour
            hourlyTimer.Tick += (s, e) => ShiftNow(showBalloon: false);
            hourlyTimer.Start();
        }

        void BuildTrayIcon()
        {
            trayIcon = new NotifyIcon();
            try
            {
                trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                trayIcon.Icon = SystemIcons.Shield;
            }
            trayIcon.Visible = true;
            trayIcon.Text = "NoForcedReboots";

            menu = new ContextMenuStrip();

            activeHoursHeader = new ToolStripMenuItem("Active hours: --:-- - --:--") { Enabled = false };
            rebootPendingHeader = new ToolStripMenuItem("Reboot pending: checking...") { Enabled = false };

            menu.Items.Add(activeHoursHeader);
            menu.Items.Add(rebootPendingHeader);
            menu.Items.Add(new ToolStripSeparator());

            shiftNowItem = new ToolStripMenuItem("Shift active hours now", null, (s, e) => ShiftNow(showBalloon: true));
            menu.Items.Add(shiftNowItem);

            startWithWindowsItem = new ToolStripMenuItem("Start with Windows");
            startWithWindowsItem.Click += (s, e) => ToggleStartup();
            menu.Items.Add(startWithWindowsItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("by |¥|@µ®¡", null, (s, e) =>
                Process.Start(new ProcessStartInfo("https://mauweb.net") { UseShellExecute = true }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => { trayIcon.Visible = false; Application.Exit(); });

            menu.Opening += (s, e) => RefreshMenu();
            trayIcon.ContextMenuStrip = menu;
        }

        void RefreshMenu()
        {
            var (start, end) = WindowsUpdateManager.GetActiveHours();
            activeHoursHeader.Text = $"Active hours: {start:00}:00 - {end:00}:00";

            bool pending = WindowsUpdateManager.IsRebootPending();
            rebootPendingHeader.Text = pending ? "Reboot pending: YES" : "Reboot pending: no";

            startWithWindowsItem.Checked = StartupTask.Exists(TaskName);
        }

        void ShiftNow(bool showBalloon)
        {
            try
            {
                var (start, end) = WindowsUpdateManager.ShiftActiveHoursNow();
                trayIcon.Text = Truncate($"NoForcedReboots - Active {start:00}:00-{end:00}:00", 63);

                if (showBalloon)
                {
                    trayIcon.BalloonTipTitle = "Active hours shifted";
                    trayIcon.BalloonTipText = $"Now set to {start:00}:00 - {end:00}:00. Auto-restart stays blocked.";
                    trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    trayIcon.ShowBalloonTip(3000);
                }
            }
            catch (Exception ex)
            {
                trayIcon.BalloonTipTitle = "NoForcedReboots";
                trayIcon.BalloonTipText = "Could not update active hours: " + ex.Message;
                trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                trayIcon.ShowBalloonTip(5000);
            }
        }

        void ToggleStartup()
        {
            try
            {
                if (StartupTask.Exists(TaskName))
                    StartupTask.Remove(TaskName);
                else
                    StartupTask.Create(TaskName, Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update the startup task: " + ex.Message,
                    "NoForcedReboots", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void EnsureStartupTaskOnFirstRun()
        {
            try
            {
                StartupTask.EnsureOnFirstRun(TaskName, Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not enable Start with Windows: " + ex.Message,
                    "NoForcedReboots", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST)
            {
                int wParam = m.WParam.ToInt32();
                if (wParam == PBT_APMRESUMESUSPEND || wParam == PBT_APMRESUMEAUTOMATIC)
                {
                    // the machine may have been asleep for hours; the active
                    // hours window could already be stale, so re-center it
                    // the moment we come back instead of waiting for the
                    // next hourly tick
                    ShiftNow(showBalloon: false);
                }
            }
            base.WndProc(ref m);
        }
    }
}
