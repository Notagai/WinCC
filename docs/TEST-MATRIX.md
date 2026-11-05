# WinCC Windows 11 verification matrix

> Test plan for future implementation, **not evidence of passed tests**. Every G1/G2 check starts `NOT RUN`. AI agents must never mark Windows-native tests PASSED without running them on an actual Windows 11 environment.

## Test protocol

For each test run, capture:

```text
Run ID:
Date:
Commit SHA:
Gate: G1 / G2 / G3 / G4
Tester / environment:
Windows 11 edition and OS build (winver):
CPU / GPU:
Monitors (resolution, orientation, physical positions, scaling):
Wallpaper Engine installed/running/wallpaper type:
Host strategy: normal window / WorkerW experiment / fallback
Renderer: WPF only / WebView2 / WebView2CompositionControl / other
Result: PASS / FAIL / BLOCKED / NOT RUN
Expected / actual:
Reproduction steps:
Evidence: screenshot, capture, log path, or notes:
Fallback and remaining limitation:
```

## G1 — composition and desktop behavior

| ID | Setup and actions | Expected result |
| --- | --- | --- |
| G1-001 | Windows desktop wallpaper, launch/exit WinCC host | Transparent content renders, desktop usable on exit |
| G1-002 | Wallpaper Engine actively animating; launch WinCC host | Wallpaper remains visible; WinCC does not control or pause wallpaper |
| G1-003 | Move an ordinary app over WinCC widget | App window remains above home surface |
| G1-004 | Open Start, taskbar menus and system tray | All native UI remains accessible and visible |
| G1-005 | Click widget area, then empty background | Widget interacts; empty background does not create unusable click blocker |
| G1-006 | Right-click desktop icons and empty desktop | Native desktop actions behave as documented for chosen strategy |
| G1-007 | Resize/restore and redraw renderer 20 times | No persistent opaque flash or invalid compositing |
| G1-008 | Run fullscreen or borderless fullscreen application | Home layer neither steals focus nor overlays the app |
| G1-009 | Restart Explorer while WinCC is open | WinCC reattaches safely or falls back without corrupting desktop |
| G1-010 | Kill WinCC process using Task Manager | Windows desktop, wallpaper and taskbar keep functioning |
| G1-011 | End renderer process or simulate renderer initialization failure | Native host does not trap input; exit/recovery remains available |
| G1-012 | Run with Wallpaper Engine absent/stopped | WinCC has documented functional fallback |
| G1-013 | Launch WinCC twice | No duplicate always-on-top desktop surface; behavior documented |
| G1-014 | Compare native, WebView2 and composition-renderer modes | Record transparency, airspace, click behavior, CPU/GPU/RAM tradeoffs |

## G1/G4 — displays, coordinates and lifecycle

| ID | Setup and actions | Expected result |
| --- | --- | --- |
| D-001 | Two displays with different DPI scale | Each display renders with correct bounds and widget scale |
| D-002 | Position secondary monitor to left of primary (negative X) | Position calculations remain correct; no offscreen misclamping |
| D-003 | Use monitor above primary (negative Y) | Correct physical/logical coordinate conversions |
| D-004 | Set screen scaling 100% then 150% | Window and renderer adapt; no double-scale or misalignment |
| D-005 | Disconnect secondary display | Widgets remain accessible, safe fallback occurs |
| D-006 | Reconnect secondary display | Topology refreshes, stored layout is handled predictably |
| D-007 | Change primary monitor | Persisted layouts resolve using documented monitor strategy |
| D-008 | Rotate display landscape ↔ portrait | Window bounds and clamping update |
| D-009 | Sleep then resume | Home returns to stable, usable layout |
| D-010 | Switch Windows virtual desktop | Observe and document WinCC visibility policy; no unexpected input capture |
| D-011 | Change taskbar configuration/work area | Widget clamping respects current usable area or documented behavior |
| D-012 | Change resolution while WinCC is running | No stale HWND geometry and no locked-offscreen control |

## G2 — widget and persistence

| ID | Setup and actions | Expected result |
| --- | --- | --- |
| G2-001 | First launch without layout file | One clock at valid default position |
| G2-002 | Drag clock then release | Stable preview; final location committed |
| G2-003 | Begin drag then press Escape | Last committed position restored |
| G2-004 | Drag near each screen boundary | Widget remains reachable and within defined work area |
| G2-005 | Drag clock, close app normally, relaunch | Last committed position restored |
| G2-006 | Drag clock, forcibly kill app, relaunch | Last durably saved state or safe prior copy restored |
| G2-007 | Replace layout with malformed JSON | No crash; error surfaced and recoverable default chosen |
| G2-008 | Put future schemaVersion in layout | Original data preserved, no destructive overwrite |
| G2-009 | Duplicate widget ID or invalid geometry | Validation rejects input; no crash |
| G2-010 | Make layout directory read-only/inaccessible | UI continues; clear local save error |
| G2-011 | Disconnect target monitor after saving | Widget moves to usable display; original layout not silently destroyed |
| G2-012 | Use touchpad, mouse and keyboard navigation | No blocked pointer or focus interactions; accessibility reviewed |
| G2-013 | System time format changes | Clock respects chosen system/12/24h preference |
| G2-014 | Exit WinCC from native control during WebView failure | WinCC process closes and desktop remains usable |

## Exit criteria and evidence

- A gate passes only when **all relevant required IDs** have actual PASS evidence. A BLOCKED test is not PASS; record reason and owner-approved exception if needed.
- Attach an implementation summary: OS build, candidate strategies attempted, selected implementation, rejected options, known limitations and rollbacks.
- In environments without Windows 11 GUI execution, run pure/unit checks if available and mark native checks **NOT RUN**.
- Do not invent FPS, memory, CPU or rendering measurements. Record observed values with sampling method when measured.
