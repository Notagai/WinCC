# ADR 0001: WinCC planning baseline

- **Status:** provisional; scope clarified
- **Date:** 2026-11-04

## Context

WinCC is a new repository for a Windows Command Center. The owner requested readiness for AI agents before product implementation, and clarified that the project is **home-screen only**.

## Decisions

1. Windows 11 is the initial platform.
2. WinCC covers the custom desktop home screen and command center only.
3. Wallpaper Engine remains independent; no internal integration is assumed.
4. WPF/.NET and WebView2/React are candidate technologies pending testing.
5. Agent instructions and documentation can be prepared now; implementation requires explicit authorization.
6. Custom lock-screen experiences are excluded from WinCC's scope and milestones.
7. Desktop-host stability, multi-monitor behavior and normal Explorer usability are first-class requirements.

## Open decisions

- Native desktop host: WPF vs WinUI 3.
- Window hosting strategy and fallback.
- Frontend and widget APIs.
- Visual style, widgets and layout conventions.
- Multi-monitor, virtual desktop and wallpaper coexistence verification.

New accepted architecture decisions should be recorded as separate ADRs.
