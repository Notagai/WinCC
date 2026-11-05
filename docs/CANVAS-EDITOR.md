# WinCC freeform canvas and Edit Mode — approved product behavior

> **Status:** Owner-approved product interaction specification, documentation only. No executable implementation authorized. This supersedes any earlier statement that WinCC widgets must occupy fixed left/right columns.

## Product model

WinCC is a **freeform desktop widget canvas**, rendered above the independently managed Wallpaper Engine wallpaper and below normal application windows. Users may add, remove, move, resize and arrange widgets **only in Edit Mode**. In **Normal Mode**, geometry and widget collection are locked, but widget functionality remains available.

The earlier two-column screenshot and [dashboard schematic](../design/dashboard-layout.svg) are an **example preset** called *Winter Glass*, not a required layout or permanent docking system. A new user can start from a blank canvas or optionally apply this preset. The owner chooses whether/when to apply a preset; no fixed layout is enforced.

### Core mode invariants

| Behavior | Normal Mode | Edit Mode |
| --- | --- | --- |
| Move widget | Forbidden | Drag widget by edit-only handle/header |
| Resize widget | Forbidden | Drag edit-only edge/corner handles |
| Add widget | Forbidden | Open widget catalog and place an instance |
| Remove widget | Forbidden | Delete selected widget with confirmation/undo |
| Reorder widgets | Forbidden | Bring forward/send backward within WinCC |
| Configure widget appearance | Settings if explicitly allowed | Inspector/controls |
| Use Spotify controls, calendar navigation, notes, AI | Allowed | May preview or configure, with explicit non-drag controls |
| Select text / copy / edit notes | Allowed | Allowed inside explicitly focused content controls |
| Grid, guides, bounding boxes | Hidden | Visible as configured |
| Persist widget geometry | No changes allowed | Commit when user selects **Done** |

**Normal Mode is the default at startup**, including after crashes. The persisted mode is never Edit Mode. Changing to Edit Mode is a deliberate user action.

## Entering and leaving Edit Mode

- Entry: WinCC tray menu **Edit Dashboard** or a small unobtrusive WinCC-owned edit/settings affordance. A Windows desktop context-menu extension is optional and must not be required. No global hotkey interception.
- On entry, snapshot the current saved layout into an in-memory draft; show a clear **Edit Mode** indicator and unobtrusive toolbar: **Add Widget**, **Undo**, **Redo**, **Reset Layout**, **Cancel**, **Done**.
- **Done** validates the draft, durably saves it, then switches to Normal Mode. If save fails, stay in Edit Mode and show a retry/error state; do not falsely claim success.
- **Cancel** discards the draft and restores the exact prior saved layout. If any destructive operation would discard unsaved changes, require confirmation.
- **Escape** during a drag/resize cancels only that gesture; when no gesture is active, Escape may deselect a widget. Escape must **not** silently discard an entire edit session.
- Window focus loss, another application becoming active, or clicking the wallpaper must **not** commit or discard a layout.
- If the app crashes during an uncommitted edit session, restart in Normal Mode using the last durable layout. Do not persist half-dragged geometry.
- Normal Mode must never expose draggable handles or respond to move/resize keyboard shortcuts. Native Windows shortcuts remain untouched.

## Freeform placement

- No required columns, rows, auto-packing, or mandatory grid. Users can position widgets anywhere within the **usable monitor work area**.
- Use monitor-local DIPs as the authoritative layout coordinate space, with host conversion from physical pixels and monitor DPI. The canvas is per monitor; negative virtual-desktop coordinates are supported.
- **Grid snapping is optional**, off by default, with configurable spacing later. Optional smart alignment guides snap to other widgets and monitor edges only while dragging; modifier-key behavior must be documented before implementation.
- Overlap is permitted intentionally. Maintain a widget-local `zIndex` and controls to bring forward/send backward. Overlap must not make a widget impossible to select in Edit Mode.
- When adding a widget, spawn a visible, valid-sized instance near a safe center region of the active monitor or a user-chosen drop point, avoiding taskbar/work-area overlap. Never force a predefined column.
- Widget minimum/maximum size, aspect constraints and content behavior are defined by **widget type**. Never stretch content blindly; reflow calendar, music controls, graphs and notes within supported sizes.
- Keep the widget header/drag handle and at least one resize handle reachable within the work area. Constrain geometry during drag/resize and revalidate on commit.
- On monitor disconnection, remap offscreen widgets to a reachable monitor without destructively overwriting their original monitor association. Restoration policy needs an explicit ADR.

