# ADR 0003: Native implementation candidate after owner authorization

- **Status:** Implemented candidate; Windows acceptance pending
- **Date:** 2026-11-05
- **Authorization:** The owner explicitly requested: “Create the entire project now, make sure it works as per the repo docs”. This supersedes the G0-only implementation restriction, not the product exclusions or evidence requirements.

## Decisions

1. Implement the full requested canvas candidate rather than claiming a completed G1/G2 release. G1 remains unpassed until the Windows matrix is exercised. Later functionality exists for review under this broad request, without implying earlier gate acceptance.
2. Use .NET 10 / WPF for the native renderer. Pin the tested SDK in `global.json`. React/WebView2 remain alternative G1 experiments, not dependencies of this candidate. There is no WebView or privileged UI/native message bridge.
3. Use one bounded, non-topmost window per widget instead of a full-monitor input surface. All unoccupied desktop space has no WinCC window. A separate ordinary Command Center provides edit controls and native exit even if a widget fails. Desktop mode maintains the widget group below ordinary windows using `SetWindowPos`; ordinary `--windowed` mode is the fallback. No WorkerW/Progman messages, `SetParent`, Explorer modifications, input hooks or auto-start.
4. Missing layouts start with zero widgets. This follows the accepted freeform/blank-canvas behavior. The exact-one-clock G2 milestone can be exercised by adding one clock. Applying Winter Glass is an explicit, undoable draft operation and never a startup default.
5. Persist schema-1 layout transactions only on Done. One MiB limit, 20 profiles, 100 widgets/profile, bounded settings and 100 undo entries. No schema migration exists yet; unknown versions enter protected recovery mode rather than being overwritten. Keep a previous backup with same-directory, flushed atomic replacement. Widget content autosaves separately. No chat or provider key is persisted by WinCC.
6. Display identity uses `EnumDisplayDevices` interface IDs where supplied, falling back to the OS display name. Coordinates are work-area-local DIPs. Absent-monitor geometry is projected at runtime onto the primary work area without saving a new association. Returning monitors regain their saved placement. Explicit edit/move operations may accept the new display; monitor selection is available in the Command Center. This identity strategy remains subject to physical Windows tests.
7. Optional weather uses Open-Meteo with explicitly entered coordinates and Celsius/Fahrenheit. Disabled by default; 15-minute refresh while visible, explicit manual refresh, bounded HTTP response, cancellation and no automatic location lookup. Attribution is visible; commercial deployments must review provider terms.
8. Optional Spotify uses Windows Global System Media Transport Controls, filtered to Spotify sessions. Disabled by default. Use provider-reported capabilities for playback, seeking, shuffle and repeat; load bounded artwork. No Spotify credentials or web account integration.
9. Optional AI uses the fixed OpenAI Responses HTTPS endpoint, a user-selected model (initial `gpt-5-mini`) and `WINCC_AI_API_KEY` inherited from the process. Requests happen only on Send, carry at most 20 messages, request `store: false`, and expose no tools or native execution. The app stores neither the key nor conversation on disk. Provider retention/usage is governed by its own terms; `store: false` is not a claim of zero provider retention. No arbitrary endpoints or remote scripts.
10. Local launchers are user-chosen existing local `.exe` / `.lnk` targets, with no generated command line or arguments. Shortcuts retain their own Windows-defined targets; users must trust the selected file. No shell replacement or startup registration.

## Consequences and remaining evidence

The source compiles and core/provider contracts run on Linux. This proves neither transparent composition nor Spotify/Windows interaction. WPF glass is tinted alpha, not a claimed native backdrop blur. GPU, temperature and network metrics are explicitly unavailable. Fullscreen detection is sampled, not an exclusive-fullscreen guarantee. Touch, high contrast, display identity stability, input propagation, multi-DPI gesture behavior, virtual desktops and Explorer recovery still require real Windows evidence.

See [validation](../VALIDATION.md) for actual results and [development](../DEVELOPMENT.md) for reproducible commands and recovery. Historical ADR 0001/0002 authorization statements describe the earlier baseline.
