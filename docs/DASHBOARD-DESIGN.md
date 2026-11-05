# WinCC dashboard — approved visual direction

**Status:** Design direction approved by owner; **documentation only**. No code or product implementation authorized. **Reference:** the *second* generated dashboard mockup in the design conversation, over the user's existing snowy floating-island / white-tree Wallpaper Engine desktop. The image is a visual reference, not a literal data source.

> The accompanying [layout diagram](../design/dashboard-layout.svg) is a **vector schematic** committed to GitHub so agents have a repository-local visual reference. It is not a pixel-perfect copy of the generated screenshot or the user's wallpaper.

## Core composition: freeform canvas, not fixed columns

**The owner clarified that WinCC must be a blank, freeform widget canvas.** Widgets can be added, moved, resized, removed, and reordered **only in Edit Mode**; Normal Mode locks geometry while keeping widget controls and editable notes functional. See **[CANVAS-EDITOR.md](CANVAS-EDITOR.md)** for the normative behavior.

The previously approved second mockup is an **optional Winter Glass starter preset**: it illustrates a tasteful arrangement, not enforced docking. Users can start with zero widgets and position any available widget anywhere within the usable work area. A central wallpaper focal point is a *preset design choice*, not a hard constraint.

### Optional Winter Glass preset: left side

1. **Date/time:** prominent local time (hours and minutes; 12/24-hour preference), day, weekday and full date. Date/time are actual system values, not hardcoded mockup data.
2. **Calendar:** compact month grid immediately below the time. Today highlighted; month navigation; optional event indicators and selected-day agenda. Initial data source and event sync permissions remain undecided; a functioning local month calendar must not require cloud integration.
3. **Spotify:** compact playback panel with cover art, title, artist, seek/progress, previous/play-pause/next, shuffle/repeat where supported. When no playback session is present, show a calm disconnected/idle state rather than fake track data. Spotify API and Geekify integration require a separate authorization and feasibility decision; do not assume full playback control is available.

### Optional Winter Glass preset: right side

1. **System data:** live CPU, GPU, RAM, optionally temperatures and network metrics; compact graphs with labels and units. No invented readings. Unavailable metrics must say unavailable instead of 0. Polling must be efficient and stop when hidden.
2. **Weather:** current temperature, conditions, location, and short forecast. User-configurable location and units. No location inferred from wallpaper or mockup. Weather provider and key storage must be selected separately.
3. **Notes:** **editable freeform note**, not merely a checklist. Support multiline text, autosave, restore, keyboard selection and copy/paste, visible unsaved/error state, and a deliberate reset/export option later. Store locally by default. Notes must be independent of calendar/tasks.

### Optional Winter Glass preset: top center

**Responsive Dynamic Island / AI chat launcher.** In idle state it is a slim, centered pill with an AI/chat affordance. Click or keyboard activation expands it into a chat input/panel. It must animate smoothly, close/collapse predictably, support Escape, keyboard focus, and accessibility. It should not permanently obstruct the wallpaper or native Windows controls. Actual model/provider/API-key handling is a later separately approved feature; never embed secrets in frontend assets.

### Bottom

**Preserve the actual Windows 11 taskbar initially.** The generated second mockup includes a custom-looking app dock, but that is **not** an approved replacement for Explorer's taskbar. Do not implement a dock or taskbar replacement unless separately requested.

## Style guide

- **Visual tone:** soft, translucent blue-gray glassmorphism over cool snowy wallpaper; subtle purple/lavender accent and restrained cyan details.
- **Cards:** rounded corners, gentle blur/tint, fine border, subtle shadow; avoid heavy opaque backgrounds. Maintain text contrast over bright snow and dark mountains.
- **Typography:** clean modern sans-serif; large time; small but legible data labels; avoid glowing or blurry text.
- **Motion:** subtle hover, expansion and dragging transitions; honor reduced-motion setting. No constant distracting effects.
- **Spacing:** consistent gutters and safe distance from native taskbar and screen edges in the optional preset. User-defined layouts may use arbitrary spacing.
- **Wallpaper:** center composition including the white tree must remain largely unobstructed. WinCC does not replace or alter Wallpaper Engine wallpaper.

## Optional preset geometry (not canvas constraints)

Design reference: **1650 × 928 screenshot** (approximately 16:9). Coordinates below are **normalized fractions of usable monitor work area**, not fixed pixel measurements or mandatory CSS dimensions.

- For the optional Winter Glass preset only, outer margin: 1.5–2.0% of width.
- For the optional preset only, side card width: approximately 19–22% of width at 16:9; the user can freely resize each widget in Edit Mode within type-specific constraints.
- Left stack: date/time at top, calendar beneath, music beneath calendar; allow vertical scrolling or collapsed cards when space is insufficient.
- Right stack: system data, weather, notes.
- Dynamic Island: centered horizontally at top, idle width approximately 25–33% of viewport, expanding within safe bounds.
- Center clear zone: aim to leave at least 50% of width mostly unobstructed at standard desktop widths.
- At narrow viewports or high scaling, individual widgets must reflow their contents, remain reachable, and avoid the native taskbar. Intentional widget overlap is permitted; no forced column rearrangement.
- Use per-monitor DIP coordinates and host-owned display geometry per docs/CONTRACTS.md. No hardcoded 1920×1080 assumptions.

## Widget state and behavior requirements

Each card is independent and can load/fail without taking down the host. Every network-backed card needs loading, unavailable, permission-denied, offline, and error states. Every live-data widget needs a defined refresh interval and cancellation behavior. User notes must never be lost because Spotify/weather fails. The optional starter preset is **not fixed**. Normal Mode prevents repositioning/resizing; Edit Mode permits freeform add/move/resize/remove with saved and cancelable changes. Notes content remains editable in Normal Mode.

The image's sample time, date, temperatures, metrics, songs, artwork, and note text are **illustrative only**. Do not hardcode or claim they are real user data.

## Implementation sequencing (after explicit authorization)

1. **G1:** prove transparent desktop hosting, z-order, mouse interaction, wallpaper visibility, renderer reliability.
2. **G2:** establish card design system and real clock/calendar with local persistence and notes autosave, if the owner approves this as the updated G2 scope. Otherwise retain G2's existing minimal clock milestone.
3. **G3:** system metrics and widget lifecycle; weather provider; Spotify connection and permission model; AI island UI and provider integration, each independently reviewed.
4. **G4/G5:** responsive monitor/DPI support, performance budgets, fallback, packaging.

**Important:** This design does **not** automatically override the currently approved gate boundaries in docs/IMPLEMENTATION.md. Any expansion of G2 requires a separately scoped task/ADR.

## Acceptance criteria for eventual dashboard

- Optional Winter Glass preset resembles the **second** approved mockup; freeform canvas can be blank or arranged however the user chooses.
- Local time/date and calendar are correct, responsive, and accessible.
- Notes are directly editable, save locally, and survive restart, with clear save-error behavior.
- System metrics are real, unit-labeled, and degrade gracefully when unavailable.
- Weather and Spotify show explicit disconnected states until configured; no fake values.
- Dynamic Island opens/closes AI chat UI, supports keyboard and Escape, and does not intercept unrelated desktop interactions.
- Wallpaper Engine and normal Windows taskbar remain independent and usable.
- No custom lock-screen, credential, or Windows login functionality.

## Design authority

**CANVAS-EDITOR.md overrides all fixed-placement language** in the earlier screenshot-derived design. The screenshot and SVG are examples of one layout, not the layout engine. Edit Mode and Normal Mode must behave as specified before considering a dashboard implementation complete.
