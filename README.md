# ayugram-telegram-alias

Runs portable AyuGram under the name `Telegram.exe`, so any process-based
router treats it as stock Telegram. Tools like ExitLag, NoPing or Mudfish
match traffic by process name, and AyuGram ships as a separate `AyuGram.exe`
binary — so their "Telegram" profiles never see it. The launcher recreates
`Telegram.exe` as a hard link to the fresh `AyuGram.exe` on every start and
launches that instead. No need to disable auto-update.

Single windowless `.exe`: pins to the taskbar directly, no `.bat` / `.vbs` needed.

## How it works

1. The launcher locates `AyuGram.exe`: first in its own `.ini`, then next to
   itself, then in common spots (`Downloads\AyuGram`, `Desktop\AyuGram`). If
   nothing is found, it shows a file picker once — the path is remembered in
   `ayugram-telegram-alias.ini` next to the exe.
2. It waits for `Updater.exe` to finish and terminates any lingering
   `AyuGram.exe` / `Telegram.exe` **from the AyuGram folder only**
   (a stock Telegram install is left alone).
3. It deletes the old `Telegram.exe` and creates a new hard link to the fresh
   `AyuGram.exe` (full copy as a fallback). If the link is already fresh
   (same file, same size and timestamp), the file is left untouched.
4. It launches `Telegram.exe` from the same folder — the same `tdata` is
   picked up, so it's the same session.
5. It stays in the tray (AyuGram icon) as a watchdog: if auto-update restarts
   the app under its original name, the link is recreated and the launch
   is switched back to `Telegram.exe`. When you quit AyuGram, the launcher
   exits by itself. Tray menu: open the AyuGram folder / quit the launcher.

## Build (nothing to install)

Windows with .NET Framework 4.x is enough (ships with Win10/11).
The `csc.exe` compiler is already on the system.

```
Build.bat
```

Output: `ayugram-telegram-alias.exe` with the AyuGram icon
(`AyuGram.ico` is picked up automatically when placed next to the script).

Tech: C#, .NET Framework (System.Windows.Forms only for the tray icon and
the file picker), `CreateHardLink` from kernel32. No NuGet packages, no SDK,
a single `Program.cs` file.

## Usage

1. Put the launcher exe next to `AyuGram.exe` (or anywhere — the path will be
   asked once via a file picker).
2. In your router, select the Telegram profile → Apply Routes → wait for
   active routes. (ExitLag example: pick Telegram, hit Apply Routes.)
3. Run the launcher. Pin it to the taskbar.
4. Verify: Task Manager shows a `Telegram.exe` process; toggle your router off
   mid-call — the connection should drop (meaning it went through the tunnel).

## Limitations

- Only one instance per `tdata` — don't run the original and the copy
  together; the second one silently exits.
- During auto-update, a couple of seconds of traffic go past the router
  (`Updater.exe` + a brief restart under the old name).
- `tg://` links from the browser open the original `AyuGram.exe`, i.e. outside
  the router.
- Overlays (Discord, OBS) see the process as "Telegram".
- An unsigned exe triggers SmartScreen on first launch — normal for
  self-built utilities.

## Icon attribution

`AyuGram.ico` is extracted from AyuGram Desktop resources and belongs to its
authors (Radolyn Labs). It is bundled here solely so the launcher is
recognizable next to the original app.

## Files

| File | Purpose |
| --- | --- |
| `Program.cs` | Source (C#, single file, zero dependencies) |
| `AyuGram.ico` | Icon extracted from AyuGram.exe resources |
| `Build.bat` | Builds a single exe via the built-in csc.exe |
| `README.md` | This file |
| `.gitignore` | Keeps the local `.ini` and the built exe out of the repo |
| `LICENSE` | MIT |
