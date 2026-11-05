# WinCC — precise implementation specification

> **Status:** Normative engineering specification. The owner subsequently authorized an implementation candidate; see [ADR 0003](decisions/0003-native-implementation-candidate.md), [development](DEVELOPMENT.md) and [validation](VALIDATION.md). G1 and Windows acceptance remain unverified. WinCC is a **Windows 11 home screen / desktop command center**. Custom locking and sign-in are out of scope.
>
> **Normative language:** MUST = required when implementing this scope; SHOULD = recommended and deviation requires explanation; EXPERIMENT = behavior not verified and not safe to assume.

## 0. Contract and feature gates

The goal is a desktop surface that lets the user's independent Wallpaper Engine animation remain visible, renders clickable home widgets, persists layout locally, and sits behind ordinary application windows. This is **not** a shell replacement or Wallpaper Engine integration.

Release gates, in order:

- **G0 — Repository-only (historical baseline):** docs and agent tooling, no executable code.
- **G1 — Window composition spike:** prove native desktop positioning, transparency, click handling, ordinary window stacking, and recovery on Windows 11. Do not build widgets until G1 is reviewed.
- **G2 — Minimal canvas:** Normal Mode and Edit Mode with one clock; move/resize only in Edit Mode, Done/Cancel, persistence, diagnostics and safe exit.
- **G3 — Widgets and launcher:** modular widget API, launcher, settings and themes.
- **G4 — Reliability:** monitor/DPI/virtual-desktop transitions, Explorer restart and user-optional startup.
- **G5 — Packaging:** controlled install, rollback, performance and compatibility sign-off.

Normally an agent **MUST NOT** implement later gates while the present gate is unapproved. The owner's subsequent entire-project request authorized a broader implementation candidate (ADR 0003); it did not pass acceptance gates. Each gate still requires observed evidence. A failed or unrun G1 is not solved by inventing fake UI behavior.

## 1. Proposed technology decisions (to validate at G1)

| Subsystem | Initial candidate | Decision gate |
| --- | --- | --- |
| Host/window lifecycle | C# / modern .NET / WPF | Verify native window positioning and transparent visuals |
| Frontend | TypeScript / React / Vite | Only after host and renderer coexist |
| Browser surface | Microsoft Edge WebView2; evaluate **WebView2CompositionControl** | Test compositing, airspace, opacity, input and GPU/memory behavior |
| Interop | Narrow P/Invoke adapter around Win32 | No Win32 calls inside widget components |
| State | Local versioned JSON | SQLite only with a concrete need |
| Logging | Local structured text files | Disabled network telemetry by default |
| Packaging | Portable first, installer later | Must include clean exit and uninstall |
| External tools | Wallpaper Engine independent; Windhawk optional | Neither required to run WinCC |

**Critical risk:** `WebView2.DefaultBackgroundColor = Transparent` does not prove the hosting WPF window can composite alpha with a third-party wallpaper, nor that input can pass through transparent pixels. WPF `HwndHost` WebView2 has known airspace constraints; test `WebView2CompositionControl` and, if necessary, a native composition host or WPF-only proof-of-concept. Do not commit to WebView2 before observing actual Windows 11 behavior.

Official references:
- WPF/WebView2 airspace: https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf
- WebView2 composition: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/overview-features-apis
- Layered-window behavior: https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features
- SetWindowPos: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
- DPI manifest: https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process

## 2. Proposed source layout (create ONLY when app implementation is authorized)

```text
WinCC/
  AGENTS.md
  docs/
    IMPLEMENTATION.md
    CONTRACTS.md
    CANVAS-EDITOR.md
    TEST-MATRIX.md
    decisions/
  src/
    WinCC.Host/                  # WPF entry point, lifecycle, graceful exit
    WinCC.DesktopInterop/        # HWND, monitor enumeration, shell events, DPI
    WinCC.Layout/                # schema, validation, persistence, migration
    WinCC.Diagnostics/           # log events and troubleshooting
    WinCC.UI/                    # React + TypeScript frontend, if approved
      src/
        app/
        widgets/clock/
        layout/
        bridge/
        themes/
  tests/
    WinCC.Layout.Tests/         # pure state logic and migration tests
    WinCC.Interop.Tests/        # bounded native integration checks
    WinCC.UI.Tests/             # widgets/bridge tests
  tools/
    windows-test-plan/          # manual test evidence only
```

These names and folders are **planned**, not existing. Avoid creating `WinCC.Auth`, `WinCC.Lock`, `WinCC.CredentialProvider`, an input driver, or a background privileged service.

## 3. G1: desktop layering spike — exact procedure

### 3.1 Environment and process

