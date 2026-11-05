# WinCC product brief

## Identity and target

**WinCC = Windows Command Center.** Target platform: **Windows 11**.

## Goal

Create a highly customizable home screen and command center that is visually layered over or alongside Wallpaper Engine while remaining an independent Windows application. This is a **desktop experience**, not a Windows shell replacement requirement.

## Future feature targets

- Desktop-level, transparent native surface behind ordinary apps and above the animated wallpaper where feasible.
- Launcher, pinned app shortcuts, search and command controls.
- Widgets: an initial clock, then extensible system, calendar and media modules when requested.
- Movable, resizable, hideable widgets and persistent local layout settings.
- Customizable visual design without hardwiring one specific aesthetic.
- Multiple monitors, virtual desktops, Windows display scaling, fullscreen apps and Explorer restarts.
- Wallpaper Engine remains optional and independently controlled; normal Windows usability must not depend on it.
- Stable close/exit behavior and clear configuration options.

## Explicit exclusions

- All custom lock-screen, Windows sign-in, credential, biometric, and authentication functionality belongs to a **different project/repository**.
- No Windows session security, shortcut suppression, or logon integration in WinCC.
- No executable application, UI implementation, installer or background service during Phase 0.
- No Wallpaper Engine injection or dependency on private internals.
- No predetermined theme, account system, telemetry, remote backend or monetization model.

## First authorized engineering milestone

1. Start and close a Windows 11 native desktop host without impairing Explorer.
2. Place a transparent home UI so Wallpaper Engine can remain visible.
3. Drag a clock widget and retain its location after restarting WinCC.
4. Keep ordinary application windows above the custom desktop surface.
5. Provide an exit/fallback if desktop positioning fails.
6. Document reproducible test results and compatibility limitations.

These are acceptance criteria for future approved work, not implementation authorization.
