# Clock-only refractive surface

The owner's latest scope supersedes the earlier dashboard catalog: only clock, Settings with headings/buttons, and actual sampled-background refraction. Other widget views and provider controls are removed from the application. Legacy layout decoding and historical provider contracts remain to preserve old files and test data. No old notes or layout data is deleted.

## Implementation

- `BackdropCapture` owns user32/gdi32 interop and creates a top-down BGRA DIB for a padded screen region. Every DC and GDI object is released in a finally block. `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` excludes the clock before sampling. Failure to enable exclusion disables sampling, avoiding recursive image feedback.
- `GlassOptics` runs independently of WPF. A rounded-rectangle signed-distance field defines the surface and alpha mask. Numerical normals and a curved perimeter profile produce background displacement. B/G/R use slightly different refraction offsets. Bilinear source sampling avoids nearest-neighbour pixel stepping. A Fresnel-like edge reflection and directional rim lighting affect the perimeter; the centre remains clear.
- `LiquidGlass` uses configurable completion-paced frame deadlines, never queues concurrent frames, and performs capture/optics on a worker thread. The maximum material dimension is 640 pixels. The capture region has a 12.5% margin per side to support displacement beyond the clock bounds. Size changes invalidate pending results; unloading/disposal drops frames and stops the timer. The rendered bitmap is frozen before crossing back to the UI thread.
- In Regular mode, sampled background luminance switches clock text between light and dark with hysteresis. Clear instead keeps bright foreground text and dims locally beneath it, following Apple’s documented Clear guidance.
- The clock uses an ordinary opaque WPF host with a rounded window region, not a layered/translucent backdrop host. Its apparent transmission is the custom sampled optical image. This also avoids relying on capture exclusion support for layered clock windows. The window region only clips the outline; it supplies no glass effect.
- The host draws the clock independently over the refracted material. The existing proportional viewport scales its typography, padding and edit affordances. Native window positioning and Wallpaper Engine configuration are unchanged.
- Settings uses a solid app surface. It has no explanatory paragraphs, provider setup, launcher or non-clock catalog. Errors appear only when an operation fails.

## Tradeoffs requiring Windows validation

The current screen sampler is a practical CPU implementation, not a zero-copy GPU compositor. Ten requested updates per second can lag fast wallpaper animation; very large clocks upscale their material sample. Performance must be measured on the target Windows machine. HDR and protected content may not match the visible desktop capture. Capture exclusion intentionally makes the app absent from supported screenshot/recording paths. It is not a privacy/security guarantee, and no claim is made that all capture applications honor it.

GDI capture exclusion must be verified on the actual Windows build. Test a moving, high-contrast wallpaper behind the clock and inspect the edges for displacement, directional highlight, and absence of recursive clock images. Test dragging, resize, mixed DPI, negative display origins, display edges, sleep/resume, fullscreen, Explorer restart and exit. Capture the result externally if OS screenshot exclusion hides the app. Do not claim native acceptance or Apple-equivalent quality from Linux builds or synthetic tests.

## Actual checks

Release compilation: zero warnings/errors. Contract runner: 53 passed, zero failed, including three optical contract cases. These test clear transmission, exterior masking, displacement of a striped source, reaction to changing source content and rejection of invalid buffers/parameters. Windows UI/capture/visual tests remain NOT RUN.

## Recovery

Exit WinCC to stop capture and release windows. Settings and layout are in `%LOCALAPPDATA%\WinCC`; old notes are preserved. Archive that directory before manually changing files. No screen sample is written there. If refraction fails, the clock text remains usable without a refracted sample; restart to retry sampling. No system backdrop or shell setting has been changed.

## Optical inspection image

Run `dotnet run --project tools/WinCC.GlassProbe -c Release` from the repository root to generate `artifacts/glass-optics.ppm`. It composites the production optical calculation over a synthetic colored grid and diagonal stripe so displacement is visible. This exercises the real refraction math, not the Windows capture backend. The initial 640×280 probe took about 30 ms on this cloud machine (single cold measurement, not a Windows performance budget). The generated PNG inspection image showed the line bending at both lens boundaries and a clear centre; it is not a screenshot of the app.

## Apple guidance follow-up

See [the source-to-implementation mapping](APPLE-LIQUID-GLASS-RESEARCH.md) for the official Apple sources actually read, the selected behaviors implemented and the limitations. Settings now offers mutually exclusive Clear/Regular materials and explicit reduced-motion/transparency controls. Windows high contrast and client-area animation preferences also affect rendering.

## Rigid floating lens and frame pacing