1. Capture edition, build (`winver`), architecture, Windows display scale, active monitor resolutions, GPU, wallpaper software status and whether Explorer manages the desktop.
2. Create **one** non-elevated process and **one** experimental window with an obvious exit command. Do not use automatic startup.
3. Record every candidate host strategy and result separately. Do not call an undocumented strategy “supported.”
4. Try (A) ordinary non-topmost top-level window with managed z-order; (B) guarded Explorer/Progman/WorkerW experiment; (C) fallback normal window mode that can always be closed.
5. For strategy B, discover and verify HWND identity before each operation. Re-enumerate after Explorer restarts. **Never assume** a particular WorkerW handle, hierarchy, message side effect, or permanent parent-child relation on all Windows 11 builds.
6. Do not modify Windows Explorer binaries, registry shell replacements, drivers, other processes, or Wallpaper Engine. Any permanent `SetParent` technique needs separate review and demonstrated recovery.
7. Exit/kill WinCC and confirm Explorer, wallpaper and taskbar remain usable. If any strategy fails the exit test, reject it.

### 3.2 Window semantics

Target properties, to be **measured** rather than assumed:
- Window has no user-visible frame or taskbar button in normal home mode.
- It is **not topmost**, does not steal keyboard focus when a normal app is active, and never overlays exclusive fullscreen apps.
- Unobscured regions show the actual wallpaper without opaque black/white backgrounds.
- Interactive widget regions accept pointer input. Non-widget regions must not create an input-blocking dead zone. Verify actual propagation to desktop icons/context menu in the selected host strategy.
- Widget manipulation must not raise a full-screen desktop overlay above ordinary apps.
- Windows native shortcuts, Start menu, taskbar, desktop icon behavior, and app activation remain normal.

