# WinCC home-screen architecture (proposal)

**Status:** candidate architecture; nothing implemented.

## Components

1. **Native desktop host:** desktop window lifecycle and Windows 11 shell interop.
2. **Home UI renderer:** widget and launcher interface, theme and animation layer.
3. **Widget runtime:** component contracts and optional future modules.
4. **Layout/state store:** local widget positions, preferences and versioned migrations.
5. **Windows adapter:** Explorer events, DPI, monitors, virtual desktops, visibility and window order.
6. **Settings/diagnostics:** local configuration, safe exit, and test logs.
7. **Optional shell styling adapter:** Windhawk-related customization only if later approved and justified.

Keep UI, widget state and Windows interop separate so desktop host changes do not require redesigning widgets.

## Candidate technology (not yet locked)

- Host: C#/.NET + WPF; compare with WinUI 3 if technical spike warrants it.
- UI: WebView2 + TypeScript/React; evaluate rendering cost and input transparency.
- Storage: JSON initially; SQLite only if state gets complex.
- Native interop: Win32 via narrow, auditable adapters.
- Optional external customization: Windhawk compatibility, not a required dependency.

Do not scaffold a complete stack until owner authorization and initial feasibility verification.

## Wallpaper Engine layering

- Wallpaper Engine owns the background; WinCC owns the home interface.
- Do not control, inject into, or depend on Wallpaper Engine internals.
- Evaluate normal desktop window, Progman/WorkerW placement and safe fallback approaches in a Windows 11 VM/test machine.
- WorkerW/Progman is undocumented for permanent third-party hosting and may vary between OS builds.
- Ensure interaction, keyboard focus and click-through behavior are deliberate. Do not place the interface always-on-top above regular apps.
- Verify restart recovery, display updates, taskbar interactions and fullscreen behavior.

## Minimum viable app test (future)

A transparent home surface plus one draggable clock widget with persisted position. No automatic startup until the owner explicitly authorizes it. The app exits cleanly and leaves the desktop intact.

## Beyond MVP

Launcher, widget SDK, themes, multiple monitors, accessibility, performance, optional shell appearance integration, and packaging. No other Windows security or sign-in subsystem belongs here.

See docs/SECURITY.md for desktop stability and local data requirements.
