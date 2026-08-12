using System;
using Microsoft.Win32;

namespace NoForcedReboots
{
    /// <summary>
    /// All the registry reads/writes that make Windows Update think "right now"
    /// always falls inside your active hours, and that force it to never
    /// auto-restart while you're logged on.
    /// </summary>
    static class WindowsUpdateManager
    {
        const string UxSettingsPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        const string AuPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

        public static (int start, int end) GetActiveHours()
        {
            int start = 8, end = 17;
            using (var key = Registry.LocalMachine.OpenSubKey(UxSettingsPath))
            {
                if (key != null)
                {
                    start = ToInt(key.GetValue("ActiveHoursStart"), start);
                    end = ToInt(key.GetValue("ActiveHoursEnd"), end);
                }
            }
            return (start, end);
        }

        static int GetActiveHoursMaxRange()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(UxSettingsPath))
            {
                if (key != null)
                {
                    var v = key.GetValue("ActiveHoursMaxRange");
                    if (v != null) return ToInt(v, 18);
                }
            }
            return 18; // Windows' default max active-hours span
        }

        /// <summary>
        /// Re-centers the active hours window around the current time and
        /// re-asserts the "never auto-restart while logged on" policy.
        /// Returns the new (start, end) hours.
        /// </summary>
        public static (int start, int end) ShiftActiveHoursNow()
        {
            int range = GetActiveHoursMaxRange();
            if (range < 1) range = 1;
            if (range > 23) range = 23;

            int now = DateTime.Now.Hour;

            // center the window on "now", clamped to a single day (0-23)
            // rather than wrapping past midnight, since the active-hours
            // registry values don't support an overnight wrap.
            int start = now - range / 2;
            if (start < 0) start = 0;
            int end = start + range;
            if (end > 23)
            {
                end = 23;
                start = end - range;
                if (start < 0) start = 0;
            }

            using (var key = Registry.LocalMachine.CreateSubKey(UxSettingsPath))
            {
                key.SetValue("ActiveHoursStart", start, RegistryValueKind.DWord);
                key.SetValue("ActiveHoursEnd", end, RegistryValueKind.DWord);
                key.SetValue("UserChoiceActiveHoursStart", start, RegistryValueKind.DWord);
                key.SetValue("UserChoiceActiveHoursEnd", end, RegistryValueKind.DWord);
                // stop Windows from silently recalculating active hours
                // based on usage patterns and overriding what we just set
                key.SetValue("SmartActiveHoursState", 0, RegistryValueKind.DWord);
            }

            EnforceNoAutoReboot();

            return (start, end);
        }

        static void EnforceNoAutoReboot()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(AuPolicyPath))
            {
                key.SetValue("NoAutoRebootWithLoggedOnUsers", 1, RegistryValueKind.DWord);
            }
        }

        public static bool IsRebootPending()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                {
                    if (k != null) return true;
                }
            }
            catch { }

            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\WindowsUpdate\Auto Update\RebootRequired"))
                {
                    if (k != null) return true;
                }
            }
            catch { }

            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager"))
                {
                    if (k != null && k.GetValue("PendingFileRenameOperations") != null) return true;
                }
            }
            catch { }

            return false;
        }

        static int ToInt(object value, int fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToInt32(value); }
            catch { return fallback; }
        }
    }
}
