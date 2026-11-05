# WinCC roadmap and authorization gates

**CURRENT STATE: G0.** The owner requested detailed AI-agent readiness, **not application development**. Advancing to a gate needs a specifically authorized engineering task. Read [IMPLEMENTATION.md](IMPLEMENTATION.md) for exact work procedures and [TEST-MATRIX.md](TEST-MATRIX.md) for verification.

## G0 — planning and precise agent instructions (current)

- [x] Canonical instructions (`AGENTS.md`) and agent entry points
- [x] Product, security, architecture and decision documentation
- [x] Precise implementation sequencing and platform API guidance
- [x] Versioned state/bridge contracts and Windows 11 test matrix
- [ ] Explicit owner authorization to start G1

No application code, scaffolding, assets, installer, background service or Windows changes.

## G1 — native Windows desktop composition experiment

1. Windows build/environment inventory.
2. Compare native non-topmost and guarded shell-hosted approaches, each with a safe exit.
3. Validate WPF transparency; compare WebView2, WebView2CompositionControl, native composition if necessary.
4. Check wallpaper visibility, input passthrough, normal foreground windows, Start/taskbar, Explorer restart, renderer failure, force exit.
5. Record evidence, failure modes and an ADR choosing the host/renderer.

**Gate to G2:** Win11-specific required G1 tests pass (or owner explicitly accepts a documented limitation). Do not claim viable composition from HTML screenshots.

## G2 — minimal home MVP

1. One clock widget and draggable interaction with cancel and commit.
2. Stable monitor-local geometry, validated host bridge and layout JSON.
3. Atomic save/restore and crash-safe recovery.
4. Explicit native exit when renderer fails.
5. G2 tests passed on Windows 11.

**Gate to G3:** all required G2 interaction, persistence and desktop safety tests have evidence.

## G3 — expandable home UI

Widgets, launcher, themes, local settings, accessibility and user-approved interfaces. Do not add unnecessary external services.

## G4 — Windows integration and reliability

Multiple monitors/DPI/virtual desktops, Explorer recovery, performance tuning, optional and reversible auto-start.

## G5 — packaging and release

Portable release first; if requested, installer/uninstaller with clean rollback; compatibility verification on supported Windows 11 builds.

**Out of scope at every gate:** lock-screen, Windows sign-in, authentication/credentials and keyboard interception. Another repo handles that project.
