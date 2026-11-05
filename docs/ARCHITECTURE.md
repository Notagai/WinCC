# Architecture proposal (not implemented)

Status: **proposed, subject to Windows 11 proof-of-concept**.

## Conceptual components

1. **Desktop host:** native window lifecycle, positioning, monitor/session event handling.
2. **Home UI:** independent rendering and widgets/launcher experience.
3. **Layout store:** local configuration, widget state, migration path.
4. **Interop adapters:** isolated Win32 APIs and (if explicitly approved) optional Windhawk/other hooks.
5. **Future privacy-lock UI:** custom Stage 1 surface in the logged-in user session.
6. **Future Windows-lock coordinator:** tracks actual session state; native lock is authoritative.

## Candidate stack (not yet selected)

- Desktop host: C# / WPF / modern .NET, or WinUI 3 if later tests justify it.
- UI: WebView2 + TypeScript/React, with a native fallback considered for performance.
- IPC/state: local, versioned contracts; exact mechanism TBD.
- Persistence: local JSON initially; SQLite if state complexity warrants it.
- Shell experimentation: Windows native API first. Windhawk/AutoHotkey/Interception evaluated only as separately testable options.

Record a decision before fixing versions or generating a project.

## Wallpaper and shell layering

Wallpaper Engine owns the wallpaper; WinCC owns the overlay. The app should not alter Wallpaper Engine process state or rely on its private APIs.

Explorer's Progman/WorkerW behavior is undocumented/unstable for permanent app hosting. Treat desktop window embedding, z-order, click-through behavior, taskbar interaction, monitor changes, virtual desktops, and Explorer restarts as proof-of-concept questions. Design a fallback desktop-level window and an obvious exit path.

## Future lock boundaries

**Stage 1:** visual privacy/deterrence in the already signed-in session. A custom PIN or other credential only dismisses this application's overlay.

**Stage 2:** native Windows lock controlled by Windows. A custom credential-provider component, if ever authorized, must operate within Microsoft-supported credential-provider interfaces and preserve built-in authentication.

Native Windows login cannot be treated as a general-purpose customizable WebView. Win+L interception is not guaranteed even with third-party input frameworks. Custom sign-in behavior, secure desktop, sleep/resume, and fallback must be tested independently.

## Operational constraints

- No elevated privileges for the ordinary home-screen host unless there is a proven need.
- Isolate optional shell modifications and privileged integrations from the core application.
- Crash/uninstall must restore an ordinary desktop experience.
- Avoid creating a fake secured state when Windows is actually unlocked.

See docs/SECURITY.md before any lock or input experiment.
