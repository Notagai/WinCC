# 0006 — Preserve surface detail at native resolution

Status: implementation candidate, 2026-11-07. Native Windows acceptance pending.

The owner still reports poor quality when scaling the clock. ADR 0005 made image output and glyph coverage native, but retained a 768px surface grid with nearest-pixel mask reduction. Two new regression cases demonstrate lost contour lighting and inconsistent surface profiles under one-pixel movement.

Replace the coarse grid with native-resolution glyph surface construction and a compact cache. Each output pixel stores packed signed normal components and a half-precision rim, totaling six bytes. Distance calculations use a single interior map and float intermediate storage; separable blur traverses vertical data in contiguous row sections. The capsule is evaluated analytically instead of using an enlarged field. Cache clear-label dimming in separable dimension-sized tables and enable layout rounding/device snapping for the glass control.

This supersedes ADR 0005's bounded 768px geometry choice, preserving its explicit resolution controls, native capture alignment, pixel-budget safeguards and full-screen sizing. No layouts, fonts, presets or settings require migration. Memory and cache construction cost increase with output size; native surface construction remains off the UI thread and steady frames reuse it. The new field does not establish GPU performance or guarantee the requested FPS.

Validation: both regressions fail on the old optics and pass on the new optics; 70 contracts pass. Release build and synthetic before/after optical inspection are recorded in GLASS.md. Windows capture, compositing, device snapping, seconds-enabled formats, resizing and visual quality are NOT RUN here (G1-007 and D-001/D-004/D-012 as applicable). Reproduce at Native · 100%, the owner's font/tint and full-screen size, then compare thin strokes and curved contours at 100% image zoom. Rollback is to revert this change; stored user data is unchanged.
