# WinCC architecture overview

**Status:** Proposed, unimplemented. Read [IMPLEMENTATION.md](IMPLEMENTATION.md) for normative work packages and [CONTRACTS.md](CONTRACTS.md) for planned message and state schemas.

## Mission and boundary

WinCC provides **only** a Windows 11 home-screen/command-center UI. Its primary interaction model is a **freeform widget canvas** with locked Normal Mode and explicitly entered Edit Mode; see [CANVAS-EDITOR.md](CANVAS-EDITOR.md). Wallpaper Engine is independent and optional; Explorer/taskbar remain normal. Any lock-screen or sign-in work belongs to another repo.

## Process/modules (future)

```text
Windows 11: Explorer / DWM / normal apps
        |
        +-- Wallpaper Engine (independent wallpaper process, if installed)
        |
        +-- WinCC.Host (normal user process)
             |-- DesktopInterop (placement, display/DPI, shell recovery)
             |-- UI Renderer (WPF + candidate WebView2 composition surface)
             |-- Widget Runtime (clock first, launcher later)
             |-- Layout Store (local versioned JSON)
             |-- Diagnostics and native recovery/exit
```

No privileged service or driver is required for the current goal.

## Required architectural separations

- Desktop placement strategy is replaceable. UI code cannot know WorkerW/Progman details.
- The host validates and owns window geometry, monitoring and persistence. Edit Mode mutations are staged in an in-memory draft and durably committed only on Done; Normal Mode rejects geometry changes.
- The frontend may request a widget move but cannot directly invoke arbitrary Win32 APIs, launch programs or change HWND parenting.
- Renderer and native host lifecycles are separate enough to exit when browser rendering fails.
- Monitor service supplies physical bounds, DPI and persistent logical monitor mapping.
- Layout store works without Windows APIs so validation/migrations can be unit tested on any developer machine. A zero-widget layout is valid; the approved two-column mockup is an optional preset, not a forced layout.

## G1 unresolved platform risks

Windows `WorkerW`/`Progman` desktop hosting is undocumented; a normal window may also fail required ordering. Both must be tested. A WPF WebView2 surface may conflict with layered window composition, despite a transparent browser background. Standard WPF WebView2 uses `HwndHost`, and composition control avoids some airspace problems but is not guaranteed to provide desired desktop/input behavior.

The **first decision** is which host + renderer combination actually works on the target Windows 11 build. Do not begin a full React widget app before proving this.

## Technical candidates

WPF/.NET host; WebView2CompositionControl (compare standard WebView2); React/TypeScript UI only after verified host; Win32 native interop; local JSON state. Keep these as **candidates** until an owner-reviewed G1 ADR.

## Evidence and error handling

Every system-level change gets reproducible Windows 11 test steps and test IDs from [TEST-MATRIX.md](TEST-MATRIX.md). Desktop failures must fail visibly and recoverably, not leave hidden input-capturing windows. Crash/exit must never impair Windows Explorer or independent wallpapers.
