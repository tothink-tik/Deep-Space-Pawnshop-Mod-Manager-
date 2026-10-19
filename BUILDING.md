# Building Deep Space Pawnshop Mod Manager

Author: totrymod · Version: 1.1.0 Beta

This repository contains the full source of the Windows desktop app. It is published so users, Nexus staff, and antivirus vendors can audit the binary.

## Requirements

- Windows 10/11 x64
- .NET 10 SDK (not just the runtime): https://dotnet.microsoft.com/download/dotnet/10.0
- Git (optional)

There are no NuGet package dependencies.

## Build

From the repository root:

```bash
dotnet build DangpuZhanggui/DangpuZhanggui.csproj -c Release
```

## Publish the same single-file layout as the released build

```bash
dotnet publish DangpuZhanggui/DangpuZhanggui.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out
```

Output: `out/深空当铺mod局外管理.exe`.

The app is framework-dependent (it needs the .NET 10 Desktop Runtime on the target machine), unsigned, and has no installer.

## Run from source

```bash
dotnet run --project DangpuZhanggui/DangpuZhanggui.csproj
```

## Project layout

- `App.xaml`, `App.xaml.cs` — startup, theme, error logging.
- `MainWindow.xaml` — shell, navigation, background layer, music button.
- `Models/` — mod, save, search, and theme models.
- `Services/` — MelonLoader, Nexus API, backups, saves, diagnostics, background/music, modpacks, settings.
- `Views/` — the eleven pages (overview, mods, logs, config, profiles, backups, saves, diagnosis, discover, modpacks, settings).
- `Theme/` — color and control resources.
- `Assets/` — app icon.
- `Shooter.cs`, `*SelfTest.cs` — local screenshot/self-test harness; not required to run the app.

## Privacy

The source does not contain an API key, user settings, saved profiles, logs, or any bundled background image or music.

The Nexus API key, when configured by a user, is stored only in `%LOCALAPPDATA%\DangpuZhanggui\settings.json` and is sent only to `api.nexusmods.com` as the `apikey` request header.

## License

See `LICENSE.txt`. The source is published for transparency and audit; it does not grant permission to redistribute the compiled application.
