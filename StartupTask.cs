using System.Diagnostics;

namespace NoForcedReboots
{
    /// <summary>
    /// Manages a Task Scheduler entry (instead of a Run-key shortcut) so the
    /// app can launch elevated at logon without a UAC prompt every time.
    /// Requires the process to already be running elevated.
    /// </summary>
    static class StartupTask
    {
        public static bool Exists(string taskName)
        {
            var psi = new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{taskName}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode == 0;
            }
        }

        public static void Create(string taskName, string exePath)
        {
            string args = $"/Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F";
            Run(args);
        }

        public static void Remove(string taskName)
        {
            Run($"/Delete /TN \"{taskName}\" /F");
        }

        static void Run(string args)
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
            }
        }
    }
}