## Pointer, keyboard and touch behavior

- Dragging starts only from an **edit-only drag affordance**, not from Spotify buttons, text, notes editor, calendar dates, or AI chat.
- Pointer down captures the pointer, stores initial rectangle and starting monitor-local position. Pointer move previews only the draft. Pointer up ends the gesture and adds one undo transaction. Pointer cancel, capture loss, or Escape restores the pre-gesture rectangle.
- Resize handles appear at corners and optionally edges when a widget is selected; enforce per-type min/max bounds and the monitor work area.
- Keyboard accessibility: in Edit Mode a focused selected widget supports arrow-key nudge and a documented larger-step modifier; editing shortcuts must not hijack keystrokes while typing in notes or AI chat.
- Undo/Redo operates on **layout edits** (add, remove, move, resize, z-order), not note text or other widget data. Limit history size and reset it on successful Done/Cancel.
- Touch input and high-DPI hit targets must be tested separately; never assume mouse-only interactions suffice.

## Input and desktop interaction

A transparent full-monitor renderer may still capture pointer events in blank areas. The host must experimentally verify real desktop click-through, Explorer icon selection and right-click menus in both modes (G1). A CSS `pointer-events: none` rule alone does not guarantee cross-process Win32 click-through.

- In Normal Mode, only visible interactive widget regions should accept pointer input; all blank canvas regions should behave like the ordinary desktop.
- In Edit Mode, selection and drag operations must not trap users or cover the Windows taskbar/Start menu. If a temporary canvas overlay is used, its input routing and exit affordance must be tested.
- The Dynamic Island is a **special movable/resizable widget** in Edit Mode, with its own safe min/max bounds. In Normal Mode it can expand to show AI chat without changing saved layout geometry; transient expansion must respect work area and avoid trapping focus.
- The canvas itself is **not** a replacement for Windows Explorer, desktop icons, taskbar, or Wallpaper Engine.

## Widget catalog and initial instances

Planned widget types: `clock`, `calendar`, `spotify`, `system`, `weather`, `notes`, `ai-island`. Catalog should be extensible and allow multiple instances where meaningful; enforce singleton constraints explicitly where required (for example, an AI island per monitor if decided). The user chooses which to add. **Blank canvas means zero widgets is a valid layout.**

Each widget has stable `id`, `type`, `monitorKey`, `rectDip`, `zIndex`, `settings`. Geometry changes only through the validated Edit Mode draft transaction. Functional data such as notes content and Spotify playback state is separate from layout and continues updating in Normal Mode.

## State machine

```text
START -> NORMAL (load last durable layout)
NORMAL -- explicit Edit Dashboard --> EDITING (snapshot + draft)
EDITING -- pointer gesture --> EDITING (draft preview, undo transaction)
EDITING -- Cancel --> NORMAL (discard draft)
EDITING -- Done, validated and saved --> NORMAL (new durable layout)
EDITING -- Done, save failure --> EDITING (retry or cancel)
EDITING -- crash --> START -> NORMAL (previous durable layout)
```

Do not write each pointer movement to disk. The saved layout is updated only on successful Done. Widget **content** persistence, especially notes autosave, is independent and can happen in either mode.

## Verification requirements

Add/execute the mode-specific cases in [TEST-MATRIX.md](TEST-MATRIX.md): normal-mode lockout; enter/exit; drag/resize; add/delete; undo/redo; cancel; save failure; crash recovery; multi-monitor bounds; keyboard accessibility; notes editing in Normal Mode; blank canvas click-through.

## Implementation authorization

Current repository state is **G0, specification only**. The G1 desktop-composition spike must establish correct input and z-order behavior. A future G2 should demonstrate the Edit/Normal distinction with one clock widget, but only after an explicit owner-approved implementation task. Do not build the application from this document alone.
