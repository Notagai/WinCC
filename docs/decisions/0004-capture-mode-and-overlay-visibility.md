# 0004 — Explicit capture mode and desktop visibility

Status: implementation candidate, 2026-11-07. Native Windows acceptance pending.

The owner reports that the clock disappears during Alt-Tab and snipping. Live optical sampling needs capture exclusion to avoid sampling the clock recursively, but that same exclusion removes it from supported screenshots. The previous fullscreen heuristic also hid the clock when system overlays filled the display.

Remove explicit fullscreen polling/hiding and rely on ordinary desktop z-order below applications. Do not introduce a permanent topmost overlay. Add an explicit screenshot mode that freezes optical rendering before removing capture exclusion. Resume restores exclusion before sampling. Keep this mode transient and return to live mode when rebuilding windows.

This deliberately replaces the automatic fullscreen-hide candidate behavior. It does not guarantee visibility above system UI or change virtual-desktop policy. Screenshot mode freezes background and time-glyph optics; it does not continuously capture while the clock is included. No samples are persisted or transmitted.

Validate G1 activation/fullscreen behavior and snipping on a real Windows 11 build, including animated wallpaper, repeated mode toggles, startup without a frame and exit. These checks are NOT RUN on the Linux build machine. Rollback is to revert this commit; existing layouts/settings need no migration.
