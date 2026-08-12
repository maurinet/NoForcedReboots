# NoForcedReboots

A small Windows tray utility that keeps your PC permanently inside Windows
Update's "Active Hours" window and blocks auto-restart while you're logged
on, so Windows never yanks the rug out from under you mid-work.

Inspired by [AlwaysActiveHours](https://github.com/TechTank/AlwaysActiveHours),
which does the right thing but as a confusing batch-file menu with no
persistent status. NoForcedReboots does the same job as a proper tray app:
right-click for a plain-English status, one click to force a shift, and it
keeps itself current automatically in the background.

## The problem

Windows Update will only auto-restart your PC *outside* your configured
Active Hours (by default a max 18-hour span). If you're away, asleep, or
just outside that window when an update wants to install, Windows reboots
your machine on its own, closing whatever you had open. Active Hours also
drift out of date on their own if "smart" active hours re-guesses your usage
pattern.

## The solution

NoForcedReboots sits in the tray and does three things:

1. **Keeps Active Hours centered on the current time.** Every hour (and
   immediately when your PC wakes from sleep), it recalculates the Active
   Hours window so "now" is always safely in the middle of it, using
   whatever max span Windows allows (`ActiveHoursMaxRange`, 18 hours by
   default). It also disables `SmartActiveHoursState` so Windows can't
   silently override the values with its own guess.
2. **Blocks auto-restart while you're logged on**, by setting the
   `NoAutoRebootWithLoggedOnUsers` policy every time it runs a shift, so
   even a stale Active Hours window can't cause a surprise restart.
3. **Shows you exactly what's going on**, right-click the tray icon for:
   - the current Active Hours window
   - whether a reboot is currently pending
   - **Shift active hours now** to force an immediate recalculation
   - **Start with Windows** to toggle a Task Scheduler entry that launches
     it elevated at logon (no UAC prompt every time)

## Requirements

- Windows 10 or 11
- Administrator rights (the app self-elevates via UAC on launch, since it
  writes to `HKLM`)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build it

## Building

```powershell
git clone https://github.com/maurinet/NoForcedReboots.git
cd NoForcedReboots
dotnet publish -c Release -r win-x64 --self-contained false -o dist
```

or just run `build.bat`. Your executable will be at
`dist\NoForcedReboots.exe`.

## Running it

Double-click `NoForcedReboots.exe` and accept the UAC prompt (it needs
admin rights to touch `HKLM\SOFTWARE\Microsoft\WindowsUpdate\...`). It has
no visible window, only a tray icon.

Right-click the tray icon for:

- **Active hours: HH:mm - HH:mm** — current window (read-only, informational)
- **Reboot pending: yes/no** — whether Windows is already waiting to finish
  an install on next restart (read-only, informational)
- **Shift active hours now** — recalculate immediately instead of waiting
  for the hourly tick
- **Start with Windows** — check to launch automatically (elevated, no
  prompt) at logon; uncheck to remove
- **Exit**

## What it actually touches

| Key | Value | Purpose |
|---|---|---|
| `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings` | `ActiveHoursStart`, `ActiveHoursEnd`, `UserChoiceActiveHoursStart`, `UserChoiceActiveHoursEnd` | the active hours window itself |
| `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings` | `SmartActiveHoursState = 0` | stops Windows from overriding the above with its own guess |
| `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU` | `NoAutoRebootWithLoggedOnUsers = 1` | blocks auto-restart while any user is logged on |

No services, no network access, no third-party dependencies.

## Notes and limitations

- On domain-joined or heavily policy-managed machines, Group Policy can
  override these settings; that's outside this app's control.
- This blocks Windows from auto-restarting *while you're logged on*. It
  does not, and cannot, bypass Windows 11's hard update deadlines forced by
  IT policy, or a restart you trigger yourself.
- Since the executable isn't code-signed, Windows SmartScreen or your
  antivirus may flag it on first run. "More info" → "Run anyway", or add an
  exclusion.

## License

MIT — see [LICENSE](LICENSE).

## Author

[|¥|@µ®¡](https://mauweb.net)
