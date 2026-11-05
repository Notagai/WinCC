# WinCC

**WinCC (Windows Command Center)** is a planned customizable **Windows 11 desktop home screen and command center**.

> **Current phase: Phase 0, agent-ready specification only. No application code has been written.**

## Project scope

WinCC will provide a desktop-hosted home screen that coexists with Wallpaper Engine **without integrating with it**, eventually supporting widgets, a launcher, local layouts, custom themes and reliable Windows desktop behavior.

**Not in this repository:** custom lock screen, credentials, biometrics, Windows authentication, sign-in UI, Win+L interception or system-login changes. Those belong to a separate project.

## Start here for AI agents

**Read [AGENTS.md](AGENTS.md) before any change.** It makes phase boundaries and verification requirements explicit.

| Document | Contents |
| --- | --- |
| [Product brief](docs/PRODUCT.md) | Goals and non-goals |
| [Implementation spec](docs/IMPLEMENTATION.md) | Precise gates, Windows APIs, window behavior and build sequence |
| [Contracts](docs/CONTRACTS.md) | Proposed layout JSON, coordinate system and UI/host messages |
| [Windows test matrix](docs/TEST-MATRIX.md) | Manual repro steps, test IDs, expected results and evidence format |
| [Architecture](docs/ARCHITECTURE.md) | Candidate native host and UI layout |
| [Roadmap](docs/ROADMAP.md) | G0-G5 phase approvals |
| [Operational safety](docs/SECURITY.md) | Exit, recovery and data safeguards |
| [Decision log](docs/decisions/0001-planning-baseline.md) | Provisional design choices |

## First engineering deliverable (only after owner approval)

**G1:** Demonstrate a Windows 11 transparent desktop host with real wallpaper visibility, correct interactivity, ordinary app window layering, and safe recovery. Choose a renderer based on measured G1 results rather than assumption.

**G2:** Build the smallest usable home screen: one draggable clock widget, persisted position, and an obvious exit mechanism.

No build, install, or test commands yet: executable work has not been authorized.
