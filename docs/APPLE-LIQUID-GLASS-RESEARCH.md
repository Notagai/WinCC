# Apple Liquid Glass research and implemented adaptations

## Sources actually read

1. [Meet Liquid Glass — WWDC25, session 219](https://developer.apple.com/videos/play/wwdc2025/219/), including the full published transcript. Relevant chapters: Dynamics (1:29), Adaptivity (6:00), Principles (10:31).
2. [Liquid Glass technology overview](https://developer.apple.com/documentation/technologyoverviews/liquid-glass), read through Apple's documentation JSON at `https://developer.apple.com/tutorials/data/documentation/technologyoverviews/liquid-glass.json`.
3. [Adopting Liquid Glass](https://developer.apple.com/documentation/technologyoverviews/adopting-liquid-glass), read through Apple's documentation JSON at `https://developer.apple.com/tutorials/data/documentation/technologyoverviews/adopting-liquid-glass.json`.

These sources describe behavior and adoption APIs. They do **not** disclose Apple's shader source, proprietary optical equations, layer coefficients, or compositor implementation. No Apple implementation code has been copied. WinCC independently implements selected described behaviors in C#/WPF on Windows.

## What changed based on the research

| Apple description | WinCC implementation | Limit |
| --- | --- | --- |
| Clear is permanently transparent and uses localized dimming for bright foreground content (Principles) | Default Clear material keeps bright clock text, removes background-driven text flipping, and adds a smooth local dimming field beneath the label | Dimming shape/coefficient are our own, not disclosed Apple values |
| Regular and Clear have distinct behavior, not a mixed material | Mutually exclusive Clear/Regular selection in Settings | Our Regular approximation is much simpler than Apple's layered adaptive system |
| Larger material has more substantial lensing and softer light scattering (Adaptivity) | Logical size controls lens thickness; Regular adds a small multi-sample scattering layer | CPU sampler with a bounded image resolution, not Apple's GPU compositor |
| Light moves over the surface and interaction energizes the material (Dynamics/Principles) | Pointer-directed light target, eased highlight direction, and interaction energy feed the optical calculation | Mouse-driven lighting; no device-motion sensor input or neighbouring-surface light transfer |
| Material appears by modulating lensing instead of fading opacity (Dynamics) | Initial refraction strength ramps exponentially; no opacity fade is applied to the material | No fluid shape-morphing or multi-element merging |
| Reduced Motion removes elastic behavior and reduces effects | App Reduce motion setting and Windows ClientAreaAnimation disable optical emergence and dynamic pointer lighting | No spring deformation is implemented |
| Reduced Transparency makes the material frostier | Explicit app setting adds a wider multi-sample scatter and stronger backing | Windows transparency preference is not automatically mirrored |
| Increased Contrast uses predominantly black/white material | Windows HighContrast selects a dark neutral material and bright text | Must still be visually tested with actual Windows high-contrast themes |
| Avoid glass-on-glass and overuse; keep content primary | One clock surface; text has no second glass panel; Settings uses a solid surface | A floating desktop clock is a custom adaptation, not an Apple navigation component |

The earlier signed-distance lens, numerical normals, chromatic dispersion, bilinear sampling and reflection equations remain our own model. Apple's references support the desired lensing behavior, not those specific equations. We do not claim to reproduce Apple's private implementation or pixel-identical output.

## Validation

47 contracts pass, including material-specific checks for localized Clear dimming, interaction/size changes, and distinct Regular/frosted/high-contrast outputs. The complete Windows-targeted Release build passes with zero warnings/errors. Reduced-motion wiring and live capture require an actual Windows UI test; pure optical tests do not establish their native behavior.

The renderer still uses local CPU capture and a 100 ms request interval, processes one frame at a time, and caps material resolution at 640 pixels. It does not provide Apple-level GPU performance. Capture exclusion can hide WinCC from screenshots/recordings. These limitations remain material to evaluating the result.

## Clarified rigid reference and performance follow-up

The latest owner reference requests a rigid floating lens. Geometry wobble has been removed from the runtime clock. Lensing and lighting remain the material cues. Settings now has frame-rate targets and uncapped scheduling; see GLASS.md for measured optical improvements and limits. This is a user-directed choice, not a claim that Apple specifies a particular desktop-clock silhouette or FPS policy.
