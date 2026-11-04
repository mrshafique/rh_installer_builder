# RH Installer Builder

> **Author:** Shafik · **Version:** 1.0.0 · **Brand:** RH

Create a professional Windows installer (`Setup.exe`) for your **Flutter
desktop app** — no Inno Setup, no WiX, no downloads, no extra files.

`RH-Installer-Builder.exe` is a single portable file. It zips your Flutter
Windows Release build, embeds it inside a generated Setup program, and
compiles it — using only tools already present on every Windows PC
(.NET Framework + C# compiler).

The Setup it generates gives your client a real install experience:

- Install-folder picker (Browse...)
- Administrator / UAC support (optional)
- Start Menu + Desktop shortcuts
- Shows in **Settings → Apps → Installed apps** with version, size, date
- Working uninstall (Settings entry, Start Menu entry, `uninstall.exe`)
- Smart upgrades: newer version blocks downgrades, same version asks to
  reinstall, older version offers upgrade-in-place / fresh / cancel

---

## Requirements

| Need              | Notes                                                        |
|-------------------|--------------------------------------------------------------|
| Windows 10/11     | 64-bit recommended                                           |
| Flutter SDK       | Only on the machine that builds the app, not the client PC   |
| .NET Framework    | Pre-installed on Windows; provides the C# compiler (`csc.exe`) |
| Nothing else      | No Inno, no WiX, no Node, no admin rights to run the builder |

---

## First release — full steps

### 1. Build your Flutter app for Windows

```powershell
cd your_flutter_app
flutter build windows --release
```

This produces the Release folder, typically:

```
your_flutter_app\build\windows\x64\runner\Release\
  your_app.exe
  flutter_windows.dll
  ...other dlls...
  data\...
```

> Every file in this folder ships with your app — don't delete anything
> from it before packaging.

### 2. Run RH Installer Builder

Double-click `RH-Installer-Builder.exe` (copy it anywhere — USB stick,
client PC, build machine — it needs no installation).

### 3. Follow the 5-step wizard

| Step | What to do |
|------|------------|
| **1 · App** | App name (e.g. `Hostel Management`), Version (e.g. `1.0.0`), Publisher (e.g. `RH`) |
| **2 · Files** | **Browse...** to the Release folder from step 1, then pick the main `.exe` from the dropdown |
| **3 · Output** | **Save as...** to choose where the Setup.exe is written. Tick **Require Administrator** if the app installs to Program Files (recommended). Tick **Desktop shortcut** if you want one |
| **4 · Build** | Press **Build**. Watch the progress bar + log: packing → generating → compiling |
| **5 · Done** | **Open output location** to find your Setup, **Create new** to package another app, or **Close** |

### 4. Share the Setup with your client

Send the single `YourApp-Setup-1.0.0.exe` file. The client:

1. Double-clicks it (accepts the UAC prompt if admin was enabled)
2. Picks the install folder (default `C:\Program Files\Your App`)
3. Clicks **Install** — shortcuts + Add/Remove Programs entry are created
4. Launches the app when asked

> First run on the client PC may show Windows SmartScreen ("Unknown
> publisher") because the exe isn't code-signed. The client clicks
> **More info → Run anyway**. This is normal for unsigned installers.

---

## Next version — release an update

When you change your Flutter code and want to ship `1.0.1`:

### 1. Rebuild Flutter

```powershell
cd your_flutter_app
flutter build windows --release
```

### 2. Build the new installer

Open `RH-Installer-Builder.exe` again:

- **Step 1** — bump **Version** to `1.0.1` (keep the **same App name** —
  the name decides the Add/Remove Programs entry, so the same name =
  upgrade, a different name = duplicate entry)
- **Step 2** — browse to the fresh Release folder, pick the main `.exe`
- **Step 3** — save as the new file name (see naming below)
- **Step 4** — Build. **Step 5** — share it.

### 3. What the client sees

Running the new Setup on a PC with the old version installed:

- **Older → newer**: "Version 1.0.0 is installed. Yes = upgrade in place
  to 1.0.1 / No = fresh install / Cancel = stop"
- **Same version**: "Version 1.0.1 is already installed. Reinstall it?"
- **Newer → older**: blocked — "A NEWER version is already installed.
  This setup will not downgrade it."

Upgrades overwrite files in place, delete stale files from the old
version, keep the same folder, and update the single Add/Remove Programs
entry (version, size, date). No duplicates, no side-by-side copies.

### Installer file naming

Use this pattern so versions sort and clients can tell files apart:

```
<AppName>-Setup-<Version>.exe
```

Examples:

```
HostelManagement-Setup-1.0.0.exe
HostelManagement-Setup-1.0.1.exe
HostelManagement-Setup-1.1.0.exe
```

Rules:

- Keep `<AppName>` identical across versions (no spaces is safest).
- Use semantic versioning: `MAJOR.MINOR.PATCH` (`1.0.0` → `1.0.1` fix,
  `1.1.0` feature, `2.0.0` breaking).
- Archive old Setups; only ever send the latest to clients.

---

## Project files

| File | Purpose |
|------|---------|
| `RH-Installer-Builder.exe` | The portable builder — the only file you need to run |
| `BuilderApp.cs` | Full source of the wizard + the Setup template it generates |
| `README.md` | This guide |

Recompile from source any time with:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:RH-Installer-Builder.exe BuilderApp.cs
```

(The builder finds `csc.exe` automatically on any machine, so the exact
path above is only needed for manual rebuilds.)

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `C# compiler (csc.exe) not found` | Install .NET Framework 4.x (Windows Features → .NET Framework) |
| `Cannot replace 'x.dll' — close the running app` | Close the installed app (or the old Setup) and retry — a file is locked |
| `Cannot clear old folder (is the app still running?)` | Same as above; the fresh-install path needs the folder free |
| Client blocked by SmartScreen | Expected for unsigned exes — More info → Run anyway, or code-sign the Setup |
| Setup installs but app won't start on client PC | The client needs the Visual C++ Redistributable (`flutter build windows` apps may require `VCRedist`); bundle or link it |
| Builder window won't open / instant crash | Use the current `RH-Installer-Builder.exe`; very old builds had a transparency bug that is fixed |

## License

Free to use for your own apps and client deliveries.

---
*RH Installer Builder v1.0.0 — by Shafik (RH).*
