# WinCC

A Windows 11 desktop clock with an app-owned refractive glass surface.

## Run

Use the .NET SDK pinned in `global.json`:

```powershell
dotnet build WinCC.sln -c Release
.\tools\dev-test.ps1
dotnet run --project src/WinCC.Host -c Release
```

Settings contains headings and controls for clock editing, time format, display, typeface and text size. Closing Settings minimizes it; Exit quits WinCC. In Edit Mode, drag the header or resize with the bottom handle. Done saves and Cancel discards the draft. Content scales with the clock. Wallpaper Engine runs independently.

Only the clock is active. Older widget layouts and notes remain on disk for recovery; the removed widgets are not rendered or offered in Settings. A blank saved canvas remains valid; use Edit clock → Add clock.

## Refractive glass

The renderer captures a padded local background region into memory, then applies a rounded-lens displacement field, bilinear sampling, slight RGB dispersion, Fresnel reflection and edge highlights. The centre transmits the captured background. The clock host itself is opaque and clipped to the lens outline; the apparent transmission comes from the sampled optical image, not OS translucency. Clock text is rendered separately and remains sharp. Settings offers Clear/Regular materials, reduced motion, and reduced transparency. Clear uses bright foreground text with local dimming; Regular can adapt text contrast. Pointer input shifts highlights, and appearance ramps lensing rather than opacity. No Mica, Acrylic, DWM backdrop effect, remote service or external image is used.

This is a custom CPU optical renderer, not Apple's proprietary implementation. It defaults to a 60 FPS target, with saved presets, ±5 FPS controls (1–500), and an Uncapped mode (0), with a maximum 640-pixel render dimension; large clocks upscale the sampled material while text stays vector rendered. A single frame is processed at a time, off the UI thread. Hidden/minimized clocks stop sampling. Background pixels are neither saved nor uploaded.

**Capture exclusion:** WinCC excludes its own windows from OS capture to prevent recursive feedback. The clock and Settings may be absent from screenshots, recordings and screen sharing. If exclusion cannot be enabled, refraction is disabled and an error is shown. A failed capture discards the sampled image rather than displaying stale material. Protected content, remote desktops, HDR/color management and driver-specific capture behavior require Windows testing.

## Validation

The Release build passes and **53 contracts pass**, including tests that actual background samples are displaced, the centre stays clear, rounded corners have an alpha mask, and changing the background changes the optical output. Windows live capture, compositing, performance and visual acceptance are **NOT RUN** in this Linux environment. Compilation and optical tests do not certify Apple-like visual quality.

See [Apple research and implementation mapping](docs/APPLE-LIQUID-GLASS-RESEARCH.md), [glass implementation notes](docs/GLASS.md), [development](docs/DEVELOPMENT.md), and [agent instructions](AGENTS.md).

The lens is rigid and rounded, matching the supplied glass reference. Its floating feel comes from depth, refraction and interactive lighting; its outline no longer wobbles. Rigid geometry is cached, native clipping changes only with size/DPI, and edge pixels blend with the actual background rather than black. Settings → Frame rate changes apply live without recreating the clock. Uncapped removes the application frame delay; capture, CPU rendering and the WPF compositor still determine actual output FPS.
