# WinCC

**WinCC (Windows Command Center)** is a planned, customizable Windows 11 desktop experience.

> **Status: Phase 0, agent-ready planning only.** No WinCC app, lock screen, interception module, installer, or runtime has been implemented.

## Vision

- A custom home screen that coexists with **Wallpaper Engine**, without depending on Wallpaper Engine internals.
- A native Windows-hosted desktop surface with an independently themed widget and launcher UI.
- Future two-stage locking: a **custom first-stage privacy screen** followed by the **actual Windows lock**.
- Potential deeper Windows customization through optional extensions, evaluated and isolated separately.

The first product milestone, once implementation is authorized, will be a transparent home-screen surface with a draggable clock widget and persistent position. The lock screen comes later.

## Working with AI agents

Start with [AGENTS.md](AGENTS.md). It defines the project boundaries and expected workflow for Codex, Copilot, Claude, Cursor, and other contributors.

| Document | Purpose |
| --- | --- |
| [Product brief](docs/PRODUCT.md) | Scope, vision, and non-goals |
| [Architecture](docs/ARCHITECTURE.md) | Proposed components and integration boundaries |
| [Roadmap](docs/ROADMAP.md) | Phases and acceptance gates |
| [Security](docs/SECURITY.md) | Trust model and safety rules |
| [Decision log](docs/decisions/0001-planning-baseline.md) | Initial assumptions and open decisions |

These documents describe **intent**, not an implemented feature set or a final design. Agents must not begin product implementation without an explicit task authorizing it.

## Contribution workflow

Use a short-lived branch for each bounded task, consult the relevant documents, and submit a PR using the included template. Report verified behavior separately from assumptions or untested Windows-specific behavior. Never commit credentials, biometric material, or local machine-specific settings.

## Project status

- [x] Repository guidance for AI agents
- [x] Initial product, architecture, security, and roadmap notes
- [ ] Approved technical spike for desktop layering
- [ ] Native desktop host
- [ ] Widget runtime
- [ ] Lock-screen work

No build or test commands are defined yet because no executable project exists.
