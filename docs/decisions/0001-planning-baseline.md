# ADR 0001: Initial planning baseline

- **Status:** provisional planning baseline
- **Date:** 2026-11-04

## Context

WinCC is a new Windows Command Center repository. The owner requested agent readiness first, not product implementation.

## Decisions

1. Windows 11 is the initial target platform.
2. The home screen is first; the custom lock screen comes later.
3. Wallpaper Engine runs independently; WinCC is an overlay or complementary desktop host, not a Wallpaper Engine extension.
4. Native Windows remains authoritative for actual session locking and unlocking.
5. Two-stage locking is the desired later experience. Reserved Win+L interception is experimental and is not a guaranteed engineering requirement until validated.
6. WPF/.NET with WebView2/React is a **candidate architecture**, not a final stack lock.
7. Agents may update docs and task scaffolding in Phase 0, but no application, driver, lock, or installer code without explicit owner authorization.

## Open decisions

- Final native UI host (WPF versus WinUI 3).
- Desktop placement technique and fallback.
- Chosen frontend and widget model.
- Visual direction and layout conventions.
- Supported multi-monitor and virtual desktop behaviors.
- Whether to prototype any hotkey interception in a VM after the home-screen milestone.

Update with a new ADR when one of these decisions is made rather than quietly rewriting the history.
