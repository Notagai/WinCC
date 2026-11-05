# WinCC

**WinCC (Windows Command Center)** is a planned customizable **Windows 11 desktop home screen and command center**.

> **Current phase: Phase 0, agent-ready specification only. No application code has been written.**

## Approved dashboard design

The owner approved the **second** blue-gray glass dashboard visualization, over a snowy floating-islands / white-tree Wallpaper Engine wallpaper.

- **Left:** large local clock/date, calendar, Spotify.
- **Right:** live system data, weather, editable freeform notes.
- **Top center:** responsive Dynamic Island that expands into an AI chat.
- **Center:** clear view of Wallpaper Engine's wallpaper.
- **Bottom:** preserve the actual Windows 11 taskbar, not an unrequested custom dock.

See **[dashboard design specification](docs/DASHBOARD-DESIGN.md)** and **[repository-local layout schematic](design/dashboard-layout.svg)**. The schematic is not the original generated raster mockup. Sample values in designs are illustrative.

## Project scope

WinCC will provide a desktop-hosted home screen that coexists with Wallpaper Engine **without integrating with it**, eventually supporting widgets, a launcher, local layouts, custom themes and reliable Windows desktop behavior.

**Not in this repository:** custom lock screen, credentials, biometrics, Windows authentication, sign-in UI, Win+L interception or system-login changes. Those belong to a separate project.

## Start here for AI agents

**Read [AGENTS.md](AGENTS.md) before any change.**

| Document | Contents |
| --- | --- |
| [Dashboard design](docs/DASHBOARD-DESIGN.md) | Approved widget arrangement, behavior and visual style |
| [Dashboard schematic](design/dashboard-layout.svg) | Visual placement reference |
| [Product brief](docs/PRODUCT.md) | Goals and non-goals |
| [Implementation spec](docs/IMPLEMENTATION.md) | Precise gates, Windows APIs, window behavior and build sequence |
| [Contracts](docs/CONTRACTS.md) | Proposed layout JSON, coordinate system and UI/host messages |
| [Windows test matrix](docs/TEST-MATRIX.md) | Manual repro steps, test IDs, expected results and evidence format |
| [Architecture](docs/ARCHITECTURE.md) | Candidate native host and UI layout |
| [Roadmap](docs/ROADMAP.md) | G0-G5 phase approvals |
| [Operational safety](docs/SECURITY.md) | Exit, recovery and data safeguards |
| [Decision log](docs/decisions/0001-planning-baseline.md) | Provisional design choices |

## First engineering deliverable (only after owner approval)

**G1:** Demonstrate a Windows 11 transparent desktop host with real wallpaper visibility, correct interactivity, ordinary app window layering, and safe recovery.

**G2:** Build the smallest usable home screen; broader dashboard features are future authorized tasks.

No build, install, or test commands yet: executable work has not been authorized.
