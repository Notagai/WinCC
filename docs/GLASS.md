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
