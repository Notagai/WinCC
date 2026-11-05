# WinCC product brief

## Identity and target

**WinCC = Windows Command Center.** Target platform: **Windows 11**.

## Goal

Create a highly customizable home screen and command center that is visually layered over or alongside Wallpaper Engine while remaining an independent Windows application. This is a **desktop experience**, not a Windows shell replacement requirement.

## Core interaction model (approved)

- WinCC is a **blank, freeform desktop canvas**, not a permanently docked two-column dashboard.
- **Normal Mode (default):** widget placement, size, collection, and stacking order are locked; normal widget functions still work, including editing note contents.
- **Edit Mode:** explicitly entered by the user; add, remove, drag, resize and reorder widgets with visible handles, optional alignment guides, Undo/Redo, Cancel, and Done. Done validates and durably saves the draft; Cancel restores the previous saved layout.
- A layout with zero widgets is valid. The approved glass two-column screenshot is an **optional starter preset**, not a constraint.
- See [CANVAS-EDITOR.md](CANVAS-EDITOR.md) for precise state machine, pointer semantics, persistence, and acceptance criteria.

## Future feature targets

- Desktop-level, transparent native surface behind ordinary apps and above the animated wallpaper where feasible.
- Launcher, pinned app shortcuts, search and command controls.
- Widgets: an initial clock, then extensible system, calendar and media modules when requested.
- User-addable, removable, movable, resizable, and reorderable widgets in **Edit Mode only**, with persistent local layouts. Geometry stays locked in Normal Mode.
- Customizable visual design without hardwiring one specific aesthetic.
- Multiple monitors, virtual desktops, Windows display scaling, fullscreen apps and Explorer restarts.
- Wallpaper Engine remains optional and independently controlled; normal Windows usability must not depend on it.
- Stable close/exit behavior and clear configuration options.

## Explicit exclusions

- Custom lock-screen, Windows sign-in, credential, biometric and authentication functionality is **out of scope**.
- No Windows session security, shortcut suppression, or logon integration in WinCC.
- No executable application, UI implementation, installer or background service during Phase 0.
- No Wallpaper Engine injection or dependency on private internals.
- No predetermined theme, account system, telemetry, remote backend or monetization model.

## First authorized engineering milestone

1. Start and close a Windows 11 native desktop host without impairing Explorer.
2. Place a transparent home UI so Wallpaper Engine can remain visible.
3. Enter Edit Mode, drag and resize a clock, select Done, and retain its geometry after restarting WinCC. Verify geometry cannot be changed in Normal Mode.
4. Keep ordinary application windows above the custom desktop surface.
5. Provide an exit/fallback if desktop positioning fails.
6. Document reproducible test results and compatibility limitations.

The owner subsequently authorized implementation (ADR 0003). These remain acceptance criteria requiring Windows evidence; authorization does not establish that they passed. See [validation](VALIDATION.md).
