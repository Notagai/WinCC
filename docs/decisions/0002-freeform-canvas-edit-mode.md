# ADR 0002: Freeform widget canvas and explicit Edit Mode

- **Status:** Accepted product behavior, implementation pending
- **Date:** 2026-11-05

## Context

An earlier dashboard visualization used two fixed columns around a wallpaper focal point. The owner clarified that WinCC should instead be a **blank canvas** where widgets can be placed and resized in Edit Mode, and cannot be repositioned or resized in Normal Mode.

## Decision

1. Freeform placement is the primary model; no mandatory columns or grid. Empty layouts are valid.
2. Normal Mode is the default after startup, including crash recovery. Widget functionality continues, including editing notes content.
3. Only explicit Edit Mode allows adding, removing, moving, resizing and reordering widgets.
4. Edit Mode stages layout changes in memory. **Done** validates and durably saves; **Cancel** discards. Undo/Redo applies to layout edits.
5. The prior two-column screenshot and repository SVG are an **optional Winter Glass starter preset**, not a layout restriction.
6. Widget geometry uses monitor-local DIPs, per-type size constraints and host validation. Optional snapping, not mandatory grid.
7. Wallpaper Engine, Explorer desktop and native Windows taskbar remain independent.

## Consequences

- G2's clock milestone must demonstrate Normal/Edit mode separation, drag/resize and Done/Cancel, not merely drag-and-save.
- G3 adds the catalog, multi-widget editing and optional alignment guides.
- All implementation details and tests must follow [CANVAS-EDITOR.md](../CANVAS-EDITOR.md).
- No executable work is authorized by this ADR. WinCC remains at G0.

## Open implementation decisions

- Exact edit-entry UI (tray vs small WinCC control), with no global input interception.
- Optional snapping defaults and keyboard nudge increments.
- How multiple monitors, lost monitors and widget overlap selection behave in final implementation.
- Exact per-widget minimum/maximum sizes and expansion policies.
