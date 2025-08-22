# AmbLighting — One‑shot Setup (Windows)

All commands run from the repo root (this folder). These commands set up everything and put the app in Startup (Hidden).

## Quick Setup (Recommended)

Run PowerShell as Administrator, then paste:

```powershell
# Clone (if you haven't already)
# git clone https://github.com/Sohail0707/AmbLighting.git
# cd AmbLighting

# One-shot bootstrap: installs local .NET if missing, publishes app, registers login task (Hidden)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup.ps1
```

That’s it. The app will auto-run at your next logon (and starts once now). Edit `ColorExtractor\config.json` to tweak settings.

## Uninstall / Remove from Startup

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup.ps1 -Uninstall
```

## Manual Setup (Fallback)

If you prefer manual steps or can’t elevate, use these:

```powershell
# Restore & build Release
dotnet restore .\ColorExtractor\ColorExtractor.csproj
dotnet build .\ColorExtractor\ColorExtractor.csproj -c Release

# Register startup task (Hidden) and start once now
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-task.ps1 -TriggerType AtLogon -Hidden -RunNow
```

To run it immediately without a task:

```powershell
.\ColorExtractor\bin\Release\net10.0-windows\ColorExtractor.exe
```

## Notes

- The setup publishes a self‑contained single‑file exe (win‑x64) if needed and registers a Hidden scheduled task.
- The app is windowless. Logs won’t show at startup. Run the exe manually to see console output.
- All tunables live in `ColorExtractor\config.json` next to the exe; edit anytime (no rebuild required).
