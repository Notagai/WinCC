# WinCC

**WinCC (Windows Command Center)** is a planned customizable **Windows 11 home screen and desktop command center**.

> **Status: Phase 0, AI-agent repository preparation only.** No executable product is implemented.

## Vision

- Customizable home screen layered over or alongside **Wallpaper Engine**, without integrating with its internals.
- Native Windows-hosted desktop surface with a flexible widget system, app launcher, and command-center UI.
- Independent themes, local layout persistence, and reliable Windows desktop behavior.

**Scope boundary:** WinCC is only for the home screen/command center. A custom lock-screen project belongs in a different repository, if pursued later.

## AI agent entry point

Read [AGENTS.md](AGENTS.md) first. This is the canonical workflow and scope guide for coding agents, including Codex, Claude Code, GitHub Copilot, and Cursor.

| Document | Purpose |
| --- | --- |
| [Product brief](docs/PRODUCT.md) | Goals, requirements, and exclusions |
| [Architecture](docs/ARCHITECTURE.md) | Proposed home-screen components |
| [Roadmap](docs/ROADMAP.md) | Phased implementation and acceptance gates |
| [Safety](docs/SECURITY.md) | Desktop stability and local-data safeguards |
| [Decisions](docs/decisions/0001-planning-baseline.md) | Initial architecture assumptions |

All stack choices are provisional until tested and approved. Agents must not implement product features without an explicitly scoped task.

## First engineering milestone (future)

A transparent Windows 11 desktop surface with a movable clock widget whose position persists, while Wallpaper Engine remains independent and ordinary application windows behave normally.

## Repository status

- [x] Agent instructions, planning documents, and task templates
- [ ] Approved desktop-layer technical spike
- [ ] Native desktop host
- [ ] Home-screen widgets and launcher
- [ ] Customization and resilience testing

No build or test commands exist yet because this is a planning-only repository.