The owner's clarified reference supersedes the previous breathing-blob experiment: the shipping clock is rigid, with an almost capsule-shaped rounded outline. It has no geometry wobble, drift, shear, or per-frame window-region reconstruction. Refraction and eased lighting supply the floating material impression. The organic math remains only for legacy contracts and comparison, not the runtime clock.

A single bounded geometry cache stores normals, coverage, edge thickness, Fresnel/rim factors and label dimming. A size change replaces that cache; no captured background pixels are cached there. Capture and optical output byte buffers are reused at a stable size. Edge antialiasing is resolved over the actual background before the image reaches the opaque host. The native rounded outline is rebuilt only for a new size/DPI.

Settings has FPS presets (15/30/60/90/120/144/165/240/360), ±5 controls for 1–500, and Uncapped (stored as 0). Default target: 60. The single-shot background-priority dispatcher scheduler subtracts render time from each target interval. If a frame runs late, the next one is immediately eligible rather than sleeping another full interval. Uncapped inserts no application delay and still permits only one in-flight frame. Hidden/unloaded windows stop scheduling capture; minimized windows poll slowly for restoration. Lighting easing uses elapsed time, so changing FPS does not change its response speed. No setting guarantees that native capture, CPU work or WPF presentation can reach that rate; uncapped can use more CPU.

## Performance measurements and checks

A Linux CPU-only comparison used the previous renderer from commit `f63ecd7` (Organic=true, Phase=1, CompositeToBackdrop=true) versus the new cached rigid renderer (Organic=false, CompositeToBackdrop=true). Each used identical seeded random BGRA source buffers, Clear material, eight paired warm-ups and median times from 30 renders. The final run measured:

| Output size | Previous wobble | Cached rigid | Optical speedup |
| --- | --- | --- | --- |
| 280×150 | 11.94 ms | 5.04 ms | 2.37× |
| 640×280 | 50.39 ms | 9.73 ms | 5.18× |

This excludes Windows GDI capture, bitmap creation, UI dispatch and display presentation, and does not claim end-to-end Windows FPS. Timings varied across runs. Buffer reuse in the host reduces large managed allocations; those savings are also outside this comparison.

53 contracts pass. New cases cover frame deadlines, uncapped zero delay, overruns without extra sleeps, fixed silhouettes under lighting changes, cache correctness and reusable output buffers. Windows smoothness, capture, actual FPS and final edge quality remain unverified.

## Time-shaped glass, presets and tray lifecycle

Clock mode can use the actual formatted time's glyphs as a material mask. WPF converts the selected bundled typeface into a glyph outline; the outline is rasterized only when time/font/size changes. A cached two-pass distance transform produces per-glyph normals and refractive edges, including counters (holes in digits). Unmodified background is retained outside the glass glyphs to avoid black fill in the opaque host. The date is drawn separately below. This mode changes the optical material contour, not merely a white text overlay. The native hit region remains a rounded clock-sized area so movement/editing remains reachable.

Settings has custom tab templates in a larger window. Close/X/Alt+F4/taskbar close hide Settings without destroying its HWND or the dashboard. An app-drawn clock tray icon reopens Settings and exposes an Exit action. TaskbarCreated re-adds the icon after Explorer restarts; explicit Exit/session shutdown removes it. Failure to register the icon is surfaced instead of leaving a hidden app with no reopening path. Native tray behavior still needs Windows validation.

There are exactly three atomic layout preset files. Saving captures current geometry and clock format; global appearance/FPS preferences remain app-wide. Load asks before replacing an unsaved draft, validates the preset before mutation, then commits through the main layout store. Corrupt/future presets are preserved and rejected. Preset tests exercise slots, restart persistence, invalid slot rejection and corrupt data preservation.

Current build: zero warnings/errors; 55 contracts pass. The new cases cover preset persistence and glyph material masks. WPF glyph extraction, native tray/taskbar-close behavior, drag/resize UI and the visual date composition remain NOT RUN on Windows in this Linux task.

## Custom design and percent anchors

`ClockDesign` is an immutable validated record stored with app settings. The Design form commits as one operation, with rollback on settings-write failure. It controls glyph font/weight/italic, size, independent X/Y stretch, tracking, stroke dilation, refraction strength, and subtle tint. A custom installed family name is supported but URI/path font loading is rejected. Regular lens and time-shaped modes share material tint/refraction; glyph-specific transforms apply to the time-shaped material. Date visibility, format, font, color, size and lift are independently configurable. Disabling the date centres the time-shaped composition vertically. Formats are validated before saving.

Glyph outlines are built character by character for tracking, optionally dilated with a stroke, then independently transformed in X and Y. The final transformed bounds are contained in the viewport, so overly large values cannot spill into unrelated desktop areas. Font changes and custom formats invalidate the cached glyph mask; steady frames reuse it.

