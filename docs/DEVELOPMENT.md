# Developing and running WinCC

## Requirements and startup

Runtime: Windows 11 x64 desktop with Explorer. Portable publishing includes the .NET/WPF runtime. Development: the .NET SDK version in `global.json`. No Node, WebView2, service, database, admin rights or Wallpaper Engine dependency is required. Wallpaper Engine, if present, runs independently.

Use the existing checkout in each isolated cloud task; do not create a worktree unless explicitly requested.

From the repository root on Windows:

```powershell
dotnet restore WinCC.sln
dotnet build WinCC.sln -c Release --no-restore
./tools/dev-test.ps1
dotnet run --project src/WinCC.Host -c Release -- --windowed
```

The first run is a valid blank canvas in Normal Mode. Enter Edit Dashboard, choose a display, add a Clock, and select Done. In desktop mode, only widget-sized windows exist; the Command Center remains a normal taskbar window. Closing it minimizes the Command Center and keeps widgets running. Use Exit WinCC to quit; it asks about unsaved data. A second instance reports the existing process instead of creating duplicate widgets.

Use `--windowed` for ordinary application windows; omit it to try desktop placement. Exit and restart to switch modes. Neither mode changes system settings, installs background services, registers hotkeys or enables startup. Desktop placement is an experiment pending the Windows matrix.

## Editing and storage

- Header drag and lower-right resize handle are visible only in Edit Mode. Escape or capture cancellation restores the current gesture. Pointer motion never writes a layout file.
- Focus a move handle and use arrows for 1 DIP nudge, Shift+arrows for 10 DIP. Content fields keep their normal keyboard behavior. Display moves and overlap ordering are available for every widget in the Command Center list.
- Add/remove, reset, preset, moves, resizing and ordering are undoable (100 transactions). Reset is confirmed; removal is undoable. Done atomically saves the validated draft, then returns to Normal Mode. Failed saves stay in Edit Mode. Cancel asks before discarding changes.
- Notes autosave after 600 ms of inactivity, independently of layout. Save now retries failures; Export saves a text copy. Rebuilding the UI is blocked if existing notes cannot flush. Exit asks before losing unsaved notes. Notes removed from a layout are retained on disk.
- Clock follows the system culture; Edit Mode offers system/12/24-hour formats. Calendar is a local month browser; no cloud events are implied. CPU and RAM use actual Windows counters; unavailable GPU/temperature data is labelled unavailable.
- Weather/Spotify polling occurs only while the widget is visible. Provider views cancel pending work when disposed. AI collapse ends an active request; completed chat and unsent prompt text survive in memory across layout UI rebuilds until process exit.
- Geometry is local to the monitor work area. Missing-monitor placement is projected at runtime and is not silently saved over the original. Use Move to display to accept a new target. An extremely small display can clip a runtime projection below the widget minimum; Done will reject invalid edit geometry.

Data directory: `%LOCALAPPDATA%\WinCC`:

| File | Purpose |
| --- | --- |
| `layout.json`, `.bak` | Last committed layout and prior backup |
| `notes/<widget-id>.txt`, `.bak` | Independent notes |
| `settings.json`, `.bak` | Theme, launcher targets and optional provider preferences; no keys |
| `diagnostics.jsonl`, `.old` | Local event/reason diagnostics, rotated at 1 MiB |

The app does not log note text, prompts, replies, coordinates or keys. Windows/process environment storage and crash dumps are outside that guarantee.

## Optional providers

