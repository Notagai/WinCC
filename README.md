# WinCC

**WinCC (Windows Command Center)** is a customizable **Windows 11 desktop home screen and command center**.

> **Implementation candidate:** native WPF application, layout engine, widgets, optional provider adapters, tests and portable packaging. Windows 11 desktop acceptance gates remain **NOT RUN**. See [development instructions](docs/DEVELOPMENT.md) and [validation evidence](docs/VALIDATION.md).

## Build and run

On Windows 11, install the .NET SDK pinned in [global.json](global.json), then:

```powershell
dotnet build WinCC.sln -c Release
.\tools\dev-test.ps1
dotnet run --project src/WinCC.Host -c Release -- --windowed
```

The dev test command runs the dependency-free layout and provider contract suite; it does not require Windows. On Linux, use `bash tools/cloud-dotnet.sh run --project tests/WinCC.Layout.Tests -c Release`.

Start in ordinary window mode first. Omit `--windowed` to try experimental desktop positioning. The Command Center stays available on the taskbar; closing it minimizes the Command Center; Exit WinCC closes all widgets. Nothing changes Explorer, Wallpaper Engine or Windows startup settings.

For a portable build that includes the runtime:

```powershell
dotnet publish src/WinCC.Host -c Release -r win-x64 --self-contained true -o artifacts/win-x64
.\artifacts\win-x64\WinCC.exe --windowed
```

The canvas starts blank. Choose **Edit Dashboard**, add widgets or apply **Winter Glass**, arrange them, then **Done**. **Cancel** restores the saved layout; notes autosave independently. Weather and Spotify are opt-in under **Optional connections**. AI requires `WINCC_AI_API_KEY` in the app process environment and sends messages only when requested. See [provider setup](docs/DEVELOPMENT.md#optional-providers).

## Approved dashboard: blank canvas with two modes

**WinCC is a freeform widget canvas**, not a fixed two-column dashboard.

- **Normal Mode (default):** widgets remain fixed in place and cannot be moved, resized, added or removed. Their controls still work, including editing notes and opening AI chat.
- **Edit Mode:** add, move, resize, remove and reorder widgets with visible handles, optional guides, Undo/Redo, **Done** to save, and **Cancel** to revert.
- **Blank canvas is valid:** start with zero widgets or choose an optional preset. The blue-gray glass mockup is a *Winter Glass example layout*, not a mandatory arrangement.
- **Widget catalog:** date/time, local calendar, Spotify, system metrics, weather, editable notes and expandable AI Dynamic Island. Unconfigured or unavailable providers show explicit status.
- Wallpaper Engine remains visible; the real Windows 11 taskbar stays intact.

See **[freeform canvas specification](docs/CANVAS-EDITOR.md)**, **[dashboard visual style](docs/DASHBOARD-DESIGN.md)**, and **[example preset diagram](design/dashboard-layout.svg)**. Sample data in design images is illustrative.

## Project scope

WinCC will provide a desktop-hosted home screen that coexists with Wallpaper Engine **without integrating with it**, eventually supporting widgets, a launcher, local layouts, custom themes and reliable Windows desktop behavior.

**Not in this repository:** custom lock screen, credentials, biometrics, Windows authentication, sign-in UI, Win+L interception or system-login changes. These are explicitly outside WinCC's scope.

## Start here for AI agents

**Read [AGENTS.md](AGENTS.md) before any change.**

| Document | Contents |
| --- | --- |
| [Canvas and Edit Mode](docs/CANVAS-EDITOR.md) | Normative freeform layout, Normal/Edit modes, save/cancel and resize rules |
| [Dashboard design](docs/DASHBOARD-DESIGN.md) | Approved widgets, optional Winter Glass preset and visual style |
| [Dashboard schematic](design/dashboard-layout.svg) | Example optional starter preset, not fixed placement |
| [Product brief](docs/PRODUCT.md) | Goals and non-goals |
| [Implementation spec](docs/IMPLEMENTATION.md) | Precise gates, Windows APIs, window behavior and build sequence |
| [Contracts](docs/CONTRACTS.md) | Proposed layout JSON, coordinate system and UI/host messages |
| [Windows test matrix](docs/TEST-MATRIX.md) | Manual repro steps, test IDs, expected results and evidence format |
| [Architecture](docs/ARCHITECTURE.md) | Candidate native host and UI layout |
| [Roadmap](docs/ROADMAP.md) | G0-G5 phase approvals |
| [Operational safety](docs/SECURITY.md) | Exit, recovery and data safeguards |
| [Decision log](docs/decisions/0001-planning-baseline.md) | Provisional design choices |

## Acceptance status

**G1 remains unverified:** Windows 11 transparency, interactivity, ordinary app window layering, and recovery need real desktop evidence.

**G2/G3 candidate:** canvas transactions, seven widget types, local persistence and optional integrations are implemented. Automated core/provider contracts pass; this does not pass the Windows UI test matrix.

The owner authorized implementation after the original G0 planning baseline. [ADR 0003](docs/decisions/0003-native-implementation-candidate.md) records implementation choices and remaining release gates.
