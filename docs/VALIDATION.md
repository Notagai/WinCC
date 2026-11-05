# Implementation validation — 2026-11-05

## Environment and scope

Validated in the provided Linux x64 cloud checkout, using Microsoft .NET SDK **10.0.403**. The SDK archive's SHA-512 was checked against Microsoft's official release metadata before extraction. Windows reference/runtime packages were restored through the inherited HTTPS proxy with verification enabled. Source has not been pushed or deployed by this task.

The owner authorized the complete project after the G0 baseline. This is a compiled implementation candidate, not a Windows-certified release. Historical native test entries remain NOT RUN. No Windows OS build or desktop evidence is available from this machine.

## Checks

From `/workspace/WinCC`, the cloud helper substitutes the installed SDK and writable CLI/package directories:

| Command/check | Observed result |
| --- | --- |
| `bash tools/install-cloud-sdk.sh` | Existing pinned SDK recognized; initial installation SHA-512 verified |
| `bash tools/cloud-dotnet.sh build WinCC.sln -c Release -m:2` | PASS: entire solution, zero warnings/errors |
| `bash tools/cloud-dotnet.sh run --project tests/WinCC.Layout.Tests -c Release` | PASS: 41 contracts, zero failures |
| `bash tools/cloud-dotnet.sh publish src/WinCC.Host -c Release -r win-x64 --self-contained true -o artifacts/win-x64` | PASS: portable Windows x64 app and runtime produced |
| Published apphost | Windows PE x64 executable; required app, runtime and WPF assemblies present |
| `git diff --check` | PASS |
| Local Markdown targets, project/XAML/manifest/SVG XML, Bash syntax | PASS |
| Windows process startup and desktop matrix | **NOT RUN**: Linux machine |
| Real Open-Meteo request | **NOT RUN**: provider contracts use an injected HTTP handler |
| Live Spotify session / media controls | **NOT RUN**: no Windows media session |
| Live AI conversation | **NOT RUN**: no provider credential used |
| GitHub Actions | Added; **NOT RUN** in this task |

The console contract runner returns nonzero on failure and requires at least one executed case. It is intentionally run with `dotnet run`; `dotnet test` is not a substitute. Provider tests exercise request destination/payload, response parsing, no-storage/no-tools AI flags, opt-in input, credential absence, cancellation, response bounds and HTTP failures using synthetic responses; they do not demonstrate real account authorization.

## Covered behavior

- Normal Mode lockout and detached snapshots; retained mutation references cannot bypass validation.
- Blank canvas, catalog sizes, add/remove, undo/redo, bounded history, complete Cancel restoration, invalid-mutation rollback.
- Done/restart persistence, simulated and actual filesystem failure retention, uncommitted-crash recovery, atomic backup, corrupt and future-schema protection, external schema changes.
- Duplicate IDs, unknown types/fields, malformed/null JSON, non-object roots, invalid versions, nonfinite/out-of-range geometry, oversized layouts and invalid settings.
- Negative monitor origins, mixed-DPI conversion, bounds clamping, tiny-display projection and absent-monitor association preservation.
- Independent notes persistence, path traversal rejection and size bounds.
- Weather and AI HTTP contracts and errors, with no real requests or secrets.

Associations with G2/C/D test IDs in the runner cover the pure part of those cases only. They do not pass the full native interaction cases.

## Remaining release gates and limitations

1. **G1:** All native entries G1-001–G1-014 remain NOT RUN. Real wallpaper visibility, window alpha, native pointer handling, foreground order, app activation/fullscreen, Explorer restart and renderer alternatives need evidence. This candidate uses WPF only; WorkerW, WebView2, WebView2CompositionControl and native composition alternatives are not implemented experiments. No unrun technique is described as supported.
2. **G2/G3:** Mouse/touch/keyboard UI, notes controls, accessibility/high contrast and dynamic-island focus must be exercised on Windows 11. The full candidate starts blank rather than injecting a mandatory clock or preset; add a clock explicitly for G2 tests. Dragging stays on the current display; use the explicit display selector for cross-monitor moves.
3. **G4:** D-001–D-012 remain NOT RUN. Display-name fallback identities can change; per-monitor DPI/native window frame effects, fullscreen polling (2 seconds), sleep/resume and virtual-desktop behavior need physical testing. The app does not promise visibility on all virtual desktops. CPU counters may be limited by Windows processor-group behavior on systems with more than 64 logical processors.
4. GPU/temperature/network graphs and cloud calendar synchronization are not implemented. Unavailable metrics are labelled, not fabricated. Glass is translucent tint; native backdrop blur is not claimed. There is no optional snapping/grid configuration yet, no taskbar replacement, installer, auto-start, privileged service or telemetry.
5. Weather uses Open-Meteo's public endpoint and requires explicit coordinates and opt-in. Live provider availability, commercial terms and the chosen AI model/account must be checked before deployment. Local Spotify session capabilities vary; album art and controls are conditional on actual media metadata.
6. Schema 1 has no migration from future schemas. Protected recovery requires archiving originals and choosing a known-good backup or new blank layout. Notes and settings are stored locally without encryption; no provider key or chat is saved by WinCC.
7. The portable x64 output is unsigned. CI does not substitute for desktop acceptance or a signing/release process. No installer/uninstaller is provided; portable publishing is the documented initial packaging path.

## Windows verification handoff

Use the entire `artifacts/win-x64` directory. Start `WinCC.exe --windowed`, then exercise Edit/Normal mode with one clock and notes. Exit and start without the flag for desktop tests. Keep the Command Center accessible on the taskbar; closing it exits all widgets. Terminate only WinCC if necessary; Explorer and wallpaper configuration were not changed.

Run `powershell -File tools/collect-windows-evidence.ps1` from the checkout to collect OS/build/GPU inventory and a checklist copy. This helper is provided for Windows and was **not executed here**; it marks no tests passed. Record monitor positions/scaling, Wallpaper Engine state, each actual result and evidence in the [test matrix](TEST-MATRIX.md). In particular, validate G1 before treating later UI work as an accepted desktop product.
