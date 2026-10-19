# Deep Space Pawnshop Mod Manager (深空当铺mod局外管理)

A local, out-of-game mod manager for **Probably Stolen**, by **totrymod**. Formerly known as Dangpu Zhanggui.

Unofficial tool. Not affiliated with Questing Goose Studio or tinyBuild.

Version 1.0.0 Beta · Windows 10/11 x64

## What it does

**Mods**
- Lists DLLs and Melon subfolders in `Mods`; toggle enable/disable without deleting files (renames to `.dll.disabled`).
- Drag DLLs, ZIPs, or whole folders anywhere onto the window to install. The window becomes a drop target and shows what will be installed.
- Install from a folder, export a detailed mod list (Markdown/CSV), delete to the Recycle Bin.
- Batch enable/disable all, and cycle sorting by name, modified time, enabled state, or issues.
- Reads `UserData/ModManager/mod-details.json` for Chinese names and summaries.

**Loader (MelonLoader)**
- Shows loader version, disabled state, and whether IL2CPP assemblies were generated.
- One-click install/repair from the official GitHub release, with progress, resume, and offline cache.
- Install from a local ZIP, or export the current loader (`version.dll` + `MelonLoader/`) for another machine.
- Toggle loader via `UserData/Loader.cfg` (`disable`), equivalent to `--no-mods`.

**UI styles**
- Terminal: dark teal metal with the HUD grid and corner marks.
- Night: the 2.0–3.x dark teal + amber look, without HUD.
- Graphite: the 1.x neutral graphite + cool blue look.
- Switch in Settings → UI Style; each style has a default accent you can override.

**Background and music**
- No bundled background images or audio. Pick your own files; they are copied to `%LOCALAPPDATA%\DangpuZhanggui\backgrounds` and `music` and can be cleared at any time.
- Background dim (0–90%) and blur (0–30); defaults keep text readable.
- Music supports mp3 / wav / wma / m4a / aac, with a playlist, previous/next, loop, volume, and optional autoplay. The header speaker button appears only when music is loaded.

**Discovery (Nexus search)**
- With an API key, the Discover page searches Probably Stolen mods through the official Nexus GraphQL API: covers, author, version, downloads, endorsements, and update date.
- Sort by relevance, downloads, endorsements, or recently updated; 20 per page with "load more".
- Clicking a result opens the Nexus mod page. The app does not download mod files (the API does not expose free download links).
- Without a key the page points to Settings; thumbnails are cached locally.

**Nexus Mods (optional)**
- Add a free personal API key to fetch the mod cover, author, and full description from the official Nexus API.
- Cache lasts 7 days. Without a key, the local catalog summary is used.
- Bind a Nexus page manually for mods that are not in the local catalog.
- Batch version check: shows "Update available" when the Nexus version is newer.

**Profiles, backups, saves**
- Save enabled/disabled combinations as profiles; apply normally or strictly (disable anything not listed).
- ZIP backups of `Mods` + both config files, with a pre-restore snapshot.
- Save snapshots: captures `SaveFile.es3`, `saves_index.es3`, and the newest `save_N.es3` only. Keeps the last 20; auto-snapshots before applying a profile.

**Diagnosis**
- Reads `MelonLoader/Latest.log` and the mod metadata, then reports:
  load failures, missing MelonInfo, missing dependencies, duplicate assemblies, enabled-but-not-loaded mods,
  grouped exceptions, Harmony conflict traces, trampoline spam, missing assemblies.
- Each finding includes evidence and suspect mods; click a suspect to jump to it.
- "Clean launch" starts the game with `--no-mods`.

**Logs and config**
- Log viewer with error/warning highlighting, search, and export.
- Edit `UserData/MelonPreferences.cfg` with validation, backups, and a global search box.

## Requirements

- Windows 10 or 11, x64.
- **.NET 10 Desktop Runtime** (`Microsoft.WindowsDesktop.App` 10.0.x):
  https://dotnet.microsoft.com/download/dotnet/10.0
- Probably Stolen (Steam).
- MelonLoader is optional at first; the app can install it.

## Install

1. Extract the ZIP anywhere (not into the game folder).
2. Run `深空当铺mod局外管理.exe`.
3. The app auto-detects the Steam library. If not, open **Settings** and select the folder containing `Probably Stolen.exe`.
4. Optional: **Settings → Loader** to install/repair MelonLoader.
5. Optional: **Settings → Nexus Mods** to add a personal API key.

## Data and privacy

Everything is stored under `%LOCALAPPDATA%\DangpuZhanggui`:
`settings.json`, `backups\`, `profiles\`, `nexus-links.json`, `cache\nexus\`, `loader\`, `error.log`.

Network access only happens when you install/repair MelonLoader (GitHub) or when a Nexus API key is configured and mod info is requested (api.nexusmods.com). Logs and mods are never uploaded.

The API key is stored locally in `settings.json` and masked in the UI. Do not share your key. Each user supplies their own personal key; the app does not bundle or transmit keys anywhere except to api.nexusmods.com.

## Notes and limits

- The app does not load mods and does not replace MelonLoader.
- Diagnosis is rule-based on the last ~2000 log lines; it points at suspects and evidence, not a guaranteed verdict.
- Update checking compares numeric version segments; unusual version formats may produce false positives.
- Save snapshots cover the current save, index, and pointer only, not every slot.
- This is an unsigned executable; SmartScreen may warn once.

## Credits

- **MelonLoader** by LavaGang and contributors (Apache License 2.0).
- **Nexus Mods API** for mod metadata and images.
- Probably Stolen and all game assets belong to Questing Goose Studio / tinyBuild.

## Changelog

**1.0.0-beta**
- First public test build.
- Mod management, MelonLoader install/repair, profiles, backups, save snapshots, and crash diagnosis.
- Nexus covers, manual page binding, update checks, and in-app search.
- Terminal / Night / Graphite UI styles, plus custom background and music (no bundled media).
