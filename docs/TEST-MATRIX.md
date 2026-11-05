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

## G2 — minimal canvas, mode separation and persistence

| ID | Setup and actions | Expected result |
| --- | --- | --- |
| G2-001 | First launch without layout file | One clock at valid default position in **Normal Mode**, with no edit handles |
| G2-002 | Attempt drag/resize in Normal Mode | Widget geometry cannot change |
| G2-003 | Enter Edit Mode, drag clock, release | Draft preview updates; one undoable move, no disk write yet |
| G2-004 | Enter Edit Mode, resize clock near screen boundary | Widget remains reachable and respects type-specific min/max dimensions |
| G2-005 | Enter Edit Mode, move/resize, select Done, relaunch | New geometry persists and app restarts in Normal Mode |
| G2-006 | Enter Edit Mode, move/resize, force-kill app, relaunch | Previous durable layout restored in Normal Mode; no unsaved draft persists |
| G2-007 | Replace layout with malformed JSON | No crash; error surfaced and recoverable default chosen |
| G2-008 | Put future schemaVersion in layout | Original data preserved, no destructive overwrite |
| G2-009 | Duplicate widget ID or invalid geometry | Validation rejects input; no crash |
| G2-010 | Make layout directory read-only/inaccessible, select Done | Stays in Edit Mode, save failure shown, no false success |
| G2-011 | Disconnect target monitor after saving | Widget moves to usable display; original layout not silently destroyed |
| G2-012 | Use touchpad, mouse and keyboard navigation in both modes | No blocked pointer/focus interactions; accessibility reviewed |
| G2-013 | System time format changes | Clock respects chosen system/12/24h preference |
| G2-014 | Exit WinCC from native control during WebView failure | WinCC process closes and desktop remains usable |

## Exit criteria and evidence

- A gate passes only when **all relevant required IDs** have actual PASS evidence. A BLOCKED test is not PASS; record reason and owner-approved exception if needed.
- Attach an implementation summary: OS build, candidate strategies attempted, selected implementation, rejected options, known limitations and rollbacks.
- In environments without Windows 11 GUI execution, run pure/unit checks if available and mark native checks **NOT RUN**.
- Do not invent FPS, memory, CPU or rendering measurements. Record observed values with sampling method when measured.


## C — freeform canvas, Edit Mode and Normal Mode (planned; all NOT RUN)

| ID | Setup and actions | Expected result |
| --- | --- | --- |
| C-001 | Launch WinCC with previously saved widgets | Starts in **Normal Mode**, no drag/resize handles |
| C-002 | Try dragging, resizing, deleting or reordering a widget in Normal Mode | Geometry and widget collection remain unchanged |
| C-003 | In Normal Mode, type in notes, control Spotify, navigate calendar, open AI chat | Functional widget interactions work; geometry remains locked |
| C-004 | Enter Edit Mode via explicit WinCC control | Edit toolbar/handles appear; draft equals last durable layout |
| C-005 | Drag clock from edit-only handle and release | Draft position updates; one undoable action; no disk save yet |
| C-006 | Resize clock from corner/edge handle | Size respects per-type bounds and work area; widget content reflows |
| C-007 | Start dragging then press Escape or lose pointer capture | Only current gesture cancels; draft and session remain intact |
| C-008 | Change position/size then select **Cancel** | Previous durable layout restored, including z-order; Normal Mode resumes |
| C-009 | Change position/size then select **Done** | Validated layout durably saved; Normal Mode resumes; relaunch retains geometry |
| C-010 | Make layout folder unwritable and select Done | Save failure shown; stays in Edit Mode; no false success |
| C-011 | Force-kill WinCC during unsaved Edit Mode session, relaunch | Last durable layout loads in Normal Mode; no partial edit persisted |
| C-012 | Start with a valid layout containing zero widgets | Empty canvas works, Wallpaper Engine/desktop interaction intact |
| C-013 | In Edit Mode add/remove widgets (G3) | Changes remain in draft until Done; Cancel/Undo restores |
| C-014 | In Edit Mode reorder overlapping widgets (G3) | zIndex only changes WinCC widgets; obscured widgets remain selectable |
| C-015 | Move/resize with optional snapping disabled (G3) | Freeform placement works without mandatory grid |
| C-016 | Move/resize with optional snapping enabled (G3) | Predictable guides/snapping, no unexplained position jumps |
| C-017 | Drag near taskbar and all monitor edges | Handles remain reachable; no window goes irrecoverably offscreen |
| C-018 | Two monitors with negative coordinates and different scaling | Widget geometry maps correctly to chosen monitor |
| C-019 | Use keyboard nudge/undo while editing; type in notes (G3) | Keyboard operations affect intended target only; no text-editor shortcut hijack |
| C-020 | Click blank canvas/desktop icons and native right-click in both modes | No dead zones or unintended input capture; Explorer remains usable |
| C-021 | Expand Dynamic Island in Normal Mode (G3) | AI panel works without changing saved widget rectangle or enabling dragging |
| C-022 | Enter Edit Mode, switch apps, return | Draft retained; no implicit save/cancel on focus change |
| C-023 | Edit Mode reset layout, then Cancel (G3) | Prior saved layout restored; destructive reset is not committed |
| C-024 | Change monitor topology during edit session | Layout remains reachable and can be saved/canceled without data loss |

**Required G2 mode cases:** C-001, C-002, C-004 through C-012, C-017, C-018, C-020 and C-022 where applicable. C-003 and advanced catalog/AI/notes cases are G3 or later when those widgets exist. No Windows-native test has been run at the time this spec was written.
