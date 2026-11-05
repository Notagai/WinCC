# WinCC

**WinCC (Windows Command Center)** is a planned customizable **Windows 11 desktop home screen and command center**.

> **Current phase: Phase 0, agent-ready specification only. No application code has been written.**

## Approved dashboard: blank canvas with two modes

**WinCC is a freeform widget canvas**, not a fixed two-column dashboard.

- **Normal Mode (default):** widgets remain fixed in place and cannot be moved, resized, added or removed. Their controls still work, including editing notes and opening AI chat.
- **Edit Mode:** add, move, resize, remove and reorder widgets with visible handles, optional guides, Undo/Redo, **Done** to save, and **Cancel** to revert.
- **Blank canvas is valid:** start with zero widgets or choose an optional preset. The blue-gray glass mockup is a *Winter Glass example layout*, not a mandatory arrangement.
- **Available planned widgets:** date/time, calendar, Spotify, system metrics, weather, editable notes and expandable AI Dynamic Island.
- Wallpaper Engine remains visible; the real Windows 11 taskbar stays intact.

See **[freeform canvas specification](docs/CANVAS-EDITOR.md)**, **[dashboard visual style](docs/DASHBOARD-DESIGN.md)**, and **[example preset diagram](design/dashboard-layout.svg)**. Sample data in design images is illustrative.

## Project scope

WinCC will provide a desktop-hosted home screen that coexists with Wallpaper Engine **without integrating with it**, eventually supporting widgets, a launcher, local layouts, custom themes and reliable Windows desktop behavior.

**Not in this repository:** custom lock screen, credentials, biometrics, Windows authentication, sign-in UI, Win+L interception or system-login changes. Those belong to a separate project.

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

## First engineering deliverable (only after owner approval)

**G1:** Demonstrate a Windows 11 transparent desktop host with real wallpaper visibility, correct interactivity, ordinary app window layering, and safe recovery.

**G2:** Build the smallest usable home screen; broader dashboard features are future authorized tasks.

No build, install, or test commands yet: executable work has not been authorized.