Tint uses selective per-channel transmission, not an opaque fill, and high-contrast mode bypasses it. The material still samples the real background. Text customization and native font selection need Windows visual validation.

Saved X/Y anchors represent centre coordinates in the usable work area, not absolute screen pixels; 50/50 centres the entire clock. Position clamps to reachable bounds, reprojects on display changes and supports negative monitor origins. Manual move/resize/nudge releases the relative anchor; canceled gestures restore it. The layout validator rejects nonfinite and out-of-range anchor metadata. Layout presets include anchors; appearance/design remains app-wide.

59 contracts cover the current implementation, including design serialization/validation, percent-centre projection, anchor persistence and tint channel behavior. Native WPF controls and transformed text are not visually validated on Windows here.

## Softer optics, tint correction and full-display sizing

The former displacement cap and slope factor were about three times stronger, and RGB offsets were about ten times farther apart. Those exaggerated bends/fringes are reduced. Rim reflection is more restrained, with a soft highlight and a small opposing-edge exposure reduction to define the surface without laying down a solid color. Clear label dimming is lighter; Regular retains source-derived scattering but its fixed gray mix is removed. Accessibility Reduced Transparency still intentionally supplies stronger backing.

Tint previously multiplied the selected amount by another 0.3 mix, making it unexpectedly weak. It now uses a Beer–Lambert-inspired per-channel transmission coefficient, calculated once per frame. Stronger tint attenuates complementary channels more; sampled background detail stays intact. A white tint is neutral, zero strength is explicitly off, and high-contrast mode bypasses tint. Swatches populate color and 18% strength in the form; Apply design commits the change. Typography, Material & tint, and Date are separate nested pages with one shared Apply action, preserving form values across those pages.

Clock sizing no longer uses the old 1200×1000 catalog cap. A large serialization bound remains for malformed inputs, and runtime dimensions are constrained to the monitor work area. Initial host bounds are clamped before showing the window. Fill display sets that full work-area size with a 50/50 anchor; outside Edit Mode it commits immediately, and inside Edit Mode it remains undoable until Done. Actual render samples remain bounded for performance.

62 contracts pass. Added cases validate 5120×2120 layout sizing, tint monotonicity/background preservation, and minimal chromatic separation of neutral source content. Windows appearance, full-display input behavior and end-to-end rendering performance still require Windows tests.

## Screenshot mode, visibility and font selection

The owner reported disappearing clocks during Alt-Tab and Win+Shift+S. The fullscreen polling heuristic is removed: it could mistake system overlays for fullscreen applications and explicitly hide the clock. Ordinary window stacking still keeps the clock below apps; this does not force it above Task View or system overlays. See ADR 0004. Native reproduction and regression checks remain NOT RUN.

Tray → Screenshot mode (also Settings → App) freezes the last optical image, invalidates pending frames and removes capture exclusion. Use Win+Shift+S after enabling it, then Resume live glass. Resuming restores exclusion before sampling restarts. No image file is created by WinCC. The mode is transient; applying design/layout changes returns to live sampling. The optical time glyphs and wallpaper stay frozen during this mode, even if foreground date/clock text updates. Before the first frame exists the toggle reports an error rather than freezing an empty surface. This is a manual capture mode, not a system shortcut interceptor.

Glass/date font dropdowns include installed and bundled families. Four curated Google Fonts can be fetched on demand: Space Mono, Share Tech Mono, Rajdhani and Bungee. Official upstream metadata selects the regular TrueType file. Requests have bounded sizes/time, retain TLS verification, and reject redirects and unsafe filenames. Fonts and OFL licenses cache in `%LOCALAPPDATA%\WinCC\fonts`; no API key, system installation or startup request is needed. Choose a family, Get Google font, then Apply design. The download check retrieved all four fonts/licenses and verified their family names with FreeType on Linux; WPF loading remains NOT RUN.

The old eight-direction chamfer approximation for glyph distance could generate faceted normals. An exact Euclidean distance transform now supplies radial distances, tested against off-axis analytic distances. This removes a mathematical source of directional ridges; the user's specific visual artifact still needs screenshot review. No claim of native visual acceptance follows from this change.

Current verification: 64 contracts pass, Release solution build has zero warnings/errors. On Windows, check Alt-Tab and Task View repeatedly, enable Screenshot mode, take a snip containing the clock, resume sampling against animated wallpaper, and verify no recursive images. Test downloaded fonts for both time-shaped glyphs and date, then restart offline. Record Windows build and results; G1-003/G1-004/G1-008 window ordering and capture/visual acceptance remain NOT RUN here.