Test Win32 `SetWindowPos` **only** as one possible z-order tool, not a solution by itself. Test any `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, `WS_EX_LAYERED`, `WS_EX_TRANSPARENT`, `WM_NCHITTEST` or `WM_MOUSEACTIVATE` behavior explicitly: flags interact with hosting technology. In particular, `HTTRANSPARENT` has same-thread caveats and is **not** a guaranteed pass-through across independent processes. Prefer a proven window/input layout over unsupported hit-test tricks.

### 3.3 Rendering experiment matrix

For each host strategy, test:
- WPF-only transparent clock/panel.
- Standard WPF WebView2.
- `WebView2CompositionControl` if available and compatible.
- Native composition alternative if WebView2 approaches fail.
- Windows desktop wallpaper without Wallpaper Engine **and** live Wallpaper Engine wallpaper.

Record separately: visible alpha, black/white flashing at navigation/resize, frame ordering, right-click-through, input focus, animation stutter, GPU/memory behavior. Choose a stack based on data; do not turn an observed failure into a CSS workaround without proving the fix.

### 3.4 DPI and coordinates

- Set **Per Monitor v2** DPI awareness through the application manifest before window creation; avoid setting competing DPI modes at runtime.
- The Win32 virtual desktop uses **physical pixels** and monitor origins may be negative.
- WPF layout uses device-independent units at 96 units/inch. Treat CSS px as renderer coordinates, not automatically identical to Win32 physical px.
- On each monitor: `scale = monitorDpi / 96`; convert a point by `logicalDip = physicalPx / scale` **only in the correct monitor-local coordinate space**. Translate to the relevant monitor origin first.
- Handle `WM_DPICHANGED`; use recommended window rectangles where applicable and re-query monitor topology. Test WPF/WebView2 actual scaling rather than manually applying a second scale transform.
- Do not store raw HWND values or transient monitor indices as permanent device identities.

### 3.5 Explorer and display lifecycle

Handle or observe `TaskbarCreated` via `RegisterWindowMessage` as an Explorer/taskbar recreation signal, then verify shell objects rather than blindly reusing cached handles. Detect display topology changes and reevaluate bounds. Add bounded re-enumeration with backoff; **no endless high-frequency polling**.

For Windows display change, DPI change, GPU reset, sleep/resume and Explorer restart, log state transitions and prove the host can regain valid placement or fall back safely. A simple visible recovery/exit route must work even when the WebView renderer fails.

### 3.6 G1 exit evidence

A PR must supply the completed G1 entries in docs/TEST-MATRIX.md (or a run record linked from it), OS build, reproducible screenshots/video if feasible, chosen host strategy and rejected alternatives, crash recovery findings and remaining risks. **G1 remains failed/undecided if transparency, interaction or z-order cannot be demonstrated**.

## 4. G2: first real home-screen milestone

Authorized only after G1 approval.

### 4.1 Lifecycle

- One normal-user WinCC host process launches the UI; no admin prompt and no startup task by default.
- A single instance per interactive Windows user is preferred; second launch focuses or reports the existing instance rather than creating a duplicate desktop surface.
- If UI initialization fails, expose a native exit/error action; do not leave an invisible input-capturing window.
- Shutdown flushes pending state, releases handles and closes the browser environment. Windows desktop state remains unmodified.

### 4.2 First widget

- Exactly one clock widget for G2. Use system local time with locale-aware formatting; no network requirement.
- **Normal Mode** is the startup default. The clock works but cannot be dragged or resized. No edit handles appear.
- Explicit **Edit Mode** permits dragging and resizing the clock by dedicated handles; no other widget interactions may accidentally initiate movement.
- Pointer-down captures origin; pointer-move updates a draft preview; pointer-up adds one undoable draft mutation. Escape during a gesture cancels that gesture only.
- Show **Done** and **Cancel**. Done validates and durably saves the draft before returning to Normal Mode. Cancel restores the last durable layout. Save failure keeps Edit Mode open.
- Clamp widget geometry within monitor work area and per-type minimum/maximum sizes. Restore layout on restart without jumps, even when monitors or scaling change.
- **G2 proof must include both modes**, not merely a draggable clock. The full catalog/add/remove workflow belongs to G3.
- See [CANVAS-EDITOR.md](CANVAS-EDITOR.md) for the authoritative behavior and state machine.

### 4.3 Local persistence

`%LOCALAPPDATA%\WinCC\layout.json` is the proposed user-level layout location (not committed to git). Create the directory lazily. Schema and examples are in docs/CONTRACTS.md.

- Validate every field and reject malformed/out-of-range data without crashing.
- Layout geometry and widget collection changes are staged in memory during Edit Mode. **Only Done persists the validated draft**. Do not debounce disk writes for pointer movement. Functional widget content (for example, notes text later) may autosave independently.
- Write a temp file **in the same directory**, flush, and atomically replace/move into place using a suitable Windows filesystem operation. Keep a last-known-good copy when overwriting.
- After interrupted writes, restore a verified valid prior copy or fresh default and report a nonblocking diagnostic warning.
- Explicit `schemaVersion` and migration tests. No silent deletion of unknown saved data.

### 4.4 Acceptance criteria

G2 succeeds only if the clock is visible over Wallpaper Engine, **cannot move/resize in Normal Mode**, can be moved/resized in Edit Mode, Done durably saves the new geometry, Cancel discards draft changes, a forced crash during editing restores the last saved layout in Normal Mode, normal app windows remain above the dashboard, and the desktop stays usable. Each criterion requires a test record.

## 5. G3: launcher, widget API and theming

Develop behind independent interfaces so replacing the native hosting approach does not rewrite widgets.

- Widget contract: stable ID, type, geometry, per-type min/max size and resize behavior, versioned config, lifecycle hooks, optional commands and accessibility labels.
- Build an Add Widget catalog, remove/undo, widget stacking, optional snapping and alignment guides, and per-monitor freeform placement. Never enforce the screenshot's two-column arrangement. A zero-widget canvas is valid.
- Widget state is declarative; native host owns persistence and validated system commands.
- Launcher data sources and app launching must be local and opt-in. Resolve only approved targets; never execute arbitrary command strings received from an untrusted WebView event.
- Host-to-frontend bridge is an explicit allowlist with versioned request/response messages, validation, bounded payloads, and request correlation IDs (docs/CONTRACTS.md).
- Themes change tokens/assets, not native host logic; don't fix visual design until owner supplies direction.
- No network widget is part of G2. Any future weather/media integration requires separate requirements, permission review and test plan.

## 6. G4/G5: integration and release

- Monitor additions/removals, mismatched DPI, fullscreen games, taskbar relocation, wallpaper absence, Explorer restart, repeated relaunch, session sleep/resume.
- Startup registration is **opt-in and reversible**; no scheduled tasks or registry Run keys introduced silently.
- Installer (if approved) must not replace Explorer/shell, patch system DLLs, or leave overlays running after uninstall. Provide portable build first.
- Define performance targets **after G1 measurements**; record baseline idle RSS/CPU/GPU, animation frame cadence and cold startup, then fix budgets by ADR rather than inventing guarantees.

## 7. Agent execution discipline

For every task, agent MUST:
1. State the exact gate and specific acceptance criteria it is implementing.
2. List files to change, APIs and anticipated side effects.
3. Limit the diff to one reviewable task (prefer one PR per substep).
4. Include unit/bridge tests for pure logic and a Windows 11 manual test checklist for platform-specific functionality.
5. Report **observed results** separately from expectations; if testing on Windows is unavailable, say “not tested on Windows.”
6. Document fallback/recovery and relevant logs; link an ADR if design deviates from this spec.
7. Never start unrelated features or modify unrelated code without authorization.

## 8. Non-negotiable repository scope

WinCC is only the **Windows home screen/command center**. Do not add lock screen, login UI, credentials, biometrics, reserved-key interception, Winlogon, or Windows authentication components. They are not part of this product.

## 9. Specification references

- docs/CANVAS-EDITOR.md — approved blank canvas, Edit/Normal modes and interaction semantics
- docs/CONTRACTS.md — proposed layout/state/bridge formats
- docs/TEST-MATRIX.md — test IDs, expected outcomes and run record template
- docs/ROADMAP.md — approval gates
- docs/SECURITY.md — desktop stability and local data
