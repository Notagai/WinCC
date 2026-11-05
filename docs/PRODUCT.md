# Product brief

## Identity

**WinCC = Windows Command Center.** Target platform: **Windows 11**.

## Goal

Offer a customizable Windows home-screen experience layered over or alongside Wallpaper Engine while remaining an independent product. Later, extend this with a custom first-stage privacy screen and a separate native Windows lock experience.

## Desired home-screen capabilities (future)

- Transparent desktop surface, behind ordinary application windows and above the wallpaper where feasible.
- Application launcher and pinned shortcuts.
- Movable, resizable widgets (first proof: clock).
- Local layout persistence.
- Configurable visual design independent of native Windows integration.
- Multi-monitor behavior, Explorer restart recovery, and compatibility with fullscreen apps.
- No requirement for Wallpaper Engine to be installed to keep Windows usable.

## Future lock concept (not for first milestone)

- Stage 0: Windows session unlocked, home screen running.
- Stage 1: Custom full-screen privacy UI with independently configured credentials. This is **not** Windows session security.
- Stage 2: Actual Windows session locked; Windows remains authoritative for native sign-in.
- A first Win+L to Stage 1 and second Win+L to Stage 2 is the **desired user experience**, not a verified or promised capability. Windows-reserved shortcut interception is an experimental feasibility question.
- User may later configure their own Stage 1 authentication methods. No credentials are to be requested, stored, or implemented during Phase 0.

## Explicit non-goals (current phase)

- No executable code, UI mockup, Windows service, installer, credential provider, or authentication implementation.
- No Wallpaper Engine integration or injection.
- No replacing Winlogon or disabling native Windows unlock.
- No fixed aesthetic, widget catalog, telemetry, cloud account system, or proprietary service requirement.

## Success criteria for the first authorized engineering milestone

1. A Windows 11 host can display a transparent desktop UI without replacing Windows Explorer.
2. Wallpaper Engine (if present) remains visible and independent.
3. A clock widget can be dragged; its position survives restart.
4. Ordinary application windows remain above the home screen.
5. The app can exit without damaging or impairing the normal Windows desktop.
6. Tested cases and unresolved compatibility issues are documented.

Until approved, these are acceptance criteria for **future work**, not work to start.
