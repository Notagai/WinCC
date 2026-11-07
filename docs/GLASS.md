# Clock-only refractive surface

The owner's latest scope supersedes the earlier dashboard catalog: only clock, Settings with headings/buttons, and actual sampled-background refraction. Other widget views and provider controls are removed from the application. Legacy layout decoding and historical provider contracts remain to preserve old files and test data. No old notes or layout data is deleted.

## Implementation

- `BackdropCapture` owns user32/gdi32 interop and creates a top-down BGRA DIB for a padded screen region. Every DC and GDI object is released in a finally block. `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` excludes the clock before sampling. Failure to enable exclusion disables sampling, avoiding recursive image feedback.
- `GlassOptics` runs independently of WPF. A rounded-rectangle signed-distance field defines the surface and alpha mask. Numerical normals and a curved perimeter profile produce background displacement. B/G/R use slightly different refraction offsets. Bilinear source sampling avoids nearest-neighbour pixel stepping. A Fresnel-like edge reflection and directional rim lighting affect the perimeter; the centre remains clear.
- `LiquidGlass` requests frames at 100 ms intervals, skips concurrent work, and performs capture/optics on a worker thread. The maximum material dimension is 640 pixels. The capture region has a 12.5% margin per side to support displacement beyond the clock bounds. Size changes invalidate pending results; unloading/disposal drops frames and stops the timer. The rendered bitmap is frozen before crossing back to the UI thread.
- Sampled background luminance switches clock text between light and dark with hysteresis, avoiding rapid flipping near a single threshold.
- The clock uses an ordinary opaque WPF host with a rounded window region, not a layered/translucent backdrop host. Its apparent transmission is the custom sampled optical image. This also avoids relying on capture exclusion support for layered clock windows. The window region only clips the outline; it supplies no glass effect.
- The host draws the clock independently over the refracted material. The existing proportional viewport scales its typography, padding and edit affordances. Native window positioning and Wallpaper Engine configuration are unchanged.
- Settings uses a solid app surface. It has no explanatory paragraphs, provider setup, launcher or non-clock catalog. Errors appear only when an operation fails.

## Tradeoffs requiring Windows validation

The current screen sampler is a practical CPU implementation, not a zero-copy GPU compositor. Ten requested updates per second can lag fast wallpaper animation; very large clocks upscale their material sample. Performance must be measured on the target Windows machine. HDR and protected content may not match the visible desktop capture. Capture exclusion intentionally makes the app absent from supported screenshot/recording paths. It is not a privacy/security guarantee, and no claim is made that all capture applications honor it.

GDI capture exclusion must be verified on the actual Windows build. Test a moving, high-contrast wallpaper behind the clock and inspect the edges for displacement, directional highlight, and absence of recursive clock images. Test dragging, resize, mixed DPI, negative display origins, display edges, sleep/resume, fullscreen, Explorer restart and exit. Capture the result externally if OS screenshot exclusion hides the app. Do not claim native acceptance or Apple-equivalent quality from Linux builds or synthetic tests.

## Actual checks

Release compilation: zero warnings/errors. Contract runner: 44 passed, zero failed, including three optical contract cases. These test clear transmission, exterior masking, displacement of a striped source, reaction to changing source content and rejection of invalid buffers/parameters. Windows UI/capture/visual tests remain NOT RUN.

## Recovery

Exit WinCC to stop capture and release windows. Settings and layout are in `%LOCALAPPDATA%\WinCC`; old notes are preserved. Archive that directory before manually changing files. No screen sample is written there. If refraction fails, the clock text remains usable without a refracted sample; restart to retry sampling. No system backdrop or shell setting has been changed.

## Optical inspection image

Run `dotnet run --project tools/WinCC.GlassProbe -c Release` from the repository root to generate `artifacts/glass-optics.ppm`. It composites the production optical calculation over a synthetic colored grid and diagonal stripe so displacement is visible. This exercises the real refraction math, not the Windows capture backend. The initial 640×280 probe took about 30 ms on this cloud machine (single cold measurement, not a Windows performance budget). The generated PNG inspection image showed the line bending at both lens boundaries and a clear centre; it is not a screenshot of the app.
