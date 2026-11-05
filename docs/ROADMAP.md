# WinCC roadmap

## Phase 0: agent-ready planning (**current**)

- [x] Canonical instructions and agent entry points
- [x] Product/architecture/safety documentation and decision record
- [x] Issue/PR templates
- [ ] Owner-authorized technical spike and confirmed technology choices

**Gate:** This document does not authorize implementation. Wait for a specific, explicitly scoped engineering task.

## Phase 1: Windows 11 desktop layering spike (future)

Compare native host window placement options, wallpaper visibility, input routing, window z-order, Explorer restart, virtual desktops and multi-monitor behavior.

**Exit criteria:** reproducible Windows 11 tests with OS build, failed-case notes and safe exit/fallback.

## Phase 2: minimum home-screen MVP (future)

Transparent native desktop host, one draggable clock widget, persistent widget position. Wallpaper Engine remains a separate process. No additional product modules unless requested.

**Exit criteria:** ordinary windows work, wallpaper remains visible, layout survives restart and exit restores normal desktop behavior.

## Phase 3: customizable home (future)

Widget architecture, launcher, search/shortcuts, saved layouts, themes, animations, accessibility.

## Phase 4: Windows integration and reliability (future)

Optional startup registration, display/DPI/virtual desktop handling, Explorer recovery, fullscreen performance and settings.

## Phase 5: packaging and testing (future)

Diagnostics, clean uninstall, compatibility tests, versioned config migration and performance benchmarks.

## First recommended engineering task

'Investigate Windows 11 desktop layering for WinCC and build the smallest approved proof-of-concept host.' The owner must authorize implementation first and define verification criteria.

**Out of scope at every phase:** custom lock screen, Windows authentication, login UI and credential systems. Those belong to a separate repository.