**Weather:** In Command Center, enter latitude and longitude with decimal points, choose units, enable Open-Meteo and Save connections. Coordinates are sent only after opt-in. Weather data includes a 3-day forecast and observation timestamp, not invented readings. Network destination: `api.open-meteo.com`. Review [Open-Meteo terms](https://open-meteo.com/en/terms) for your use, especially commercial deployment. Disabling disconnects the provider.

**Spotify:** Enable local Spotify access, save, and play a track in the Windows Spotify app. The widget reads only a Spotify-identified Windows media session, presents actual title/artist/artwork/timeline and enables only advertised controls. Absent sessions and denied access are explicit states. Windows/Spotify support may vary; no successful live media integration has been demonstrated in this Linux environment.

**AI:** Supply your own OpenAI API access using the process variable `WINCC_AI_API_KEY`; WinCC has no key-entry field or key storage. Configure a model you can access in Command Center. Open the AI island and send a message. API usage can incur charges. Requests go only to `api.openai.com`, have no executable tools, and specify `store: false`. The app holds conversations in memory, capped at 20 recent messages; Clear discards them. Provider retention terms still apply. Do not put keys into source files, checked-in launch scripts or command history.

For managed cloud environments, supply credentials securely in environment settings and allow the exact provider destinations if needed. Provider setup is optional: build and contract tests require no credentials and never make real provider requests.

## Build, test and package

```powershell
./tools/dev-test.ps1
dotnet build WinCC.sln -c Release
dotnet publish src/WinCC.Host -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

The contract runner is a console executable; use `dotnet run`, not `dotnet test`. It prints the executed count and returns nonzero on failure. CI runs these contracts on Linux and Windows and builds/publishes Windows x64 artifacts. CI does not certify native desktop acceptance.

Copy the **entire** published directory and run `WinCC.exe --windowed`; it requires no installed SDK. Output is an unsigned portable build, not an installer. Deleting it removes the binaries; local data stays until deliberately removed. No install/uninstall registry entries exist.

On this Linux cloud machine, the verified SDK is in `/workspace/tools/dotnet`. Use `bash tools/cloud-dotnet.sh` in place of `dotnet` to keep CLI state and NuGet packages under writable `/workspace` paths. Sandboxed named mutex initialization and proxy access may require the tool's supported escalation. Do not disable TLS, checksums or bypass the inherited proxy.

## Recovery and rollback

1. Exit WinCC from Command Center, or terminate only `WinCC.exe` if the UI is unavailable. Nothing has to be detached from Explorer.
2. For a damaged or future-version layout, the app preserves the original and disables layout saves. It may display a valid `.bak` as a read-only recovery layout.
3. Close WinCC and archive the complete data directory before replacing any file. Restore a known-good schema-1 backup to `layout.json`, or move the original out of the data directory to start blank. Never downgrade an unknown schema by overwriting its version field. Notes are independent and should be preserved.
4. If settings are unreadable, settings changes are disabled. Archive the original `settings.json` before restoring its backup or moving it aside. Note load errors make the note read-only; preserve the text file and any `.bak` before recovery.
5. For layering, display or input issues, exit and use `--windowed`. Record Windows edition/build, display arrangement/scaling, exact test ID and observations. Do not mark G1 passed or introduce an undocumented shell workaround.

The complete platform checklist is [TEST-MATRIX.md](TEST-MATRIX.md); implementation limitations are [VALIDATION.md](VALIDATION.md).

## Widget resizing and fonts

Enter Edit Dashboard, then drag the full-width Resize handle at the bottom of any widget. Focus that handle and use arrows to resize by 1 DIP, or Shift+arrows for 10 DIP. Done saves; Cancel restores the layout. Normal Mode keeps geometry locked. Clock and provider content scroll at compact sizes.

Typography defaults to [VT323 on Google Fonts](https://fonts.google.com/specimen/VT323), with [the Google Fonts CSS link](https://fonts.googleapis.com/css2?family=VT323&display=swap) documented in the font assets. WPF uses the bundled VT323 Regular TTF resource so text renders offline. Lato Regular/Bold remain available as bundled assets. The SIL Open Font License ships with the app.

Under Appearance, choose separate interface and widget font sizes (12–32), then Apply text size. Reset text size restores 18 for both. Choices are saved in settings.json and apply independently of layout Edit Mode. Explicit heading/clock sizes scale proportionally; compact content remains scrollable.

## Custom widget interfaces

The calendar month grid and keyboard/mouse seek rail are implemented by WinCC (MonthView and SeekRail), replacing the platform calendar/slider visuals. Each widget has its own accent material and content composition: oversized clock, purple date navigator, warm notes workspace, cyan performance panel, green music player, blue forecast and violet conversation island. Text editing uses an app-defined input template over WPF text-editing primitives. Native file pickers remain for choosing/exporting user files. Windows UI visual acceptance is still pending.
